import type { CommercialTransactionTimelineGranularity } from "@/lib/importer-api";

const DAILY_RANGE_LIMIT_DAYS = 31;
const WEEKLY_RANGE_LIMIT_DAYS = 120;
const MILLISECONDS_PER_DAY = 86_400_000;
const SHORT_MONTH_FORMATTER = new Intl.DateTimeFormat("pt-BR", { month: "short", timeZone: "UTC" });
const SHORT_DAY_MONTH_FORMATTER = new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "2-digit", timeZone: "UTC" });

export type SalesPeriodPreset = "today" | "week" | "month" | "quarter" | "year" | "custom";

export function resolveSalesTimelineGranularity(input: {
  periodPreset: SalesPeriodPreset;
  dateFrom: string;
  dateTo: string;
}): CommercialTransactionTimelineGranularity {
  switch (input.periodPreset) {
    case "today":
    case "week":
    case "month":
      return "daily";
    case "quarter":
      return "weekly";
    case "year":
      return "monthly";
    case "custom":
      return resolveCustomTimelineGranularity(input.dateFrom, input.dateTo);
    default:
      return "monthly";
  }
}

export function formatSalesTimelineLabel(
  periodStart: string,
  granularity: CommercialTransactionTimelineGranularity,
): string {
  const date = new Date(`${periodStart}T00:00:00Z`);
  if (Number.isNaN(date.getTime())) return periodStart;

  if (granularity === "daily") {
    return SHORT_DAY_MONTH_FORMATTER.format(date);
  }

  if (granularity === "weekly") {
    return `Sem ${SHORT_DAY_MONTH_FORMATTER.format(date)}`;
  }

  const month = SHORT_MONTH_FORMATTER.format(date).replace(".", "");
  const year = String(date.getUTCFullYear()).slice(-2);
  return `${month}/${year}`;
}

function resolveCustomTimelineGranularity(
  dateFrom: string,
  dateTo: string,
): CommercialTransactionTimelineGranularity {
  const start = new Date(`${dateFrom}T00:00:00Z`);
  const end = new Date(`${dateTo}T00:00:00Z`);

  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) || end < start) {
    return "monthly";
  }

  const inclusiveRangeDays = Math.floor((end.getTime() - start.getTime()) / MILLISECONDS_PER_DAY) + 1;

  if (inclusiveRangeDays <= DAILY_RANGE_LIMIT_DAYS) {
    return "daily";
  }

  if (inclusiveRangeDays <= WEEKLY_RANGE_LIMIT_DAYS) {
    return "weekly";
  }

  return "monthly";
}
