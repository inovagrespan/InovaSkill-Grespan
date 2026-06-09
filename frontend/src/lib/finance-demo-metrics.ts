import { calculateAverageTicket } from "./business-metrics";

export type FinanceDemoTransaction = {
  customer: string;
  date: string;
  revenue: number;
  orders: number;
  quantity: number;
};

export type FinanceFilters = {
  customer: string;
  dateFrom: string;
  dateTo: string;
  allTime: boolean;
};

export type FinanceMetrics = {
  totalRevenue: number;
  totalOrders: number;
  totalQuantity: number;
  averageTicket: number;
  items: FinanceDemoTransaction[];
};

export type FinanceRevenueGranularity = "weekly" | "monthly" | "yearly";

export type FinanceRevenueTrendPoint = {
  period: string;
  label: string;
  revenue: number;
};

export type FinanceCustomerRevenuePoint = {
  customer: string;
  revenue: number;
};

const MONTH_LABEL_FORMATTER = new Intl.DateTimeFormat("pt-BR", { month: "short" });
const TOP_FINANCE_CUSTOMERS_LIMIT = 5;
const DAYS_IN_WEEK = 7;

export const financeDemoTransactions: FinanceDemoTransaction[] = [
  { customer: "Mercado São Bento", date: "2026-01-12", revenue: 18_900, orders: 8, quantity: 720 },
  { customer: "Mercado São Bento", date: "2026-02-18", revenue: 21_300, orders: 9, quantity: 840 },
  { customer: "Mercado São Bento", date: "2026-05-20", revenue: 26_650, orders: 11, quantity: 900 },
  { customer: "Atacado Primavera", date: "2026-02-05", revenue: 16_800, orders: 6, quantity: 1_050 },
  { customer: "Atacado Primavera", date: "2026-04-11", revenue: 24_400, orders: 10, quantity: 1_480 },
  { customer: "Atacado Primavera", date: "2026-06-03", revenue: 11_100, orders: 5, quantity: 650 },
  { customer: "Super Lopes", date: "2026-03-09", revenue: 14_200, orders: 7, quantity: 580 },
  { customer: "Super Lopes", date: "2026-05-28", revenue: 24_740, orders: 11, quantity: 1_180 },
  { customer: "Distribuidora Central", date: "2026-01-30", revenue: 12_600, orders: 4, quantity: 390 },
  { customer: "Distribuidora Central", date: "2026-06-06", revenue: 18_900, orders: 8, quantity: 590 },
];

function isInsideDateRange(itemDate: string, dateFrom: string, dateTo: string): boolean {
  if (dateFrom && itemDate < dateFrom) return false;
  if (dateTo && itemDate > dateTo) return false;
  return true;
}

function normalizeFinanceSearchText(value: string): string {
  return value
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .trim()
    .toLowerCase();
}

export function listFinanceCustomers(items: FinanceDemoTransaction[] = financeDemoTransactions): string[] {
  return Array.from(new Set(items.map((item) => item.customer))).sort((a, b) => a.localeCompare(b, "pt-BR"));
}

export function calculateFinanceMetrics(
  filters: FinanceFilters,
  items: FinanceDemoTransaction[] = financeDemoTransactions,
): FinanceMetrics {
  const normalizedCustomer = normalizeFinanceSearchText(filters.customer);
  const filtered = items.filter((item) => {
    const matchesCustomer = !normalizedCustomer || normalizeFinanceSearchText(item.customer).includes(normalizedCustomer);
    const matchesDate = filters.allTime || isInsideDateRange(item.date, filters.dateFrom, filters.dateTo);
    return matchesCustomer && matchesDate;
  });

  const totalRevenue = filtered.reduce((total, item) => total + item.revenue, 0);
  const totalOrders = filtered.reduce((total, item) => total + item.orders, 0);
  const totalQuantity = filtered.reduce((total, item) => total + item.quantity, 0);

  return {
    totalRevenue,
    totalOrders,
    totalQuantity,
    averageTicket: calculateAverageTicket(totalRevenue, totalOrders),
    items: filtered,
  };
}

function toWeekStart(value: string): Date {
  const date = new Date(`${value}T00:00:00`);
  const mondayOffset = (date.getDay() + 6) % DAYS_IN_WEEK;
  date.setDate(date.getDate() - mondayOffset);
  return date;
}

function toDateKey(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function resolveFinanceRevenuePeriod(item: FinanceDemoTransaction, granularity: FinanceRevenueGranularity): FinanceRevenueTrendPoint {
  if (granularity === "weekly") {
    const weekStart = toWeekStart(item.date);
    return {
      period: toDateKey(weekStart),
      label: `Sem ${weekStart.toLocaleDateString("pt-BR", { day: "2-digit", month: "2-digit" })}`,
      revenue: 0,
    };
  }

  if (granularity === "yearly") {
    const year = item.date.slice(0, 4);
    return { period: year, label: year, revenue: 0 };
  }

  const month = item.date.slice(0, 7);
  const [year, monthNumber] = month.split("-").map(Number);
  const monthLabel = MONTH_LABEL_FORMATTER.format(new Date(year, monthNumber - 1, 1)).replace(".", "");
  return { period: month, label: monthLabel, revenue: 0 };
}

export function buildFinanceRevenueTrend(
  items: FinanceDemoTransaction[],
  granularity: FinanceRevenueGranularity = "monthly",
): FinanceRevenueTrendPoint[] {
  const revenueByPeriod = new Map<string, FinanceRevenueTrendPoint>();

  for (const item of items) {
    const period = resolveFinanceRevenuePeriod(item, granularity);
    const current = revenueByPeriod.get(period.period) ?? period;
    revenueByPeriod.set(period.period, { ...current, revenue: current.revenue + item.revenue });
  }

  return Array.from(revenueByPeriod.values()).sort((left, right) => left.period.localeCompare(right.period));
}

export function buildFinanceMonthlyRevenue(items: FinanceDemoTransaction[]): FinanceRevenueTrendPoint[] {
  return buildFinanceRevenueTrend(items, "monthly");
}

export function buildFinanceCustomerRevenueRanking(items: FinanceDemoTransaction[]): FinanceCustomerRevenuePoint[] {
  const revenueByCustomer = new Map<string, number>();

  for (const item of items) {
    revenueByCustomer.set(item.customer, (revenueByCustomer.get(item.customer) ?? 0) + item.revenue);
  }

  return Array.from(revenueByCustomer.entries())
    .map(([customer, revenue]) => ({ customer, revenue }))
    .sort((left, right) => right.revenue - left.revenue || left.customer.localeCompare(right.customer, "pt-BR"))
    .slice(0, TOP_FINANCE_CUSTOMERS_LIMIT);
}
