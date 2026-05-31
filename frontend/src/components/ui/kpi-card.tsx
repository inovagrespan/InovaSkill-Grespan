import { Loader2, Minus, TrendingDown, TrendingUp } from "lucide-react";
import type { ComponentType } from "react";
import { cn } from "@/lib/utils";
import { resolveTrendDirection, type TrendDirection } from "./kpi-card.utils";

type KpiCardProps = {
  title: string;
  value: string;
  valueTooltip?: string;
  showPercentageChange?: boolean;
  percentageChange?: number | null;
  trendDirection?: TrendDirection;
  trendData?: number[];
  periodLabel?: string;
  icon?: ComponentType<{ className?: string }>;
  loading?: boolean;
  description?: string;
  className?: string;
  allowWrapValue?: boolean;
};

function formatPct(value?: number | null): string {
  if (value == null) return "N/A";
  const sign = value > 0 ? "+" : "";
  return `${sign}${value.toFixed(1)}%`;
}

export function KpiCard({
  title,
  value,
  valueTooltip,
  showPercentageChange = true,
  percentageChange = null,
  trendDirection,
  trendData = [],
  periodLabel,
  icon: Icon,
  loading = false,
  description,
  className,
  allowWrapValue = false,
}: KpiCardProps) {
  const direction = resolveTrendDirection(percentageChange, trendDirection);
  const toneClass =
    direction === "up"
      ? "text-[var(--success)]"
      : direction === "down"
        ? "text-[var(--error)]"
        : "text-muted-foreground";
  return (
    <div
      className={cn(
        "h-full rounded-xl border border-border bg-[linear-gradient(180deg,rgba(255,255,255,1),rgba(250,251,253,0.96))] p-4 shadow-xs transition-all duration-200 hover:-translate-y-0.5 hover:border-primary/35 hover:shadow-sm dark:bg-[linear-gradient(180deg,rgba(23,28,37,0.98),rgba(20,25,34,0.98))]",
        "animate-soft-enter",
        className,
      )}
    >
      <div className="flex min-h-12 items-start justify-between gap-3">
        <p className="text-xs text-muted-foreground">{title}</p>
        {showPercentageChange ? (
          <div className={cn("inline-flex items-center gap-1 text-xs font-semibold", toneClass)}>
            {direction === "up" && <TrendingUp className="size-3.5" />}
            {direction === "down" && <TrendingDown className="size-3.5" />}
            {direction === "stable" && <Minus className="size-3.5" />}
            {formatPct(percentageChange)}
          </div>
        ) : null}
      </div>

      <div className="flex min-h-20 flex-1 items-center justify-between gap-3">
        {loading ? (
          <Loader2 className="size-5 animate-spin text-muted-foreground" />
        ) : (
          <p
            title={valueTooltip ?? value}
            className={cn(
              "min-w-0 flex-1 pr-1 text-2xl font-display leading-tight tracking-tight text-[var(--text-primary)] sm:text-3xl",
              allowWrapValue
                ? "whitespace-normal break-words"
                : "overflow-hidden text-ellipsis whitespace-nowrap",
            )}
          >
            {value}
          </p>
        )}
        <div className="flex flex-col items-end gap-2">
          {Icon ? (
            <div className="inline-flex size-8 items-center justify-center rounded-full border border-primary/15 bg-[var(--soft-red-background)] text-primary">
              <Icon className="size-4" />
            </div>
          ) : null}
        </div>
      </div>

      {periodLabel ? <p className="min-h-8 text-[11px] text-muted-foreground">{periodLabel}</p> : null}
    </div>
  );
}
