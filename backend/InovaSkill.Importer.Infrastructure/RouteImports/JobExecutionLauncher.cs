using System.Text;
using System.Text.Json;
using InovaSkill.Importer.Application.RouteImports;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.RouteImports;

public sealed class JobExecutionLauncher(
    ImportDbContext db,
    IBackgroundJobDispatcher dispatcher) : IJobExecutionLauncher
{
    public async Task<JobLaunchResult> LaunchAsync(JobLaunchRequest request, CancellationToken cancellationToken)
    {
        var definition = OperationalJobCatalog.GetRequired(request.JobType);
        if (request.ContractVersion != definition.ContractVersion)
            throw new ArgumentException($"Versão {request.ContractVersion} não suportada para {definition.JobType}.");
        if (Encoding.UTF8.GetByteCount(request.ParametersJson) > GenericJobPolicy.MaximumJsonBytes)
            throw new ArgumentException("O JSON de parâmetros excede o limite de 1 MB.");

        using var document = ParseObject(request.ParametersJson);
        if (definition.JobType is OperationalJobCodes.CustomerRegistrationAddressEnrichment or OperationalJobCodes.CustomerAddressCoordinateEnrichment)
        {
            _ = CustomerRegistrationAddressCustomerStatuses.Read(document.RootElement);
            if (definition.JobType == OperationalJobCodes.CustomerRegistrationAddressEnrichment)
                _ = CustomerRegistrationAddressEnrichmentProcessor.ReadRefreshResolved(document.RootElement);
            if (definition.JobType == OperationalJobCodes.CustomerAddressCoordinateEnrichment)
            {
                _ = CustomerAddressCoordinateEnrichmentProcessor.ReadReprocessFailed(document.RootElement);
                _ = CustomerAddressCoordinateEnrichmentProcessor.ReadMaximumRequests(document.RootElement);
            }
        }
        var relatedEntityId = definition.JobType switch
        {
            OperationalJobCodes.MunicipalityCoordinateEnrichment => ReadRequiredGuid(document.RootElement, "importId"),
            OperationalJobCodes.CustomerRegistrationAddressEnrichment =>
                ReadOptionalGuid(document.RootElement, "importId") ??
                await ResolveCurrentCustomerImportIdAsync(cancellationToken),
            OperationalJobCodes.CustomerAddressCoordinateEnrichment =>
                ReadOptionalGuid(document.RootElement, "importId") ??
                await ResolveCurrentCustomerImportIdAsync(cancellationToken),
            OperationalJobCodes.ProcessImport => ReadRequiredGuid(document.RootElement, "importId"),
            OperationalJobCodes.WhatsAppMessageProcessing => ReadRequiredGuid(document.RootElement, "receiptId"),
            _ => throw new InvalidOperationException($"Job sem lançador: {definition.JobType}.")
        };
        await ValidateReferenceAsync(definition.JobType, relatedEntityId, cancellationToken);
        if (!definition.AllowConcurrentRuns && await db.JobExecutions.AnyAsync(job =>
            job.JobType == definition.JobType &&
            (job.Status == JobExecutionStatus.Queued || job.Status == JobExecutionStatus.Processing ||
             job.Status == JobExecutionStatus.Retrying), cancellationToken))
            throw new ArgumentException("Já existe uma execução deste serviço na fila ou em processamento.");

        var now = DateTime.UtcNow;
        var job = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobType = definition.JobType,
            ContractVersion = definition.ContractVersion,
            Queue = definition.Queue,
            Trigger = request.Trigger,
            ParametersJson = document.RootElement.GetRawText(),
            Status = JobExecutionStatus.Queued,
            RelatedEntityId = relatedEntityId,
            RequestedByUserId = request.RequestedByUserId,
            ScheduleId = request.ScheduleId,
            RetriedFromJobExecutionId = request.RetriedFromJobExecutionId,
            ProgressPercent = 0,
            ProgressMessage = "Na fila",
            CreatedAt = now
        };
        db.JobExecutions.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            if (definition.JobType == OperationalJobCodes.ProcessImport)
                dispatcher.EnqueueImport(relatedEntityId, job.Id);
            else
                dispatcher.EnqueueOperationalJob(job.Id);
        }
        catch (Exception exception)
        {
            job.Status = JobExecutionStatus.Failed;
            job.ErrorMessage = $"Falha ao enfileirar no Hangfire: {exception.Message}";
            job.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
        return new JobLaunchResult(job.Id, "QUEUED");
    }

    private async Task ValidateReferenceAsync(string jobType, Guid id, CancellationToken cancellationToken)
    {
        var exists = jobType switch
        {
            OperationalJobCodes.ProcessImport or OperationalJobCodes.MunicipalityCoordinateEnrichment or
                OperationalJobCodes.CustomerRegistrationAddressEnrichment or
                OperationalJobCodes.CustomerAddressCoordinateEnrichment =>
                await db.RouteImports.AnyAsync(item => item.Id == id, cancellationToken),
            OperationalJobCodes.WhatsAppMessageProcessing =>
                await db.WhatsAppMessageReceipts.AnyAsync(item => item.Id == id, cancellationToken),
            _ => false
        };
        if (!exists) throw new ArgumentException("A referência informada no payload não existe.");
    }

    private async Task<Guid> ResolveCurrentCustomerImportIdAsync(CancellationToken cancellationToken)
    {
        var importId = await db.DataSources.AsNoTracking()
            .Where(source => source.Code == CustomerImportCodes.DataSource)
            .Select(source => source.CurrentImportId)
            .SingleOrDefaultAsync(cancellationToken);
        return importId ?? throw new ArgumentException(
            "Não existe um snapshot atual de clientes para enriquecer.");
    }

    private static JsonDocument ParseObject(string json)
    {
        try
        {
            var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw new ArgumentException("O payload deve ser um objeto JSON.");
            }
            return document;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"JSON inválido: {exception.Message}", exception);
        }
    }

    private static Guid ReadRequiredGuid(JsonElement root, string name) =>
        ReadOptionalGuid(root, name) ?? throw new ArgumentException($"$.{name} é obrigatório e deve ser um UUID válido.");

    private static Guid? ReadOptionalGuid(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        return value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id)
            ? id
            : throw new ArgumentException($"$.{name} deve ser um UUID válido.");
    }

    private static string ReadRequiredString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new ArgumentException($"$.{name} é obrigatório.");
}
