import { cn } from "@/lib/utils";
import { TrendingUp, TrendingDown, Minus, ArrowUpRight, ArrowDownRight } from "lucide-react";

interface TrendBadgeProps {
  trend: "Crescimento" | "Queda" | "Estavel" | "Crescendo" | "Caindo" | string;
  confidence?: "alta" | "media" | "baixa";
  size?: "sm" | "md" | "lg";
  showIcon?: boolean;
  className?: string;
}

const trendConfig: Record<string, {
  icon: typeof TrendingUp;
  arrow: typeof ArrowUpRight;
  color: string;
  bg: string;
  border: string;
  dot: string;
}> = {
  Crescimento: {
    icon: TrendingUp,
    arrow: ArrowUpRight,
    color: "text-[#059669]",
    bg: "bg-[#059669]/10",
    border: "border-[#059669]/25",
    dot: "bg-[#059669]",
  },
  Queda: {
    icon: TrendingDown,
    arrow: ArrowDownRight,
    color: "text-[#B91C1C]",
    bg: "bg-[#B91C1C]/10",
    border: "border-[#B91C1C]/25",
    dot: "bg-[#B91C1C]",
  },
  default: {
    icon: Minus,
    arrow: Minus,
    color: "text-[#2563EB]",
    bg: "bg-[#2563EB]/10",
    border: "border-[#2563EB]/25",
    dot: "bg-[#2563EB]",
  },
};

const confidenceLabels: Record<string, string> = {
  alta: "Alta confiança",
  media: "Média confiança",
  baixa: "Baixa confiança",
};

const confidenceColors: Record<string, string> = {
  alta: "text-[#059669]",
  media: "text-[#D97706]",
  baixa: "text-[#B91C1C]",
};

export function TrendBadge({ trend, confidence, size = "md", showIcon = true, className }: TrendBadgeProps) {
  const trendMap: Record<string, string> = { Crescimento: "Crescimento", Crescendo: "Crescimento", Queda: "Queda", Caindo: "Queda", Estavel: "default", Estável: "default" };
  const trendKey = trendMap[trend] ?? "default";
  const config = trendConfig[trendKey] ?? trendConfig.default;
  const Icon = config.icon;
  const Arrow = config.arrow;
  const isDefault = trendKey === "default";

  const sizeClasses = size === "sm" ? "px-2 py-0.5 text-[10px] gap-1"
    : size === "lg" ? "px-3.5 py-1.5 text-sm gap-2"
    : "px-2.5 py-1 text-xs gap-1.5";

  const iconSize = size === "sm" ? "w-3 h-3" : size === "lg" ? "w-5 h-5" : "w-3.5 h-3.5";

  return (
    <div className={cn("inline-flex items-center gap-1.5 whitespace-nowrap", className)}>
      <span className={cn(
        "inline-flex items-center rounded-full font-semibold border whitespace-nowrap",
        isDefault
          ? "bg-[#2563EB]/8 text-[#2563EB] border-[#2563EB]/15"
          : config.bg + " " + config.color + " " + config.border,
        sizeClasses,
        !showIcon && "pl-2.5"
      )}>
        {showIcon && (
          <Icon className={cn("shrink-0", iconSize, config.color)} />
        )}
        {trend}
      </span>
      {confidence && (
        <span className={cn(
          "inline-flex items-center gap-1 text-[11px] font-medium",
          confidenceColors[confidence] ?? "text-muted-foreground"
        )}>
          <span className={cn("w-1.5 h-1.5 rounded-full", confidence === "alta" ? "bg-[#059669]" : confidence === "media" ? "bg-[#D97706]" : "bg-[#B91C1C]")} />
          {confidenceLabels[confidence] ?? confidence}
        </span>
      )}
    </div>
  );
}

export function MiniTrend({ value, className }: { value: number; className?: string }) {
  const isUp = value >= 0;
  const isDown = value < 0;
  return (
    <span className={cn(
      "inline-flex items-center gap-1 text-sm font-semibold",
      isUp ? "text-[#059669]" : "text-[#B91C1C]",
      className
    )}>
      {isUp ? <ArrowUpRight className="w-4 h-4" /> : <ArrowDownRight className="w-4 h-4" />}
      {isUp ? "+" : ""}{value.toFixed(1)}%
    </span>
  );
}
