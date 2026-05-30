import { Loader2, Minus, TrendingDown, TrendingUp } from "lucide-react";
import type { ComponentType } from "react";
import { cn } from "@/lib/utils";
import { buildSparklinePoints, resolveTrendDirection, type TrendDirection } from "./kpi-card.utils";

type KpiCardProps = {
  title: string;
  value: string;
  percentageChange?: number | null;
  trendDirection?: TrendDirection;
  trendData?: number[];
  periodLabel?: string;
  icon?: ComponentType<{ className?: string }>;
  loading?: boolean;
  description?: string;
  className?: string;
};

function formatPct(value?: number | null): string {
  if (value == null) return "N/A";
  const sign = value > 0 ? "+" : "";
  return `${sign}${value.toFixed(1)}%`;
}

export function KpiCard({
  title,
  value,
  percentageChange = null,
  trendDirection,
  trendData = [],
  periodLabel,
  icon: Icon,
  loading = false,
  description,
  className,
}: KpiCardProps) {
  const direction = resolveTrendDirection(percentageChange, trendDirection);
  const toneClass =
    direction === "up"
      ? "text-emerald-400"
      : direction === "down"
        ? "text-red-400"
        : "text-slate-400";
  const SparklinePoints = buildSparklinePoints(trendData);

  return (
    <div
      className={cn(
        "rounded-xl border border-border bg-surface p-4 transition-all duration-200 hover:-translate-y-0.5 hover:border-red-500/40",
        "animate-soft-enter",
        className,
      )}
      title={description ?? title}
    >
      <div className="flex items-start justify-between gap-3">
        <p className="text-xs text-muted-foreground">{title}</p>
        <div className={cn("inline-flex items-center gap-1 text-xs font-semibold", toneClass)}>
          {direction === "up" && <TrendingUp className="size-3.5" />}
          {direction === "down" && <TrendingDown className="size-3.5" />}
          {direction === "stable" && <Minus className="size-3.5" />}
          {formatPct(percentageChange)}
        </div>
      </div>

      <div className="mt-2 flex items-center justify-between gap-3">
        {loading ? (
          <Loader2 className="size-5 animate-spin text-muted-foreground" />
        ) : (
          <p className="text-4xl font-display leading-none tracking-tight">{value}</p>
        )}
        {Icon ? <Icon className="size-4 text-muted-foreground" /> : null}
      </div>

      {trendData.length > 1 ? (
        <svg viewBox="0 0 100 26" preserveAspectRatio="none" className="mt-3 h-7 w-full opacity-80">
          <polyline
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            className={toneClass}
            points={SparklinePoints}
          />
        </svg>
      ) : (
        <div className="mt-3 h-7 w-full rounded-md border border-border/70 bg-black/10" />
      )}

      {periodLabel ? <p className="mt-2 text-[11px] text-muted-foreground">{periodLabel}</p> : null}
    </div>
  );
}
