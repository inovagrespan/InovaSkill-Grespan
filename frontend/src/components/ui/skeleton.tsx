import { cn } from "@/lib/utils";

function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      aria-hidden="true"
      className={cn("skeleton-shimmer rounded-md", className)}
      {...props}
    />
  );
}

function SkeletonMetricCard({ className }: { className?: string }) {
  return (
    <div className={cn("rounded-xl border border-border bg-surface p-4 shadow-xs", className)}>
      <div className="flex items-start justify-between gap-3">
        <Skeleton className="h-3 w-24" />
        <Skeleton className="h-5 w-14 rounded-full" />
      </div>
      <div className="mt-6 flex items-center justify-between gap-3">
        <Skeleton className="h-8 w-32" />
        <Skeleton className="size-8 rounded-full" />
      </div>
      <Skeleton className="mt-4 h-3 w-40" />
    </div>
  );
}

function SkeletonCard({ className, lines = 3 }: { className?: string; lines?: number }) {
  return (
    <div className={cn("rounded-lg border border-border bg-surface p-4", className)}>
      <Skeleton className="h-4 w-1/3" />
      <div className="mt-4 space-y-2">
        {Array.from({ length: lines }).map((_, index) => (
          <Skeleton key={index} className={cn("h-3", index === lines - 1 ? "w-2/3" : "w-full")} />
        ))}
      </div>
    </div>
  );
}

function SkeletonTable({ rows = 6, columns = 5, className }: { rows?: number; columns?: number; className?: string }) {
  return (
    <div className={cn("w-full overflow-hidden rounded-lg border border-border", className)}>
      <div className="grid gap-3 border-b border-border bg-muted/30 p-3" style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }}>
        {Array.from({ length: columns }).map((_, index) => (
          <Skeleton key={index} className="h-3 w-3/4" />
        ))}
      </div>
      <div className="divide-y divide-border/60">
        {Array.from({ length: rows }).map((_, rowIndex) => (
          <div key={rowIndex} className="grid gap-3 p-3" style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }}>
            {Array.from({ length: columns }).map((_, columnIndex) => (
              <Skeleton key={columnIndex} className={cn("h-4", columnIndex === columns - 1 ? "w-2/3" : "w-full")} />
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}

function SkeletonChart({ className }: { className?: string }) {
  return (
    <div className={cn("h-[220px] rounded-lg border border-border p-4", className)}>
      <div className="flex h-full items-end gap-3">
        {[55, 80, 42, 68, 92, 64, 76].map((height, index) => (
          <Skeleton key={index} className="flex-1 rounded-t-md" style={{ height: `${height}%` }} />
        ))}
      </div>
    </div>
  );
}

function SkeletonList({ rows = 5, className }: { rows?: number; className?: string }) {
  return (
    <div className={cn("space-y-2", className)}>
      {Array.from({ length: rows }).map((_, index) => (
        <div key={index} className="rounded-lg border border-border p-3">
          <Skeleton className="h-4 w-1/2" />
          <Skeleton className="mt-3 h-3 w-4/5" />
        </div>
      ))}
    </div>
  );
}

function SkeletonModalContent({ className }: { className?: string }) {
  return (
    <div className={cn("space-y-4", className)}>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <SkeletonCard lines={2} />
        <SkeletonCard lines={2} />
        <SkeletonCard lines={2} />
      </div>
      <SkeletonTable rows={4} columns={4} />
      <SkeletonCard lines={5} />
    </div>
  );
}

export { Skeleton, SkeletonCard, SkeletonChart, SkeletonList, SkeletonMetricCard, SkeletonModalContent, SkeletonTable };
