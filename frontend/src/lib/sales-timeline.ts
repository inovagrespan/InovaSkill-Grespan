import type { CommercialTransactionTimelineGranularity } from "@/lib/importer-api";

const DAILY_RANGE_LIMIT_DAYS = 7;
const WEEKLY_RANGE_LIMIT_DAYS = 90;
const MONTHLY_RANGE_LIMIT_DAYS = 730;
const MILLISECONDS_PER_DAY = 86_400_000;
const SHORT_MONTH_FORMATTER = new Intl.DateTimeFormat("pt-BR", { month: "short", timeZone: "UTC" });
const SHORT_DAY_MONTH_FORMATTER = new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "short", timeZone: "UTC" });

export type SalesPeriodPreset = "today" | "week" | "month" | "quarter" | "year" | "custom";

export function resolveSalesTimelineGranularity(input: {
  periodPreset: SalesPeriodPreset;
  dateFrom: string;
  dateTo: string;
}): CommercialTransactionTimelineGranularity {
  switch (input.periodPreset) {
    case "today":
      return "hour";
    case "week":
    case "month":
      return "day";
    case "quarter":
      return "week";
    case "year":
      return "month";
    case "custom":
      return resolveCustomTimelineGranularity(input.dateFrom, input.dateTo);
    default:
      return "month";
  }
}

export function formatSalesTimelineLabel(
  periodStart: string,
  granularity: CommercialTransactionTimelineGranularity,
): string {
  const date = new Date(periodStart.includes("T") ? periodStart : `${periodStart}T00:00:00Z`);
  if (Number.isNaN(date.getTime())) return periodStart;

  if (granularity === "hour") {
    return `${String(date.getUTCHours()).padStart(2, "0")}h`;
  }

  if (granularity === "day") {
    return SHORT_DAY_MONTH_FORMATTER.format(date).replace(".", "");
  }

  if (granularity === "week") {
    const end = new Date(date);
    end.setUTCDate(end.getUTCDate() + 6);
    const startLabel = SHORT_DAY_MONTH_FORMATTER.format(date).replace(".", "");
    const endLabel = SHORT_DAY_MONTH_FORMATTER.format(end).replace(".", "");
    return `${startLabel} - ${endLabel}`;
  }

  if (granularity === "quarter") {
    const quarter = Math.floor(date.getUTCMonth() / 3) + 1;
    return `T${quarter} ${date.getUTCFullYear()}`;
  }

  const month = SHORT_MONTH_FORMATTER.format(date).replace(".", "");
  return month.charAt(0).toUpperCase() + month.slice(1);
}

function resolveCustomTimelineGranularity(
  dateFrom: string,
  dateTo: string,
): CommercialTransactionTimelineGranularity {
  const start = new Date(`${dateFrom}T00:00:00Z`);
  const end = new Date(`${dateTo}T00:00:00Z`);

  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) || end < start) {
    return "month";
  }

  const inclusiveRangeDays = Math.floor((end.getTime() - start.getTime()) / MILLISECONDS_PER_DAY) + 1;

  if (inclusiveRangeDays <= DAILY_RANGE_LIMIT_DAYS) {
    return "day";
  }

  if (inclusiveRangeDays <= WEEKLY_RANGE_LIMIT_DAYS) {
    return "week";
  }

  if (inclusiveRangeDays <= MONTHLY_RANGE_LIMIT_DAYS) {
    return "month";
  }

  return "quarter";
}
