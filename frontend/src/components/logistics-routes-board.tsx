import { useMemo, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { LogisticsRegionMap } from "@/components/ui/logistics-region-map";
import {
  LOGISTICS_PERIOD_OPTIONS,
  formatLogisticsDuration,
  type LogisticsPeriodDays,
} from "@/lib/logistics-dashboard";
import {
  buildTrafficDelayRanking,
  demoLogisticsMapCustomers,
  demoLogisticsMapRoutes,
  type LogisticsTrafficSeverity,
} from "@/lib/logistics-map-data";

const INITIAL_TRAFFIC_DELAY_ROUTE_LIMIT = 4;

const periodLabels: Record<LogisticsPeriodDays, string> = {
  1: "Hoje",
  7: "7 dias",
  30: "30 dias",
  90: "90 dias",
};

function trafficSeverityClass(severity: LogisticsTrafficSeverity): string {
  if (severity === "Crítico") return "border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300";
  if (severity === "Intenso") return "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300";
  return "border-sky-200 bg-sky-50 text-sky-700 dark:border-sky-900 dark:bg-sky-950/40 dark:text-sky-300";
}

export function LogisticsRoutesBoard() {
  const [periodDays, setPeriodDays] = useState<LogisticsPeriodDays>(30);
  const [showAllTrafficDelayRoutes, setShowAllTrafficDelayRoutes] = useState(false);

  const trafficDelayRanking = useMemo(() => buildTrafficDelayRanking(demoLogisticsMapRoutes, periodDays), [periodDays]);
  const visibleTrafficDelayRanking = useMemo(
    () => showAllTrafficDelayRoutes ? trafficDelayRanking : trafficDelayRanking.slice(0, INITIAL_TRAFFIC_DELAY_ROUTE_LIMIT),
    [showAllTrafficDelayRoutes, trafficDelayRanking],
  );
  const hiddenTrafficDelayRouteCount = Math.max(0, trafficDelayRanking.length - visibleTrafficDelayRanking.length);
  const maxTrafficDelayMinutes = useMemo(
    () => Math.max(1, ...trafficDelayRanking.map((route) => route.delayMinutes)),
    [trafficDelayRanking],
  );

  function changePeriod(days: LogisticsPeriodDays) {
    setPeriodDays(days);
    setShowAllTrafficDelayRoutes(false);
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap justify-end gap-2" aria-label="Período do quadro de rotas">
        {LOGISTICS_PERIOD_OPTIONS.map((days) => (
          <Button key={days} size="sm" variant={periodDays === days ? "default" : "outline"} onClick={() => changePeriod(days)}>
            {periodLabels[days]}
          </Button>
        ))}
      </div>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2" aria-label="Quadro de rotas">
        <Card className="h-full">
          <CardHeader>
            <CardTitle className="text-lg">Clientes, rotas e trânsito</CardTitle>
            <CardDescription>Todos os clientes, trajetos operacionais e pontos com impacto de congestionamento.</CardDescription>
          </CardHeader>
          <CardContent>
            <LogisticsRegionMap customers={demoLogisticsMapCustomers} routes={demoLogisticsMapRoutes} periodDays={periodDays} compact />
          </CardContent>
        </Card>
        <Card className="logistics-city-chart-card h-full">
          <CardHeader>
            <CardTitle className="text-lg">Rotas com mais atrasos por congestionamento</CardTitle>
            <CardDescription>Minutos perdidos por rota no período selecionado, considerando apenas ocorrências de trânsito.</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {visibleTrafficDelayRanking.map((route) => {
                const delayPercent = Math.min(100, Math.max(0, (route.delayMinutes / maxTrafficDelayMinutes) * 100));
                return (
                  <article key={route.routeId} className="rounded-lg border bg-background p-4 text-left transition-colors hover:border-primary/40">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="text-sm font-semibold">{route.routeName}</p>
                        <p className="mt-1 truncate text-xs text-muted-foreground">{route.cities}</p>
                      </div>
                      <Badge variant="outline" className={trafficSeverityClass(route.severity)}>
                        {route.severity}
                      </Badge>
                    </div>
                    <div className="mt-4">
                      <div className="h-2 overflow-hidden rounded-full bg-muted">
                        <div className="h-full rounded-full bg-primary transition-all" style={{ width: `${delayPercent}%` }} />
                      </div>
                      <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
                        <span>{formatLogisticsDuration(route.delayMinutes)} de atraso</span>
                        <span>{route.congestionCount} registros</span>
                      </div>
                    </div>
                  </article>
                );
              })}
              {hiddenTrafficDelayRouteCount > 0 ? (
                <Button
                  type="button"
                  variant="outline"
                  className="w-full"
                  onClick={() => setShowAllTrafficDelayRoutes(true)}
                >
                  Exibir mais {hiddenTrafficDelayRouteCount} rotas
                </Button>
              ) : null}
            </div>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
