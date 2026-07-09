import { createFileRoute } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { LogisticsRegionMap } from "@/components/ui/logistics-region-map";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { fetchLogisticsMapCustomers, type LogisticsMapCustomerItem } from "@/lib/importer-api";
import { LOGISTICS_PERIOD_OPTIONS, type LogisticsPeriodDays } from "@/lib/logistics-dashboard";
import { type LogisticsMapCustomer, type LogisticsMapRoute } from "@/lib/logistics-map-data";

export const Route = createFileRoute("/mapa")({ component: MapaPage });

const periodLabels: Record<LogisticsPeriodDays, string> = {
  1: "Hoje",
  7: "7 dias",
  30: "30 dias",
  90: "90 dias",
};

const mapRouteOverlays: LogisticsMapRoute[] = [];

function MapaPage() {
  const [periodDays, setPeriodDays] = useState<LogisticsPeriodDays>(30);
  const [customers, setCustomers] = useState<LogisticsMapCustomer[]>([]);
  const [withoutCoordinates, setWithoutCoordinates] = useState(0);
  const [totalCustomers, setTotalCustomers] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError("");
    fetchLogisticsMapCustomers()
      .then(result => {
        if (!active) return;
        setCustomers(result.items.map(toLogisticsMapCustomer));
        setWithoutCoordinates(result.withoutCoordinates);
        setTotalCustomers(result.total);
      })
      .catch(reason => {
        if (active) setError((reason as Error).message);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <span className="page-header-kicker">Mapa</span>
            <Badge variant="outline">Clientes reais</Badge>
          </div>
          <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">Mapa de rotas</h1>
          <p className="mt-1 text-sm text-muted-foreground">Visualize clientes reais posicionados pelo município cadastrado.</p>
        </div>
        <div className="flex flex-wrap gap-2" aria-label="Período do mapa de rotas">
          {LOGISTICS_PERIOD_OPTIONS.map((days) => (
            <Button key={days} size="sm" variant={periodDays === days ? "default" : "outline"} onClick={() => setPeriodDays(days)}>
              {periodLabels[days]}
            </Button>
          ))}
        </div>
      </header>

      {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}
      {!error && withoutCoordinates > 0 && (
        <Alert>
          <AlertDescription>
            {withoutCoordinates} de {totalCustomers} cliente(s) ainda não têm coordenada municipal resolvida.
          </AlertDescription>
        </Alert>
      )}

      {loading ? (
        <Skeleton className="h-[500px] min-h-[420px] w-full rounded-xl" />
      ) : (
        <LogisticsRegionMap customers={customers} routes={mapRouteOverlays} periodDays={periodDays} />
      )}
    </div>
  );
}

function toLogisticsMapCustomer(item: LogisticsMapCustomerItem): LogisticsMapCustomer {
  return {
    id: item.id,
    name: item.name,
    city: item.city,
    type: item.type,
    status: item.status,
    lastDelivery: item.lastDelivery,
    nextDelivery: item.nextDelivery,
    situation: item.situation,
    route: item.route,
    priority: item.priority,
    lat: item.lat,
    lng: item.lng,
  };
}
