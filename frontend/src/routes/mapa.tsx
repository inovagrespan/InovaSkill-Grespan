import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { LogisticsRegionMap } from "@/components/ui/logistics-region-map";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { LOGISTICS_PERIOD_OPTIONS, type LogisticsPeriodDays } from "@/lib/logistics-dashboard";
import { demoLogisticsMapCustomers, demoLogisticsMapRoutes } from "@/lib/logistics-map-data";

export const Route = createFileRoute("/mapa")({ component: MapaPage });

const periodLabels: Record<LogisticsPeriodDays, string> = {
  1: "Hoje",
  7: "7 dias",
  30: "30 dias",
  90: "90 dias",
};

function MapaPage() {
  const [periodDays, setPeriodDays] = useState<LogisticsPeriodDays>(30);

  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <span className="page-header-kicker">Mapa</span>
            <Badge variant="outline">Base demonstrativa</Badge>
          </div>
          <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">Mapa de rotas</h1>
          <p className="mt-1 text-sm text-muted-foreground">Visualize clientes, trajetos desenhados e pontos de congestionamento.</p>
        </div>
        <div className="flex flex-wrap gap-2" aria-label="Período do mapa de rotas">
          {LOGISTICS_PERIOD_OPTIONS.map((days) => (
            <Button key={days} size="sm" variant={periodDays === days ? "default" : "outline"} onClick={() => setPeriodDays(days)}>
              {periodLabels[days]}
            </Button>
          ))}
        </div>
      </header>

      <Card>
        <CardContent className="p-4 sm:p-6">
          <LogisticsRegionMap customers={demoLogisticsMapCustomers} routes={demoLogisticsMapRoutes} periodDays={periodDays} />
        </CardContent>
      </Card>
    </div>
  );
}
