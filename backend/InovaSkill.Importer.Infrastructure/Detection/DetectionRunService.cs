using System.Data;
using InovaSkill.Importer.Application.Detection;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InovaSkill.Importer.Infrastructure.Detection;

public sealed class DetectionRunService(
    ImportDbContext dbContext,
    IDetectorRegistry detectorRegistry,
    ILogger<DetectionRunService> logger) : IDetectionRunService
{
    public async Task ExecuteAsync(Guid detectionRunId, CancellationToken cancellationToken)
    {
        var run = await dbContext.DetectionRuns
            .Include(x => x.DetectorDefinition)
            .SingleOrDefaultAsync(x => x.Id == detectionRunId, cancellationToken);

        if (run is null)
        {
            logger.LogWarning("DetectionRun {DetectionRunId} não encontrada.", detectionRunId);
            return;
        }

        if (run.Status is DetectionRunStatus.Succeeded)
        {
            logger.LogInformation("DetectionRun {DetectionRunId} já está concluída. Ignorando.", detectionRunId);
            return;
        }

        var detectorCode = run.DetectorDefinition!.Code;
        IDetector detector;
        try
        {
            detector = detectorRegistry.Get(detectorCode);
        }
        catch (InvalidOperationException)
        {
            await FailRunAsync(run, $"Detector '{detectorCode}' não encontrado no registry.", cancellationToken);
            return;
        }

        run.Status = DetectionRunStatus.Running;
        run.StartedAt ??= DateTime.UtcNow;
        run.AttemptCount++;
        await dbContext.SaveChangesAsync(cancellationToken);

        var context = new DetectionContext(
            DetectionRunId: run.Id,
            DetectorDefinitionId: run.DetectorDefinitionId,
            DetectorCode: detectorCode,
            ReferenceTime: DateTime.UtcNow);

        DetectionResult result;
        try
        {
            result = await detector.DetectAsync(context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Detector {DetectorCode} falhou na execução (run {DetectionRunId}).",
                detectorCode, detectionRunId);
            await FailRunAsync(run, $"Erro na execução do detector: {ex.Message}", cancellationToken);
            return;
        }

        if (result.AnalyzedItems < 0)
        {
            await FailRunAsync(run, "AnalyzedItems não pode ser negativo.", cancellationToken);
            return;
        }

        var validationError = ValidateFindings(result.Findings);
        if (validationError is not null)
        {
            await FailRunAsync(run, validationError, cancellationToken);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            var now = DateTime.UtcNow;
            foreach (var candidate in result.Findings)
            {
                var finding = new Finding
                {
                    Id = Guid.NewGuid(),
                    DetectionRunId = run.Id,
                    Fingerprint = candidate.Fingerprint,
                    Title = candidate.Title,
                    Description = candidate.Description,
                    SubjectType = candidate.SubjectType,
                    SubjectId = candidate.SubjectId,
                    SubjectLabel = candidate.SubjectLabel,
                    DetectedAt = now
                };
                dbContext.Findings.Add(finding);

                foreach (var evCandidate in candidate.Evidences)
                {
                    var evidence = new FindingEvidence
                    {
                        Id = Guid.NewGuid(),
                        FindingId = finding.Id,
                        Name = evCandidate.Name,
                        Value = evCandidate.Value,
                        ReferenceValue = evCandidate.ReferenceValue,
                        Unit = evCandidate.Unit,
                        Description = evCandidate.Description,
                        SourceType = evCandidate.SourceType,
                        SourceId = evCandidate.SourceId,
                        ObservedAt = evCandidate.ObservedAt
                    };
                    dbContext.FindingEvidences.Add(evidence);
                }
            }

            run.Status = DetectionRunStatus.Succeeded;
            run.AnalyzedItems = result.AnalyzedItems;
            run.FindingsCount = result.FindingsCount;
            run.FinishedAt = DateTime.UtcNow;
            run.StatusReason = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "DetectionRun {DetectionRunId} concluída. {AnalyzedItems} analisados, {FindingsCount} encontrados.",
                detectionRunId, result.AnalyzedItems, result.FindingsCount);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Falha ao persistir resultado da DetectionRun {DetectionRunId}.", detectionRunId);
            await FailRunAsync(run, $"Falha ao persistir resultado: {ex.Message}", cancellationToken);
        }
    }

    private static string? ValidateFindings(IReadOnlyCollection<FindingCandidate> findings)
    {
        if (findings is null)
            return "Findings não pode ser nulo.";

        var fingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var finding in findings)
        {
            if (string.IsNullOrWhiteSpace(finding.Fingerprint))
                return "Finding possui fingerprint vazio.";
            if (string.IsNullOrWhiteSpace(finding.Title))
                return "Finding possui title vazio.";
            if (string.IsNullOrWhiteSpace(finding.SubjectType))
                return "Finding possui subject type vazio.";
            if (string.IsNullOrWhiteSpace(finding.SubjectId))
                return "Finding possui subject id vazio.";

            if (!fingerprints.Add(finding.Fingerprint))
                return $"Fingerprint duplicado na mesma execução: '{finding.Fingerprint}'.";

            if (finding.Evidences is not null)
            {
                foreach (var ev in finding.Evidences)
                {
                    if (string.IsNullOrWhiteSpace(ev.Name))
                        return "Evidence possui nome vazio.";
                    if (string.IsNullOrWhiteSpace(ev.Value))
                        return "Evidence possui valor vazio.";
                }
            }
        }

        return null;
    }

    private async Task FailRunAsync(DetectionRun run, string reason, CancellationToken cancellationToken)
    {
        run.Status = DetectionRunStatus.Failed;
        run.StatusReason = reason;
        run.FinishedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogWarning("DetectionRun {DetectionRunId} marcada como Failed: {Reason}", run.Id, reason);
    }
}
