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

    expect(meetings).toHaveLength(3);
    expect(meetings.map((meeting) => meeting.title)).toEqual([
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
});
