export type TrendDirection = "up" | "down" | "stable";

export function resolveTrendDirection(percentageChange?: number | null, trendDirection?: TrendDirection): TrendDirection {
  if (trendDirection) return trendDirection;
  if (percentageChange == null) return "stable";
  if (percentageChange > 0) return "up";
  if (percentageChange < 0) return "down";
  return "stable";
}

export function buildSparklinePoints(data: number[]): string {
  if (data.length === 0) return "";
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = Math.max(1e-9, max - min);
  return data
    .map((v, i) => {
      const x = (i / Math.max(1, data.length - 1)) * 100;
      const y = 100 - ((v - min) / range) * 100;
      return `${x},${y}`;
    })
    .join(" ");
}

