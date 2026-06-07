import fs from "node:fs";
import path from "node:path";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { saveAuthToken } from "./auth";

vi.mock("@/features/import-template-builder/utils/extract-headers-in-worker", () => ({
  extractHeadersInWorker: vi.fn(),
}));

vi.mock("@/lib/importer-progress", () => ({
  buildFallbackStages: vi.fn(() => []),
}));

describe("customer commercial health", () => {
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

  it("carrega prontuario comercial pelo endpoint dedicado do cliente", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({
      header: {
        customerCode: "C1",
        customerName: "Cliente A",
        city: "Campinas",
        linkedCompany: "Empresa Alpha",
        lastPurchaseDate: "2026-05-20T00:00:00Z",
        daysWithoutPurchase: 12,
        averageDaysBetweenPurchases: 4,
        commercialStatus: "Atenção",
      },
      score: { value: 64, label: "Atenção", explanation: "Score 64/100." },
      health: { status: "Atenção", tone: "warning", summary: "Cliente em acompanhamento.", detail: "Histórico médio é de 4 dias." },
      trend: { status: "Queda", tone: "warning", summary: "Consumo caiu.", detail: "Últimos 90 dias." },
      potential: { expectedRevenue: 120000, expectedQuantity: 400, label: "Potencial esperado", explanation: "Média recente." },
      dependency: { status: "Alta dependência", explanation: "80% em 2 produtos.", productsToReachEightyPercent: 2, topProductSharePercent: 55 },
      products: [{ productCode: "P1", productDescription: "Produto 1", quantity: 10, revenue: 100, sharePercent: 55 }],
      timeline: [{ date: "2026-05-20T00:00:00Z", orders: 1, revenue: 100, quantity: 10 }],
      evolution: [{ periodStart: "2026-05-01T00:00:00Z", revenue: 100, quantity: 10, orders: 1, averageTicket: 100 }],
      comparisons: [{ label: "Últimos 30 dias", revenue: 100, previousRevenue: 200, quantity: 10, previousQuantity: 20, orders: 1, previousOrders: 2, averageTicket: 100, previousAverageTicket: 100, revenueVariationPercent: -50, quantityVariationPercent: -50, ordersVariationPercent: -50, averageTicketVariationPercent: 0 }],
      recommendations: [{ priority: "Alta", title: "Contato imediato", detail: "Recuperar recorrência." }],
      alerts: [{ severity: "warning", title: "Queda relevante de faturamento", detail: "Consumo caiu." }],
    }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    const { fetchCustomerCommercialHealth } = await import("./importer-api");

    const report = await fetchCustomerCommercialHealth({ customerId: "C1" });

    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5279/api/customers/C1/commercial-health",
      expect.objectContaining({
        headers: expect.any(Headers),
      }),
    );
    const [, init] = fetchMock.mock.calls[0];
    expect(new Headers(init?.headers).get("Authorization")).toMatch(/^Bearer /);
    expect(report.header.customerName).toBe("Cliente A");
    expect(report.score.value).toBe(64);
    expect(report.products[0].sharePercent).toBe(55);
    expect(report.recommendations[0].priority).toBe("Alta");
  });

  it("expoe rota, menu e atalho para analise comercial completa", () => {
    const routeSource = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.analise-comercial.tsx"), "utf8");
    const clientsSource = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");
    const sidebarSource = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");

    expect(routeSource).toContain('createFileRoute("/clientes/analise-comercial")');
    expect(routeSource).toContain("Score comercial");
    expect(routeSource).toContain("Recomendações comerciais");
    expect(clientsSource).toContain("Ver análise completa");
    expect(sidebarSource).toContain('to: "/clientes/analise-comercial"');
    expect(sidebarSource).toContain('label: "Análise Comercial"');
  });
});
