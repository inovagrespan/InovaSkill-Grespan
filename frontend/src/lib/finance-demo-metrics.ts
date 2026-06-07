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

export function listFinanceCustomers(items: FinanceDemoTransaction[] = financeDemoTransactions): string[] {
  return Array.from(new Set(items.map((item) => item.customer))).sort((a, b) => a.localeCompare(b, "pt-BR"));
}

export function calculateFinanceMetrics(
  filters: FinanceFilters,
  items: FinanceDemoTransaction[] = financeDemoTransactions,
): FinanceMetrics {
  const normalizedCustomer = filters.customer.trim().toLowerCase();
  const filtered = items.filter((item) => {
    const matchesCustomer = !normalizedCustomer || item.customer.toLowerCase() === normalizedCustomer;
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
