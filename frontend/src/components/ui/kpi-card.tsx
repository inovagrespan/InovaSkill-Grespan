import { Minus, TrendingDown, TrendingUp } from "lucide-react";
import type { ComponentType, KeyboardEvent } from "react";
import { cn } from "@/lib/utils";
import { resolveKpiValueSizeClass, resolveTrendDirection, type TrendDirection } from "./kpi-card.utils";
import { Skeleton } from "./skeleton";

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
  valueClassName?: string;
  allowWrapValue?: boolean;
  tone?: "neutral" | "success" | "danger" | "info";
  periodLabelClassName?: string;
  onClick?: () => void;
  ariaLabel?: string;
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
  valueClassName,
  allowWrapValue = false,
  tone = "neutral",
  periodLabelClassName,
  onClick,
  ariaLabel,
}: KpiCardProps) {
  const direction = resolveTrendDirection(percentageChange, trendDirection);
  const toneClass =
    direction === "up"
      ? "text-[var(--success)]"
      : direction === "down"
        ? "text-[var(--error)]"
        : "text-muted-foreground";
  const cardToneClass = {
    neutral: "",
    success: "border-[color:var(--success)]/35 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--success)_8%,var(--surface)),var(--surface))]",
    danger: "border-[color:var(--error)]/35 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--error)_8%,var(--surface)),var(--surface))]",
    info: "border-[color:var(--info)]/35 bg-[linear-gradient(180deg,color-mix(in_srgb,var(--info)_8%,var(--surface)),var(--surface))]",
  }[tone];
  const valueToneClass = {
    neutral: "",
    success: "text-[var(--success)]",
    danger: "text-[var(--error)]",
    info: "text-[var(--info)]",
  }[tone];
  const iconToneClass = {
    neutral: "border-primary/15 bg-[var(--soft-red-background)] text-primary",
    success: "border-[color:var(--success)]/25 bg-[color-mix(in_srgb,var(--success)_12%,transparent)] text-[var(--success)]",
    danger: "border-[color:var(--error)]/25 bg-[color-mix(in_srgb,var(--error)_12%,transparent)] text-[var(--error)]",
    info: "border-[color:var(--info)]/25 bg-[color-mix(in_srgb,var(--info)_12%,transparent)] text-[var(--info)]",
  }[tone];
  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (!onClick) return;
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      onClick();
    }
  }

  return (
    <div
      role={onClick ? "button" : undefined}
      tabIndex={onClick ? 0 : undefined}
      aria-label={ariaLabel}
      onClick={onClick}
      onKeyDown={handleKeyDown}
      className={cn(
        "h-full rounded-xl border border-border bg-[linear-gradient(180deg,rgba(255,255,255,1),rgba(250,251,253,0.96))] px-5 py-4 shadow-xs transition-all duration-200 hover:-translate-y-0.5 hover:border-primary/35 hover:shadow-sm dark:bg-[linear-gradient(180deg,rgba(23,28,37,0.98),rgba(20,25,34,0.98))]",
        "metric-card-item animate-soft-enter",
        onClick ? "cursor-pointer outline-none ring-primary/40 focus-visible:ring-2" : "",
        cardToneClass,
        className,
      )}
    >
      <div className="flex min-h-12 items-start justify-between gap-3">
        <p className="text-xs text-muted-foreground">{title}</p>
        <div className="flex shrink-0 items-center gap-2">
          {showPercentageChange ? (
          <div className={cn("inline-flex items-center gap-1 text-xs font-semibold", toneClass)}>
            {direction === "up" && <TrendingUp className="size-3.5" />}
            {direction === "down" && <TrendingDown className="size-3.5" />}
            {direction === "stable" && <Minus className="size-3.5" />}
            {formatPct(percentageChange)}
          </div>
          ) : null}
          {Icon ? (
            <div className={cn("inline-flex size-8 items-center justify-center rounded-full border", iconToneClass)}>
              <Icon className="size-4" />
            </div>
          ) : null}
        </div>
      </div>

      <div className="flex min-h-20 flex-1 items-center">
        {loading ? (
          <div className="flex min-w-0 flex-1 flex-col gap-3">
            <Skeleton className="h-8 w-32" />
            <Skeleton className="h-3 w-40" />
          </div>
        ) : (
          <p
            title={valueTooltip ?? value}
            className={cn(
              "min-w-0 flex-1 pr-1 font-display leading-tight tracking-tight text-[var(--text-primary)]",
              resolveKpiValueSizeClass(value),
              allowWrapValue
                ? "whitespace-normal break-words"
                : "overflow-hidden text-ellipsis whitespace-nowrap",
              valueClassName,
              valueToneClass,
            )}
          >
            {value}
          </p>
        )}
      </div>

      {periodLabel ? <p className={cn("min-h-8 text-[11px] text-muted-foreground", periodLabelClassName)}>{periodLabel}</p> : null}
    </div>
  );
}
