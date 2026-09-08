import { useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { CheckCircle2, ChevronLeft, ChevronRight, MapPin, Search, Sparkles, Truck, XCircle } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SkeletonList, SkeletonModalContent } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { RouteSnapshotDateSelect } from "@/components/RouteSnapshotDateSelect";
import { RouteOccupancyIndicator } from "@/components/RouteOccupancyIndicator";
import { RouteRoadMap } from "@/components/RouteRoadMap";
import {
  fetchImportedRoutes,
  fetchImportedRouteDetail,
  fetchRouteRoadPath,
  fetchLogisticsFuelSettings,
  fetchOptimizedRouteRoadPath,
  fetchDailyRouteOptimization,
  startDailyRouteOptimization,
  decideDailyRouteOptimization,
  type ImportedRouteItem,
  type ImportedRouteDetail,
  type RouteRoadPath,
  type DailyOptimizationExecution,
  type DailyOptimizationRoute,
} from "@/lib/importer-api";
import { getCurrentUserRole } from "@/lib/auth";
import { canRoleUseRouteSimulation } from "@/lib/access-control";
import { formatCapacityKg, formatRouteLoadKg, type OccupancyLevel } from "@/lib/route-occupancy";
import { getCurrentLocalDate } from "@/lib/route-snapshot-history";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";
import { groupRoutesByWeekday } from "@/lib/route-weekday";
import { groupOptimizedRouteCustomers } from "@/lib/optimized-route-customers";
import { estimateFuelCost, estimateRouteFuelConsumption, formatFuelConsumptionRange, formatRouteDuration } from "@/lib/route-fuel-consumption";

export const Route = createFileRoute("/rotas")({ component: RotasPage });

const weekdayLabels: Record<string, string> = {
  MONDAY: "Segunda-feira",
  TUESDAY: "Terça-feira",
  WEDNESDAY: "Quarta-feira",
  THURSDAY: "Quinta-feira",
  FRIDAY: "Sexta-feira",
};

const ALL_OCCUPANCY_LEVELS = "all";
const ALL_WEEKDAYS = "all";
const OPTIMIZATION_POLL_INTERVAL_MS = 2_000;

function RotasPage() {
  const currentRole = getCurrentUserRole();
  const canSimulate = canRoleUseRouteSimulation(currentRole);
  const [routes, setRoutes] = useState<ImportedRouteItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, TEXT_SEARCH_DEBOUNCE_MS);
  const [snapshotDate, setSnapshotDate] = useState(() => getCurrentLocalDate());
  const [occupancyLevel, setOccupancyLevel] = useState<OccupancyLevel | typeof ALL_OCCUPANCY_LEVELS>(ALL_OCCUPANCY_LEVELS);
  const [weekday, setWeekday] = useState(ALL_WEEKDAYS);
  const pageSize = 20;
  const [snapshotImportId, setSnapshotImportId] = useState<string | null>(null);
  const [optimizationWeekday, setOptimizationWeekday] = useState("MONDAY");
  const [optimization, setOptimization] = useState<DailyOptimizationExecution | null>(null);
  const [optimizationLoading, setOptimizationLoading] = useState(false);
  const [optimizationError, setOptimizationError] = useState<string | null>(null);
  const [decisionJustification, setDecisionJustification] = useState("");
  const [decisionSaving, setDecisionSaving] = useState(false);

  const [apiError, setApiError] = useState<string | null>(null);
  const [selectedRoute, setSelectedRoute] = useState<ImportedRouteDetail | null>(null);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [dieselPricePerLiter, setDieselPricePerLiter] = useState<number | null>(null);
  const [roadPath, setRoadPath] = useState<RouteRoadPath | null>(null);
  const [roadPathLoading, setRoadPathLoading] = useState(false);
  const [roadPathError, setRoadPathError] = useState<string | null>(null);
  const [optimizedDetailsOpen, setOptimizedDetailsOpen] = useState(false);
  const [selectedOptimizedRoute, setSelectedOptimizedRoute] = useState<DailyOptimizationRoute | null>(null);
  const [optimizedRoadPath, setOptimizedRoadPath] = useState<RouteRoadPath | null>(null);
  const [optimizedRoadPathLoading, setOptimizedRoadPathLoading] = useState(false);
  const [optimizedRoadPathError, setOptimizedRoadPathError] = useState<string | null>(null);

  async function load(p: number = page) {
    setLoading(true);
    setApiError(null);
    try {
      const data = await fetchImportedRoutes(p, pageSize, {
        search: debouncedSearch || undefined,
        date: snapshotDate,
        occupancyLevel: occupancyLevel === ALL_OCCUPANCY_LEVELS ? undefined : occupancyLevel,
        weekday: weekday === ALL_WEEKDAYS ? undefined : weekday,
      });
      setRoutes(data.items);
      setSnapshotImportId(data.importId);
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
  }, [debouncedSearch, snapshotDate, occupancyLevel, weekday]);

  useEffect(() => {
    void fetchLogisticsFuelSettings()
      .then(settings => setDieselPricePerLiter(settings.dieselPricePerLiter))
      .catch(() => setDieselPricePerLiter(null));
  }, []);

  async function loadOptimization() {
    if (!snapshotImportId) return;
    try {
      setOptimization(await fetchDailyRouteOptimization(snapshotImportId, optimizationWeekday));
      setOptimizationError(null);
    } catch (error) {
      setOptimizationError((error as Error).message);
    }
  }

  useEffect(() => {
    setOptimization(null);
    void loadOptimization();
  }, [snapshotImportId, optimizationWeekday]);

  useEffect(() => {
    if (!optimization || !["Queued", "Processing", "Retrying"].includes(optimization.status)) return;
    const timer = window.setInterval(() => void loadOptimization(), OPTIMIZATION_POLL_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, [optimization?.status, snapshotImportId, optimizationWeekday]);

  async function optimizeDay() {
    if (!snapshotImportId) return;
    setOptimizationLoading(true);
    setOptimizationError(null);
    try {
      await startDailyRouteOptimization(snapshotImportId, optimizationWeekday);
      await loadOptimization();
    } catch (error) {
      setOptimizationError((error as Error).message);
    } finally {
      setOptimizationLoading(false);
    }
  }

  async function decideOptimization(approved: boolean) {
    if (!optimization) return;
    setDecisionSaving(true);
    setOptimizationError(null);
    try {
      await decideDailyRouteOptimization(optimization.jobExecutionId, approved, decisionJustification);
      setDecisionJustification("");
      await loadOptimization();
    } catch (error) {
      setOptimizationError((error as Error).message);
    } finally {
      setDecisionSaving(false);
    }
  }

  async function openDetails(route: ImportedRouteItem) {
    setDetailsOpen(true);
    setDetailsLoading(true);
    setRoadPath(null);
    setRoadPathError(null);
    setRoadPathLoading(true);
    void fetchRouteRoadPath(route.id)
      .then(setRoadPath)
      .catch((error: Error) => setRoadPathError(error.message))
      .finally(() => setRoadPathLoading(false));
    try {
      const detail = await fetchImportedRouteDetail(route.id);
      setSelectedRoute(detail);
    } catch {
      setSelectedRoute(null);
    } finally {
      setDetailsLoading(false);
    }
  }

  async function openOptimizedDetails(route: DailyOptimizationRoute) {
    setSelectedOptimizedRoute(route);
    setOptimizedDetailsOpen(true);
    setOptimizedRoadPath(null);
    setOptimizedRoadPathError(null);
    setOptimizedRoadPathLoading(true);
    try {
      setOptimizedRoadPath(await fetchOptimizedRouteRoadPath(
        optimization?.result?.importId ?? snapshotImportId ?? "",
        route.name,
        route.stops.map(stop => stop.municipalityId),
      ));
    } catch (error) {
      setOptimizedRoadPathError((error as Error).message);
    } finally {
      setOptimizedRoadPathLoading(false);
    }
  }

  const pageCount = Math.max(1, Math.ceil(total / pageSize));
  const optimizedCustomerGroups = groupOptimizedRouteCustomers(
    selectedOptimizedRoute?.stops ?? [],
    optimizedRoadPath,
  );
  const selectedOptimizedDistanceMeters = optimizedRoadPath?.distanceMeters ?? selectedOptimizedRoute?.distanceMeters ?? 0;
  const selectedOptimizedDurationSeconds = optimizedRoadPath?.durationSeconds ?? selectedOptimizedRoute?.durationSeconds ?? 0;
  const selectedOptimizedFuel = selectedOptimizedRoute
    ? estimateRouteFuelConsumption(
        selectedOptimizedRoute.vehicleType,
        selectedOptimizedDistanceMeters,
      )
    : null;
  const originalRouteFuel = selectedRoute && roadPath
    ? estimateRouteFuelConsumption(selectedRoute.vehicleType, roadPath.distanceMeters)
    : null;
  const originalFuelCost = estimateFuelCost(
    originalRouteFuel,
    dieselPricePerLiter,
  );
  const formatCurrency = (value: number) => value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });

  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in">
        <span className="page-header-kicker">Rotas</span>
        <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">Rotas</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Visualize as rotas importadas e acompanhe indicadores de ocupação e desempenho.
        </p>
      </header>

      <Tabs defaultValue="original" className="space-y-4">
        <TabsList aria-label="Visão das rotas">
          <TabsTrigger value="original">Original</TabsTrigger>
          <TabsTrigger value="optimization"><Sparkles className="mr-1.5 size-4" />Otimização por IA</TabsTrigger>
        </TabsList>
        <TabsContent value="original">
      <Card className="animate-soft-enter border-border bg-surface">
        <CardHeader>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <CardTitle>Todas as rotas</CardTitle>
            <div className="flex w-full flex-col gap-3 sm:w-auto sm:flex-row sm:items-end">
              <RouteSnapshotDateSelect
                value={snapshotDate}
                onValueChange={setSnapshotDate}
              />
              <label className="flex w-full flex-col gap-1.5 text-xs font-medium text-foreground sm:w-44">
                Dia da semana
                <Select value={weekday} onValueChange={setWeekday}>
                  <SelectTrigger aria-label="Filtrar por dia da semana" className="bg-surface">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={ALL_WEEKDAYS}>Todos os dias</SelectItem>
                    {Object.entries(weekdayLabels).map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}
                  </SelectContent>
                </Select>
              </label>
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
              {search ? "Nenhuma rota encontrada para esta busca." : "Nenhuma rota importada encontrada. Importe um arquivo XLSX na página de Importações para começar."}
            </p>
          )}

          {groupRoutesByWeekday(routes).map((group) => (
            <section key={group.weekday} className="space-y-3" aria-labelledby={`weekday-${group.weekday}`}>
              <div className="flex items-center gap-3 border-b border-border pb-2 pt-2">
                <h3 id={`weekday-${group.weekday}`} className="font-display text-lg font-semibold">{weekdayLabels[group.weekday]}</h3>
                <Badge variant="outline">{group.routes.length} rota(s) nesta página</Badge>
              </div>
              {group.routes.map((r) => <div
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
                </div>
                <RouteOccupancyIndicator value={r.overallOccupancy} compact />
              </button>
              </div>)}
            </section>
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
        </TabsContent>
        <TabsContent value="optimization">
          <Card className="animate-soft-enter border-border bg-surface">
            <CardHeader>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div><CardTitle>Rotas otimizadas do dia</CardTitle><p className="mt-1 text-sm text-muted-foreground">Simulação por custo: prioriza frota própria, combustível, autonomia e o menor custo de apoio.</p></div>
                <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
                  <label className="flex flex-col gap-1.5 text-xs font-medium">Dia da semana
                    <Select value={optimizationWeekday} onValueChange={setOptimizationWeekday}>
                      <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
                      <SelectContent>{Object.entries(weekdayLabels).map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}</SelectContent>
                    </Select>
                  </label>
                  <Button onClick={() => void optimizeDay()} disabled={!snapshotImportId || optimizationLoading || ["Queued", "Processing", "Retrying"].includes(optimization?.status ?? "")}>
                    <Sparkles className="mr-2 size-4" />{optimizationLoading ? "Iniciando..." : "Otimizar este dia"}
                  </Button>
                </div>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              {!snapshotImportId && <p className="text-sm text-muted-foreground">Não existe snapshot de rotas disponível para a data selecionada.</p>}
              {optimizationError && <p className="rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{optimizationError}</p>}
              {optimization && ["Queued", "Processing", "Retrying"].includes(optimization.status) && <div className="rounded-lg border border-border p-4"><p className="text-sm font-medium">{optimization.progressMessage ?? "Processando otimização"}</p><p className="mt-1 text-xs text-muted-foreground">{optimization.progressPercent.toLocaleString("pt-BR")}% concluído</p></div>}
              {!optimization && snapshotImportId && !optimizationError && <p className="text-sm text-muted-foreground">Selecione o dia e execute a primeira simulação.</p>}
              {optimization?.result && (
                <>
                  {optimization.result.message && <p className="rounded-lg border border-border p-3 text-sm">{optimization.result.message}</p>}
                  <div className="rounded-lg border border-border p-4">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div><p className="font-medium">Decisão sobre a simulação</p><p className="text-xs text-muted-foreground">A decisão é auditável e não altera as rotas originais.</p></div>
                      <Badge variant={optimization.decision.status === "APPROVED" ? "default" : "outline"}>{optimization.decision.status === "APPROVED" ? "Aprovada" : optimization.decision.status === "REJECTED" ? "Rejeitada" : "Aguardando decisão"}</Badge>
                    </div>
                    {optimization.decision.status === "PENDING" && canSimulate ? <div className="mt-3 space-y-3"><textarea aria-label="Justificativa da decisão" maxLength={1000} value={decisionJustification} onChange={(event) => setDecisionJustification(event.target.value)} placeholder="Justificativa opcional" className="min-h-20 w-full rounded-md border border-border bg-background px-3 py-2 text-sm"/><div className="flex flex-wrap gap-2"><Button disabled={decisionSaving} onClick={() => void decideOptimization(true)}><CheckCircle2 className="mr-2 size-4"/>Aprovar simulação</Button><Button disabled={decisionSaving} variant="outline" onClick={() => void decideOptimization(false)}><XCircle className="mr-2 size-4"/>Rejeitar simulação</Button></div></div> : null}
                    {optimization.decision.status !== "PENDING" ? <div className="mt-3 text-sm"><p>Decisão registrada por {optimization.decision.decidedByUserName ?? "usuário autorizado"}{optimization.decision.decidedAt ? ` em ${new Date(optimization.decision.decidedAt).toLocaleString("pt-BR")}` : ""}.</p>{optimization.decision.justification ? <p className="mt-1 text-muted-foreground">{optimization.decision.justification}</p> : null}</div> : null}
                  </div>
                  <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                    <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Distância proposta</p><p className="text-lg font-semibold">{(optimization.result.proposed.distanceMeters / 1000).toLocaleString("pt-BR", { maximumFractionDigits: 1 })} km</p></div>
                    <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Tempo estimado</p><p className="text-lg font-semibold">{Math.round(optimization.result.proposed.durationSeconds / 60).toLocaleString("pt-BR")} min</p></div>
                    <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Veículos utilizados</p><p className="text-lg font-semibold">{optimization.result.proposed.vehiclesUsed}</p></div>
                    <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Veículos alugados</p><p className="text-lg font-semibold">{optimization.result.proposed.rentalVehicles}</p></div>
                  </div>
                  <div className="space-y-3">{optimization.result.routes.map(route => (
                    <button type="button" onClick={() => void openOptimizedDetails(route)} key={`${route.sequence}-${route.name}`} className="w-full rounded-lg border border-border p-3 text-left transition-colors hover:bg-muted/40">
                      <div className="flex flex-wrap items-start justify-between gap-2"><div><p className="font-medium">{route.name}</p><p className="text-xs text-muted-foreground">{route.vehicleType} · {route.stops.length} cidade(s)</p></div><Badge variant={route.isRental ? "default" : "outline"}>{route.isRental ? "Alugado" : "Próprio"}</Badge></div>
                      <RouteOccupancyIndicator value={route.occupancy} compact />
                      <p className="mt-2 text-xs text-muted-foreground">{route.stops.map(stop => stop.municipalityName).join(" → ")}</p>
                    </button>
                  ))}</div>
                </>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="max-w-6xl border-border bg-surface max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Detalhes da Rota</DialogTitle>
            <DialogDescription>
              Percurso rodoviário, cidades, entregas e informações do veículo.
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
                  <p className="font-medium">Caminho da rota</p>
                  <p className="text-xs text-muted-foreground">Traçado pelas ruas e rodovias a partir das coordenadas HERE.</p>
                </div>
                {roadPathLoading && <div className="h-[340px] animate-pulse rounded-lg bg-muted" />}
                {!roadPathLoading && roadPathError && <p className="text-sm text-destructive">{roadPathError}</p>}
                {!roadPathLoading && roadPath && (
                  <>
                    <RouteRoadMap route={roadPath} />
                    <div className="flex flex-wrap gap-x-5 gap-y-1 text-xs text-muted-foreground">
                      <span>{(roadPath.distanceMeters / 1000).toLocaleString("pt-BR", { maximumFractionDigits: 1 })} km</span>
                      <span>{Math.round(roadPath.durationSeconds / 60).toLocaleString("pt-BR")} min estimados</span>
                      <span>{Math.max(0, roadPath.stops.length - 2)} cliente(s)</span>
                    </div>
                  </>
                )}
              </div>

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

              <section className="space-y-4 rounded-xl border border-border p-4" aria-label="Dados da rota">
                <div><h3 className="font-semibold">Dados da rota</h3><p className="text-xs text-muted-foreground">Resumo operacional e estimativa de custos do percurso original.</p></div>
                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                  <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Quilômetros da rota</p><p className="text-lg font-semibold">{roadPath ? `${(roadPath.distanceMeters / 1000).toLocaleString("pt-BR", { maximumFractionDigits: 1 })} km` : "Indisponível"}</p></div>
                  <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Tempo da rota</p><p className="text-lg font-semibold">{roadPath ? formatRouteDuration(roadPath.durationSeconds) : "Indisponível"}</p></div>
                  <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Consumo estimado</p><p className="text-lg font-semibold">{originalRouteFuel ? formatFuelConsumptionRange(originalRouteFuel) : "Não cadastrado"}</p><p className="text-xs text-muted-foreground">{originalRouteFuel ? `${originalRouteFuel.minimumEfficiencyKmPerLiter.toLocaleString("pt-BR")} a ${originalRouteFuel.maximumEfficiencyKmPerLiter.toLocaleString("pt-BR")} km/L` : `Sem referência para ${selectedRoute.vehicleType}`}</p></div>
                  <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Caminhão</p><p className="text-lg font-semibold">{selectedRoute.vehicleType}</p></div>
                </div>
                <div className="rounded-lg bg-primary/5 p-3">
                  <p className="text-xs text-muted-foreground">Gasto estimado com combustível</p>
                  <p className="font-semibold text-primary">{originalFuelCost ? `${formatCurrency(originalFuelCost.minimumFuelCost)} a ${formatCurrency(originalFuelCost.maximumFuelCost)}` : "Cadastre o preço do diesel em Veículos"}</p>
                  {dieselPricePerLiter !== null && <p className="text-xs text-muted-foreground">Calculado com Diesel S10 a {formatCurrency(dieselPricePerLiter)} por litro, cadastrado na aba Veículos.</p>}
                </div>
              </section>

            </div>
          )}

          {!detailsLoading && !selectedRoute && (
            <p className="text-sm text-muted-foreground">Não foi possível carregar os detalhes desta rota.</p>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={optimizedDetailsOpen} onOpenChange={setOptimizedDetailsOpen}>
        <DialogContent className="max-h-[90vh] max-w-6xl overflow-y-auto border-border bg-surface">
          <DialogHeader>
            <DialogTitle>Detalhes da rota otimizada</DialogTitle>
            <DialogDescription>Percurso simulado e alterações propostas em relação às rotas originais.</DialogDescription>
          </DialogHeader>
          {selectedOptimizedRoute && (
            <div className="space-y-4">
              <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
                <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Rota</p><p className="font-medium">{selectedOptimizedRoute.name}</p></div>
                <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Caminhão utilizado</p><p className="font-medium">{selectedOptimizedRoute.vehicleType} · {selectedOptimizedRoute.isRental ? "Alugado" : "Próprio"}</p></div>
                <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Carga</p><p className="font-medium">{formatRouteLoadKg(selectedOptimizedRoute.loadKg)} de {formatCapacityKg(selectedOptimizedRoute.capacityKg)}</p></div>
                <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Quilômetros rodados</p><p className="font-medium">{(selectedOptimizedDistanceMeters / 1000).toLocaleString("pt-BR", { maximumFractionDigits: 1 })} km</p></div>
                <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Tempo para completar a rota</p><p className="font-medium">{formatRouteDuration(selectedOptimizedDurationSeconds)}</p></div>
                <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Combustível estimado</p><p className="font-medium">{selectedOptimizedFuel ? formatFuelConsumptionRange(selectedOptimizedFuel) : "Consumo não cadastrado"}</p>{selectedOptimizedFuel && <p className="text-xs text-muted-foreground">Referência: {selectedOptimizedFuel.minimumEfficiencyKmPerLiter.toLocaleString("pt-BR")} a {selectedOptimizedFuel.maximumEfficiencyKmPerLiter.toLocaleString("pt-BR")} km/L</p>}</div>
                <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Tanque e autonomia</p>{typeof selectedOptimizedRoute.tankCapacityLiters === "number" && typeof selectedOptimizedRoute.tankUsagePercent === "number" && typeof selectedOptimizedRoute.remainingAutonomyKm === "number" ? <><p className="font-medium">{selectedOptimizedRoute.tankCapacityLiters.toLocaleString("pt-BR")} L · {selectedOptimizedRoute.tankUsagePercent.toLocaleString("pt-BR", { maximumFractionDigits: 1 })}% utilizado</p><p className="text-xs text-muted-foreground">Autonomia restante com reserva: {selectedOptimizedRoute.remainingAutonomyKm.toLocaleString("pt-BR", { maximumFractionDigits: 1 })} km</p></> : <p className="font-medium">Execute uma nova otimização</p>}</div>
                {selectedOptimizedRoute.isRental && <div className="rounded-lg border p-3"><p className="text-xs text-muted-foreground">Diária estimada do aluguel</p><p className="font-medium">{typeof selectedOptimizedRoute.rentalDailyCostMinimum === "number" && typeof selectedOptimizedRoute.rentalDailyCostMaximum === "number" ? `${formatCurrency(selectedOptimizedRoute.rentalDailyCostMinimum)} a ${formatCurrency(selectedOptimizedRoute.rentalDailyCostMaximum)}` : "Execute uma nova otimização"}</p><p className="text-xs text-muted-foreground">Veículo dimensionado pelo peso remanejado</p></div>}
                <RouteOccupancyIndicator value={selectedOptimizedRoute.occupancy} className="sm:col-span-2 lg:col-span-3" />
              </div>

              <div className="space-y-3 rounded-lg border border-border p-3">
                <div><p className="font-medium">Novo percurso</p><p className="text-xs text-muted-foreground">Sequência atualizada sobre ruas e rodovias, com saída e retorno à Matriz Grespan.</p></div>
                {optimizedRoadPathLoading && <div className="h-[340px] animate-pulse rounded-lg bg-muted" />}
                {!optimizedRoadPathLoading && optimizedRoadPathError && <p className="text-sm text-destructive">{optimizedRoadPathError}</p>}
                {!optimizedRoadPathLoading && optimizedRoadPath && <><RouteRoadMap route={optimizedRoadPath} /><div className="flex flex-wrap gap-x-5 gap-y-1 text-xs text-muted-foreground"><span>{(optimizedRoadPath.distanceMeters / 1000).toLocaleString("pt-BR", { maximumFractionDigits: 1 })} km</span><span>{Math.round(optimizedRoadPath.durationSeconds / 60).toLocaleString("pt-BR")} min estimados</span><span>{Math.max(0, optimizedRoadPath.stops.length - 2)} cliente(s)</span></div><div className="divide-y divide-border rounded-lg border border-border">{optimizedCustomerGroups.map((group, index) => <details key={group.municipalityId} className="group"><summary className="flex cursor-pointer list-none items-center justify-between gap-3 px-3 py-2.5 text-sm hover:bg-muted/40"><span className="font-medium">{index + 1}. {group.municipalityName}</span><span className="text-xs text-muted-foreground">{group.customers.length} cliente(s)</span></summary><div className="border-t border-border bg-muted/20 px-3 py-2">{group.customers.length > 0 ? <ul className="space-y-1.5">{group.customers.map(customer => <li key={customer.id} className="text-sm text-muted-foreground">{customer.label}</li>)}</ul> : <p className="text-sm text-muted-foreground">Nenhum cliente ativo com posição exata cadastrada.</p>}</div></details>)}</div></>}
              </div>

              <div className="rounded-lg border border-border">
                <div className="border-b px-3 py-2"><p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">O que mudou</p></div>
                <div className="divide-y divide-border">
                  {selectedOptimizedRoute.isRental && <div className="px-3 py-2.5 text-sm"><p className="font-medium">Transporte adicional</p><p className="text-xs text-muted-foreground">Foi sugerida a locação de um {selectedOptimizedRoute.vehicleType} dimensionado pelo peso remanejado e pelo menor custo total.</p></div>}
                  {!selectedOptimizedRoute.isRental && <div className="px-3 py-2.5 text-sm"><p className="font-medium">Veículo próprio reaproveitado</p><p className="text-xs text-muted-foreground">A rota utiliza o veículo da rota original e recebe uma nova sequência de cidades.</p></div>}
                  {selectedOptimizedRoute.stops.map((stop, index) => {
                    const remanejado = selectedOptimizedRoute.originalRouteId !== stop.originalRouteId;
                    return <div key={`${stop.municipalityId}-${index}`} className="flex items-center justify-between gap-3 px-3 py-2.5 text-sm"><div className="flex items-center gap-3"><span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-muted text-xs">{index + 1}</span><div><p className="font-medium">{stop.municipalityName}</p><p className="text-xs text-muted-foreground">{selectedOptimizedRoute.isRental ? "Alocada no novo transporte alugado" : remanejado ? "Remanejada de outra rota original" : "Mantida neste veículo com sequência otimizada"}</p></div></div><div className="text-right text-xs text-muted-foreground"><p>{stop.deliveries} entrega(s)</p><p>{formatRouteLoadKg(stop.loadKg)}</p></div></div>;
                  })}
                </div>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

    </div>
  );
}
