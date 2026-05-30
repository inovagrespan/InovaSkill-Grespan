import type { CustomerNewCustomersMonthlyPoint } from "./importer-api";

export type NewCustomersInsights = {
  totalMonths: number;
  averagePerMonth: number;
  peakMonthLabel: string;
  peakMonthValue: number;
};

export function computeNewCustomersInsights(points: CustomerNewCustomersMonthlyPoint[]): NewCustomersInsights {
  if (points.length === 0) {
    return {
      totalMonths: 0,
      averagePerMonth: 0,
      peakMonthLabel: "N/A",
      peakMonthValue: 0,
    };
  }

  const total = points.reduce((acc, item) => acc + item.newCustomers, 0);
  const peak = points.reduce((best, current) => (current.newCustomers > best.newCustomers ? current : best), points[0]);

  return {
    totalMonths: points.length,
    averagePerMonth: total / points.length,
    peakMonthLabel: new Date(peak.monthStart).toLocaleDateString("pt-BR", { month: "short", year: "numeric" }),
    peakMonthValue: peak.newCustomers,
  };
}
