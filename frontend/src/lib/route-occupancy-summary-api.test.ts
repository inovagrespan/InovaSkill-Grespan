import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { saveAuthToken } from "./auth";

describe("route occupancy summary api", () => {
  const localStorageMap = new Map<string, string>();
  const sessionStorageMap = new Map<string, string>();

  function createToken(exp: number): string {
    const header = Buffer.from(JSON.stringify({ alg: "HS256", typ: "JWT" })).toString("base64url");
    const payload = Buffer.from(JSON.stringify({ sub: "1", exp })).toString("base64url");
    return `${header}.${payload}.signature`;
  }

  beforeEach(() => {
    localStorageMap.clear();
    sessionStorageMap.clear();
    vi.stubGlobal("window", {
      localStorage: {
        getItem: (key: string) => localStorageMap.get(key) ?? null,
        setItem: (key: string, value: string) => localStorageMap.set(key, value),
        removeItem: (key: string) => localStorageMap.delete(key),
      },
      sessionStorage: {
        getItem: (key: string) => sessionStorageMap.get(key) ?? null,
        setItem: (key: string, value: string) => sessionStorageMap.set(key, value),
        removeItem: (key: string) => sessionStorageMap.delete(key),
      },
      location: { assign: vi.fn(), pathname: "/", search: "" },
    });
    saveAuthToken(createToken(Math.floor(Date.now() / 1000) + 60));
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("carrega a taxa de ocupação do snapshot atual sem filtro de período", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({
      occupancyRatePercent: 74.6,
      totalWeightKg: 58_320.5,
      totalCapacityKg: 78_150,
      routeCount: 42,
      routesWithCapacity: 39,
      routesWithoutCapacity: 3,
      snapshot: {
        importId: "import-1",
        version: 12,
        fileName: "rotas-julho.xlsx",
        finishedAt: "2026-07-08T13:42:00Z",
      },
    }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const { fetchRouteOccupancySummary } = await import("./importer-api");
    const result = await fetchRouteOccupancySummary();
    const requestedUrl = String(fetchMock.mock.calls[0][0]);

    expect(requestedUrl).toContain("/api/routes/occupancy-summary");
    expect(requestedUrl).not.toContain("period");
    expect(requestedUrl).not.toContain("date");
    expect(result).toEqual({
      occupancyRatePercent: 74.6,
      totalWeightKg: 58_320.5,
      totalCapacityKg: 78_150,
      routeCount: 42,
      routesWithCapacity: 39,
      routesWithoutCapacity: 3,
      snapshot: {
        importId: "import-1",
        version: 12,
        fileName: "rotas-julho.xlsx",
        finishedAt: "2026-07-08T13:42:00Z",
      },
    });
  });
});
