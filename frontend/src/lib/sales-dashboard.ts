import type {
  CommercialTransactionSummaryResponse,
  CommercialTransactionTimelineResponse,
} from "@/lib/importer-api";
import { formatSalesTimelineLabel } from "@/lib/sales-timeline";

export type SalesTrendPoint = {
  label: string;
  value: number;
  tooltipLabel: string;
};

export function buildSalesTrendData(
  timeline: CommercialTransactionTimelineResponse | null,
): SalesTrendPoint[] {
  if (!timeline) {
    return [];
  }

  return timeline.items.map((item) => ({
    label: formatSalesTimelineLabel(item.periodStart, timeline.granularity),
    value: item.totalAmount,
    tooltipLabel: `${item.periodStart}T00:00:00Z`,
  }));
}

export function buildSalesRevenueComparisonText(
  summary: CommercialTransactionSummaryResponse | null,
): string {
  if (!summary || summary.totalRecords === 0) {
    return "Sem resultado para o período e filtros atuais.";
  }

  if (summary.totalGrowthPercent == null) {
    return "Sem base comparativa para o período anterior.";
  }

  if (summary.totalGrowthPercent < 0) {
    return "Faturamento abaixo do período anterior.";
  }

  if (summary.totalGrowthPercent > 0) {
    return "Faturamento acima do período anterior.";
  }

  return "Faturamento igual ao período anterior.";
}

export function describeSalesTimelineGranularity(
  granularity: CommercialTransactionTimelineResponse["granularity"],
): string {
  switch (granularity) {
    case "daily":
      return "diária";
    case "weekly":
      return "semanal";
    default:
      return "mensal";
  }
}
