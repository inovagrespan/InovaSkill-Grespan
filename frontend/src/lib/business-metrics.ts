const MILLISECONDS_PER_DAY = 86_400_000;
const DAYS_PER_WEEK = 7;
const AVERAGE_DAYS_PER_MONTH = 30.4375;
const MINIMUM_PERIOD_UNITS = 1;

export type PeriodAverages = {
  weekly: number;
  monthly: number;
};

function parseInputDate(value: string): Date | null {
  if (!value.trim()) return null;
  const date = new Date(`${value}T00:00:00`);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function calculateInclusivePeriodDays(dateFrom: string, dateTo: string): number {
  const from = parseInputDate(dateFrom);
  const to = parseInputDate(dateTo);
  if (!from || !to || to < from) return MINIMUM_PERIOD_UNITS;

  return Math.floor((to.getTime() - from.getTime()) / MILLISECONDS_PER_DAY) + 1;
}

export function calculatePeriodAverages(totalAmount: number, dateFrom: string, dateTo: string): PeriodAverages {
  const days = calculateInclusivePeriodDays(dateFrom, dateTo);
  const weeks = days / DAYS_PER_WEEK;
  const months = days / AVERAGE_DAYS_PER_MONTH;

  return {
    weekly: totalAmount / weeks,
    monthly: totalAmount / months,
  };
}

export function calculateAverageTicket(totalAmount: number, totalOrders: number): number {
  if (totalOrders <= 0) return 0;
  return totalAmount / totalOrders;
}
