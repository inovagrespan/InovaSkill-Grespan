import {
  MEDIUM_OCCUPANCY_THRESHOLD,
  classifyOccupancy,
  formatOccupancy,
} from "@/lib/route-occupancy";
import { cn } from "@/lib/utils";

type RouteOccupancyIndicatorProps = {
  value: number | null;
  compact?: boolean;
  className?: string;
};

export function RouteOccupancyIndicator({
  value,
  compact = false,
  className,
}: RouteOccupancyIndicatorProps) {
  const presentation = classifyOccupancy(value);
  const isAvailable = value !== null && Number.isFinite(value);
  const percentage = isAvailable ? value * 100 : 0;
  const visualPercentage = Math.min(100, Math.max(0, percentage));

  return (
    <div
      className={cn(
        "flex items-center gap-3",
        compact ? "mt-3" : "rounded-lg border border-border p-3",
        className,
      )}
    >
      <div
        className={cn(
          "grid shrink-0 place-items-center rounded-full",
          compact ? "size-14" : "size-20",
        )}
        style={{
          background: `conic-gradient(${presentation.color} ${visualPercentage}%, ${presentation.backgroundColor} 0)`,
        }}
        role="progressbar"
        aria-label={`Ocupação ${presentation.label}`}
        aria-valuenow={isAvailable ? percentage : undefined}
        aria-valuemin={0}
      >
        <div
          className={cn(
            "grid place-items-center rounded-full bg-surface font-semibold",
            compact ? "size-11 text-xs" : "size-16 text-sm",
          )}
          style={{ color: presentation.color }}
        >
          {isAvailable ? formatOccupancy(value) : "—"}
        </div>
      </div>

      <div className="min-w-0 flex-1 space-y-1.5">
        <div className="flex items-center justify-between gap-2">
          <span className="text-xs text-muted-foreground">Taxa de ocupação</span>
          <span
            className="rounded-full border px-2 py-0.5 text-xs font-semibold"
            style={{
              borderColor: presentation.color,
              color: presentation.color,
              backgroundColor: presentation.backgroundColor,
            }}
          >
            {presentation.label}
          </span>
        </div>
        <div className="h-2 overflow-hidden rounded-full bg-muted">
          <div
            className="h-full rounded-full transition-[width] duration-300"
            style={{
              width: `${visualPercentage}%`,
              backgroundColor: presentation.color,
            }}
          />
        </div>
        {percentage > 100 && (
          <p className="text-xs font-medium text-red-600">
            Sobrecarga de {formatOccupancy(value! - 1)}
          </p>
        )}
        {isAvailable && value < MEDIUM_OCCUPANCY_THRESHOLD && (
          <p className="text-xs font-medium" style={{ color: presentation.color }}>
            Ociosidade: {formatOccupancy(1 - value)} da capacidade livre
          </p>
        )}
      </div>
    </div>
  );
}
