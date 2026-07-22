import { useMemo, useState } from "react";
import { Bot, Lightbulb, LoaderCircle, ShieldAlert, Truck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { ImportedRouteDetail, VehicleTypeItem } from "@/lib/importer-api";
import { askBusinessAssistant } from "@/lib/assistant-api";
import { formatCapacityKg, formatOccupancy } from "@/lib/route-occupancy";
import { buildRouteAiAnalysisPrompt, buildRouteDecisionSupport } from "@/lib/route-decision-support";

type RouteDecisionSupportProps = {
  route: ImportedRouteDetail;
  vehicleTypes: VehicleTypeItem[];
  loading: boolean;
  error: string | null;
};

export function RouteDecisionSupport({ route, vehicleTypes, loading, error }: RouteDecisionSupportProps) {
  const support = useMemo(() => buildRouteDecisionSupport(route, vehicleTypes), [route, vehicleTypes]);
  const [aiAnalysis, setAiAnalysis] = useState<string | null>(null);
  const [aiError, setAiError] = useState<string | null>(null);
  const [aiLoading, setAiLoading] = useState(false);

  async function analyzeWithAi() {
    setAiLoading(true);
    setAiError(null);
    try {
      const response = await askBusinessAssistant(buildRouteAiAnalysisPrompt(route, support));
      setAiAnalysis(response.answer);
    } catch (analysisError) {
      setAiAnalysis(null);
      setAiError((analysisError as Error).message);
    } finally {
      setAiLoading(false);
    }
  }

  return (
    <section className="space-y-3 rounded-xl border border-primary/25 bg-primary/[0.03] p-4" aria-label="Apoio à decisão da rota">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2"><Lightbulb className="size-4 text-primary" /><h3 className="font-semibold">Apoio à decisão</h3></div>
          <p className="mt-1 text-xs text-muted-foreground">Cenários calculados com a carga atual e os veículos cadastrados.</p>
        </div>
        <Button size="sm" variant="outline" onClick={() => void analyzeWithAi()} disabled={loading || aiLoading || vehicleTypes.length === 0}>
          {aiLoading ? <LoaderCircle className="mr-1.5 size-3.5 animate-spin" /> : <Bot className="mr-1.5 size-3.5" />}
          Analisar com IA
        </Button>
      </div>

      {loading && <p className="text-sm text-muted-foreground">Calculando alternativas...</p>}
      {!loading && error && <p className="text-sm text-destructive">{error}</p>}
      {!loading && !error && (
        <>
          <p className="text-sm">{support.summary}</p>
          <div className="grid gap-3 md:grid-cols-3">
            {support.alternatives.map((item) => (
              <article key={item.vehicleTypeId} className="rounded-lg border border-border bg-surface p-3">
                <div className="flex items-start justify-between gap-2">
                  <div><Truck className="mb-1 size-4 text-muted-foreground" /><p className="text-sm font-semibold">{item.vehicleName}</p></div>
                  <Badge variant={item.status === "Atenção" ? "destructive" : "outline"}>{item.status}</Badge>
                </div>
                <dl className="mt-3 space-y-1 text-xs">
                  <div className="flex justify-between gap-2"><dt className="text-muted-foreground">Capacidade</dt><dd>{formatCapacityKg(item.capacityKg)}</dd></div>
                  <div className="flex justify-between gap-2"><dt className="text-muted-foreground">Ocupação</dt><dd className="font-semibold">{formatOccupancy(item.occupancy)}</dd></div>
                </dl>
                <p className="mt-3 text-xs text-muted-foreground">{item.rationale}</p>
                <p className="mt-2 flex gap-1.5 text-xs"><ShieldAlert className="mt-0.5 size-3.5 shrink-0" />{item.risk}</p>
              </article>
            ))}
          </div>
        </>
      )}

      {aiAnalysis && <div className="rounded-lg border border-border bg-surface p-3"><p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Resumo da IA</p><p className="mt-2 whitespace-pre-line text-sm leading-relaxed">{aiAnalysis}</p></div>}
      {aiError && <p className="rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{aiError}</p>}
      <p className="text-center text-[11px] text-muted-foreground">Recomendação informativa. Nenhuma alteração é aplicada automaticamente.</p>
    </section>
  );
}
