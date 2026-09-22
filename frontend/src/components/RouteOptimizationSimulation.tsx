import { useCallback, useEffect, useState } from "react";
import {
  AlertTriangle,
  ArrowRight,
  CircleMinus,
  CirclePlus,
  Clock3,
  MapPin,
  PackageCheck,
  Route as RouteIcon,
  Sparkles,
  Truck,
} from "lucide-react";
import type { ReactNode } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { SkeletonList } from "@/components/ui/skeleton";
import { RouteOccupancyIndicator } from "@/components/RouteOccupancyIndicator";
import { RouteOptimizationRemediationDialog } from "@/components/RouteOptimizationRemediationDialog";
import { RouteRoadMap } from "@/components/RouteRoadMap";
import { ConsolidatedRouteTollKpi } from "@/components/ConsolidatedRouteTollKpi";
import {
  fetchDailyRouteCosts,
  fetchRouteOptimizationDetail,
  fetchOptimizedVehicleRoadPath,
  fetchRouteOptimizations,
  simulateRoutes,
  type RouteCostItem,
  type RouteOptimizationDetail,
  type RouteOptimizationsResponse,
  type RouteOptimizationSummary,
  type RouteRoadPath,
} from "@/lib/importer-api";
import { isJobExecutionActive, JOB_STATUS_POLL_INTERVAL_MS } from "@/lib/job-runtime";
import { formatCapacityKg, formatOccupancy, formatRouteLoadKg } from "@/lib/route-occupancy";
import { formatRouteDuration } from "@/lib/route-fuel-consumption";
import {
  dailyFleetMovement,
  dailyRouteComparison,
  formatSignedPercentage,
  suggestedRouteName,
  type SuggestedVehicle,
} from "@/lib/route-optimization-view-model";

const weekdayLabels: Record<string, string> = {
  MONDAY: "Segunda",
  TUESDAY: "Terça",
  WEDNESDAY: "Quarta",
  THURSDAY: "Quinta",
  FRIDAY: "Sexta",
  SATURDAY: "Sábado",
  SUNDAY: "Domingo",
};
const statusLabels: Record<string, string> = {
  Optimized: "Otimizada",
  NoImprovement: "Sem melhoria",
  Infeasible: "Inviável",
  InsufficientData: "Dados insuficientes",
};

type SelectedSuggestion = {
  summary: RouteOptimizationSummary;
  vehicle: SuggestedVehicle;
  name: string;
};

const formatDistance = (value: number) =>
  `${(value / 1_000).toLocaleString("pt-BR", { maximumFractionDigits: 1 })} km`;
const formatSegmentDistance = (value: number) => {
  if (value < 1_000) return `${Math.round(value).toLocaleString("pt-BR")} m`;
  return formatDistance(value);
};
const formatDuration = (value: number) => formatRouteDuration(value);
const formatCurrencyRange = (minimum: number | null, maximum: number | null): string => {
  if (minimum === null || maximum === null) return "Indisponível";
  const format = (value: number) => value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
  return minimum === maximum ? format(minimum) : `${format(minimum)} a ${format(maximum)}`;
};

function formatCustomerAddress(address: SuggestedVehicle["stops"][number]["customerAddress"]): string {
  if (!address) return "Endereço não informado";
  const street = [address.streetType, address.street].filter(Boolean).join(" ");
  const locality = [address.neighborhood, address.city, address.stateCode].filter(Boolean).join(" · ");
  return [street && [street, address.number].filter(Boolean).join(", "), locality].filter(Boolean).join(" — ") || "Endereço não informado";
}

export function RouteOptimizationSimulation({
  date,
  weekday,
  canSimulate,
  canResolveIssues,
}: {
  date: string;
  weekday?: string;
  canSimulate: boolean;
  canResolveIssues: boolean;
}) {
  const [response, setResponse] = useState<RouteOptimizationsResponse | null>(null);
  const [details, setDetails] = useState<Record<string, RouteOptimizationDetail>>({});
  const [dailyCosts, setDailyCosts] = useState<Awaited<ReturnType<typeof fetchDailyRouteCosts>> | null>(null);
  const [costError, setCostError] = useState<string | null>(null);
  const [selected, setSelected] = useState<SelectedSuggestion | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [simulating, setSimulating] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [remediation, setRemediation] = useState<RouteOptimizationSummary | null>(null);
  const [selectedRoadPath, setSelectedRoadPath] = useState<RouteRoadPath | null>(null);
  const [selectedRoadPathLoading, setSelectedRoadPathLoading] = useState(false);
  const [selectedRoadPathError, setSelectedRoadPathError] = useState<string | null>(null);

  const load = useCallback(
    async (showLoading: boolean) => {
      if (showLoading) setLoading(true);
      setError(null);
      setCostError(null);
      try {
        const [optimizationResult, costResult] = await Promise.allSettled([
          fetchRouteOptimizations(date, weekday),
          fetchDailyRouteCosts({ date, weekday }),
        ]);
        if (optimizationResult.status === "rejected") throw optimizationResult.reason;
        const data = optimizationResult.value;
        setResponse(data);
        if (costResult.status === "fulfilled") {
          setDailyCosts(costResult.value);
        } else {
          setDailyCosts(null);
          setCostError((costResult.reason as Error).message);
        }
        const validSuggestionIds = new Set(
          data.items
            .filter((item) => item.status === "Optimized" && !item.isStale)
            .map((item) => item.id),
        );
        setSelected((current) =>
          current && validSuggestionIds.has(current.summary.id) ? current : null,
        );
        const settledDetails = await Promise.allSettled(
          data.items
            .filter((item) => item.status === "Optimized" && !item.isStale)
            .map((item) => fetchRouteOptimizationDetail(item.id)),
        );
        const loadedDetails: Record<string, RouteOptimizationDetail> = {};
        for (const settled of settledDetails) {
          if (settled.status === "fulfilled") loadedDetails[settled.value.id] = settled.value;
        }
        setDetails(loadedDetails);
        if (settledDetails.some((item) => item.status === "rejected")) {
          setError(
            "Parte dos detalhes sugeridos não pôde ser carregada. Tente atualizar novamente.",
          );
        }
      } catch (reason) {
        setError((reason as Error).message);
      } finally {
        if (showLoading) setLoading(false);
      }
    },
    [date, weekday],
  );

  useEffect(() => {
    setResponse(null);
    setDetails({});
    setDailyCosts(null);
    setCostError(null);
    setSelected(null);
    setSelectedRoadPath(null);
    setMessage(null);
    void load(true);
  }, [load]);

  useEffect(() => {
    if (!selected) return;
    setSelectedRoadPath(null);
    setSelectedRoadPathError(null);
    setSelectedRoadPathLoading(true);
    void fetchOptimizedVehicleRoadPath(selected.summary.id, selected.vehicle.id)
      .then(setSelectedRoadPath)
      .catch((reason: Error) => setSelectedRoadPathError(reason.message))
      .finally(() => setSelectedRoadPathLoading(false));
  }, [selected]);

  const executionIsActive = isJobExecutionActive(response?.latestExecution?.status);
  useEffect(() => {
    if (!executionIsActive) return;
    const pollingId = window.setInterval(() => void load(false), JOB_STATUS_POLL_INTERVAL_MS);
    return () => window.clearInterval(pollingId);
  }, [executionIsActive, load]);

  async function simulate() {
    setSimulating(true);
    setError(null);
    setMessage(null);
    try {
      await simulateRoutes();
      setMessage(
        "Simulação enviada. O resultado será atualizado automaticamente após a conclusão.",
      );
      await load(false);
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setSimulating(false);
    }
  }

  const latestExecution = response?.latestExecution;
  const canRecalculate = canSimulate && response?.isCurrentSnapshot === true;

  return (
    <div className="space-y-4">
      <Card className="border-border bg-surface">
        <CardHeader className="flex flex-row items-center justify-between gap-3">
          <div>
            <CardTitle>Sugestões de rotas</CardTitle>
            <p className="mt-1 text-xs text-muted-foreground">
              Redistribuição por blocos municipais, sem alterar as rotas publicadas.
            </p>
          </div>
          {canSimulate && (
            <Button
              onClick={() => void simulate()}
              disabled={!canRecalculate || simulating || executionIsActive}
            >
              {simulating
                ? "Enviando..."
                : executionIsActive
                  ? "Processando..."
                  : "Recalcular sugestões"}
            </Button>
          )}
        </CardHeader>
        <CardContent className="space-y-3">
          {loading && <SkeletonList rows={3} />}
          {!loading && response && !response.isCurrentSnapshot && (
            <p className="rounded-lg border border-border bg-muted/40 p-3 text-sm">
              Snapshot histórico: as sugestões podem ser consultadas, mas o recálculo está
              disponível somente para o snapshot atual.
            </p>
          )}
          {message && (
            <p className="rounded-lg border border-border bg-muted/40 p-3 text-sm">{message}</p>
          )}
          {latestExecution && executionIsActive && (
            <p className="flex items-center gap-2 rounded-lg border border-border p-3 text-sm">
              <Clock3 className="size-4" />
              {latestExecution.progressMessage ?? "Processando sugestões"} ·{" "}
              {latestExecution.progressPercent.toLocaleString("pt-BR", {
                maximumFractionDigits: 1,
              })}
              %
            </p>
          )}
          {latestExecution?.status === "Failed" && (
            <p className="flex gap-2 rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
              <AlertTriangle className="size-4 shrink-0" />
              <span>
                {latestExecution.errorMessage ?? "A nova simulação falhou."}
                {response.items.length > 0 && " A última sugestão válida permanece exibida."}
              </span>
            </p>
          )}
          {error && (
            <div className="space-y-2 rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
              <p>{error}</p>
              <Button size="sm" variant="outline" onClick={() => void load(true)}>
                Tentar novamente
              </Button>
            </div>
          )}
          {!loading && response?.items.length === 0 && (
            <p className="text-sm text-muted-foreground">
              Ainda não existe sugestão persistida para este snapshot.
            </p>
          )}
        </CardContent>
      </Card>

      {response?.items.map((summary) => {
        const detail = details[summary.id];
        const comparison = dailyRouteComparison(summary);
        return (
          <Card key={summary.id} className="border-border bg-surface">
            <CardHeader className="space-y-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <CardTitle>{weekdayLabels[summary.weekday] ?? summary.weekday}</CardTitle>
                <div className="flex items-center gap-2">
                  <Badge variant="outline">{statusLabels[summary.status] ?? summary.status}</Badge>
                  {summary.isStale && <Badge variant="destructive">Coordenadas atualizadas</Badge>}
                  {summary.isInherited && <Badge variant="outline">Mantido da versão anterior</Badge>}
                  <span className="text-xs text-muted-foreground">
                    {new Date(summary.createdAt).toLocaleString("pt-BR")}
                  </span>
                </div>
              </div>
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
                <OptimizationComparisonCard
                  icon={<RouteIcon className="size-4" />}
                  label="Distância"
                  current={formatDistance(comparison.distance.current)}
                  proposed={
                    summary.status === "Optimized"
                      ? formatDistance(comparison.distance.proposed)
                      : "rota real mantida"
                  }
                  impact={
                    summary.status === "Optimized"
                      ? formatSignedPercentage(comparison.distance.changePercent)
                      : undefined
                  }
                  tone="blue"
                />
                <OptimizationComparisonCard
                  icon={<Clock3 className="size-4" />}
                  label="Duração"
                  current={formatDuration(comparison.duration.current)}
                  proposed={
                    summary.status === "Optimized"
                      ? formatDuration(comparison.duration.proposed)
                      : "rota real mantida"
                  }
                  impact={
                    summary.status === "Optimized"
                      ? formatSignedPercentage(comparison.duration.changePercent)
                      : undefined
                  }
                  tone="violet"
                />
                <OptimizationComparisonCard
                  icon={<Truck className="size-4" />}
                  label="Veículos em rota"
                  current={`${comparison.vehicles.current}`}
                  proposed={
                    summary.status === "Optimized"
                      ? `${comparison.vehicles.proposed}`
                      : "rota real mantida"
                  }
                  impact={
                    summary.status === "Optimized"
                      ? `${comparison.vehicles.change > 0 ? "+" : ""}${comparison.vehicles.change} ${Math.abs(comparison.vehicles.change) === 1 ? "veículo" : "veículos"}`
                      : undefined
                  }
                  tone="emerald"
                />
                <OptimizationMetricCard
                  icon={<CirclePlus className="size-4" />}
                  label="Capacidade introduzida"
                  value={formatCapacityKg(comparison.additionalCapacityKg)}
                  supportingText={`${comparison.additionalVehicleCount} ${comparison.additionalVehicleCount === 1 ? "veículo introduzido" : "veículos introduzidos"}`}
                  tone="primary"
                />
                <OptimizationMetricCard
                  icon={<PackageCheck className="size-4" />}
                  label="Carga total"
                  value={formatRouteLoadKg(comparison.totalWeightKg)}
                  supportingText="Carga integral preservada"
                  tone="amber"
                />
              </div>
              {summary.status === "Optimized" && detail && (
                <FleetMovementSummary vehicles={detail.vehicles} />
              )}
            </CardHeader>
            <CardContent className="space-y-3">
              {summary.isStale && (
                <div className="flex items-center gap-2 rounded-lg border border-amber-500/30 bg-amber-500/5 p-3 text-sm text-amber-700 dark:text-amber-300">
                  <AlertTriangle className="size-4 shrink-0" />
                  <span>
                    Esta sugestão foi calculada antes da atualização das coordenadas e não é mais válida.
                    Recalcule as rotas para obter distância, tempo, ordem e custos atuais.
                  </span>
                </div>
              )}
              {summary.reason && !summary.isStale && (
                <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border p-3 text-sm">
                  <p className="flex min-w-0 gap-2"><AlertTriangle className="size-4 shrink-0" /><span>{summary.reason}</span></p>
                  {summary.status === "InsufficientData" && (
                    <Button size="sm" variant="outline" onClick={() => setRemediation(summary)}>
                      {canResolveIssues && response.isCurrentSnapshot ? "Resolver pendências" : "Ver pendências"}
                      {summary.issueCount > 0 && ` (${summary.issueCount})`}
                    </Button>
                  )}
                </div>
              )}
              {summary.status === "Optimized" && !summary.isStale && !detail && <SkeletonList rows={2} />}
              {detail?.vehicles.map((vehicle) => {
                const name = suggestedRouteName(vehicle, detail.vehicles);
                return (
                  <button
                    type="button"
                    key={vehicle.id}
                    onClick={() => setSelected({ summary, vehicle, name })}
                    className="w-full rounded-lg border border-border/80 p-3 text-left transition-all duration-200 hover:border-border hover:bg-white/[0.03]"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex min-w-0 items-center gap-2">
                        <MapPin className="size-4 shrink-0 text-muted-foreground" />
                        <p className="truncate text-sm font-medium">{name}</p>
                      </div>
                      <div className="flex flex-wrap justify-end gap-2">
                        {vehicle.isAdditional && <Badge>Adicional</Badge>}
                        {vehicle.isIdle && <Badge variant="outline">Ocioso</Badge>}
                      </div>
                    </div>
                    <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
                      <span className="flex items-center gap-1">
                        <Truck className="size-3" />
                        {vehicle.vehicleType}
                      </span>
                      <span>{vehicle.deliveryCount} cliente(s) · {vehicle.municipalityCount} cidade(s)</span>
                      <span>
                        {formatRouteLoadKg(vehicle.loadKg)} / {formatCapacityKg(vehicle.capacityKg)}
                      </span>
                    </div>
                    <RouteOccupancyIndicator value={vehicle.occupancy} compact />
                  </button>
                );
              })}
            </CardContent>
          </Card>
        );
      })}

      <Dialog
        open={selected !== null}
        onOpenChange={(open) => {
          if (!open) setSelected(null);
        }}
      >
        <DialogContent className="custom-scrollbar max-h-[90vh] w-[96vw] max-w-6xl overflow-x-hidden overflow-y-auto border-border bg-surface">
          <DialogHeader>
            <DialogTitle>{selected?.name ?? "Detalhes da sugestão"}</DialogTitle>
            <DialogDescription>Sequência dos clientes, percurso e custos consolidados da rota sugerida.</DialogDescription>
          </DialogHeader>
          {selected && (
            <SuggestionDetail
              selected={selected}
              cost={dailyCosts?.optimized?.items.find(
                (item) => item.optimizationVehicleId === selected.vehicle.id,
              ) ?? null}
              dieselPricePerLiter={dailyCosts?.dieselPricePerLiter ?? null}
              costError={costError}
              roadPath={selectedRoadPath}
              roadPathLoading={selectedRoadPathLoading}
              roadPathError={selectedRoadPathError}
            />
          )}
        </DialogContent>
      </Dialog>
      <RouteOptimizationRemediationDialog
        summary={remediation}
        open={remediation !== null}
        onOpenChange={(open) => { if (!open) setRemediation(null); }}
        onCompleted={() => void load(false)}
      />
    </div>
  );
}

function SuggestionDetail({
  selected,
  cost,
  dieselPricePerLiter,
  costError,
  roadPath,
  roadPathLoading,
  roadPathError,
}: {
  selected: SelectedSuggestion;
  cost: RouteCostItem | null;
  dieselPricePerLiter: number | null;
  costError: string | null;
  roadPath: RouteRoadPath | null;
  roadPathLoading: boolean;
  roadPathError: string | null;
}) {
  const { summary, vehicle } = selected;
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-2">
        <Badge variant="outline">{weekdayLabels[summary.weekday] ?? summary.weekday}</Badge>
        {vehicle.isAdditional && <Badge>Veículo adicional</Badge>}
        {vehicle.isIdle && <Badge variant="outline">Veículo ocioso</Badge>}
      </div>
      <div className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
        <Metric label="Veículo" value={vehicle.vehicleType} />
        <Metric
          label="Carga / capacidade"
          value={`${formatRouteLoadKg(vehicle.loadKg)} / ${formatCapacityKg(vehicle.capacityKg)}`}
        />
        <Metric label="Ocupação" value={formatOccupancy(vehicle.occupancy)} />
        <Metric label="Clientes" value={`${vehicle.deliveryCount}`} />
        <Metric label="Cidades" value={`${vehicle.municipalityCount}`} />
      </div>
      <div className="grid min-w-0 grid-cols-1 gap-3 text-sm lg:grid-cols-3">
        <div className="route-kpi-grid grid min-w-0 grid-cols-1 gap-3 sm:grid-cols-2 lg:col-span-2 lg:h-full lg:grid-rows-2">
          <div className="route-kpi-card rounded-xl border border-border/80 bg-background/30 p-4 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-primary">Quilometragem</p>
            <p className="mt-2 text-xl font-display font-semibold">
              {cost?.distanceMeters == null ? formatDistance(vehicle.distanceMeters) : formatDistance(cost.distanceMeters)}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">Percurso rodoviário consolidado</p>
          </div>
          <div className="route-kpi-card rounded-xl border border-border/80 bg-background/30 p-4 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-primary">Tempo para concluir</p>
            <p className="mt-2 text-xl font-display font-semibold">
              {cost?.durationSeconds == null ? formatDuration(vehicle.durationSeconds) : formatDuration(cost.durationSeconds)}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">Tempo rodoviário consolidado</p>
          </div>
          <div className="route-kpi-card rounded-xl border border-border/80 bg-background/30 p-4 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-primary">Gasto estimado com combustível</p>
            <p className="mt-2 text-lg font-display font-semibold">
              {formatCurrencyRange(cost?.minimumFuelCost ?? null, cost?.maximumFuelCost ?? null)}
            </p>
            <p className="mt-2 text-xs text-muted-foreground">
              {cost?.isAvailable
                ? `${cost.minimumFuelLiters?.toLocaleString("pt-BR")} a ${cost.maximumFuelLiters?.toLocaleString("pt-BR")} L · ${vehicle.vehicleType}`
                : cost?.unavailableReason ?? costError ?? "Consolidação de custo indisponível."}
            </p>
            {dieselPricePerLiter != null && (
              <p className="mt-1 text-xs text-muted-foreground">
                Diesel de referência: {dieselPricePerLiter.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}/L
              </p>
            )}
          </div>
          <ConsolidatedRouteTollKpi cost={cost} />
        </div>
        <div className="route-kpi-card border-primary/30 bg-primary/5 p-4 shadow-sm lg:h-auto lg:self-start lg:aspect-square">
          <p className="text-xs font-semibold uppercase tracking-wider text-primary">Gastos totais</p>
          <p className="mt-2 min-w-0 break-words text-base font-display font-semibold">
            {formatCurrencyRange(cost?.minimumTotalCost ?? null, cost?.maximumTotalCost ?? null)}
          </p>
          <p className="mt-2 text-xs text-muted-foreground">Combustível + pedágio</p>
        </div>
      </div>
      <div className="space-y-2 rounded-lg border border-border p-3">
        <div>
          <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">Mapa da rota otimizada</p>
          <p className="mt-1 text-sm text-muted-foreground">Percurso rodoviário do veículo, com origem, clientes e retorno ao depósito.</p>
        </div>
        {roadPathLoading && <div className="h-[340px] animate-pulse rounded-lg bg-muted" aria-label="Carregando mapa da rota otimizada" />}
        {!roadPathLoading && roadPathError && <p className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{roadPathError}</p>}
        {!roadPathLoading && roadPath && <RouteRoadMap route={roadPath} />}
      </div>
      <div className="rounded-lg border border-border">
        <div className="border-b border-border px-3 py-3">
          <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
            Sequência dos clientes
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            Distância e tempo mostrados em cada linha representam somente o trecho desde a parada anterior. Na primeira entrega, a referência é a Matriz; o total da rota aparece nos indicadores acima. Um trecho de 0 m / 0 min indica que os pontos foram considerados na mesma localização.
          </p>
        </div>
        {vehicle.stops.length === 0 ? (
          <p className="p-3 text-sm text-muted-foreground">
            Este veículo ficou ocioso na sugestão.
          </p>
        ) : (
          <div className="divide-y divide-border">
            {vehicle.stops.map((stop, index) => (
              <div
                key={stop.id}
                className="flex items-center justify-between gap-3 px-3 py-2.5 text-sm"
              >
                <div className="flex items-center gap-3">
                  <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-muted text-xs text-muted-foreground">
                    {index + 1}
                  </span>
                  <div>
                    <p className="font-medium">{stop.customerName ?? `Cliente ${stop.customerCode ?? "não identificado"}`}</p>
                    <p className="text-xs text-muted-foreground">
                      Cliente {stop.customerCode ?? "não identificado"} · {stop.municipality}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {formatCustomerAddress(stop.customerAddress)}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {formatRouteLoadKg(stop.weightKg)}
                    </p>
                  </div>
                </div>
                <div className="min-w-[104px] text-right text-xs text-muted-foreground">
                  <p className="mb-1 text-[10px] font-medium uppercase tracking-wider text-muted-foreground/70">
                    Trecho anterior
                  </p>
                  <p className="flex items-center justify-end gap-1">
                    <RouteIcon className="size-3" />
                    {formatSegmentDistance(stop.distanceFromPreviousMeters)}
                  </p>
                  <p>{formatDuration(stop.durationFromPreviousSeconds)}</p>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

type OptimizationMetricTone = "blue" | "violet" | "emerald" | "primary" | "amber";

const optimizationMetricToneClasses: Record<
  OptimizationMetricTone,
  { container: string; icon: string; impact: string }
> = {
  blue: {
    container: "border-blue-500/25 bg-gradient-to-br from-blue-500/[0.10] to-surface",
    icon: "bg-blue-500/15 text-blue-500",
    impact: "border-blue-500/25 bg-blue-500/10 text-blue-500",
  },
  violet: {
    container: "border-violet-500/25 bg-gradient-to-br from-violet-500/[0.10] to-surface",
    icon: "bg-violet-500/15 text-violet-500",
    impact: "border-violet-500/25 bg-violet-500/10 text-violet-500",
  },
  emerald: {
    container: "border-emerald-500/25 bg-gradient-to-br from-emerald-500/[0.10] to-surface",
    icon: "bg-emerald-500/15 text-emerald-500",
    impact: "border-emerald-500/25 bg-emerald-500/10 text-emerald-500",
  },
  primary: {
    container: "border-primary/25 bg-gradient-to-br from-primary/[0.10] to-surface",
    icon: "bg-primary/15 text-primary",
    impact: "border-primary/25 bg-primary/10 text-primary",
  },
  amber: {
    container: "border-amber-500/25 bg-gradient-to-br from-amber-500/[0.10] to-surface",
    icon: "bg-amber-500/15 text-amber-500",
    impact: "border-amber-500/25 bg-amber-500/10 text-amber-500",
  },
};

function OptimizationComparisonCard({
  icon,
  label,
  current,
  proposed,
  impact,
  tone,
}: {
  icon: ReactNode;
  label: string;
  current: string;
  proposed: string;
  impact?: string;
  tone: OptimizationMetricTone;
}) {
  const colors = optimizationMetricToneClasses[tone];
  return (
    <div className={`relative overflow-hidden rounded-xl border p-3.5 ${colors.container}`}>
      <div className="flex items-start justify-between gap-2">
        <div className="flex items-center gap-2">
          <span className={`flex size-8 shrink-0 items-center justify-center rounded-lg ${colors.icon}`}>
            {icon}
          </span>
          <p className="text-xs font-medium text-muted-foreground">{label}</p>
        </div>
        {impact && (
          <Badge variant="outline" className={`px-1.5 py-0 text-[11px] ${colors.impact}`}>
            {impact}
          </Badge>
        )}
      </div>
      <div className="mt-3 flex min-w-0 items-center gap-2">
        <span className="truncate text-sm text-muted-foreground">{current}</span>
        <ArrowRight className="size-4 shrink-0 text-muted-foreground" />
        <span className="truncate text-lg font-semibold tracking-tight">{proposed}</span>
      </div>
    </div>
  );
}

function OptimizationMetricCard({
  icon,
  label,
  value,
  supportingText,
  tone,
}: {
  icon: ReactNode;
  label: string;
  value: string;
  supportingText: string;
  tone: OptimizationMetricTone;
}) {
  const colors = optimizationMetricToneClasses[tone];
  return (
    <div className={`relative overflow-hidden rounded-xl border p-3.5 ${colors.container}`}>
      <div className="flex items-center gap-2">
        <span className={`flex size-8 shrink-0 items-center justify-center rounded-lg ${colors.icon}`}>
          {icon}
        </span>
        <p className="text-xs font-medium text-muted-foreground">{label}</p>
      </div>
      <p className="mt-3 truncate text-lg font-semibold tracking-tight">{value}</p>
      <p className="mt-0.5 truncate text-xs text-muted-foreground">{supportingText}</p>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="font-medium">{value}</p>
    </div>
  );
}

function FleetMovementSummary({ vehicles }: { vehicles: readonly SuggestedVehicle[] }) {
  const movement = dailyFleetMovement(vehicles);
  const hasMovement =
    movement.releasedVehicleCount > 0 || movement.introducedVehicleCount > 0;
  const vehicleBalanceLabel = `${movement.netVehicleCount > 0 ? "+" : ""}${movement.netVehicleCount} ${
    Math.abs(movement.netVehicleCount) === 1 ? "veículo" : "veículos"
  }`;
  const capacityBalanceLabel = `${movement.netCapacityKg > 0 ? "+" : ""}${formatCapacityKg(
    movement.netCapacityKg,
  )}`;

  if (!hasMovement) {
    return (
      <div className="flex items-center gap-3 rounded-xl border border-border bg-muted/20 p-4">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-muted text-muted-foreground">
          <Truck className="size-5" />
        </span>
        <div>
          <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
            Movimentação da frota
          </p>
          <p className="mt-0.5 text-sm font-medium">Nenhuma substituição de veículo.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="relative overflow-hidden rounded-xl border border-primary/25 bg-gradient-to-br from-primary/[0.10] via-surface to-blue-500/[0.07] p-4 shadow-sm">
      <div className="pointer-events-none absolute -right-16 -top-20 size-48 rounded-full bg-primary/10 blur-3xl" />
      <div className="relative">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <span className="flex size-8 items-center justify-center rounded-lg bg-primary/15 text-primary">
              <Truck className="size-4" />
            </span>
            <div>
              <p className="text-sm font-semibold">Movimentação da frota</p>
              <p className="text-xs text-muted-foreground">Veículos redistribuídos nesta sugestão</p>
            </div>
          </div>
          <Badge variant="outline" className="border-primary/30 bg-primary/10 text-primary">
            Substituição otimizada
          </Badge>
        </div>

        <div className="grid items-stretch gap-2 md:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)_auto_minmax(0,1fr)]">
          <div className="rounded-lg border border-blue-500/25 bg-blue-500/[0.08] p-3">
            <div className="flex items-center gap-2 text-blue-500">
              <CircleMinus className="size-4" />
              <span className="text-xs font-semibold uppercase tracking-wider">Liberados</span>
            </div>
            <p className="mt-2 text-2xl font-semibold tracking-tight">
              {movement.releasedVehicleCount}
              <span className="ml-1.5 text-sm font-medium text-muted-foreground">
                {movement.releasedVehicleCount === 1 ? "veículo" : "veículos"}
              </span>
            </p>
            <p className="text-xs text-muted-foreground">
              {formatCapacityKg(movement.releasedCapacityKg)} de capacidade
            </p>
            <FleetTypeBadges groups={movement.releasedGroups} />
          </div>

          <ArrowRight className="m-auto size-5 rotate-90 text-muted-foreground md:rotate-0" />

          <div className="rounded-lg border border-primary/30 bg-primary/[0.09] p-3">
            <div className="flex items-center gap-2 text-primary">
              <CirclePlus className="size-4" />
              <span className="text-xs font-semibold uppercase tracking-wider">Introduzidos</span>
            </div>
            <p className="mt-2 text-2xl font-semibold tracking-tight">
              {movement.introducedVehicleCount}
              <span className="ml-1.5 text-sm font-medium text-muted-foreground">
                {movement.introducedVehicleCount === 1 ? "veículo" : "veículos"}
              </span>
            </p>
            <p className="text-xs text-muted-foreground">
              {formatCapacityKg(movement.introducedCapacityKg)} de capacidade
            </p>
            <FleetTypeBadges groups={movement.introducedGroups} />
          </div>

          <ArrowRight className="m-auto size-5 rotate-90 text-muted-foreground md:rotate-0" />

          <div className="rounded-lg border border-emerald-500/30 bg-emerald-500/[0.08] p-3">
            <div className="flex items-center gap-2 text-emerald-500">
              <Sparkles className="size-4" />
              <span className="text-xs font-semibold uppercase tracking-wider">Saldo</span>
            </div>
            <p className="mt-2 text-2xl font-semibold tracking-tight text-emerald-500">
              {vehicleBalanceLabel}
            </p>
            <p className="text-sm font-medium">{capacityBalanceLabel} de capacidade</p>
            <p className="mt-1 text-xs text-muted-foreground">Resultado líquido da substituição</p>
          </div>
        </div>
      </div>
    </div>
  );
}

function FleetTypeBadges({ groups }: { groups: Array<{ vehicleType: string; count: number }> }) {
  if (groups.length === 0) return null;
  return (
    <div className="mt-2 flex flex-wrap gap-1.5">
      {groups.map((group) => (
        <Badge key={group.vehicleType} variant="outline" className="bg-surface/60 text-xs">
          {group.count}× {group.vehicleType}
        </Badge>
      ))}
    </div>
  );
}
