import { cn } from "@/lib/utils";

type InsightCardType = "alert" | "success" | "info";

type InsightCardProps = {
  type?: InsightCardType;
  className?: string;
  children: React.ReactNode;
};

const insightCardStyles: Record<InsightCardType, string> = {
  alert: "border-amber-500/25 bg-amber-500/10 text-amber-800",
  success: "border-emerald-500/25 bg-emerald-500/10 text-emerald-800",
  info: "border-sky-500/25 bg-sky-500/10 text-sky-800",
};

export function InsightCard({ type = "info", className, children }: InsightCardProps) {
  return (
    <div
      className={cn(
        "rounded-lg border px-4 py-3 text-sm leading-relaxed shadow-xs",
        insightCardStyles[type],
        className,
      )}
    >
      {children}
    </div>
  );
}
