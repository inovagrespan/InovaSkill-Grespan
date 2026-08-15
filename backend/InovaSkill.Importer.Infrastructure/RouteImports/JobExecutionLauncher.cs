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
    IBackgroundJobDispatcher dispatcher,
    IRouteOptimizationService routeOptimizationService) : IJobExecutionLauncher
{
    public async Task<JobLaunchResult> LaunchAsync(JobLaunchRequest request, CancellationToken cancellationToken)
    {
        var definition = OperationalJobCatalog.GetRequired(request.JobType);
        if (request.ContractVersion != definition.ContractVersion)
            throw new ArgumentException($"Versão {request.ContractVersion} não suportada para {definition.JobType}.");
        if (Encoding.UTF8.GetByteCount(request.ParametersJson) > GenericJobPolicy.MaximumJsonBytes)
            throw new ArgumentException("O JSON de parâmetros excede o limite de 1 MB.");

        using var document = ParseObject(request.ParametersJson);
        if (definition.JobType == OperationalJobCodes.RouteOptimization)
            return await LaunchRouteOptimizationAsync(document.RootElement, request, cancellationToken);

        var relatedEntityId = definition.JobType switch
        {
            OperationalJobCodes.MunicipalityCoordinateEnrichment => ReadRequiredGuid(document.RootElement, "importId"),
            OperationalJobCodes.ProcessImport => ReadRequiredGuid(document.RootElement, "importId"),
            OperationalJobCodes.WhatsAppMessageProcessing => ReadRequiredGuid(document.RootElement, "receiptId"),
            _ => throw new InvalidOperationException($"Job sem lançador: {definition.JobType}.")
        };
        await ValidateReferenceAsync(definition.JobType, relatedEntityId, cancellationToken);

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

    private async Task<JobLaunchResult> LaunchRouteOptimizationAsync(
        JsonElement parameters,
        JobLaunchRequest request,
        CancellationToken cancellationToken)
    {
        var scopeText = ReadRequiredString(parameters, "scope");
        if (!Enum.TryParse<RouteOptimizationScope>(scopeText, true, out var scope))
            throw new ArgumentException("$.scope deve ser 'AllRoutes' ou 'SingleRoute'.");
        if (!DateOnly.TryParse(ReadRequiredString(parameters, "referenceDate"), out var referenceDate))
            throw new ArgumentException("$.referenceDate deve ser uma data válida no formato AAAA-MM-DD.");
        var targetRouteId = ReadOptionalGuid(parameters, "targetRouteId");
        var snapshotImportId = ReadOptionalGuid(parameters, "snapshotImportId");
        var run = await routeOptimizationService.StartOptimizationAsync(new RouteOptimizationStartRequest(
            scope, referenceDate, targetRouteId,
            request.Trigger == JobExecutionTrigger.Schedule
                ? RouteOptimizationRequestedFrom.InternalProcess
                : RouteOptimizationRequestedFrom.RouteScreen,
            request.RequestedByUserId ?? 0,
            snapshotImportId), cancellationToken);
        var job = await db.JobExecutions.SingleAsync(item =>
            item.JobType == OperationalJobCodes.RouteOptimization && item.RelatedEntityId == run.Id,
            cancellationToken);
        job.Trigger = request.Trigger;
        job.ScheduleId = request.ScheduleId;
        job.RetriedFromJobExecutionId = request.RetriedFromJobExecutionId;
        job.ParametersJson = parameters.GetRawText();
        job.ProgressMessage = "Na fila";
        await db.SaveChangesAsync(cancellationToken);
        return new JobLaunchResult(job.Id, job.Status.ToString().ToUpperInvariant());
    }

    private async Task ValidateReferenceAsync(string jobType, Guid id, CancellationToken cancellationToken)
    {
        var exists = jobType switch
        {
            OperationalJobCodes.ProcessImport or OperationalJobCodes.MunicipalityCoordinateEnrichment =>
                await db.RouteImports.AnyAsync(item => item.Id == id, cancellationToken),
            OperationalJobCodes.WhatsAppMessageProcessing =>
                await db.WhatsAppMessageReceipts.AnyAsync(item => item.Id == id, cancellationToken),
            _ => false
        };
        if (!exists) throw new ArgumentException("A referência informada no payload não existe.");
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
