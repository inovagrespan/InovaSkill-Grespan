using System.Security.Claims;
using InovaSkill.Importer.Api.Contracts;
using InovaSkill.Importer.Api.Controllers;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Tests.Api;

public sealed class AiAlertsControllerTests
{
    [Fact]
    public async Task Create_StoresAlertWithInitialStatusHistoryAndManagerNotification()
    {
        await using var db = CreateDb();
        var controller = CreateController(db, "producao", "Gestor Produção");
        var request = ValidRequest(
            responsibleArea: AiAlertAreas.Producao,
            severity: AiAlertSeverities.Critico,
            involvedAreas: [AiAlertAreas.Vendas, AiAlertAreas.Logistica]);

        var result = await controller.Create(request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var payload = Assert.IsType<AiAlertItemDto>(created.Value);
        Assert.Equal(AiAlertStatuses.Novo, payload.Status);
        Assert.Equal(2, payload.NotificationCount);
        Assert.Contains(payload.NotificationHistory, x => x.Recipient == "Gestor Produção");
        Assert.Contains(payload.NotificationHistory, x => x.Recipient == "Diretoria");
        Assert.Single(payload.StatusHistory);
    }

    [Fact]
    public async Task GetDashboard_FiltersVisibleAlertsByResponsibleAndInvolvedArea()
    {
        await using var db = CreateDb();
        db.AiAlerts.AddRange(
            BuildAlert("Baixa ocupação dos caminhões", AiAlertAreas.Logistica, AiAlertSeverities.Alto, involvedAreas: ""),
            BuildAlert("Risco de ruptura do cliente A", AiAlertAreas.Producao, AiAlertSeverities.Critico, involvedAreas: AiAlertAreas.Vendas),
            BuildAlert("Conciliação pendente", AiAlertAreas.Administrativo, AiAlertSeverities.Medio, involvedAreas: ""));
        await db.SaveChangesAsync();

        var controller = CreateController(db, "vendas", "Gestor Vendas");

        var result = await controller.GetDashboard(null, null, null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AiAlertDashboardDto>(ok.Value);
        Assert.Single(payload.Alerts);
        Assert.Equal("Risco de ruptura do cliente A", payload.Alerts[0].Title);
        Assert.Equal(1, payload.Summary.Critical);
    }

    [Fact]
    public async Task GetDashboard_DirectorSeesCriticalLateAndEscalatedAlerts()
    {
        await using var db = CreateDb();
        db.AiAlerts.AddRange(
            BuildAlert("Crítico", AiAlertAreas.Producao, AiAlertSeverities.Critico),
            BuildAlert("Atrasado", AiAlertAreas.Vendas, AiAlertSeverities.Medio, responseDeadlineAt: DateTime.UtcNow.AddHours(-2)),
            BuildAlert("Escalado", AiAlertAreas.Logistica, AiAlertSeverities.Baixo, status: AiAlertStatuses.EscaladoParaDiretoria, escalatedAt: DateTime.UtcNow),
            BuildAlert("Baixo dentro do prazo", AiAlertAreas.Administrativo, AiAlertSeverities.Baixo));
        await db.SaveChangesAsync();

        var controller = CreateController(db, "diretor", "Diretor");

        var result = await controller.GetDashboard(null, null, null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AiAlertDashboardDto>(ok.Value);
        Assert.Equal(3, payload.Alerts.Count);
        Assert.DoesNotContain(payload.Alerts, x => x.Title == "Baixo dentro do prazo");
        Assert.Equal(1, payload.Summary.Late);
        Assert.Equal(1, payload.Summary.Escalated);
    }

    [Fact]
    public async Task UpdateStatus_RequiresCancellationReasonAndRegistersHistory()
    {
        await using var db = CreateDb();
        db.AiAlerts.Add(BuildAlert("Estoque insuficiente", AiAlertAreas.Producao, AiAlertSeverities.Alto));
        await db.SaveChangesAsync();
        var controller = CreateController(db, "producao", "Gestor Produção");

        var invalid = await controller.UpdateStatus(1, new UpdateAiAlertStatusRequestDto(AiAlertStatuses.CanceladoComJustificativa, null, null));
        var valid = await controller.UpdateStatus(1, new UpdateAiAlertStatusRequestDto(AiAlertStatuses.CanceladoComJustificativa, "Duplicado", "Alerta duplicado"));

        Assert.IsType<BadRequestObjectResult>(invalid.Result);
        var ok = Assert.IsType<OkObjectResult>(valid.Result);
        var payload = Assert.IsType<AiAlertItemDto>(ok.Value);
        Assert.Equal("Alerta duplicado", payload.CancellationReason);
        Assert.NotNull(payload.ResolvedAt);
        Assert.Contains(payload.StatusHistory, x => x.NewStatus == AiAlertStatuses.CanceladoComJustificativa);
    }

    [Fact]
    public async Task EvaluateEscalation_NotifiesManagerThenEscalatesLateAlertToDirector()
    {
        await using var db = CreateDb();
        db.AiAlerts.Add(BuildAlert(
            "Ação de reunião atrasada",
            AiAlertAreas.Administrativo,
            AiAlertSeverities.Medio,
            responseDeadlineAt: DateTime.UtcNow.AddHours(-3),
            notificationCount: 1));
        await db.SaveChangesAsync();
        var controller = CreateController(db, "administrativo", "Gestor Administrativo");

        var result = await controller.EvaluateEscalation(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<EvaluateAiAlertEscalationResponseDto>(ok.Value);
        Assert.True(payload.Escalated);
        Assert.Equal(AiAlertStatuses.EscaladoParaDiretoria, payload.Status);
        Assert.Equal(3, payload.NotificationCount);
        Assert.Equal(1, payload.EscalationCount);
        var alert = await db.AiAlerts.Include(x => x.EscalationHistory).SingleAsync();
        Assert.Single(alert.EscalationHistory);
    }

    private static CreateAiAlertRequestDto ValidRequest(
        string responsibleArea,
        string severity,
        IReadOnlyList<string>? involvedAreas = null)
    {
        return new CreateAiAlertRequestDto(
            "Risco de não atender demanda do cliente X",
            "A IA identificou aumento incomum de demanda com estoque abaixo do necessário.",
            responsibleArea,
            "Gestor Produção",
            involvedAreas,
            ["operacao@local.test"],
            severity,
            AiAlertOrigins.Ia,
            "{\"fonte\":\"vendas\"}",
            "Risco de perda de faturamento e atraso ao cliente.",
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(24),
            "Agendar reunião entre Vendas, Produção e Logística.",
            true,
            ["Validar estoque", "Repriorizar produção"],
            null);
    }

    private static AiAlert BuildAlert(
        string title,
        string area,
        string severity,
        string involvedAreas = "",
        string status = AiAlertStatuses.Novo,
        DateTime? responseDeadlineAt = null,
        DateTime? escalatedAt = null,
        int notificationCount = 0)
    {
        return new AiAlert
        {
            Title = title,
            Description = "Descrição do alerta.",
            ResponsibleArea = area,
            ResponsibleManager = $"Gestor {area}",
            InvolvedAreasCsv = involvedAreas,
            Severity = severity,
            Status = status,
            ExpectedImpact = "Impacto operacional.",
            ResponseDeadlineAt = responseDeadlineAt ?? DateTime.UtcNow.AddHours(2),
            ActionDeadlineAt = DateTime.UtcNow.AddHours(24),
            AiSuggestion = "Avaliar ação corretiva.",
            EscalatedAt = escalatedAt,
            NotificationCount = notificationCount,
            EscalationCount = escalatedAt is null ? 0 : 1
        };
    }

    private static ImportDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ImportDbContext>()
            .UseInMemoryDatabase($"ai-alerts-{Guid.NewGuid():N}")
            .Options;

        return new ImportDbContext(options);
    }

    private static AiAlertsController CreateController(
        ImportDbContext db,
        string role,
        string name)
    {
        var controller = new AiAlertsController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("role", role),
                    new Claim(ClaimTypes.Role, role),
                    new Claim("name", name),
                    new Claim(ClaimTypes.Name, name)
                ], "TestAuth"))
            }
        };

        return controller;
    }
}
