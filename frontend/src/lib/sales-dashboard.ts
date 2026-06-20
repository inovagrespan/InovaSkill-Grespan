import type { CommercialInvoiceAnalyticsResponse } from "@/lib/importer-api";
import { formatSalesTimelineLabel } from "@/lib/sales-timeline";

export type SalesTrendMetric = "invoiceCount" | "totalAmount" | "totalWeightKg";
export type SalesRankingMetric = "amount" | "invoiceCount" | "items" | "weight";

export type SalesTrendPoint = {
  label: string;
  value: number;
  tooltipLabel: string;
};

export type SalesRankingPoint = {
  companyName: string;
  value: number;
};

export function buildSalesTrendData(
  analytics: CommercialInvoiceAnalyticsResponse | null,
  metric: SalesTrendMetric,
): SalesTrendPoint[] {
  if (!analytics) {
    return [];
  }

  return analytics.trend.map((item) => ({
    label: formatSalesTimelineLabel(item.periodStart, analytics.granularity),
    value: item[metric],
    tooltipLabel: item.periodStart.includes("T") ? item.periodStart : `${item.periodStart}T00:00:00Z`,
  }));
}

export function buildSalesRankingData(
  analytics: CommercialInvoiceAnalyticsResponse | null,
  metric: SalesRankingMetric,
): SalesRankingPoint[] {
  if (!analytics) {
    return [];
  }

  const resolveValue = (item: CommercialInvoiceAnalyticsResponse["ranking"][number]) => {
    switch (metric) {
      case "invoiceCount":
        return item.invoiceCount;
      case "items":
        return item.totalItems;
      case "weight":
        return item.totalWeightKg;
      default:
        return item.totalAmount;
    }
  };

  return [...analytics.ranking]
    .sort((left, right) => {
      const difference = resolveValue(right) - resolveValue(left);
      if (difference !== 0) {
        return difference;
      }

      return left.customerName.localeCompare(right.customerName, "pt-BR");
    })
    .slice(0, 8)
    .map((item) => ({
      companyName: item.customerName,
      value: resolveValue(item),
    }));
}

export function describeSalesTimelineGranularity(
  granularity: CommercialInvoiceAnalyticsResponse["granularity"],
): string {
  switch (granularity) {
    case "day":
      return "dia";
    case "week":
      return "semana";
    default:
      return "mês";
  }
}

export function describeSalesTrendMetric(metric: SalesTrendMetric): string {
  switch (metric) {
    case "invoiceCount":
      return "Quantidade de notas emitidas";
    case "totalWeightKg":
      return "Peso total movimentado";
    default:
      return "Valor total das notas";
  }
}

export function describeSalesRankingMetric(metric: SalesRankingMetric): string {
  switch (metric) {
    case "invoiceCount":
      return "Quantidade de notas por cliente";
    case "items":
      return "Quantidade de itens por cliente";
    case "weight":
      return "Peso total movimentado por cliente";
    default:
      return "Valor total das notas por cliente";
  }
}
