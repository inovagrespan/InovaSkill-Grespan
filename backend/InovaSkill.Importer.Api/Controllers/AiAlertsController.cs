using System.Security.Claims;
using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Application.Analytics;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Api.Controllers;

[ApiController]
[Route("api/ai-alerts")]
public sealed class AiAlertsController(ImportDbContext dbContext) : ControllerBase
{
    private const string DirectorRecipient = "Diretoria";
    private const string SuperiorRecipient = "Superior imediato";
    private const string DefaultNotificationReason = "Alerta gerado pela IA";
    private const string ResponseDeadlineMissedReason = "Prazo de resposta vencido";
    private const string ActionDeadlineMissedReason = "Prazo de ação vencido";
    private const string CriticalAlertReason = "Alerta crítico";

    [HttpGet("dashboard")]
    public async Task<ActionResult<AiAlertDashboardDto>> GetDashboard(
        [FromQuery] string? area,
        [FromQuery] string? status,
        [FromQuery] string? severity,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var current = CurrentUser();
        var alerts = (await QueryAlertsBase()
            .ToListAsync(cancellationToken))
            .Where(x => AiAlertPolicy.IsVisibleTo(x, current.Role, current.Name, current.Email))
            .ToList();

        if (!string.IsNullOrWhiteSpace(area))
        {
            alerts = alerts
                .Where(x => string.Equals(AiAlertPolicy.Normalize(x.ResponsibleArea), AiAlertPolicy.Normalize(area), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            alerts = alerts
                .Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            alerts = alerts
                .Where(x => string.Equals(x.Severity, severity, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var ordered = alerts
            .OrderByDescending(x => SeverityWeight(x.Severity))
            .ThenBy(x => x.ResponseDeadlineAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

        return Ok(new AiAlertDashboardDto(BuildSummary(ordered, now), ordered.Select(x => ToDto(x, now)).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<AiAlertItemDto>> Create(
        [FromBody] CreateAiAlertRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateCreateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var now = DateTime.UtcNow;
        var alert = new AiAlert
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            ResponsibleArea = request.ResponsibleArea.Trim(),
            ResponsibleManager = request.ResponsibleManager.Trim(),
            InvolvedAreasCsv = ToCsv(request.InvolvedAreas),
            InvolvedUsersCsv = ToCsv(request.InvolvedUsers),
            Severity = request.Severity.Trim(),
            Origin = string.IsNullOrWhiteSpace(request.Origin) ? AiAlertOrigins.Ia : request.Origin.Trim(),
            EvidenceJson = string.IsNullOrWhiteSpace(request.EvidenceJson) ? "{}" : request.EvidenceJson.Trim(),
            ExpectedImpact = request.ExpectedImpact.Trim(),
            ResponseDeadlineAt = request.ResponseDeadlineAt,
            ActionDeadlineAt = request.ActionDeadlineAt,
            AiSuggestion = request.AiSuggestion.Trim(),
            RequiresMeeting = request.RequiresMeeting,
            RelatedTasksCsv = ToCsv(request.RelatedTasks),
            LinkedDecision = request.LinkedDecision?.Trim() ?? string.Empty,
            CreatedAt = now,
            LastNotificationAt = now,
            NotificationCount = 1
        };

        alert.StatusHistory.Add(new AiAlertStatusHistory
        {
            PreviousStatus = string.Empty,
            NewStatus = AiAlertStatuses.Novo,
            ChangedBy = CurrentUser().Name,
            Justification = "Criação do alerta",
            ChangedAt = now
        });
        alert.NotificationHistory.Add(new AiAlertNotificationHistory
        {
            Recipient = alert.ResponsibleManager,
            Channel = AiAlertNotificationChannels.Sistema,
            Reason = DefaultNotificationReason,
            SentAt = now
        });

        if (AiAlertPolicy.IsCritical(alert))
        {
            alert.NotificationCount++;
            alert.NotificationHistory.Add(new AiAlertNotificationHistory
            {
                Recipient = DirectorRecipient,
                Channel = AiAlertNotificationChannels.Painel,
                Reason = CriticalAlertReason,
                SentAt = now
            });
        }

        dbContext.AiAlerts.Add(alert);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = alert.Id }, ToDto(alert, now));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AiAlertItemDto>> GetById(long id, CancellationToken cancellationToken = default)
    {
        var current = CurrentUser();
        var alert = (await QueryAlertsBase()
            .Where(x => x.Id == id)
            .ToListAsync(cancellationToken))
            .FirstOrDefault(x => AiAlertPolicy.IsVisibleTo(x, current.Role, current.Name, current.Email));

        return alert is null ? NotFound() : Ok(ToDto(alert, DateTime.UtcNow));
    }

    [HttpPost("{id:long}/status")]
    public async Task<ActionResult<AiAlertItemDto>> UpdateStatus(
        long id,
        [FromBody] UpdateAiAlertStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!AiAlertPolicy.ValidStatuses.Contains(request.Status))
        {
            return BadRequest("Status de alerta inválido.");
        }

        if (string.Equals(request.Status, AiAlertStatuses.CanceladoComJustificativa, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(request.CancellationReason))
        {
            return BadRequest("Justificativa de cancelamento é obrigatória.");
        }

        var current = CurrentUser();
        var alert = (await QueryAlertsBase()
            .Where(x => x.Id == id)
            .ToListAsync(cancellationToken))
            .FirstOrDefault(x => AiAlertPolicy.IsVisibleTo(x, current.Role, current.Name, current.Email));
        if (alert is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        var previous = alert.Status;
        alert.Status = request.Status.Trim();
        alert.StatusHistory.Add(new AiAlertStatusHistory
        {
            PreviousStatus = previous,
            NewStatus = alert.Status,
            ChangedBy = current.Name,
            Justification = request.Justification?.Trim() ?? string.Empty,
            ChangedAt = now
        });

        if (string.Equals(alert.Status, AiAlertStatuses.Visualizado, StringComparison.OrdinalIgnoreCase))
        {
            alert.ViewedAt ??= now;
        }

        if (string.Equals(alert.Status, AiAlertStatuses.Resolvido, StringComparison.OrdinalIgnoreCase))
        {
            alert.ResolvedAt = now;
        }

        if (string.Equals(alert.Status, AiAlertStatuses.CanceladoComJustificativa, StringComparison.OrdinalIgnoreCase))
        {
            alert.ResolvedAt = now;
            alert.CancellationReason = request.CancellationReason!.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(alert, now));
    }

    [HttpPost("{id:long}/evaluate-escalation")]
    public async Task<ActionResult<EvaluateAiAlertEscalationResponseDto>> EvaluateEscalation(
        long id,
        CancellationToken cancellationToken = default)
    {
        var current = CurrentUser();
        var alert = (await QueryAlertsBase()
            .Where(x => x.Id == id)
            .ToListAsync(cancellationToken))
            .FirstOrDefault(x => AiAlertPolicy.IsVisibleTo(x, current.Role, current.Name, current.Email));
        if (alert is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        var escalated = false;
        var message = "Alerta dentro do prazo.";

        if (alert.IsResolved)
        {
            return Ok(new EvaluateAiAlertEscalationResponseDto(alert.Id, alert.Status, alert.NotificationCount, alert.EscalationCount, false, "Alerta já finalizado."));
        }

        if (now > alert.ResponseDeadlineAt && string.Equals(alert.Status, AiAlertStatuses.Novo, StringComparison.OrdinalIgnoreCase))
        {
            alert.NotificationCount++;
            alert.LastNotificationAt = now;
            alert.Status = AiAlertStatuses.Atrasado;
            alert.NotificationHistory.Add(new AiAlertNotificationHistory
            {
                Recipient = alert.ResponsibleManager,
                Channel = AiAlertNotificationChannels.Sistema,
                Reason = ResponseDeadlineMissedReason,
                SentAt = now
            });
            alert.StatusHistory.Add(new AiAlertStatusHistory
            {
                PreviousStatus = AiAlertStatuses.Novo,
                NewStatus = AiAlertStatuses.Atrasado,
                ChangedBy = "Sistema",
                Justification = ResponseDeadlineMissedReason,
                ChangedAt = now
            });
            message = "Gestor notificado novamente por atraso de resposta.";
        }

        var mustEscalate = (now > alert.ResponseDeadlineAt && alert.NotificationCount > 1) ||
            (alert.ActionDeadlineAt.HasValue && now > alert.ActionDeadlineAt.Value) ||
            AiAlertPolicy.IsCritical(alert);

        if (mustEscalate && alert.EscalatedAt is null)
        {
            var previous = alert.Status;
            alert.Status = AiAlertStatuses.EscaladoParaDiretoria;
            alert.EscalatedAt = now;
            alert.EscalationCount++;
            alert.NotificationCount++;
            alert.EscalationHistory.Add(new AiAlertEscalationHistory
            {
                FromRecipient = string.IsNullOrWhiteSpace(alert.ResponsibleManager) ? SuperiorRecipient : alert.ResponsibleManager,
                ToRecipient = DirectorRecipient,
                Reason = alert.ActionDeadlineAt.HasValue && now > alert.ActionDeadlineAt.Value ? ActionDeadlineMissedReason : ResponseDeadlineMissedReason,
                EscalatedAt = now
            });
            alert.NotificationHistory.Add(new AiAlertNotificationHistory
            {
                Recipient = DirectorRecipient,
                Channel = AiAlertNotificationChannels.Painel,
                Reason = alert.ActionDeadlineAt.HasValue && now > alert.ActionDeadlineAt.Value ? ActionDeadlineMissedReason : CriticalAlertReason,
                SentAt = now
            });
            alert.StatusHistory.Add(new AiAlertStatusHistory
            {
                PreviousStatus = previous,
                NewStatus = alert.Status,
                ChangedBy = "Sistema",
                Justification = "Escalonamento automático",
                ChangedAt = now
            });
            escalated = true;
            message = "Alerta escalado para a diretoria.";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new EvaluateAiAlertEscalationResponseDto(alert.Id, alert.Status, alert.NotificationCount, alert.EscalationCount, escalated, message));
    }

    private IQueryable<AiAlert> QueryAlertsBase()
    {
        return dbContext.AiAlerts
            .Include(x => x.StatusHistory)
            .Include(x => x.NotificationHistory)
            .Include(x => x.EscalationHistory)
            .AsSplitQuery();
    }

    private static AiAlertSummaryDto BuildSummary(IReadOnlyList<AiAlert> alerts, DateTime now)
    {
        var byArea = alerts
            .GroupBy(x => x.ResponsibleArea)
            .OrderBy(x => x.Key)
            .Select(x => new AiAlertAreaSummaryDto(
                x.Key,
                x.Count(),
                x.Count(AiAlertPolicy.IsCritical),
                x.Count(alert => AiAlertPolicy.IsLate(alert, now))))
            .ToList();

        return new AiAlertSummaryDto(
            alerts.Count,
            alerts.Count(AiAlertPolicy.IsCritical),
            alerts.Count(alert => AiAlertPolicy.IsLate(alert, now)),
            alerts.Count(x => x.EscalatedAt is not null || string.Equals(x.Status, AiAlertStatuses.EscaladoParaDiretoria, StringComparison.OrdinalIgnoreCase)),
            alerts.Count(x => x.RequiresMeeting),
            byArea);
    }

    private static AiAlertItemDto ToDto(AiAlert alert, DateTime now)
    {
        return new AiAlertItemDto(
            alert.Id,
            alert.Title,
            alert.Description,
            alert.ResponsibleArea,
            alert.ResponsibleManager,
            AiAlertPolicy.Csv(alert.InvolvedAreasCsv),
            AiAlertPolicy.Csv(alert.InvolvedUsersCsv),
            alert.Severity,
            alert.Status,
            alert.Origin,
            alert.EvidenceJson,
            alert.ExpectedImpact,
            alert.ResponseDeadlineAt,
            alert.ActionDeadlineAt,
            alert.AiSuggestion,
            alert.RequiresMeeting,
            AiAlertPolicy.Csv(alert.RelatedTasksCsv),
            alert.LinkedDecision,
            alert.CreatedAt,
            alert.ResolvedAt,
            alert.CancellationReason,
            alert.ViewedAt,
            alert.LastNotificationAt,
            alert.EscalatedAt,
            alert.NotificationCount,
            alert.EscalationCount,
            AiAlertPolicy.IsLate(alert, now),
            alert.StatusHistory.OrderBy(x => x.ChangedAt).Select(x => new AiAlertStatusHistoryDto(x.PreviousStatus, x.NewStatus, x.ChangedBy, x.Justification, x.ChangedAt)).ToList(),
            alert.NotificationHistory.OrderBy(x => x.SentAt).Select(x => new AiAlertNotificationHistoryDto(x.Recipient, x.Channel, x.Reason, x.SentAt)).ToList(),
            alert.EscalationHistory.OrderBy(x => x.EscalatedAt).Select(x => new AiAlertEscalationHistoryDto(x.FromRecipient, x.ToRecipient, x.Reason, x.EscalatedAt)).ToList());
    }

    private static string? ValidateCreateRequest(CreateAiAlertRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Título do alerta é obrigatório.";
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return "Descrição do alerta é obrigatória.";
        }

        if (!AiAlertPolicy.ValidAreas.Contains(request.ResponsibleArea))
        {
            return "Área responsável inválida.";
        }

        if (string.IsNullOrWhiteSpace(request.ResponsibleManager))
        {
            return "Gestor responsável é obrigatório.";
        }

        if (!AiAlertPolicy.ValidSeverities.Contains(request.Severity))
        {
            return "Gravidade inválida.";
        }

        if (request.ResponseDeadlineAt <= DateTime.UtcNow.AddMinutes(-1))
        {
            return "Prazo de resposta deve ser futuro.";
        }

        if (request.ActionDeadlineAt.HasValue && request.ActionDeadlineAt.Value <= request.ResponseDeadlineAt)
        {
            return "Prazo de ação deve ser posterior ao prazo de resposta.";
        }

        return null;
    }

    private static int SeverityWeight(string severity)
    {
        return severity switch
        {
            AiAlertSeverities.Critico => 4,
            AiAlertSeverities.Alto => 3,
            AiAlertSeverities.Medio => 2,
            _ => 1
        };
    }

    private static string ToCsv(IReadOnlyList<string>? values)
    {
        return string.Join(", ", values?.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase) ?? []);
    }

    private (string? Role, string Name, string? Email) CurrentUser()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var name = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? "Sistema";
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        return (role, name, email);
    }
}
