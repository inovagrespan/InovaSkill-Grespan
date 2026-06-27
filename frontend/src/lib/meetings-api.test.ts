import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@/lib/auth", () => ({
  authFetch: vi.fn((input: RequestInfo | URL, init?: RequestInit) => fetch(input, init)),
  getCurrentUser: vi.fn(() => ({ sub: "1" })),
  getCurrentUserRole: vi.fn(() => "diretor"),
}));

describe("meetings api fallback", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.resetModules();
    vi.unstubAllGlobals();
  });

  it("retorna reuniões operacionais coerentes quando a lista da API vem vazia", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify([]), { status: 200 })));

    const { fetchMeetings } = await import("./meetings-api");
    const { getControlTowerScenario } = await import("./control-tower-dashboard");
    const meetings = await fetchMeetings();

    expect(meetings).toHaveLength(20);
    expect(meetings.slice(0, 3).map((meeting) => meeting.title)).toEqual([
      "Comitê de ruptura e abastecimento",
      "Revisão de metas comerciais",
      "Planejamento de rotas críticas",
    ]);
    expect(meetings[0].description).toContain("três SKUs críticos");
    expect(meetings[0].currentStage).toBe("discussao");

    const dashboardMeetingTitles = (["today", "next7", "next30"] as const).map((period) =>
      getControlTowerScenario(period).cards.find((card) => card.module === "Reuniões")?.title,
    );
    expect(dashboardMeetingTitles).toEqual([
      meetings[0].title,
      meetings[2].title,
      meetings[1].title,
    ]);
    expect(new Set(meetings.map((meeting) => meeting.id)).size).toBe(20);
    expect(meetings.some((meeting) => meeting.title === "Tratativa de alertas críticos")).toBe(true);
    expect(meetings.filter((meeting) => meeting.status === "concluida").length).toBeGreaterThanOrEqual(2);
    expect(meetings.every((meeting) => meeting.id >= 9001)).toBe(true);
  });

  it("completa reuniões reais da API com mockadas até a lista ter volume de teste", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify([{
      id: 7,
      title: "Reunião real",
      description: "Registro vindo da API.",
      status: "rascunho",
      currentStage: "contexto",
      createdByName: "Diretor",
      createdAt: "2026-06-25T10:00:00Z",
      scheduledAt: null,
      participantCount: 1,
      problemCount: 0,
      questionCount: 0,
      overdueActionCount: 0,
    }]), { status: 200 })));

    const { fetchMeetings } = await import("./meetings-api");
    const meetings = await fetchMeetings();

    expect(meetings).toHaveLength(20);
    expect(meetings[0].title).toBe("Reunião real");
    expect(meetings.filter((meeting) => meeting.id >= 9001)).toHaveLength(19);
    expect(meetings.filter((meeting) => meeting.status === "concluida" && meeting.id >= 9001)).toHaveLength(2);
  });

  it("retorna detalhe operacional quando a API de detalhe falha", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("", { status: 404 })));

    const { fetchMeeting } = await import("./meetings-api");
    const meeting = await fetchMeeting(99);

    expect(meeting.id).toBe(99);
    expect(meeting.title).toBe("Comitê de ruptura e abastecimento");
    expect(meeting.context).toContain("Pão Francês Congelado 60g");
    expect(meeting.aiSummary).toContain("três SKUs");
    expect(meeting.participants.length).toBeGreaterThan(0);
    expect(meeting.problems.length).toBeGreaterThan(0);
  });

  it("retorna reunião mockada já concluída com dados de acompanhamento", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("", { status: 404 })));

    const { fetchMeeting } = await import("./meetings-api");
    const meeting = await fetchMeeting(9002);

    expect(meeting.status).toBe("concluida");
    expect(meeting.currentStage).toBe("acompanhamento");
    expect(meeting.concludedAt).not.toBeNull();
    expect(meeting.decisions.length).toBeGreaterThan(0);
    expect(meeting.actions.length).toBeGreaterThan(0);
  });
});
