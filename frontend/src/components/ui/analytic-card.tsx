import { type ReactNode } from "react";
import { cn } from "@/lib/utils";
import { TrendingUp, TrendingDown, Minus, ArrowUpRight, ArrowDownRight } from "lucide-react";

interface AnalyticCardProps {
  title: string;
  value: string;
  subtitle?: string;
  trend?: {
    direction: "up" | "down" | "stable";
    value: string;
    label?: string;
  };
  comparison?: {
    value: string;
    isPositive: boolean;
    label?: string;
  };
  icon?: ReactNode;
  loading?: boolean;
  className?: string;
}

export function AnalyticCard({ title, value, subtitle, trend, comparison, icon, loading, className }: AnalyticCardProps) {
  if (loading) {
    return (
      <div className={cn("rounded-xl border bg-card p-5 space-y-3", className)}>
        <div className="skeleton-shimmer h-3 w-24 rounded" />
        <div className="skeleton-shimmer h-7 w-32 rounded" />
        <div className="skeleton-shimmer h-3 w-20 rounded" />
      </div>
    );
  }

  const TrendIcon = trend?.direction === "up" ? TrendingUp
    : trend?.direction === "down" ? TrendingDown
    : Minus;

  const trendColor = trend?.direction === "up" ? "text-[#16A34A]"
    : trend?.direction === "down" ? "text-[#DC2626]"
    : "text-[#D97706]";

  const trendBg = trend?.direction === "up" ? "bg-[#16A34A]/10"
    : trend?.direction === "down" ? "bg-[#DC2626]/10"
    : "bg-[#D97706]/10";

  return (
    <div className={cn(
      "rounded-xl border bg-card p-5 transition-all duration-200",
      "hover:shadow-sm hover:border-primary/20",
      className
    )}>
      <div className="flex items-start justify-between gap-3">
        <div className="space-y-1.5 flex-1 min-w-0">
          <p className="text-sm text-muted-foreground font-medium">{title}</p>
          <p className="text-2xl font-semibold tracking-tight">{value}</p>
          {subtitle && (
            <p className="text-xs text-muted-foreground">{subtitle}</p>
          )}
        </div>
        {icon && (
          <div className="shrink-0 w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center text-primary">
            {icon}
          </div>
        )}
      </div>
      {(trend || comparison) && (
        <div className="flex items-center gap-3 mt-3 pt-3 border-t border-border/50 text-nowrap overflow-hidden">
          {trend && (
            <div className={cn("inline-flex items-center gap-1.5 text-sm font-medium text-nowrap", trendColor)}>
              <span className={cn("inline-flex items-center justify-center w-5 h-5 rounded-full shrink-0", trendBg)}>
                <TrendIcon className="w-3 h-3" />
              </span>
              <span className="text-nowrap">{trend.value}</span>
              {trend.label && (
                <span className="text-muted-foreground font-normal text-nowrap">{trend.label}</span>
              )}
            </div>
          )}
          {comparison && (
            <span className={cn(
              "inline-flex items-center gap-1 text-sm font-medium text-nowrap",
              comparison.isPositive ? "text-[#16A34A]" : "text-[#DC2626]"
            )}>
              {comparison.isPositive
                ? <ArrowUpRight className="w-3.5 h-3.5 shrink-0" />
                : <ArrowDownRight className="w-3.5 h-3.5 shrink-0" />
              }
              <span className="text-nowrap">{comparison.isPositive ? "+" : ""}{comparison.value}</span>
              {comparison.label && (
                <span className="text-muted-foreground font-normal text-nowrap">{comparison.label}</span>
              )}
            </span>
          )}
        </div>
      )}
    </div>
  );
}
