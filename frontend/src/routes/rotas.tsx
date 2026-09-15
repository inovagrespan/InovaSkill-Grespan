import { useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { ChevronLeft, ChevronRight, MapPin, Search, Truck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SkeletonList, SkeletonModalContent } from "@/components/ui/skeleton";
import { RouteSnapshotDateSelect } from "@/components/RouteSnapshotDateSelect";
import { RouteOccupancyIndicator } from "@/components/RouteOccupancyIndicator";
import { RouteOptimizationSimulation } from "@/components/RouteOptimizationSimulation";
import { RouteRoadMap } from "@/components/RouteRoadMap";
import { ConsolidatedRouteTollKpi } from "@/components/ConsolidatedRouteTollKpi";
import {
  fetchImportedRoutes,
  fetchImportedRouteDetail,
  fetchRouteCost,
  fetchRouteRoadPath,
  type ImportedRouteItem,
  type ImportedRouteDetail,
  type RouteCostDetail,
  type RouteRoadPath,
} from "@/lib/importer-api";
import { getCurrentUserRole } from "@/lib/auth";
import { canRoleResolveRouteIssues, canRoleUseRouteSimulation } from "@/lib/access-control";
import { formatCapacityKg, formatRouteLoadKg, type OccupancyLevel } from "@/lib/route-occupancy";
import { formatRouteDuration } from "@/lib/route-fuel-consumption";
import { getCurrentLocalDate } from "@/lib/route-snapshot-history";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";

export const Route = createFileRoute("/rotas")({ component: RotasPage });

const weekdayLabels: Record<string, string> = {
  MONDAY: "Segunda",
  TUESDAY: "Terça",
  WEDNESDAY: "Quarta",
  THURSDAY: "Quinta",
  FRIDAY: "Sexta",
  SATURDAY: "Sábado",
  SUNDAY: "Domingo",
};

const ALL_OCCUPANCY_LEVELS = "all";
const ALL_WEEKDAYS = "all";

function formatCurrencyRange(minimum: number | null, maximum: number | null): string {
  if (minimum === null || maximum === null) return "Indisponível";
  const format = (value: number) => value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
  return minimum === maximum ? format(minimum) : `${format(minimum)} a ${format(maximum)}`;
}

function RotasPage() {
  const currentRole = getCurrentUserRole();
  const canSimulate = canRoleUseRouteSimulation(currentRole);
  const canResolveIssues = canRoleResolveRouteIssues(currentRole);
  const [routes, setRoutes] = useState<ImportedRouteItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, TEXT_SEARCH_DEBOUNCE_MS);
  const [snapshotDate, setSnapshotDate] = useState(() => getCurrentLocalDate());
  const [routeView, setRouteView] = useState<"current" | "suggested">("current");
  const [weekday, setWeekday] = useState(ALL_WEEKDAYS);
  const [occupancyLevel, setOccupancyLevel] = useState<OccupancyLevel | typeof ALL_OCCUPANCY_LEVELS>(ALL_OCCUPANCY_LEVELS);
  const pageSize = 20;

  const [apiError, setApiError] = useState<string | null>(null);
  const [selectedRoute, setSelectedRoute] = useState<ImportedRouteDetail | null>(null);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [roadPath, setRoadPath] = useState<RouteRoadPath | null>(null);
  const [roadPathLoading, setRoadPathLoading] = useState(false);
  const [roadPathError, setRoadPathError] = useState<string | null>(null);
  const [routeCost, setRouteCost] = useState<RouteCostDetail | null>(null);
  const [routeCostError, setRouteCostError] = useState<string | null>(null);

  async function load(p: number = page) {
    setLoading(true);
    setApiError(null);
    try {
      const data = await fetchImportedRoutes(p, pageSize, {
        search: debouncedSearch || undefined,
        date: snapshotDate,
        weekday: weekday === ALL_WEEKDAYS ? undefined : weekday,
        occupancyLevel: occupancyLevel === ALL_OCCUPANCY_LEVELS ? undefined : occupancyLevel,
      });
      setRoutes(data.items);
      setTotal(data.total);
      setPage(data.page);
    } catch (error) {
      setRoutes([]);
      setApiError((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load(1);
  }, [debouncedSearch, snapshotDate, weekday, occupancyLevel]);

  async function openDetails(route: ImportedRouteItem) {
    setDetailsOpen(true);
    setDetailsLoading(true);
    setRoadPath(null);
    setRoadPathError(null);
    setRouteCost(null);
    setRouteCostError(null);
    setRoadPathLoading(true);
    void fetchRouteRoadPath(route.id)
      .then(setRoadPath)
      .catch((error: Error) => setRoadPathError(error.message))
      .finally(() => setRoadPathLoading(false));
    void fetchRouteCost(route.id)
      .then(setRouteCost)
      .catch((error: Error) => setRouteCostError(error.message));
    try {
      const detail = await fetchImportedRouteDetail(route.id);
      setSelectedRoute(detail);
    } catch {
      setSelectedRoute(null);
    } finally {
      setDetailsLoading(false);
    }
  }

  const pageCount = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <span className="page-header-kicker">Rotas</span>
          <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">Rotas</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Visualize as rotas importadas e acompanhe indicadores de ocupação e desempenho.
          </p>
        </div>
        <div className="flex flex-col gap-2 sm:items-end">
          <div className="flex w-full flex-col gap-2 sm:w-auto sm:flex-row sm:items-end">
            <RouteSnapshotDateSelect value={snapshotDate} onValueChange={setSnapshotDate} />
            <label className="flex w-full flex-col gap-1.5 text-xs font-medium text-foreground sm:w-44">
              Dia da semana
              <Select value={weekday} onValueChange={setWeekday}>
                <SelectTrigger aria-label="Filtrar por dia da semana" className="bg-surface">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL_WEEKDAYS}>Todos os dias</SelectItem>
                  {Object.entries(weekdayLabels).map(([value, label]) => (
                    <SelectItem key={value} value={value}>{label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </label>
          </div>
          <div className="flex gap-2">
            <Button variant={routeView === "current" ? "default" : "outline"} onClick={() => setRouteView("current")}>Rotas reais</Button>
            <Button variant={routeView === "suggested" ? "default" : "outline"} onClick={() => setRouteView("suggested")}>Sugestões</Button>
          </div>
        </div>
      </header>

      {routeView === "suggested" ? (
        <RouteOptimizationSimulation
          date={snapshotDate}
          weekday={weekday === ALL_WEEKDAYS ? undefined : weekday}
          canSimulate={canSimulate}
          canResolveIssues={canResolveIssues}
        />
      ) : (
      <Card className="animate-soft-enter border-border bg-surface">
        <CardHeader>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <CardTitle>Todas as rotas</CardTitle>
            <div className="flex w-full flex-col gap-3 sm:w-auto sm:flex-row sm:items-end">
              <label className="flex w-full flex-col gap-1.5 text-xs font-medium text-foreground sm:w-44">
                Criticidade
                <Select value={occupancyLevel} onValueChange={(value) => setOccupancyLevel(value as typeof occupancyLevel)}>
                  <SelectTrigger aria-label="Filtrar por criticidade" className="bg-surface">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL_OCCUPANCY_LEVELS}>Todas</SelectItem>
                    <SelectItem value="critical">Crítico</SelectItem>
                    <SelectItem value="good">Saudável</SelectItem>
                    <SelectItem value="medium">Médio</SelectItem>
                    <SelectItem value="idle">Ocioso</SelectItem>
                    <SelectItem value="unavailable">Indisponível</SelectItem>
                  </SelectContent>
                </Select>
              </label>
              <div className="relative w-full sm:w-72">
                <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  placeholder="Buscar rota ou cidade..."
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  className="pl-9"
                />
              </div>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          {loading && routes.length === 0 && <SkeletonList rows={5} />}

          {!loading && apiError && (
            <div className="space-y-3 rounded-lg border border-destructive/30 bg-destructive/5 p-4">
              <p className="text-sm text-destructive">{apiError}</p>
              <Button size="sm" variant="outline" onClick={() => void load(1)}>
                Tentar novamente
              </Button>
            </div>
          )}

          {!loading && !apiError && routes.length === 0 && (
            <p className="text-sm text-muted-foreground">
              {search || weekday !== ALL_WEEKDAYS || occupancyLevel !== ALL_OCCUPANCY_LEVELS
                ? "Nenhuma rota encontrada para os filtros selecionados."
                : "Nenhuma rota importada encontrada. Importe um arquivo XLSX na página de Importações para começar."}
            </p>
          )}

          {routes.map((r) => (
            <div
              key={r.id}
              className="w-full rounded-lg border border-border/80 p-3 transition-all duration-200 hover:border-border hover:bg-white/[0.03]"
            >
              <div className="flex items-start justify-between gap-2">
                <button type="button" onClick={() => openDetails(r)} className="flex min-w-0 items-center gap-2 text-left">
                  <MapPin className="size-4 text-muted-foreground shrink-0" />
                  <p className="truncate text-sm font-medium">{r.name}</p>
                </button>
                <div className="flex shrink-0 flex-wrap items-center justify-end gap-2">
                  <Badge variant="outline">{weekdayLabels[r.weekday] ?? r.weekday}</Badge>
                </div>
              </div>
              <button type="button" onClick={() => openDetails(r)} className="w-full text-left">
                <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
                  <span className="flex items-center gap-1">
                    <Truck className="size-3" />
                    {r.vehicleType}
                  </span>
                  <span>{r.entryCount} cidade(s)</span>
                  <span>{r.totalDeliveries} entrega(s)</span>
                  {r.departureTime && <span>Saída {r.departureTime.slice(0, 5)}</span>}
                </div>
                <RouteOccupancyIndicator value={r.overallOccupancy} compact />
              </button>
            </div>
          ))}

          {!loading && total > pageSize && (
            <div className="flex items-center justify-end gap-2 pt-1">
              <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => void load(page - 1)}>
                <ChevronLeft className="size-4" />
                Anterior
              </Button>
              <span className="text-xs text-muted-foreground">
                Página {page} de {pageCount}
              </span>
              <Button size="sm" variant="outline" disabled={page >= pageCount} onClick={() => void load(page + 1)}>
                Próxima
                <ChevronRight className="size-4" />
              </Button>
            </div>
          )}
        </CardContent>
      </Card>
      )}

      <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="max-w-3xl border-border bg-surface max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Detalhes da Rota</DialogTitle>
            <DialogDescription>
              Cidades, entregas e informações do veículo.
            </DialogDescription>
          </DialogHeader>

          {detailsLoading && <SkeletonModalContent />}

          {!detailsLoading && selectedRoute && (
            <div className="space-y-4">
              <div className="grid grid-cols-1 gap-3 text-sm md:grid-cols-3">
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Nome</p>
                  <p className="font-medium">{selectedRoute.name}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Dia da semana</p>
                  <p className="font-medium">{weekdayLabels[selectedRoute.weekday] ?? selectedRoute.weekday}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Veículo</p>
                  <p className="font-medium">
                    {selectedRoute.vehicleType}
                    <span className="ml-1 text-xs text-muted-foreground">
                      ({formatCapacityKg(selectedRoute.vehicleCapacityKg)})
                    </span>
                  </p>
                </div>
                <RouteOccupancyIndicator
                  value={selectedRoute.overallOccupancy}
                  className="md:col-span-3"
                />
              </div>

              <div className="space-y-3 rounded-lg border border-border p-3">
                <div>
                  <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">Mapa da rota</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    Percurso rodoviário da rota real, com origem, paradas e retorno ao depósito.
                  </p>
                </div>
                {roadPathLoading && <div className="h-[340px] animate-pulse rounded-lg bg-muted" aria-label="Carregando mapa da rota" />}
                {!roadPathLoading && roadPathError && (
                  <p className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
                    {roadPathError}
                  </p>
                )}
                {!roadPathLoading && roadPath && <RouteRoadMap route={roadPath} />}
              </div>

              <div className="grid grid-cols-1 gap-3 text-sm lg:grid-cols-3">
                <div className="route-kpi-grid grid grid-cols-1 gap-3 sm:grid-cols-2 lg:col-span-2 lg:h-full lg:grid-rows-2">
                  <div className="route-kpi-card rounded-xl border border-border/80 bg-background/30 p-4 shadow-sm"><p className="text-xs font-semibold uppercase tracking-wider text-primary">Quilometragem</p><p className="mt-2 text-xl font-display font-semibold">{routeCost?.item.distanceMeters === null || routeCost?.item.distanceMeters === undefined ? "Indisponível" : `${(routeCost.item.distanceMeters / 1000).toLocaleString("pt-BR", { maximumFractionDigits: 1 })} km`}</p></div>
                  <div className="route-kpi-card rounded-xl border border-border/80 bg-background/30 p-4 shadow-sm"><p className="text-xs font-semibold uppercase tracking-wider text-primary">Tempo para concluir</p><p className="mt-2 text-xl font-display font-semibold">{routeCost?.item.durationSeconds === null || routeCost?.item.durationSeconds === undefined ? "Indisponível" : formatRouteDuration(routeCost.item.durationSeconds)}</p></div>
                  <div className="route-kpi-card rounded-xl border border-border/80 bg-background/30 p-4 shadow-sm"><p className="text-xs font-semibold uppercase tracking-wider text-primary">Gasto estimado com combustível</p><p className="mt-2 text-lg font-display font-semibold">{formatCurrencyRange(routeCost?.item.minimumFuelCost ?? null, routeCost?.item.maximumFuelCost ?? null)}</p><p className="mt-2 text-xs text-muted-foreground">{routeCost?.item.isAvailable ? `${routeCost.item.minimumFuelLiters?.toLocaleString("pt-BR")} a ${routeCost.item.maximumFuelLiters?.toLocaleString("pt-BR")} L · ${selectedRoute.vehicleType}` : routeCost?.item.unavailableReason ?? "Consolidação de custo indisponível."}</p>{routeCost?.dieselPricePerLiter != null && <p className="mt-1 text-xs text-muted-foreground">Diesel de referência: {routeCost.dieselPricePerLiter.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}/L</p>}</div>
                  <ConsolidatedRouteTollKpi cost={routeCost?.item ?? null} />
                </div>
                <div className="route-kpi-card border-primary/30 bg-primary/5 p-4 shadow-sm lg:h-auto lg:self-start lg:aspect-square"><p className="text-xs font-semibold uppercase tracking-wider text-primary">Gastos totais</p><p className="mt-2 min-w-0 break-words text-base font-display font-semibold">{formatCurrencyRange(routeCost?.item.minimumTotalCost ?? null, routeCost?.item.maximumTotalCost ?? null)}</p><p className="mt-2 text-xs text-muted-foreground">Combustível + pedágio</p></div>
              </div>
              {routeCostError && <p className="text-xs text-destructive">{routeCostError}</p>}

              <div className="rounded-lg border border-border">
                <div className="border-b border-border px-3 py-2">
                  <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
                    Cidades ({selectedRoute.entries.length})
                  </p>
                </div>
                <div className="divide-y divide-border">
                  {selectedRoute.entries.map((entry) => (
                    <div key={entry.id} className="flex items-center justify-between px-3 py-2.5 text-sm">
                      <div className="flex items-center gap-3">
                        <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-muted text-xs text-muted-foreground">
                          {entry.sequence}
                        </span>
                        <div>
                          <p className="font-medium">{entry.name}</p>
                          {entry.isExcludedFromOptimization && <Badge variant="outline" className="mt-1">Fora da simulação</Badge>}
                          {entry.note && (
                            <p className="text-xs text-muted-foreground">{entry.note}</p>
                          )}
                        </div>
                      </div>
                      <div className="text-right text-xs text-muted-foreground">
                        <p>{entry.deliveries} entrega(s)</p>
                        <p>{formatRouteLoadKg(entry.averagePerDay)}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>

            </div>
          )}

          {!detailsLoading && !selectedRoute && (
            <p className="text-sm text-muted-foreground">Não foi possível carregar os detalhes desta rota.</p>
          )}
        </DialogContent>
      </Dialog>

    </div>
  );
}
