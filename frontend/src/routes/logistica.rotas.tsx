import { useEffect, useState } from "react";
import { Link, createFileRoute } from "@tanstack/react-router";
import { BarChart3, ChevronLeft, ChevronRight, FlaskConical, MapPin, Route as RouteIcon, Search, Truck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SkeletonList, SkeletonModalContent } from "@/components/ui/skeleton";
import { RouteSnapshotDateSelect } from "@/components/RouteSnapshotDateSelect";
import { RouteOccupancyIndicator } from "@/components/RouteOccupancyIndicator";
import { RouteVehicleSimulationDialog } from "@/components/RouteVehicleSimulationDialog";
import {
  fetchImportedRouteDetail,
  fetchImportedRoutes,
  fetchVehicleTypes,
  type ImportedRouteDetail,
  type ImportedRouteItem,
  type VehicleTypeItem,
} from "@/lib/importer-api";
import { formatCapacityKg, formatRouteLoadKg, type OccupancyLevel } from "@/lib/route-occupancy";
import { getCurrentLocalDate } from "@/lib/route-snapshot-history";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";

export const Route = createFileRoute("/logistica/rotas")({ component: LogisticaRotasPage });

const weekdayLabels: Record<string, string> = {
  MONDAY: "Segunda",
  TUESDAY: "Terça",
  WEDNESDAY: "Quarta",
  THURSDAY: "Quinta",
  FRIDAY: "Sexta",
};

const ALL_OCCUPANCY_LEVELS = "all";

function formatDate(value: string): string {
  if (!value) return "-";
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? value : d.toLocaleString("pt-BR");
}

function LogisticaRotasPage() {
  const [routes, setRoutes] = useState<ImportedRouteItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, TEXT_SEARCH_DEBOUNCE_MS);
  const [snapshotDate, setSnapshotDate] = useState(() => getCurrentLocalDate());
  const [occupancyLevel, setOccupancyLevel] = useState<OccupancyLevel | typeof ALL_OCCUPANCY_LEVELS>(ALL_OCCUPANCY_LEVELS);
  const pageSize = 20;

  const [apiError, setApiError] = useState<string | null>(null);
  const [selectedRoute, setSelectedRoute] = useState<ImportedRouteDetail | null>(null);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [simulationOpen, setSimulationOpen] = useState(false);
  const [simulationRoute, setSimulationRoute] = useState<ImportedRouteDetail | null>(null);
  const [simulationVehicleTypes, setSimulationVehicleTypes] = useState<VehicleTypeItem[]>([]);
  const [simulationVehicleTypeId, setSimulationVehicleTypeId] = useState("");
  const [simulationLoading, setSimulationLoading] = useState(false);
  const [simulationError, setSimulationError] = useState<string | null>(null);

  async function load(p: number = page) {
    setLoading(true);
    setApiError(null);
    try {
      const data = await fetchImportedRoutes(p, pageSize, {
        search: debouncedSearch || undefined,
        date: snapshotDate,
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
  }, [debouncedSearch, snapshotDate, occupancyLevel]);

  async function openDetails(route: ImportedRouteItem) {
    setDetailsOpen(true);
    setDetailsLoading(true);
    try {
      const detail = await fetchImportedRouteDetail(route.id);
      setSelectedRoute(detail);
    } catch {
      setSelectedRoute(null);
    } finally {
      setDetailsLoading(false);
    }
  }

  async function openSimulation(route: ImportedRouteItem) {
    setSimulationOpen(true);
    setSimulationLoading(true);
    setSimulationError(null);
    setSimulationRoute(null);
    setSimulationVehicleTypes([]);
    setSimulationVehicleTypeId("");
    try {
      const [detail, vehicleTypes] = await Promise.all([
        fetchImportedRouteDetail(route.id),
        fetchVehicleTypes(),
      ]);
      setSimulationRoute(detail);
      setSimulationVehicleTypes(vehicleTypes);
      const currentVehicle = vehicleTypes.find((vehicle) => vehicle.id === detail.vehicleTypeId);
      const initialVehicle = currentVehicle?.capacityKg
        ? currentVehicle
        : vehicleTypes.find((vehicle) => vehicle.capacityKg !== null && vehicle.capacityKg > 0);
      setSimulationVehicleTypeId(initialVehicle?.id ?? "");
    } catch (error) {
      setSimulationError((error as Error).message || "Não foi possível carregar os dados da simulação.");
    } finally {
      setSimulationLoading(false);
    }
  }

  const pageCount = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <span className="page-header-kicker">Logística / Rotas</span>
            <Badge variant="outline">Importadas</Badge>
          </div>
          <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">Rotas</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Rotas importadas das planilhas com cidades, entregas e tipos de veículo.
          </p>
        </div>
        <nav className="flex flex-wrap gap-2" aria-label="Abas de logística">
          <Button variant="outline" asChild>
            <Link to="/logistica">
              <BarChart3 className="mr-2 size-4" />
              Métricas
            </Link>
          </Button>
          <Button asChild>
            <Link to="/logistica/rotas">
              <RouteIcon className="mr-2 size-4" />
              Rotas
            </Link>
          </Button>
        </nav>
      </header>

      <Card className="border-border bg-surface">
        <CardHeader>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <CardTitle>Todas as rotas</CardTitle>
            <div className="flex w-full flex-col gap-3 sm:w-auto sm:flex-row sm:items-end">
              <RouteSnapshotDateSelect
                value={snapshotDate}
                onValueChange={setSnapshotDate}
              />
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
              {search
                ? "Nenhuma rota encontrada para esta busca."
                : "Nenhuma rota importada. Importe um arquivo XLSX na página de Importações para começar."}
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
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    onClick={() => void openSimulation(r)}
                    aria-label={`Simular veículo para a rota ${r.name}`}
                  >
                    <FlaskConical className="mr-1.5 size-3.5" />
                    Simular
                  </Button>
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
                  <span>Arquivo: {r.importFileName}</span>
                  <span>Importado: {formatDate(r.createdAt)}</span>
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

      <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="max-w-3xl border-border bg-surface max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Detalhes da Rota</DialogTitle>
            <DialogDescription>Cidades, entregas e informações do veículo.</DialogDescription>
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
                          {entry.note && <p className="text-xs text-muted-foreground">{entry.note}</p>}
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

      <RouteVehicleSimulationDialog
        open={simulationOpen}
        onOpenChange={setSimulationOpen}
        route={simulationRoute}
        vehicleTypes={simulationVehicleTypes}
        selectedVehicleTypeId={simulationVehicleTypeId}
        onVehicleTypeChange={setSimulationVehicleTypeId}
        loading={simulationLoading}
        error={simulationError}
      />
    </div>
  );
}
