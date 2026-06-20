import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { saveAuthToken } from "./auth";

describe("commercial invoice api", () => {
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

  it("normaliza o resumo de notas fiscais vindo da API", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({
      Page: 1,
      PageSize: 20,
      TotalItems: 1,
      TotalAmount: 450,
      TotalQuantity: 12,
      TotalWeightKg: 30,
      Items: [
        {
          DocumentNumber: "NF-123",
          TransactionDate: "2026-06-03T00:00:00Z",
          CustomerCode: "C1",
          CustomerName: "Cliente A",
          City: "Campinas",
          TransactionType: "Venda",
          TotalAmount: 450,
          TotalQuantity: 12,
          TotalWeightKg: 30,
          TotalItems: 3,
        },
      ],
    }), { status: 200 })));

    const { fetchCommercialInvoices } = await import("./importer-api");
    const result = await fetchCommercialInvoices({ page: 1, pageSize: 20 });

    expect(result.totalItems).toBe(1);
    expect(result.totalAmount).toBe(450);
    expect(result.items[0]).toEqual(expect.objectContaining({
      documentNumber: "NF-123",
      transactionDate: "2026-06-03",
      customerName: "Cliente A",
      totalItems: 3,
    }));
  });

  it("normaliza os detalhes da nota fiscal vindos da API", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({
      DocumentNumber: "NF-123",
      TransactionDate: "2026-06-03T00:00:00Z",
      CustomerCode: "C1",
      CustomerName: "Cliente A",
      City: "Campinas",
      TransactionType: "Venda",
      TotalAmount: 450,
      TotalQuantity: 12,
      TotalWeightKg: 30,
      TotalItems: 2,
      Items: [
        {
          Id: 10,
          DocumentNumber: "NF-123",
          TransactionDate: "2026-06-03T00:00:00Z",
          CustomerCode: "C1",
          CustomerName: "Cliente A",
          ProductCode: "P1",
          ProductDescription: "Produto 1",
          Quantity: 5,
          UnitPrice: 10,
          TotalAmount: 50,
          TransactionType: "Venda",
          City: "Campinas",
          ProductGroup: "Grupo A",
          GrossWeightKg: 11,
          SourceFileJobId: 1,
        },
      ],
    }), { status: 200 })));

    const { fetchCommercialInvoiceDetails } = await import("./importer-api");
    const result = await fetchCommercialInvoiceDetails("NF-123");

    expect(result.documentNumber).toBe("NF-123");
    expect(result.customerName).toBe("Cliente A");
    expect(result.items[0]).toEqual(expect.objectContaining({
      id: 10,
      productCode: "P1",
      totalAmount: 50,
    }));
  });

  it("normaliza a análise consolidada de notas fiscais", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({
      Granularity: "week",
      Summary: {
        TotalInvoices: 3,
        TotalAmount: 1500,
        TotalWeightKg: 42.5,
        TotalCustomers: 2,
        TotalItems: 7,
        TotalQuantity: 18,
      },
      Trend: [
        {
          PeriodStart: "2026-06-01T00:00:00Z",
          InvoiceCount: 2,
          TotalAmount: 900,
          TotalWeightKg: 21.5,
        },
      ],
      Ranking: [
        {
          CustomerCode: "C1",
          CustomerName: "Cliente A",
          TotalAmount: 900,
          InvoiceCount: 2,
          TotalItems: 4,
          TotalWeightKg: 21.5,
        },
      ],
    }), { status: 200 })));

    const { fetchCommercialInvoiceAnalytics } = await import("./importer-api");
    const result = await fetchCommercialInvoiceAnalytics({ granularity: "week" });

    expect(result.granularity).toBe("week");
    expect(result.summary.totalInvoices).toBe(3);
    expect(result.summary.totalAmount).toBe(1500);
    expect(result.trend[0]).toEqual({
      periodStart: "2026-06-01",
      invoiceCount: 2,
      totalAmount: 900,
      totalWeightKg: 21.5,
    });
    expect(result.ranking[0]).toEqual({
      customerCode: "C1",
      customerName: "Cliente A",
      totalAmount: 900,
      invoiceCount: 2,
      totalItems: 4,
      totalWeightKg: 21.5,
    });
  });
});
