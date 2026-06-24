import { cn } from "@/lib/utils";

interface ScoreBadgeProps {
  score: number;
  label?: string;
  size?: "sm" | "md" | "lg";
  showBar?: boolean;
  className?: string;
}

const categoryConfig = {
  A: { color: "text-[#059669]", barClass: "score-bar-fill-a", label: "Excelente" },
  B: { color: "text-[#2563EB]", barClass: "score-bar-fill-b", label: "Bom" },
  C: { color: "text-[#D97706]", barClass: "score-bar-fill-c", label: "Atenção" },
  D: { color: "text-[#B91C1C]", barClass: "score-bar-fill-d", label: "Risco" },
};

function getCategory(score: number): keyof typeof categoryConfig {
  if (score >= 80) return "A";
  if (score >= 60) return "B";
  if (score >= 40) return "C";
  return "D";
}

export function ScoreBadge({ score, label, size = "md", showBar = true, className }: ScoreBadgeProps) {
  const category = getCategory(score);
  const config = categoryConfig[category];

  const sizeClasses = size === "sm" ? "text-sm" : size === "lg" ? "text-3xl" : "text-2xl";

  return (
    <div className={cn("space-y-2", className)}>
      <div className="flex items-baseline gap-2">
        <span className={cn("font-semibold tracking-tight", sizeClasses, config.color)}>
          {score}
          <span className="text-sm font-normal text-muted-foreground">/100</span>
        </span>
        <span className={cn("text-xs font-semibold px-2 py-0.5 rounded-md border", config.color)}>
          {label || config.label}
        </span>
      </div>
      {showBar && (
        <div className="score-bar">
          <div
            className={cn("score-bar-fill", config.barClass)}
            style={{ width: `${score}%` }}
          />
        </div>
      )}
    </div>
  );
}
