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

  const averageChangePercent = changes.length === 0 ? null : changes.reduce((sum, value) => sum + value, 0) / changes.length;
  const firstComparable = values.find((value) => value !== 0) ?? values[0] ?? null;
  const lastComparable = [...values].reverse().find((value) => value !== 0) ?? values.at(-1) ?? null;
  const totalChangePercent =
    firstComparable == null || lastComparable == null || firstComparable === 0
      ? null
      : ((lastComparable - firstComparable) / firstComparable) * PERCENT_MULTIPLIER;

  const trendLabel = resolveTrendLabel(ordered, averageChangePercent, totalChangePercent);
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
  ordered: CustomerTimelinePoint[],
  averageChangePercent: number | null,
  totalChangePercent: number | null,
): CustomerPeriodTrend["trendLabel"] {
  if (ordered.length < 2) {
    return "Sem base";
  }

  const halfIndex = Math.floor(ordered.length / 2);
  const firstHalf = ordered.slice(0, halfIndex).map((point) => Number(point.value));
  const lastHalf = ordered.slice(ordered.length - halfIndex).map((point) => Number(point.value));
  const firstAverage = average(firstHalf);
  const lastAverage = average(lastHalf);

  let halfVariationPercent: number | null = null;
  if (firstAverage != null && lastAverage != null && firstAverage !== 0) {
    halfVariationPercent = ((lastAverage - firstAverage) / firstAverage) * PERCENT_MULTIPLIER;
  }

  const signal = halfVariationPercent ?? averageChangePercent ?? totalChangePercent;
  if (signal == null || Number.isNaN(signal)) {
    return "Sem base";
  }

  if (signal >= GROWTH_THRESHOLD_PERCENT) return "Crescendo";
  if (signal <= DROP_THRESHOLD_PERCENT) return "Caindo";
  return "Estável";
}

function average(values: number[]): number | null {
  const valid = values.filter((value) => Number.isFinite(value));
  if (valid.length === 0) return null;
  return valid.reduce((sum, value) => sum + value, 0) / valid.length;
}
