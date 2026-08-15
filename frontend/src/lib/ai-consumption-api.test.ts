import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { saveAuthToken } from "./auth";

describe("API de consumo de IA", () => {
  beforeEach(() => {
    const storage = new Map<string, string>();
    const sessionStorage = new Map<string, string>();
    vi.stubGlobal("window", {
      localStorage: { getItem: (key: string) => storage.get(key) ?? null, setItem: (key: string, value: string) => storage.set(key, value), removeItem: (key: string) => storage.delete(key) },
      sessionStorage: { getItem: (key: string) => sessionStorage.get(key) ?? null, setItem: (key: string, value: string) => sessionStorage.set(key, value), removeItem: (key: string) => sessionStorage.delete(key) },
      location: { assign: vi.fn(), pathname: "/", search: "" },
    });
    const payload = Buffer.from(JSON.stringify({ sub: "1", exp: Math.floor(Date.now() / 1000) + 60 })).toString("base64url");
    saveAuthToken(`header.${payload}.signature`);
  });

  afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });

  it("busca usuários por nome ou e-mail com paginação no servidor", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({ page: 2, pageSize: 20, total: 42, items: [] }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    const { listAiConsumptionUsers } = await import("./ai-consumption-api");

    const result = await listAiConsumptionUsers({ search: "maria+financeiro@test.com", page: 2, pageSize: 20 });
    const url = String(fetchMock.mock.calls[0][0]);

    expect(url).toContain("/api/admin/ai-consumption/users?");
    expect(url).toContain("search=maria%2Bfinanceiro%40test.com");
    expect(url).toContain("page=2&pageSize=20");
    expect(result.total).toBe(42);
  });

  it("pagina o detalhamento sem alterar o período agregado", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({ detailPage: 3, detailPageSize: 25, detailTotal: 80, details: [], total: {} }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    const { getAiConsumptionReport } = await import("./ai-consumption-api");

    await getAiConsumptionReport("2026-08-01T00:00:00Z", "2026-09-01T00:00:00Z", "7", 3, 25);
    const url = String(fetchMock.mock.calls[0][0]);

    expect(url).toContain("userId=7");
    expect(url).toContain("detailPage=3&detailPageSize=25");
  });
});
