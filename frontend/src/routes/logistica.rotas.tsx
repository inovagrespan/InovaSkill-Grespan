import { useEffect, useState } from "react";
import { Link, createFileRoute } from "@tanstack/react-router";
import { ArrowRight, BarChart3, CalendarClock, CheckCircle2, ChevronLeft, ChevronRight, FlaskConical, Gauge, MapPin, Route as RouteIcon, Search, Sparkles, Truck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SkeletonList, SkeletonModalContent } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { RouteSnapshotDateSelect } from "@/components/RouteSnapshotDateSelect";
import { RouteOccupancyIndicator } from "@/components/RouteOccupancyIndicator";
import { RouteDecisionSupport } from "@/components/RouteDecisionSupport";
import { askBusinessAssistant } from "@/lib/assistant-api";
import {
  fetchLatestGlobalRouteOptimization,
  fetchImportedRouteDetail,
  fetchImportedRoutes,
  fetchVehicleTypes,
  type ImportedRouteDetail,
  type ImportedRouteItem,
  type RouteOptimizationRun,
  type RouteOptimizationScenario,
  type VehicleTypeItem,
} from "@/lib/importer-api";
import { formatCapacityKg, formatRouteLoadKg, type OccupancyLevel } from "@/lib/route-occupancy";
import { getCurrentUserRole } from "@/lib/auth";
import { canRoleUseRouteSimulation } from "@/lib/access-control";
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
const OCCUPANCY_PERCENT_SCALE = 100;
const OCCUPANCY_BAR_MAX_PERCENT = 160;
const AI_PLAN_EXPLANATION_GROUP_LIMIT = 4;

type AiRoutePlanGroup = {
  routeId: string;
  occupancyAfter: number | null;
  totalLoadKg: number;
  cities: RouteOptimizationScenario["cityReallocations"];
  referenceCityName: string;
};

const aiSuggestionStatusLabels: Record<string, string> = {
  Pending: "Na fila",
  LoadingData: "Carregando dados",
  BuildingProblem: "Montando problema",
  CalculatingDistanceMatrix: "Calculando distâncias",
  SearchingSolutions: "Buscando cenários",
  PersistingResult: "Salvando resultado",
  Completed: "Concluída",
  NoChangeRecommended: "Sem alteração recomendada",
  InsufficientData: "Dados insuficientes",
  NoFeasibleSolution: "Sem solução viável",
  Cancelled: "Cancelada",
  Failed: "Falhou",
};

function formatPercent(value: number | null): string {
  return value == null ? "-" : `${(value * OCCUPANCY_PERCENT_SCALE).toLocaleString("pt-BR", { maximumFractionDigits: 1 })}%`;
}

function recommendedScenario(run: RouteOptimizationRun | null): RouteOptimizationScenario | null {
  if (!run) return null;
  return run.scenarios.find((scenario) => scenario.isRecommended) ?? run.scenarios[0] ?? null;
}

function scenarioTitle(scenario: RouteOptimizationScenario): string {
  if (scenario.actionType === "BuildBalancedRoutePlan") return "Plano principal recomendado";
  if (scenario.actionType === "OptimizeStopSequence") return "Sequência de cidades otimizada";
  if (scenario.actionType === "ReallocateCities") return "Realocação emergencial";
  if (scenario.actionType === "ChangeTruck") return "Troca de caminhão";
  return "Cenário analisado";
}

function RouteSequenceComparison({ scenario }: { scenario: RouteOptimizationScenario }) {
  const sequences = scenario.routeSequences ?? [];
  if (sequences.length === 0) {
    return (
      <div className="rounded-lg border border-border p-4 text-sm text-muted-foreground">
        Não há sequência calculável: são necessárias ao menos duas cidades com coordenadas na mesma rota.
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div>
        <h3 className="text-sm font-semibold text-foreground">Sequência calculada das cidades</h3>
        <p className="text-xs text-muted-foreground">Cálculo combinatório determinístico com matriz viária; a primeira cidade permanece como ponto inicial.</p>
      </div>
      {sequences.map((sequence) => (
        <div key={sequence.routeId} className="rounded-lg border border-border bg-surface-soft/40 p-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <p className="font-medium text-foreground">{sequence.routeName}</p>
              <p className="text-xs text-muted-foreground">{sequence.matrixMethod}</p>
            </div>
            <div className="text-right text-xs">
              <p className="font-medium text-emerald-600">-{sequence.distanceReductionKm.toLocaleString("pt-BR", { maximumFractionDigits: 2 })} km</p>
              <p className="text-muted-foreground">-{sequence.durationReductionMinutes} min</p>
            </div>
          </div>
          <div className="mt-3 grid gap-3 md:grid-cols-2">
            <div className="rounded-md border border-border/70 bg-surface p-3">
              <p className="mb-2 text-xs font-medium text-muted-foreground">Ordem atual · {sequence.currentDistanceKm.toLocaleString("pt-BR")} km</p>
              <ol className="space-y-1 text-sm">
                {sequence.currentStops.map((stop) => <li key={stop.cityId}>{stop.sequence}. {stop.cityName}</li>)}
              </ol>
            </div>
            <div className="rounded-md border border-emerald-500/30 bg-emerald-500/5 p-3">
              <p className="mb-2 text-xs font-medium text-emerald-700">Ordem proposta · {sequence.proposedDistanceKm.toLocaleString("pt-BR")} km</p>
              <ol className="space-y-1 text-sm">
                {sequence.proposedStops.map((stop) => <li key={stop.cityId}>{stop.sequence}. {stop.cityName}</li>)}
              </ol>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

function occupancyBarWidth(value: number | null): string {
  if (value == null) return "0%";
  const percent = Math.max(0, Math.min(value * OCCUPANCY_PERCENT_SCALE, OCCUPANCY_BAR_MAX_PERCENT));
  return `${(percent / OCCUPANCY_BAR_MAX_PERCENT) * OCCUPANCY_PERCENT_SCALE}%`;
}

function OccupancyMiniBar({
  label,
  before,
  after,
  tone,
}: {
  label: string;
  before: number | null;
  after: number | null;
  tone: "source" | "destination";
}) {
  const accent = tone === "source" ? "bg-emerald-500" : "bg-sky-500";
  const beforeAccent = tone === "source" ? "bg-red-500" : "bg-muted-foreground/50";

  return (
    <div className="rounded-lg border border-border/70 bg-surface/60 p-3">
      <div className="flex items-center justify-between gap-3">
        <p className="truncate text-xs font-medium text-foreground">{label}</p>
        <span className="shrink-0 rounded-md bg-muted px-2 py-0.5 text-[11px] text-muted-foreground">
          {formatPercent(before)} → {formatPercent(after)}
        </span>
      </div>
      <div className="mt-3 space-y-2">
        <div className="h-2 rounded-full bg-muted">
          <div className={`h-2 rounded-full ${beforeAccent}`} style={{ width: occupancyBarWidth(before) }} />
        </div>
        <div className="h-2 rounded-full bg-muted">
          <div className={`h-2 rounded-full ${accent}`} style={{ width: occupancyBarWidth(after) }} />
        </div>
      </div>
    </div>
  );
}

function AiScenarioSummary({
  scenario,
  run,
  description,
  tone,
}: {
  scenario: RouteOptimizationScenario;
  run: RouteOptimizationRun;
  description: string;
  tone: "ideal" | "emergency";
}) {
  const afterTone = tone === "ideal"
    ? "border-emerald-500/25 bg-emerald-500/5"
    : "border-amber-500/25 bg-amber-500/5";
  const afterIconTone = tone === "ideal" ? "text-emerald-500" : "text-amber-400";

  return (
    <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-xs">
      <div className="border-b border-border bg-muted/20 px-4 py-3">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div className="min-w-0">
            <p className="text-xs font-medium uppercase text-muted-foreground">{scenarioTitle(scenario)}</p>
            <p className="mt-1 text-sm leading-relaxed text-foreground">{description}</p>
          </div>
          <div className="flex shrink-0 flex-wrap gap-2">
            <Badge variant="outline" className="gap-1.5 bg-surface">
              <CheckCircle2 className="size-3.5 text-emerald-500" />
              {aiSuggestionStatusLabels[run.status] ?? run.status}
            </Badge>
            <Badge variant="outline" className="bg-surface">Snapshot v{run.snapshotImportVersion ?? "-"}</Badge>
            {run.completedAt && (
              <Badge variant="outline" className="gap-1.5 bg-surface">
                <CalendarClock className="size-3.5 text-muted-foreground" />
                {new Date(run.completedAt).toLocaleString("pt-BR")}
              </Badge>
            )}
          </div>
        </div>
      </div>
      <div className="grid gap-3 p-4 text-sm md:grid-cols-3">
        <div className="rounded-lg border border-border/70 bg-muted/15 p-3">
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            <RouteIcon className="size-3.5" />
            Rotas críticas antes
          </div>
          <p className="mt-1 text-lg font-semibold">{scenario.currentMetrics.occupancyLevel}</p>
        </div>
        <div className={`rounded-lg border p-3 ${afterTone}`}>
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            <Gauge className={`size-3.5 ${afterIconTone}`} />
            Rotas críticas depois
          </div>
          <p className="mt-1 text-lg font-semibold">{scenario.proposedMetrics.occupancyLevel}</p>
        </div>
        <div className="rounded-lg border border-border/70 bg-muted/15 p-3">
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            <MapPin className="size-3.5" />
            Distância estimada
          </div>
          <p className="mt-1 text-lg font-semibold">
            {scenario.estimatedDistanceChangeKm == null ? "-" : `${scenario.estimatedDistanceChangeKm.toLocaleString("pt-BR", { maximumFractionDigits: 1 })} km`}
          </p>
        </div>
      </div>
    </div>
  );
}

function AiScenarioReasons({
  title,
  scenario,
}: {
  title: string;
  scenario: RouteOptimizationScenario;
}) {
  if (scenario.reasons.length === 0) return null;

  return (
    <div className="space-y-2">
      <p className="text-xs font-medium uppercase text-muted-foreground">{title}</p>
      {scenario.reasons.map((reason) => (
        <div key={reason.code} className="rounded-lg border border-border p-3 text-sm">
          {reason.message}
        </div>
      ))}
    </div>
  );
}

function AiScenarioMoves({
  scenario,
  title,
  emptyMessage,
  compact = false,
}: {
  scenario: RouteOptimizationScenario;
  title: string;
  emptyMessage: string;
  compact?: boolean;
}) {
  if (scenario.cityReallocations.length === 0) {
    return (
      <p className="rounded-lg border border-border p-3 text-sm text-muted-foreground">
        {emptyMessage}
      </p>
    );
  }

  return (
    <div className="rounded-lg border border-border">
      <div className="border-b border-border px-3 py-2">
        <p className="text-xs font-medium uppercase text-muted-foreground">{title}</p>
      </div>
      <div className={compact ? "grid gap-2 p-3 md:grid-cols-2" : "divide-y divide-border"}>
        {scenario.cityReallocations.map((move) => (
          <div
            key={`${scenario.id}-${move.cityId}-${move.sourceRouteId}-${move.destinationRouteId}`}
            className={compact ? "rounded-lg border border-border/70 bg-surface/70 p-3 text-sm" : "p-4 text-sm"}
          >
            <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant="outline" className={compact ? "" : "bg-primary/10 text-primary"}>{move.cityName}</Badge>
                  <span className="text-muted-foreground">de</span>
                  <span className="font-medium">{move.sourceRouteName}</span>
                  <ArrowRight className="size-4 text-muted-foreground" />
                  <span className="font-medium">{move.destinationRouteName}</span>
                </div>
              </div>
              <div className="flex shrink-0 flex-wrap gap-2">
                <Badge variant="outline">{formatRouteLoadKg(move.cityLoadKg)}</Badge>
                <Badge variant="outline">
                  {move.estimatedDistanceChangeKm.toLocaleString("pt-BR", { maximumFractionDigits: 1 })} km
                </Badge>
              </div>
            </div>

            {!compact && (
              <>
                <div className="mt-4 grid gap-3 md:grid-cols-2">
                  <OccupancyMiniBar
                    label="Origem melhora"
                    before={move.sourceOccupancyBefore}
                    after={move.sourceOccupancyAfter}
                    tone="source"
                  />
                  <OccupancyMiniBar
                    label="Destino permanece controlado"
                    before={move.destinationOccupancyBefore}
                    after={move.destinationOccupancyAfter}
                    tone="destination"
                  />
                </div>

                {move.reasons.length > 0 && (
                  <div className="mt-3 grid gap-2 md:grid-cols-3">
                    {move.reasons.map((reason) => (
                      <p key={reason.code} className="rounded-lg border border-border/70 bg-muted/20 p-2 text-xs text-muted-foreground">
                        {reason.message}
                      </p>
                    ))}
                  </div>
                )}
              </>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

function AiRoutePlanGroups({
  scenario,
}: {
  scenario: RouteOptimizationScenario;
}) {
  if (scenario.cityReallocations.length === 0) {
    return (
      <p className="rounded-lg border border-border p-3 text-sm text-muted-foreground">
        O plano ideal não encontrou mudanças para listar.
      </p>
    );
  }

  const groups = buildAiRoutePlanGroups(scenario);

  return (
    <div className="rounded-lg border border-border">
      <div className="border-b border-border px-3 py-2">
        <p className="text-xs font-medium uppercase text-muted-foreground">Rotas redesenhadas no plano ideal</p>
      </div>
      <div className="grid gap-3 p-3 lg:grid-cols-2">
        {groups.map((group, index) => (
          <div key={group.routeId} className="rounded-lg border border-border/70 bg-surface/70 p-3">
            <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
              <div className="min-w-0">
                <p className="text-[11px] font-medium uppercase text-muted-foreground">Rota sugerida</p>
                <p className="truncate text-base font-semibold">
                  Rota {String(index + 1).padStart(2, "0")} - {group.referenceCityName}
                </p>
              </div>
              <div className="flex shrink-0 flex-wrap gap-2">
                <Badge variant="outline">{group.cities.length} cidade(s)</Badge>
                <Badge variant="outline">{formatRouteLoadKg(group.totalLoadKg)}</Badge>
                <Badge variant="outline">{formatPercent(group.occupancyAfter)}</Badge>
              </div>
            </div>

            <div className="mt-3 space-y-2">
              <p className="text-xs font-medium uppercase text-muted-foreground">Cidades nesta rota</p>
              <div className="flex flex-wrap gap-2">
                {group.cities.map((move) => (
                  <span
                    key={`${group.routeId}-${move.cityId}`}
                    className="rounded-md border border-border bg-muted/30 px-2 py-1 text-xs text-foreground"
                  >
                    {move.cityName}
                  </span>
                ))}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function AiPlanExplanation({
  text,
  loading,
}: {
  text: string;
  loading: boolean;
}) {
  return (
    <div className="rounded-lg border border-primary/20 bg-primary/5 p-4">
      <div className="flex items-center gap-2">
        <Sparkles className="size-4 text-primary" />
        <p className="text-xs font-medium uppercase text-primary">Análise da IA</p>
      </div>
      <p className="mt-2 text-sm leading-relaxed text-foreground">
        {loading ? "Gerando justificativa do plano com o assistente..." : text}
      </p>
    </div>
  );
}

function buildAiRoutePlanGroups(scenario: RouteOptimizationScenario): AiRoutePlanGroup[] {
  return Object.values(
    scenario.cityReallocations.reduce<Record<string, Omit<AiRoutePlanGroup, "referenceCityName">>>((acc, move) => {
      acc[move.destinationRouteId] ??= {
        routeId: move.destinationRouteId,
        occupancyAfter: move.destinationOccupancyAfter,
        totalLoadKg: 0,
        cities: [],
      };
      acc[move.destinationRouteId].totalLoadKg += move.cityLoadKg;
      acc[move.destinationRouteId].cities.push(move);
      return acc;
    }, {}),
  )
    .map((group) => ({
      ...group,
      referenceCityName: [...group.cities]
        .sort((a, b) => b.cityLoadKg - a.cityLoadKg || a.cityName.localeCompare(b.cityName, "pt-BR"))[0].cityName,
    }))
    .sort((a, b) => a.referenceCityName.localeCompare(b.referenceCityName, "pt-BR"));
}

function buildLocalPlanExplanation(scenario: RouteOptimizationScenario): string {
  const groups = buildAiRoutePlanGroups(scenario);
  const mainGroups = groups.slice(0, 3).map((group) => group.referenceCityName).join(", ");
  return `O plano ideal redesenha a malha com ${groups.length} rota(s) sugerida(s), usando cidades de referência como ${mainGroups || "as maiores cargas"} para formar agrupamentos mais equilibrados. A proposta reduz as rotas críticas de ${scenario.currentMetrics.occupancyLevel} para ${scenario.proposedMetrics.occupancyLevel}, sem trocar caminhões nem aplicar alterações automaticamente.`;
}

function buildAssistantPlanPrompt(scenario: RouteOptimizationScenario): string {
  const groups = buildAiRoutePlanGroups(scenario)
    .slice(0, AI_PLAN_EXPLANATION_GROUP_LIMIT)
    .map((group) => `${group.referenceCityName}: ${group.cities.length} cid, ${formatRouteLoadKg(group.totalLoadKg)}, ${formatPercent(group.occupancyAfter)}`)
    .join("; ");
  return `Reescreva em PT-BR, 1 parágrafo convincente e objetivo, sem bullets, por que este plano ideal de rotas faz sentido. Dados: críticas ${scenario.currentMetrics.occupancyLevel}->${scenario.proposedMetrics.occupancyLevel}; ${scenario.cityReallocations.length} cidades redesenhadas; distância ${scenario.estimatedDistanceChangeKm?.toLocaleString("pt-BR", { maximumFractionDigits: 1 }) ?? "-"} km; grupos: ${groups}. Diga que não aplica automaticamente e que usa caminhões atuais.`;
}

function normalizeAssistantPlanExplanation(value: string): string {
  return value
    .replace(/^[\s>*-]+/gm, "")
    .replace(/\s+/g, " ")
    .trim();
}

function LogisticaRotasPage() {
  const canSimulate = canRoleUseRouteSimulation(getCurrentUserRole());
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
  const [detailVehicleTypes, setDetailVehicleTypes] = useState<VehicleTypeItem[]>([]);
  const [decisionSupportError, setDecisionSupportError] = useState<string | null>(null);
  const [aiSuggestionOpen, setAiSuggestionOpen] = useState(false);
  const [aiSuggestionRun, setAiSuggestionRun] = useState<RouteOptimizationRun | null>(null);
  const [aiSuggestionLoading, setAiSuggestionLoading] = useState(false);
  const [aiSuggestionError, setAiSuggestionError] = useState<string | null>(null);
  const [aiPlanExplanation, setAiPlanExplanation] = useState("");
  const [aiPlanExplanationLoading, setAiPlanExplanationLoading] = useState(false);

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
    setDecisionSupportError(null);
    try {
      const detail = await fetchImportedRouteDetail(route.id);
      setSelectedRoute(detail);
      try {
        if (!canSimulate) return;
        setDetailVehicleTypes(await fetchVehicleTypes());
      } catch {
        setDetailVehicleTypes([]);
        setDecisionSupportError("Não foi possível carregar os veículos para calcular as alternativas.");
      }
    } catch {
      setSelectedRoute(null);
      setDetailVehicleTypes([]);
    } finally {
      setDetailsLoading(false);
    }
  }

  async function openAiSuggestion() {
    setAiSuggestionOpen(true);
    setAiSuggestionLoading(true);
    setAiSuggestionError(null);
    setAiSuggestionRun(null);
    setAiPlanExplanation("");
    setAiPlanExplanationLoading(false);
    try {
      setAiSuggestionRun(await fetchLatestGlobalRouteOptimization(snapshotDate));
    } catch (error) {
      setAiSuggestionError((error as Error).message);
    } finally {
      setAiSuggestionLoading(false);
    }
  }

  const pageCount = Math.max(1, Math.ceil(total / pageSize));
  const aiScenario = recommendedScenario(aiSuggestionRun);
  const emergencyScenario = aiSuggestionRun?.scenarios.find((scenario) =>
    scenario.actionType === "ReallocateCities" && scenario.id !== aiScenario?.id
  ) ?? null;

  useEffect(() => {
    if (!aiSuggestionOpen || !aiScenario || aiScenario.actionType !== "BuildBalancedRoutePlan") {
      setAiPlanExplanation("");
      setAiPlanExplanationLoading(false);
      return;
    }

    let active = true;
    setAiPlanExplanation(buildLocalPlanExplanation(aiScenario));
    setAiPlanExplanationLoading(true);
    askBusinessAssistant(buildAssistantPlanPrompt(aiScenario))
      .then((response) => {
        if (active && response.answer.trim()) {
          setAiPlanExplanation(normalizeAssistantPlanExplanation(response.answer));
        }
      })
      .catch(() => {
        if (active) setAiPlanExplanation(buildLocalPlanExplanation(aiScenario));
      })
      .finally(() => {
        if (active) setAiPlanExplanationLoading(false);
      });

    return () => {
      active = false;
    };
  }, [aiSuggestionOpen, aiScenario?.id]);

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
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="space-y-2">
              <CardTitle>Todas as rotas</CardTitle>
              <Button
                type="button"
                size="sm"
                onClick={() => void openAiSuggestion()}
              >
                <Sparkles className="mr-2 size-4" />
                Roteirização calculada
              </Button>
            </div>
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

              {canSimulate && (
                <RouteDecisionSupport
                  route={selectedRoute}
                  vehicleTypes={detailVehicleTypes}
                  loading={detailsLoading}
                  error={decisionSupportError}
                />
              )}

            </div>
          )}

          {!detailsLoading && !selectedRoute && (
            <p className="text-sm text-muted-foreground">Não foi possível carregar os detalhes desta rota.</p>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={aiSuggestionOpen} onOpenChange={setAiSuggestionOpen}>
        <DialogContent className="max-h-[90vh] max-w-5xl gap-0 overflow-y-auto border-border bg-surface p-0">
          <DialogHeader className="border-b border-border bg-[linear-gradient(135deg,var(--soft-red-background),var(--surface)_58%,var(--surface-soft))] px-5 py-5 pr-14 text-left sm:px-7">
            <div className="flex items-start gap-3">
              <div className="flex size-11 shrink-0 items-center justify-center rounded-lg border border-primary/25 bg-primary/10 text-primary shadow-xs">
                <Sparkles className="size-5" />
              </div>
              <div className="min-w-0 space-y-1">
                <DialogTitle className="text-xl">Roteirização calculada</DialogTitle>
                <DialogDescription className="max-w-3xl">
                  Leitura do último job de otimização global já processado, com o cenário recomendado pronto para análise.
                </DialogDescription>
              </div>
            </div>
          </DialogHeader>

          <div className="space-y-4 px-5 py-5 sm:px-7">
            {aiSuggestionLoading && <SkeletonModalContent />}

            {!aiSuggestionLoading && aiSuggestionError && (
              <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">
                {aiSuggestionError}
              </div>
            )}

            {!aiSuggestionLoading && aiSuggestionRun && (
              <div className="space-y-4">
                {aiScenario ? (
                  <Tabs defaultValue="ideal" className="space-y-4">
                    <TabsList className="grid w-full grid-cols-2">
                      <TabsTrigger value="ideal" className="gap-2">
                        <Sparkles className="size-4" />
                        Plano ideal
                      </TabsTrigger>
                      <TabsTrigger value="emergency" className="gap-2">
                        <FlaskConical className="size-4" />
                        Ação emergencial
                      </TabsTrigger>
                    </TabsList>

                    <TabsContent value="ideal" className="space-y-4">
                      <AiScenarioSummary
                        scenario={aiScenario}
                        run={aiSuggestionRun}
                        tone="ideal"
                        description="O cálculo redesenha a distribuição sugerida usando as cidades existentes, os caminhões disponíveis e as distâncias viárias, sem aplicar nenhuma mudança automaticamente."
                      />
                      <AiPlanExplanation
                        text={aiPlanExplanation || buildLocalPlanExplanation(aiScenario)}
                        loading={aiPlanExplanationLoading}
                      />
                      <AiRoutePlanGroups scenario={aiScenario} />
                      <RouteSequenceComparison scenario={aiScenario} />
                      {aiScenario.warnings.length > 0 && (
                        <div className="space-y-1">
                          {aiScenario.warnings.map((warning) => (
                            <p key={warning} className="text-xs text-muted-foreground">{warning}</p>
                          ))}
                        </div>
                      )}
                    </TabsContent>

                    <TabsContent value="emergency" className="space-y-4">
                      {emergencyScenario ? (
                        <>
                          <AiScenarioSummary
                            scenario={emergencyScenario}
                            run={aiSuggestionRun}
                            tone="emergency"
                            description="Essa aba mantém uma alternativa prática para aliviar rotas críticas agora, sem redesenhar toda a distribuição operacional."
                          />
                          <AiScenarioReasons title="Por que usar como paliativo" scenario={emergencyScenario} />
                          <AiScenarioMoves
                            scenario={emergencyScenario}
                            title="Movimentos para execução manual"
                            emptyMessage="A ação emergencial não encontrou movimentos para listar."
                            compact
                          />
                          {emergencyScenario.warnings.length > 0 && (
                            <div className="space-y-1">
                              {emergencyScenario.warnings.map((warning) => (
                                <p key={warning} className="text-xs text-muted-foreground">{warning}</p>
                              ))}
                            </div>
                          )}
                        </>
                      ) : (
                        <div className="rounded-lg border border-border p-4 text-sm text-muted-foreground">
                          O último job não encontrou uma ação emergencial separada do plano ideal.
                        </div>
                      )}
                    </TabsContent>
                  </Tabs>
              ) : (
                <p className="rounded-lg border border-border p-3 text-sm text-muted-foreground">
                  O último job não possui cenário persistido para explicar.
                </p>
              )}

              <div className="flex justify-end">
                <Button variant="outline" onClick={() => setAiSuggestionOpen(false)}>Fechar</Button>
              </div>
            </div>
          )}
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
