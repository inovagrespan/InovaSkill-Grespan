import { type ReactNode } from "react";
import { AlertTriangle, CheckCircle2, Info, Lightbulb, TrendingUp, type LucideIcon } from "lucide-react";

import { cn } from "@/lib/utils";

type InsightCardType = "alert" | "success" | "info" | "insight" | "opportunity";

type InsightCardProps = {
  type?: InsightCardType;
  className?: string;
  children: ReactNode;
};

const insightCardStyles: Record<InsightCardType, { icon: LucideIcon; container: string }> = {
  alert: {
    icon: AlertTriangle,
    container: "border-amber-500/25 bg-amber-500/10 text-amber-800",
  },
  success: {
    icon: CheckCircle2,
    container: "border-emerald-500/25 bg-emerald-500/10 text-emerald-800",
  },
  info: {
    icon: Info,
    container: "border-sky-500/25 bg-sky-500/10 text-sky-800",
  },
  insight: {
    icon: Lightbulb,
    container: "border-blue-500/25 bg-blue-500/10 text-blue-800",
  },
  opportunity: {
    icon: TrendingUp,
    container: "border-teal-500/25 bg-teal-500/10 text-teal-800",
  },
};

export function InsightCard({ type = "info", className, children }: InsightCardProps) {
  const style = insightCardStyles[type];
  const Icon = style.icon;

  return (
    <div
      className={cn(
        "flex items-start gap-3 rounded-lg border px-4 py-3 text-sm leading-relaxed shadow-xs",
        style.container,
        className,
      )}
    >
      <Icon className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}
