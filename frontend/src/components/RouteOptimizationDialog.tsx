import { useEffect, useState } from "react";
import { ArrowRight, CheckCircle2, RefreshCcw, Route as RouteIcon } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { SkeletonModalContent } from "@/components/ui/skeleton";
import { RouteOccupancyIndicator } from "@/components/RouteOccupancyIndicator";
import {
  fetchLatestRouteOptimization,
  type ImportedRouteItem,
  type RouteLatestOptimization,
} from "@/lib/importer-api";
import { formatCapacityKg, formatRouteLoadKg } from "@/lib/route-occupancy";

type RouteOptimizationDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  route: ImportedRouteItem | null;
  referenceDate: string;
};

const RECOMMENDATION_REVEAL_STEP_DELAY_MS = 260;
const RECOMMENDATION_REVEAL_TOTAL_STEPS = 5;

const statusLabels: Record<RouteLatestOptimization["status"], string> = {
  Pending: "Na fila",
  LoadingData: "Carregando dados",
  BuildingProblem: "Montando problema",
  CalculatingDistanceMatrix: "Calculando distâncias",
  SearchingSolutions: "Buscando cenários",
  ComparingScenarios: "Comparando cenários",
  PersistingResult: "Salvando resultado",
  Completed: "Concluída",
  NoChangeRecommended: "Sem alteração recomendada",
  InsufficientData: "Dados insuficientes",
  NoFeasibleSolution: "Sem solução viável",
  Cancelled: "Cancelada",
  Failed: "Falhou",
};

export function RouteOptimizationDialog({
  open,
  onOpenChange,
  route,
  referenceDate,
}: RouteOptimizationDialogProps) {
  const [recommendation, setRecommendation] = useState<RouteLatestOptimization | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [visibleStep, setVisibleStep] = useState(0);
  const revealing = Boolean(recommendation) && visibleStep < RECOMMENDATION_REVEAL_TOTAL_STEPS;

  useEffect(() => {
    if (!open || !route) {
      setRecommendation(null);
      setError(null);
      setLoading(false);
      setVisibleStep(0);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);
    setRecommendation(null);
    setVisibleStep(0);

    fetchLatestRouteOptimization(route.id, referenceDate)
      .then((result) => {
        if (!cancelled) setRecommendation(result);
      })
      .catch((requestError) => {
        if (!cancelled) setError((requestError as Error).message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [open, route?.id, referenceDate]);

  useEffect(() => {
    if (!recommendation) {
      setVisibleStep(0);
      return;
    }

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      setVisibleStep(RECOMMENDATION_REVEAL_TOTAL_STEPS);
      return;
    }

    setVisibleStep(0);
    const timers = Array.from({ length: RECOMMENDATION_REVEAL_TOTAL_STEPS }, (_, index) =>
      window.setTimeout(
        () => setVisibleStep(index + 1),
        RECOMMENDATION_REVEAL_STEP_DELAY_MS * (index + 1),
      ),
    );

    return () => {
      timers.forEach((timer) => window.clearTimeout(timer));
    };
  }, [recommendation?.runId, recommendation?.calculatedAt, recommendation?.status]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto border-border bg-surface">
        <DialogHeader>
          <DialogTitle>Ver recomendação</DialogTitle>
          <DialogDescription>
            Simulação: nenhuma alteração foi aplicada às rotas atuais.
          </DialogDescription>
        </DialogHeader>

        {loading && (
          <div className="space-y-3">
            <Alert>
              <RefreshCcw className="size-4 animate-spin" />
              <AlertTitle>Consultando recomendação</AlertTitle>
              <AlertDescription>
                A tela consulta somente resultados já persistidos.
              </AlertDescription>
            </Alert>
            <SkeletonModalContent />
          </div>
        )}

        {error && (
          <Alert variant="destructive">
            <AlertTitle>Não foi possível consultar</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {!loading && recommendation && (
          <div className="space-y-4">
            <div className={visibleStep >= 1 ? "animate-soft-enter flex flex-wrap items-center gap-2" : "hidden"}>
              <Badge variant="outline">{statusLabels[recommendation.status]}</Badge>
              {recommendation.sourceVersion && <Badge variant="outline">Snapshot v{recommendation.sourceVersion}</Badge>}
              {recommendation.isStale && <Badge variant="destructive">Desatualizado</Badge>}
            </div>

            {visibleStep >= 2 && <Alert className="animate-soft-enter">
              <AlertTitle>{recommendation.message}</AlertTitle>
              <AlertDescription>
                {recommendation.calculatedAt
                  ? `Calculado em ${new Date(recommendation.calculatedAt).toLocaleString("pt-BR")}.`
                  : "Nenhum cálculo concluído foi encontrado para exibir."}
              </AlertDescription>
            </Alert>}

            {recommendation.route && (
              <>
                {visibleStep >= 3 && <div className="grid animate-soft-enter grid-cols-1 gap-3 md:grid-cols-[1fr_auto_1fr] md:items-stretch">
                  <div className="rounded-lg border border-border p-3">
                    <p className="text-xs font-medium uppercase text-muted-foreground">Cenário atual</p>
                    <p className="mt-1 text-sm font-semibold">{recommendation.route.routeName}</p>
                    <p className="text-xs text-muted-foreground">
                      {formatCapacityKg(recommendation.route.currentCapacityKg)}
                    </p>
                    <RouteOccupancyIndicator value={recommendation.route.currentOccupancy} compact />
                  </div>

                  <div className="hidden items-center justify-center md:flex">
                    <ArrowRight className="size-5 text-muted-foreground" />
                  </div>

                  <div className="rounded-lg border border-border p-3">
                    <p className="text-xs font-medium uppercase text-muted-foreground">Cenário proposto</p>
                    <p className="mt-1 text-sm font-semibold">{recommendation.route.routeName}</p>
                    <p className="text-xs text-muted-foreground">
                      {formatCapacityKg(recommendation.route.proposedCapacityKg)}
                    </p>
                    <RouteOccupancyIndicator value={recommendation.route.proposedOccupancy} compact />
                  </div>
                </div>}

                {visibleStep >= 4 && (recommendation.route.removedCities.length > 0 || recommendation.route.addedCities.length > 0) && (
                  <div className="animate-soft-enter rounded-lg border border-border">
                    <div className="border-b border-border px-3 py-2">
                      <p className="text-xs font-medium uppercase text-muted-foreground">Cidades alteradas</p>
                    </div>
                    <div className="divide-y divide-border">
                      {recommendation.route.removedCities.map((item) => (
                        <div key={`removed-${item.cityId}`} className="p-3 text-sm">
                          <div className="flex flex-wrap items-center gap-2">
                            <RouteIcon className="size-4 text-muted-foreground" />
                            <span className="font-medium">{item.cityName}</span>
                            <span className="text-muted-foreground">removida para</span>
                            <span className="font-medium">{item.relatedRouteName}</span>
                          </div>
                          <p className="mt-1 text-xs text-muted-foreground">
                            Carga transferida: {formatRouteLoadKg(item.cityLoadKg)}
                          </p>
                        </div>
                      ))}
                      {recommendation.route.addedCities.map((item) => (
                        <div key={`added-${item.cityId}`} className="p-3 text-sm">
                          <div className="flex flex-wrap items-center gap-2">
                            <RouteIcon className="size-4 text-muted-foreground" />
                            <span className="font-medium">{item.cityName}</span>
                            <span className="text-muted-foreground">adicionada de</span>
                            <span className="font-medium">{item.relatedRouteName}</span>
                          </div>
                          <p className="mt-1 text-xs text-muted-foreground">
                            Carga recebida: {formatRouteLoadKg(item.cityLoadKg)}
                          </p>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {visibleStep >= 5 && <div className="animate-soft-enter space-y-2">
                  {recommendation.route.reasons.map((reason) => (
                    <div key={reason.code} className="flex gap-2 rounded-lg border border-border p-3 text-sm">
                      <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-emerald-500" />
                      <p>{reason.message}</p>
                    </div>
                  ))}
                  {recommendation.route.warnings.map((warning) => (
                    <p key={warning} className="text-xs text-muted-foreground">{warning}</p>
                  ))}
                </div>}
              </>
            )}

            {visibleStep >= 3 && !recommendation.route && (
              <Alert className="animate-soft-enter">
                <AlertTitle>{statusLabels[recommendation.status]}</AlertTitle>
                <AlertDescription>{recommendation.message}</AlertDescription>
              </Alert>
            )}

            {revealing && (
              <div className="flex items-center gap-1 rounded-lg border border-border/70 bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
                <span>Montando recomendação</span>
                {[0, 1, 2].map((item) => (
                  <span
                    key={item}
                    className="size-1.5 animate-bounce rounded-full bg-primary"
                    style={{ animationDelay: `${item * 120}ms` }}
                  />
                ))}
              </div>
            )}

            <div className={visibleStep >= RECOMMENDATION_REVEAL_TOTAL_STEPS ? "animate-soft-enter flex justify-end" : "hidden"}>
              <Button variant="outline" onClick={() => onOpenChange(false)}>Fechar</Button>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
