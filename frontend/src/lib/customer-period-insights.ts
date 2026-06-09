import type { CustomerTimelinePoint } from "./importer-api";

const GROWTH_THRESHOLD_PERCENT = 5;
const DROP_THRESHOLD_PERCENT = -5;
const PERCENT_MULTIPLIER = 100;

export type CustomerPeriodTrend = {
  averageValue: number | null;
  averageChangePercent: number | null;
  totalChangePercent: number | null;
  trendLabel: "Crescendo" | "Caindo" | "Estável" | "Sem base";
  tone: "success" | "danger" | "neutral";
  comparableIntervals: number;
  pointsCount: number;
};

export function buildCustomerPeriodTrend(points: CustomerTimelinePoint[]): CustomerPeriodTrend {
  if (points.length === 0) {
    return {
      averageValue: null,
      averageChangePercent: null,
      totalChangePercent: null,
      trendLabel: "Sem base",
      tone: "neutral",
      comparableIntervals: 0,
      pointsCount: 0,
    };
  }

  const ordered = [...points].sort((left, right) => left.periodStart.localeCompare(right.periodStart));
  const values = ordered.map((point) => Number(point.value)).filter((value) => Number.isFinite(value));
  const averageValue = values.length === 0 ? null : values.reduce((sum, value) => sum + value, 0) / values.length;

  const changes: number[] = [];
  for (let index = 1; index < ordered.length; index++) {
    const previous = Number(ordered[index - 1]?.value);
    const current = Number(ordered[index]?.value);
    if (!Number.isFinite(previous) || !Number.isFinite(current) || previous === 0) {
      continue;
    }

    changes.push(((current - previous) / previous) * PERCENT_MULTIPLIER);
  }

  const averageChangePercent =
    changes.length === 0 ? null : changes.reduce((sum, value) => sum + value, 0) / changes.length;

  const firstValue = Number(ordered[0]?.value);
  const lastValue = Number(ordered.at(-1)?.value);
  const totalChangePercent =
    !Number.isFinite(firstValue) || !Number.isFinite(lastValue) || firstValue === 0
      ? null
      : ((lastValue - firstValue) / firstValue) * PERCENT_MULTIPLIER;

  const trendLabel = resolveTrendLabel(averageChangePercent, totalChangePercent, ordered.length);
  return {
    averageValue,
    averageChangePercent,
    totalChangePercent,
    trendLabel,
    tone: trendLabel === "Crescendo" ? "success" : trendLabel === "Caindo" ? "danger" : "neutral",
    comparableIntervals: changes.length,
    pointsCount: ordered.length,
  };
}

function resolveTrendLabel(
  averageChangePercent: number | null,
  totalChangePercent: number | null,
  pointsCount: number,
): CustomerPeriodTrend["trendLabel"] {
  if (pointsCount < 2 && totalChangePercent == null) {
    return "Sem base";
  }

  const signal = averageChangePercent ?? totalChangePercent;
  if (signal == null || Number.isNaN(signal)) {
    return "Sem base";
  }

  if (signal >= GROWTH_THRESHOLD_PERCENT) return "Crescendo";
  if (signal <= DROP_THRESHOLD_PERCENT) return "Caindo";
  return "Estável";
}
