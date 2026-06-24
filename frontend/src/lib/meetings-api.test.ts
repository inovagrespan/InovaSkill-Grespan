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

  it("retorna reunião simulada quando a lista da API vem vazia", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify([]), { status: 200 })));

    const { fetchMeetings } = await import("./meetings-api");
    const meetings = await fetchMeetings();

    expect(meetings).toHaveLength(3);
    expect(meetings[0].title).toContain("Simulação");
    expect(meetings[0].currentStage).toBe("discussao");
  });

  it("retorna detalhe simulado quando a API de detalhe falha", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("", { status: 404 })));

    const { fetchMeeting } = await import("./meetings-api");
    const meeting = await fetchMeeting(99);

    expect(meeting.id).toBe(99);
    expect(meeting.title).toContain("Simulação");
    expect(meeting.participants.length).toBeGreaterThan(0);
    expect(meeting.problems.length).toBeGreaterThan(0);
  });
});
