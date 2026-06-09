import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { saveAuthToken } from "./auth";

describe("finance dashboard api", () => {
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

  it("normaliza o dashboard financeiro vindo da API", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({
      Customers: ["Cliente A", "Cliente B"],
      Summary: { TotalRevenue: 7000, TotalOrders: 8, TotalQuantity: 70, AverageTicket: 875 },
      RevenueTrend: [{ Period: "2026-02", Label: "fev", Revenue: 6000 }],
      CustomerRanking: [{ Customer: "Cliente B", Revenue: 4000 }],
      Items: [{ Customer: "Cliente A", Date: "2026-02-10T00:00:00Z", Revenue: 2000, Orders: 2, Quantity: 20 }],
      Page: 1,
      PageSize: 20,
      TotalItems: 1,
      TotalPages: 1,
    }), { status: 200 })));

    const { fetchFinanceDashboard } = await import("./importer-api");
    const result = await fetchFinanceDashboard({ allTime: true, revenueGranularity: "monthly", page: 1, pageSize: 20 });

    expect(result.customers).toEqual(["Cliente A", "Cliente B"]);
    expect(result.summary).toEqual(expect.objectContaining({ totalRevenue: 7000, averageTicket: 875 }));
    expect(result.revenueTrend[0]).toEqual(expect.objectContaining({ period: "2026-02", revenue: 6000 }));
    expect(result.customerRanking[0]).toEqual(expect.objectContaining({ customer: "Cliente B", revenue: 4000 }));
    expect(result.items[0]).toEqual(expect.objectContaining({ customer: "Cliente A", orders: 2 }));
    expect(result.page).toBe(1);
    expect(result.pageSize).toBe(20);
    expect(result.totalItems).toBe(1);
    expect(result.totalPages).toBe(1);
  });

  it("usa fallback demo quando a API financeira não responde", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => {
      throw new Error("fetch failed");
    }));

    const { fetchFinanceDashboard } = await import("./importer-api");
    const result = await fetchFinanceDashboard({ allTime: true, revenueGranularity: "monthly", page: 1, pageSize: 20 });

    expect(result.customers.length).toBeGreaterThan(0);
    expect(result.summary.totalRevenue).toBeGreaterThan(0);
    expect(result.revenueTrend.length).toBeGreaterThan(0);
    expect(result.customerRanking.length).toBeGreaterThan(0);
    expect(result.page).toBe(1);
    expect(result.pageSize).toBe(20);
    expect(result.totalItems).toBeGreaterThan(0);
    expect(result.totalPages).toBeGreaterThan(0);
  });
});
