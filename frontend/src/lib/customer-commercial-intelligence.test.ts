import { describe, expect, it } from "vitest";
import type {
  CustomerComparisonItem,
  CustomerDetailSummary,
  CustomerInsightsResponse,
  CustomerTopProductItem,
} from "./importer-api";
import { buildCustomerCommercialIntelligence } from "./customer-commercial-intelligence";

const baseDetails: CustomerDetailSummary = {
  customerCode: "CLI-001",
  customerName: "Padaria São Bento",
  city: "São Paulo",
  linkedCompany: "Grespan Distribuição",
  lastPurchaseDate: "2026-05-10T00:00:00Z",
  status: "Ativo",
  totalRevenue: 10000,
  averageTicket: 1000,
  averageRevenueMonthly: 5000,
  averageRevenueWeekly: 1250,
  totalQuantity: 100,
  totalWeight: 50,
  totalOrders: 10,
  averageDaysBetweenPurchases: 12,
};

const baseInsights: CustomerInsightsResponse = {
  averagePurchaseFrequencyDays: 12,
  estimatedNextPurchaseDate: "2026-05-22T00:00:00Z",
  predictedRevenue: 6200,
  predictedQuantity: 60,
  consumptionTrend: "Crescimento",
  riskLevel: "Sem risco",
  daysWithoutPurchase: 8,
  riskScore: 0.67,
  frequencyReason: null,
  nextPurchaseReason: null,
  revenuePredictionReason: null,
  quantityPredictionReason: null,
  riskReason: null,
  monthlyHistoryPeriods: 6,
};

const growingComparison: CustomerComparisonItem[] = [
  {
    label: "Este mês vs mês anterior",
    currentValue: 12000,
    previousValue: 10000,
    variationPercent: 20,
  },
];

const topProducts: CustomerTopProductItem[] = [
  {
    productCode: "PAN-104",
    productDescription: "Pão Francês Congelado 60g",
    quantity: 80,
    revenue: 7000,
    sharePercent: 55,
  },
  {
    productCode: "PAN-318",
    productDescription: "Croissant Congelado 80g",
    quantity: 20,
    revenue: 2000,
    sharePercent: 20,
  },
];

describe("buildCustomerCommercialIntelligence", () => {
  it("gera leitura acionável para cliente saudável em crescimento", () => {
    const result = buildCustomerCommercialIntelligence({
      details: baseDetails,
      insights: baseInsights,
      comparison: growingComparison,
      topProducts,
    });

    expect(result.health.status).toBe("Saudável");
    expect(result.trend.status).toBe("Crescimento");
    expect(result.potential.metricValue).toBe(6200);
    expect(result.recommendation.status).toBe("Expandir relacionamento");
    expect(result.productsSummary).toContain("Pão Francês Congelado 60g concentra");
  });

  it("prioriza retenção quando cliente está crítico", () => {
    const result = buildCustomerCommercialIntelligence({
      details: { ...baseDetails, status: "Inativo" },
      insights: {
        ...baseInsights,
        riskLevel: "Crítico",
        consumptionTrend: "Queda",
        daysWithoutPurchase: 90,
      },
      comparison: [
        {
          ...growingComparison[0],
          currentValue: 6000,
          previousValue: 10000,
          variationPercent: -40,
        },
      ],
      topProducts,
    });

    expect(result.health.status).toBe("Crítico");
    expect(result.trend.status).toBe("Queda");
    expect(result.stability.status).toBe("Instável");
    expect(result.recommendation.status).toBe("Ação de retenção");
    expect(result.recommendation.summary).toContain("Pão Francês Congelado 60g");
  });

  it("trata bordas com histórico insuficiente sem mostrar decisão falsa", () => {
    const result = buildCustomerCommercialIntelligence({
      details: null,
      insights: {
        ...baseInsights,
        predictedRevenue: null,
        riskLevel: "Sem histórico suficiente",
        riskScore: null,
        consumptionTrend: "Estabilidade",
        revenuePredictionReason: "Necessário histórico de pelo menos 3 meses.",
        riskReason: "Necessário frequência média e data da última compra.",
        monthlyHistoryPeriods: 1,
      },
      comparison: [],
      topProducts: [],
    });

    expect(result.health.status).toBe("Atenção");
    expect(result.potential.status).toBe("Sem base suficiente");
    expect(result.stability.status).toBe("Sem base mensal");
    expect(result.productsSummary).toContain("Sem produtos suficientes");
  });
});
