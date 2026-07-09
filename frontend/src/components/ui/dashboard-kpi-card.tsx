import type { ComponentType, ReactNode } from "react";
import { Card, CardContent } from "@/components/ui/card";
import { cn } from "@/lib/utils";

type DashboardKpiCardProps = {
  title: string;
  value: string;
  icon: ComponentType<{ className?: string }>;
  children?: ReactNode;
  className?: string;
  interactive?: boolean;
};

export function DashboardKpiCard({
  title,
  value,
  icon: Icon,
  children,
  className,
  interactive = false,
}: DashboardKpiCardProps) {
  return (
    <Card className={cn("h-full min-h-44", interactive && "cursor-pointer hover:border-primary/40", className)}>
      <CardContent className="flex h-full flex-col gap-6 px-6 pb-5 pt-5">
        <div className="flex min-h-16 items-start justify-between gap-4 pt-3">
          <h2 className="min-w-0 flex-1 text-balance text-sm font-semibold leading-snug">{title}</h2>
          <span className="inline-flex size-10 shrink-0 items-center justify-center rounded-full border border-primary/10 bg-primary/10 text-primary">
            <Icon className="size-4" />
          </span>
        </div>
        <div className="mt-auto">
          <p className="text-3xl font-display tracking-tight">{value}</p>
          {children}
        </div>
      </CardContent>
    </Card>
  );
}
