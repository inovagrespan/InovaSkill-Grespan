export type SalesFinancialPeriod = "daily" | "monthly" | "yearly";

export type SalesFinancialRecord = {
  date: string;
  revenue: number | null;
  expenses: number | null;
};

export type SalesFinancialPoint = {
  period: string;
  label: string;
  revenue: number;
  expenses: number;
};

const DAILY_POINT_COUNT = 14;
const MONTHLY_POINT_COUNT = 12;
const YEARLY_POINT_COUNT = 6;
const MILLISECONDS_PER_DAY = 86_400_000;
export const SALES_FINANCIAL_REFERENCE_DATE = "2026-06-24";

function roundCurrency(value: number | null): number {
  if (value == null || !Number.isFinite(value)) return 0;
  return Math.round((Math.max(0, value) + Number.EPSILON) * 100) / 100;
}

function parseDate(value: string): Date | null {
  const date = new Date(`${value}T00:00:00Z`);
  return Number.isNaN(date.getTime()) ? null : date;
}

function dateKey(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function monthKey(date: Date): string {
  return date.toISOString().slice(0, 7);
}

function yearKey(date: Date): string {
  return date.getUTCFullYear().toString();
}

function buildPeriods(period: SalesFinancialPeriod, referenceDate: string): Array<{ period: string; label: string }> {
  const reference = parseDate(referenceDate);
  if (!reference) return [];

  if (period === "daily") {
    return Array.from({ length: DAILY_POINT_COUNT }, (_, index) => {
      const date = new Date(reference.getTime() - (DAILY_POINT_COUNT - 1 - index) * MILLISECONDS_PER_DAY);
      return { period: dateKey(date), label: new Intl.DateTimeFormat("pt-BR", { weekday: "short", timeZone: "UTC" }).format(date).replace(".", "") };
    });
  }

  if (period === "monthly") {
    return Array.from({ length: MONTHLY_POINT_COUNT }, (_, index) => {
      const date = new Date(Date.UTC(reference.getUTCFullYear(), reference.getUTCMonth() - (MONTHLY_POINT_COUNT - 1 - index), 1));
      return { period: monthKey(date), label: new Intl.DateTimeFormat("pt-BR", { month: "short", timeZone: "UTC" }).format(date).replace(".", "") };
    });
  }

  return Array.from({ length: YEARLY_POINT_COUNT }, (_, index) => {
    const year = reference.getUTCFullYear() - (YEARLY_POINT_COUNT - 1 - index);
    return { period: year.toString(), label: year.toString() };
  });
}

export function buildSalesRevenueExpenseSeries(
  records: SalesFinancialRecord[],
  period: SalesFinancialPeriod,
  referenceDate: string,
): SalesFinancialPoint[] {
  const periods = buildPeriods(period, referenceDate);
  const totals = new Map(periods.map((item) => [item.period, { revenue: 0, expenses: 0 }]));

  for (const record of records) {
    const date = parseDate(record.date);
    if (!date) continue;
    const key = period === "daily" ? dateKey(date) : period === "monthly" ? monthKey(date) : yearKey(date);
    const total = totals.get(key);
    if (!total) continue;
    total.revenue = roundCurrency(total.revenue + roundCurrency(record.revenue));
    total.expenses = roundCurrency(total.expenses + roundCurrency(record.expenses));
  }

  return periods.map(({ period: periodKey, label }) => ({ period: periodKey, label, ...(totals.get(periodKey) ?? { revenue: 0, expenses: 0 }) }));
}

export const DEMO_SALES_FINANCIAL_RECORDS: SalesFinancialRecord[] = [
  { date: "2021-07-01", revenue: 7_460_000, expenses: 6_180_000 },
  { date: "2022-07-01", revenue: 8_140_000, expenses: 6_420_000 },
  { date: "2023-07-01", revenue: 10_280_000, expenses: 8_940_000 },
  { date: "2024-07-01", revenue: 8_760_000, expenses: 9_180_000 },
  { date: "2025-01-15", revenue: 10_010_000, expenses: 7_860_000 },
  { date: "2025-07-15", revenue: 920_000, expenses: 730_000 },
  { date: "2025-08-15", revenue: 1_140_000, expenses: 860_000 },
  { date: "2025-09-15", revenue: 980_000, expenses: 790_000 },
  { date: "2025-10-15", revenue: 1_260_000, expenses: 910_000 },
  { date: "2025-11-15", revenue: 1_010_000, expenses: 820_000 },
  { date: "2025-12-15", revenue: 1_080_000, expenses: 845_000 },
  { date: "2026-01-15", revenue: 520_000, expenses: 610_000 },
  { date: "2026-02-15", revenue: 760_000, expenses: 690_000 },
  { date: "2026-03-15", revenue: 910_000, expenses: 780_000 },
  { date: "2026-04-15", revenue: 870_000, expenses: 940_000 },
  { date: "2026-05-15", revenue: 1_180_000, expenses: 920_000 },
  { date: "2026-06-11", revenue: 126_000, expenses: 94_000 },
  { date: "2026-06-12", revenue: 172_000, expenses: 118_000 },
  { date: "2026-06-13", revenue: 149_000, expenses: 111_000 },
  { date: "2026-06-14", revenue: 203_000, expenses: 146_000 },
  { date: "2026-06-15", revenue: 181_000, expenses: 138_000 },
  { date: "2026-06-16", revenue: 224_000, expenses: 159_000 },
  { date: "2026-06-17", revenue: 196_000, expenses: 143_000 },
  { date: "2026-06-18", revenue: 118_000, expenses: 86_000 },
  { date: "2026-06-19", revenue: 154_000, expenses: 121_000 },
  { date: "2026-06-20", revenue: 97_000, expenses: 132_000 },
  { date: "2026-06-21", revenue: 186_000, expenses: 148_000 },
  { date: "2026-06-22", revenue: 142_000, expenses: 151_000 },
  { date: "2026-06-23", revenue: 231_000, expenses: 172_000 },
  { date: "2026-06-24", revenue: 168_000, expenses: 127_000 },
];
