import { createFileRoute } from "@tanstack/react-router";
import { useCallback, useEffect, useState } from "react";
import { LogisticsRegionMap } from "@/components/ui/logistics-region-map";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { fetchLogisticsMapCustomers, type LogisticsMapCustomerItem } from "@/lib/importer-api";
import { type LogisticsMapCustomer, type LogisticsMapRoute } from "@/lib/logistics-map-data";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/mapa")({ component: MapaPage });

type ActivityFilter = "all" | "true" | "false";

const activityLabels: Record<ActivityFilter, string> = {
  all: "Todos",
  true: "Ativos",
  false: "Inativos",
};

const mapRouteOverlays: LogisticsMapRoute[] = [];

function MapaPage() {
  const [activityFilter, setActivityFilter] = useState<ActivityFilter>("true");
  const [customers, setCustomers] = useState<LogisticsMapCustomer[]>([]);
  const [withoutCoordinates, setWithoutCoordinates] = useState(0);
  const [totalCustomers, setTotalCustomers] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadCustomers = useCallback(async (filter: ActivityFilter) => {
    setLoading(true);
    setError("");
    try {
      const activeParam = filter === "all" ? undefined : filter;
      const result = await fetchLogisticsMapCustomers(activeParam);
      setCustomers(result.items.map(toLogisticsMapCustomer));
      setWithoutCoordinates(result.withoutCoordinates);
      setTotalCustomers(result.total);
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadCustomers(activityFilter);
  }, [activityFilter, loadCustomers]);

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
        <div className="flex flex-wrap gap-2" aria-label="Filtro de atividade">
          {(Object.entries(activityLabels) as [ActivityFilter, string][]).map(([value, label]) => (
            <Button key={value} size="sm" variant={activityFilter === value ? "default" : "outline"} onClick={() => setActivityFilter(value)}>
              {label}
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
        <LogisticsRegionMap customers={customers} routes={mapRouteOverlays} />
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
