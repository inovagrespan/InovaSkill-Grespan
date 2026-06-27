import fs from "node:fs";
import path from "node:path";
import { describe, expect, it, vi } from "vitest";
import { fetchAiAlertsDashboard, updateAiAlertStatus } from "@/lib/importer-api";
import { getControlTowerScenario } from "@/lib/control-tower-dashboard";

vi.mock("@/lib/auth", () => ({
  authFetch: vi.fn((input: RequestInfo | URL, init?: RequestInit) => fetch(input, init)),
}));

describe("ai alerts api", () => {
  it("normaliza resumo, itens e históricos retornados pela API", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({
      Summary: {
        Total: 1,
        Critical: 1,
        Late: 1,
        Escalated: 1,
        RequiresMeeting: 1,
        ByArea: [{ Area: "Produção", Total: 1, Critical: 1, Late: 1 }],
      },
      Alerts: [{
        Id: 7,
        Title: "Risco de não atender cliente",
        Description: "Demanda acima do previsto.",
        ResponsibleArea: "Produção",
        ResponsibleManager: "Gestor Produção",
        InvolvedAreas: ["Vendas"],
        InvolvedUsers: ["operacao@local.test"],
        Severity: "Crítico",
        Status: "Escalado para diretoria",
        Origin: "IA",
        EvidenceJson: "{}",
        ExpectedImpact: "Atraso ao cliente.",
        ResponseDeadlineAt: "2026-06-23T12:00:00Z",
        ActionDeadlineAt: "2026-06-24T12:00:00Z",
        AiSuggestion: "Agendar reunião.",
        RequiresMeeting: true,
        RelatedTasks: ["Validar estoque"],
        LinkedDecision: "",
        CreatedAt: "2026-06-23T10:00:00Z",
        ResolvedAt: null,
        CancellationReason: "",
        ViewedAt: null,
        LastNotificationAt: "2026-06-23T11:00:00Z",
        EscalatedAt: "2026-06-23T11:30:00Z",
        NotificationCount: 3,
        EscalationCount: 1,
        IsLate: true,
        StatusHistory: [{ PreviousStatus: "Novo", NewStatus: "Atrasado", ChangedBy: "Sistema", Justification: "Prazo vencido", ChangedAt: "2026-06-23T11:00:00Z" }],
        NotificationHistory: [],
        EscalationHistory: [],
      }],
    }), { status: 200 }));

    const dashboard = await fetchAiAlertsDashboard({ area: "Produção", status: "Atrasado", severity: "Crítico" });

    expect(fetchMock.mock.calls[0][0].toString()).toContain("area=Produ%C3%A7%C3%A3o");
    expect(dashboard.summary.byArea[0]).toEqual({ area: "Produção", total: 1, critical: 1, late: 1 });
    expect(dashboard.alerts[0].statusHistory[0].newStatus).toBe("Atrasado");
    expect(dashboard.alerts[0].notificationCount).toBe(3);
    fetchMock.mockRestore();
  });

  it("envia atualização de status com justificativa e razão de cancelamento", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({
      id: 9,
      title: "Alerta cancelado",
      status: "Cancelado com justificativa",
      severity: "Médio",
      involvedAreas: [],
      involvedUsers: [],
      relatedTasks: [],
      statusHistory: [],
      notificationHistory: [],
      escalationHistory: [],
    }), { status: 200 }));

    await updateAiAlertStatus({
      id: 9,
      status: "Cancelado com justificativa",
      justification: "Duplicado",
      cancellationReason: "Outro alerta já cobre o caso.",
    });

    expect(fetchMock.mock.calls[0][1]?.method).toBe("POST");
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body))).toEqual({
      status: "Cancelado com justificativa",
      justification: "Duplicado",
      cancellationReason: "Outro alerta já cobre o caso.",
    });
    fetchMock.mockRestore();
  });

  it("gera fallbacks quando a API retorna alerta com informações incompletas", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({
      Summary: { Total: 1 },
      Alerts: [{ Id: 11, Title: "", Description: "", ResponsibleArea: "", ResponsibleManager: "", Status: "", Origin: "", Severity: "" }],
    }), { status: 200 }));

    const dashboard = await fetchAiAlertsDashboard();
    const alert = dashboard.alerts[0];

    expect(alert.title).toBe("Alerta gerado pela IA");
    expect(alert.description).toContain("descrição detalhada ainda não foi sincronizada");
    expect(alert.responsibleArea).toBe("Área não informada");
    expect(alert.responsibleManager).toBe("Gestor de Área não informada");
    expect(alert.status).toBe("Novo");
    expect(alert.origin).toBe("IA");
    expect(alert.severity).toBe("Médio");
    expect(alert.aiSuggestion).toContain("Revisar o alerta");
    expect(alert.expectedImpact).toContain("Impacto ainda não calculado");
    expect(alert.createdAt).not.toBe("");
    fetchMock.mockRestore();
  });

  it("usa alertas operacionais de contingência quando a API responde sem registros", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({
      Summary: { Total: 0, Critical: 0, Late: 0, Escalated: 0, RequiresMeeting: 0, ByArea: [] },
      Alerts: [],
    }), { status: 200 }));

    const dashboard = await fetchAiAlertsDashboard();

    expect(dashboard.alerts.length).toBeGreaterThan(0);
    expect(dashboard.summary.total).toBe(dashboard.alerts.length);
    expect(dashboard.alerts.some((alert) => alert.title === "Risco de atraso no atendimento da Rede Primavera")).toBe(true);
    expect(dashboard.summary.byArea.length).toBeGreaterThan(0);
    expect(dashboard.summary).toEqual(expect.objectContaining({ total: 6, critical: 2, late: 2, requiresMeeting: 4 }));
    expect(getControlTowerScenario("today").cards.find((card) => card.module === "Alertas")?.value).toBe(`${dashboard.summary.critical} críticos`);
    expect(getControlTowerScenario("next7").cards.find((card) => card.module === "Alertas")?.value).toBe(`${dashboard.summary.requiresMeeting} alertas`);
    expect(getControlTowerScenario("next30").cards.find((card) => card.module === "Alertas")?.value).toBe(`${dashboard.summary.total} alertas`);
    fetchMock.mockRestore();
  });

  it("filtra alertas de contingência pelo filtro selecionado quando a base real está vazia", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(JSON.stringify({
      Summary: { Total: 0 },
      Alerts: [],
    }), { status: 200 }));

    const dashboard = await fetchAiAlertsDashboard({ area: "Logística" });

    expect(dashboard.alerts).toHaveLength(1);
    expect(dashboard.alerts[0].responsibleArea).toBe("Logística");
    expect(dashboard.summary.byArea).toEqual([{ area: "Logística", total: 1, critical: 0, late: 0 }]);
    fetchMock.mockRestore();
  });

  it("mantém fallbacks visuais na tela de alertas para evidências, tarefas e históricos", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/alertas.tsx"), "utf8");

    expect(source).toContain("buildEvidenceItems");
    expect(source).toContain("Sem evidências estruturadas sincronizadas.");
    expect(source).toContain("Tarefas recomendadas");
    expect(source).toContain("buildNotificationHistory");
    expect(source).toContain("buildEscalationHistory");
    expect(source).toContain("Registro inicial do alerta gerado pela IA.");
    expect(source).toContain("Sem escalonamento registrado até o momento.");
  });

  it("mantém refinamentos de layout da tela de alertas", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/alertas.tsx"), "utf8");

    expect(source).toContain("xl:grid-cols-[minmax(0,1fr)_auto]");
    expect(source).toContain("xl:grid-cols-5");
    expect(source).toContain("lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1fr)_auto]");
    expect(source).toContain("lg:grid-cols-[minmax(0,1fr)_minmax(150px,180px)_minmax(150px,180px)_32px]");
    expect(source).toContain("Filtros da fila");
    expect(source).toContain("focus-visible:ring-2");
    expect(source).toContain("border-l-2 border-primary/30");
    expect(source).toContain("hover:border-primary/40");
    expect(source).not.toContain("Smart Core / Alertas");
    expect(source).not.toContain("Base operacional + dados demonstrativos");
    expect(source).not.toContain("Fila inteligente");
    expect(source).not.toContain("Priorize pelo risco e pelo prazo");
  });
});
