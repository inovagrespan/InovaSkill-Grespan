import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { AlertTriangle, CheckCircle2, Loader2, MapPin, RotateCcw, Search } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { SkeletonList } from "@/components/ui/skeleton";
import {
  createRouteOptimizationRemediation,
  fetchRouteOptimizationRemediation,
  fetchRouteOptimizationRemediationStatus,
  retryRouteOptimizationRemediation,
  searchMunicipalityCandidates,
  type MunicipalityCandidate,
  type RouteOptimizationRemediation,
  type RouteOptimizationResolution,
  type RouteOptimizationSummary,
} from "@/lib/importer-api";
import { JOB_STATUS_POLL_INTERVAL_MS } from "@/lib/job-runtime";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";

type StopForm = { weight: string; excluded: boolean; municipality: MunicipalityCandidate | null };
type CoordinateForm = { mode: "official" | "manual"; latitude: string; longitude: string };

export function RouteOptimizationRemediationDialog({
  summary,
  open,
  onOpenChange,
  onCompleted,
}: {
  summary: RouteOptimizationSummary | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCompleted: () => void;
}) {
  const [diagnostic, setDiagnostic] = useState<RouteOptimizationRemediation | null>(null);
  const [stops, setStops] = useState<Record<string, StopForm>>({});
  const [capacities, setCapacities] = useState<Record<string, string>>({});
  const [coordinates, setCoordinates] = useState<Record<string, CoordinateForm>>({});
  const [confirmAliasReplacement, setConfirmAliasReplacement] = useState(false);
  const [derivedImportId, setDerivedImportId] = useState<string | null>(null);
  const [processingMessage, setProcessingMessage] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);
  const [completed, setCompleted] = useState(false);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || !summary) return;
    setLoading(true);
    setError(null);
    setDerivedImportId(null);
    setFailed(false);
    setCompleted(false);
    void fetchRouteOptimizationRemediation(summary.id)
      .then((data) => {
        setDiagnostic(data);
        setStops(Object.fromEntries(data.routes.flatMap((route) => route.stops.map((stop) => [stop.routeEntryId, {
          weight: stop.weightKg.toLocaleString("pt-BR", { maximumFractionDigits: 3 }),
          excluded: stop.isExcludedFromOptimization,
          municipality: null,
        }]))));
        setCapacities(Object.fromEntries(data.routes.map((route) => [
          route.vehicleTypeId,
          route.capacityKg?.toLocaleString("pt-BR", { maximumFractionDigits: 3 }) ?? "",
        ])));
        const municipalityIds = data.routes.flatMap((route) => route.issues)
          .filter((issue) => issue.code === "MUNICIPALITY_COORDINATE_MISSING" && issue.municipalityId)
          .map((issue) => issue.municipalityId!);
        setCoordinates(Object.fromEntries(municipalityIds.map((id) => [id, { mode: "official", latitude: "", longitude: "" }])));
      })
      .catch((reason) => setError((reason as Error).message))
      .finally(() => setLoading(false));
  }, [open, summary]);

  useEffect(() => {
    if (!open || !derivedImportId || failed || completed) return;
    const poll = async () => {
      try {
        const status = await fetchRouteOptimizationRemediationStatus(derivedImportId);
        const latestJob = status.jobs.at(-1);
        setProcessingMessage(latestJob?.progressMessage ?? "Processando correções");
        const jobFailed = status.status === "Failed" || latestJob?.status === "Failed";
        if (jobFailed) {
          setFailed(true);
          setError(latestJob?.errorMessage ?? status.failureMessage ?? "A correção falhou.");
          return;
        }
        const optimization = status.jobs.filter((job) => job.jobType === "DAILY_ROUTE_OPTIMIZATION").at(-1);
        if (status.isPublished && optimization?.status === "Completed") {
          setProcessingMessage("Nova versão publicada e dias afetados recalculados.");
          setCompleted(true);
          onCompleted();
        }
      } catch (reason) {
        setError((reason as Error).message);
      }
    };
    void poll();
    const id = window.setInterval(() => void poll(), JOB_STATUS_POLL_INTERVAL_MS);
    return () => window.clearInterval(id);
  }, [completed, derivedImportId, failed, onCompleted, open]);

  const hasMissingMunicipality = diagnostic?.routes.some((route) =>
    route.issues.some((issue) => issue.code === "MUNICIPALITY_NOT_LINKED")) ?? false;

  async function save() {
    if (!diagnostic) return;
    const resolutions: RouteOptimizationResolution[] = [];
    const coordinateIds = new Set<string>();
    const capacityIds = new Set<string>();
    for (const route of diagnostic.routes) {
      for (const issue of route.issues) {
        if (issue.code === "VEHICLE_CAPACITY_MISSING" && !capacityIds.has(route.vehicleTypeId)) {
          const capacityKg = capacities[route.vehicleTypeId]?.trim();
          if (!capacityKg) return setError(`Informe a capacidade do veículo ${route.vehicleType}.`);
          resolutions.push({ action: "SET_VEHICLE_TYPE_CAPACITY", vehicleTypeId: route.vehicleTypeId, capacityKg });
          capacityIds.add(route.vehicleTypeId);
        }
        if (issue.code === "MUNICIPALITY_COORDINATE_MISSING" && issue.municipalityId && !coordinateIds.has(issue.municipalityId)) {
          if (issue.routeEntryId && stops[issue.routeEntryId]?.excluded) continue;
          const coordinate = coordinates[issue.municipalityId] ?? { mode: "official", latitude: "", longitude: "" };
          resolutions.push(coordinate.mode === "official"
            ? { action: "CONFIRM_OFFICIAL_COORDINATE", municipalityId: issue.municipalityId }
            : { action: "SET_MANUAL_COORDINATE", municipalityId: issue.municipalityId, latitude: coordinate.latitude, longitude: coordinate.longitude });
          coordinateIds.add(issue.municipalityId);
        }
      }
      for (const stop of route.stops) {
        const form = stops[stop.routeEntryId];
        const issueCodes = new Set(stop.issues.map((issue) => issue.code));
        if (form?.excluded && stop.weightKg === 0) {
          resolutions.push({ action: "EXCLUDE_STOP", routeId: route.routeId, routeEntryId: stop.routeEntryId });
          continue;
        }
        if (issueCodes.has("INVALID_STOP_WEIGHT")) {
          if (!form?.weight.trim()) return setError(`Informe um peso positivo para ${stop.name}.`);
          resolutions.push({ action: "SET_WEIGHT", routeId: route.routeId, routeEntryId: stop.routeEntryId, weightKg: form.weight });
        }
        if (issueCodes.has("MUNICIPALITY_NOT_LINKED")) {
          if (!form?.municipality) return setError(`Selecione o município oficial de ${stop.name}.`);
          resolutions.push({
            action: "LINK_MUNICIPALITY",
            routeId: route.routeId,
            routeEntryId: stop.routeEntryId,
            municipalityIbgeCode: form.municipality.ibgeCode,
            confirmAliasReplacement,
          });
        }
      }
    }
    setSaving(true);
    setError(null);
    try {
      const result = await createRouteOptimizationRemediation(
        diagnostic.resultId,
        diagnostic.expectedSnapshotId,
        resolutions,
      );
      setDerivedImportId(result.derivedImportId);
      setProcessingMessage("Correções salvas. Criando a nova versão...");
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setSaving(false);
    }
  }

  async function retry() {
    if (!derivedImportId) return;
    setSaving(true);
    setError(null);
    try {
      await retryRouteOptimizationRemediation(derivedImportId);
      setFailed(false);
      setCompleted(false);
      setProcessingMessage("Correção reenviada para processamento.");
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[92vh] max-w-4xl overflow-y-auto border-border bg-surface">
        <DialogHeader>
          <DialogTitle>{diagnostic?.canResolve ? "Resolver pendências" : "Pendências da otimização"}</DialogTitle>
          <DialogDescription>
            {diagnostic?.readOnly
              ? "Este diagnóstico é somente leitura porque pertence a um snapshot histórico."
              : "Corrija todos os dados abaixo de uma vez. O arquivo original e o snapshot atual serão preservados."}
          </DialogDescription>
        </DialogHeader>
        {loading && <SkeletonList rows={4} />}
        {error && <p role="alert" className="rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{error}</p>}
        {processingMessage && (
          <p className="flex items-center gap-2 rounded-lg border border-border p-3 text-sm">
            {failed ? <AlertTriangle className="size-4 text-destructive" /> : completed ? <CheckCircle2 className="size-4 text-emerald-500" /> : derivedImportId ? <Loader2 className="size-4 animate-spin" /> : <CheckCircle2 className="size-4" />}
            {processingMessage}
          </p>
        )}
        {!loading && diagnostic && !derivedImportId && (
          <div className="space-y-4">
            <div className="flex flex-wrap gap-2">
              <Badge variant="outline">{diagnostic.issueCount} pendência(s)</Badge>
              <Badge variant="outline">{diagnostic.weekday}</Badge>
            </div>
            {diagnostic.routes.filter((route) => route.issues.length > 0).map((route) => (
              <section key={route.routeId} className="space-y-3 rounded-lg border border-border p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div><h3 className="font-medium">{route.routeName}</h3><p className="text-xs text-muted-foreground">{route.sourceSheetName ?? "Origem não localizada"}</p></div>
                  <Badge variant="outline">{route.vehicleType}</Badge>
                </div>
                {route.issues.some((issue) => issue.code === "VEHICLE_CAPACITY_MISSING") && (
                  <IssueEditor title="Capacidade do veículo" message="Informe a capacidade positiva em kg.">
                    {!diagnostic.readOnly && <Input aria-label={`Capacidade de ${route.vehicleType}`} value={capacities[route.vehicleTypeId] ?? ""} onChange={(event) => setCapacities((current) => ({ ...current, [route.vehicleTypeId]: event.target.value }))} placeholder="Ex.: 12.500,000" />}
                  </IssueEditor>
                )}
                {route.stops.filter((stop) => stop.issues.length > 0).map((stop) => {
                  const form = stops[stop.routeEntryId];
                  const invalidWeight = stop.issues.some((issue) => issue.code === "INVALID_STOP_WEIGHT");
                  const missingMunicipality = stop.issues.some((issue) => issue.code === "MUNICIPALITY_NOT_LINKED");
                  return (
                    <div key={stop.routeEntryId} className="space-y-3 rounded-md bg-muted/30 p-3">
                      <div className="flex items-center justify-between gap-2"><p className="font-medium">{stop.name}</p><span className="text-xs text-muted-foreground">linha {stop.sourceRowNumber ?? "não localizada"}</span></div>
                      {stop.issues.map((issue) => <p key={issue.code} className="text-sm text-muted-foreground">{issue.message}</p>)}
                      {!diagnostic.readOnly && invalidWeight && !form?.excluded && <Input aria-label={`Peso de ${stop.name}`} value={form?.weight ?? ""} onChange={(event) => setStops((current) => ({ ...current, [stop.routeEntryId]: { ...current[stop.routeEntryId], weight: event.target.value } }))} placeholder="kg/dia" />}
                      {!diagnostic.readOnly && stop.weightKg === 0 && (
                        <label className="flex w-fit cursor-pointer items-center gap-2.5 rounded-md border border-border bg-background px-3 py-2 text-sm font-medium transition-colors hover:border-primary/40 hover:bg-muted/50"><Checkbox checked={form?.excluded ?? false} onChange={(event) => setStops((current) => ({ ...current, [stop.routeEntryId]: { ...current[stop.routeEntryId], excluded: event.target.checked } }))} /> Fora da simulação</label>
                      )}
                      {!diagnostic.readOnly && missingMunicipality && !form?.excluded && (
                        <MunicipalitySearch value={form?.municipality ?? null} initialQuery={stop.name} onChange={(municipality) => setStops((current) => ({ ...current, [stop.routeEntryId]: { ...current[stop.routeEntryId], municipality } }))} />
                      )}
                    </div>
                  );
                })}
                {route.issues.filter((issue) => issue.code === "MUNICIPALITY_COORDINATE_MISSING" && issue.municipalityId).map((issue) => {
                  const form = coordinates[issue.municipalityId!] ?? { mode: "official", latitude: "", longitude: "" };
                  return (
                    <IssueEditor key={issue.municipalityId} title="Coordenada municipal" message={issue.message}>
                      {!diagnostic.readOnly && <div className="space-y-2"><div className="flex gap-2"><Button type="button" size="sm" variant={form.mode === "official" ? "default" : "outline"} onClick={() => setCoordinates((current) => ({ ...current, [issue.municipalityId!]: { ...form, mode: "official" } }))}>Usar base oficial</Button><Button type="button" size="sm" variant={form.mode === "manual" ? "default" : "outline"} onClick={() => setCoordinates((current) => ({ ...current, [issue.municipalityId!]: { ...form, mode: "manual" } }))}>Informar manualmente</Button></div>{form.mode === "manual" && <div className="grid gap-2 sm:grid-cols-2"><Input aria-label="Latitude" value={form.latitude} onChange={(event) => setCoordinates((current) => ({ ...current, [issue.municipalityId!]: { ...form, latitude: event.target.value } }))} /><Input aria-label="Longitude" value={form.longitude} onChange={(event) => setCoordinates((current) => ({ ...current, [issue.municipalityId!]: { ...form, longitude: event.target.value } }))} /></div>}</div>}
                    </IssueEditor>
                  );
                })}
              </section>
            ))}
            {!diagnostic.readOnly && hasMissingMunicipality && <label className="flex cursor-pointer items-start gap-2.5 rounded-md border border-border bg-background p-3 text-xs text-muted-foreground transition-colors hover:border-primary/40"><Checkbox checked={confirmAliasReplacement} onChange={(event) => setConfirmAliasReplacement(event.target.checked)} /><span>Confirmo a substituição caso algum nome já possua outro vínculo municipal. O antes/depois ficará auditado.</span></label>}
          </div>
        )}
        <div className="flex justify-end gap-2">
          {failed && <Button variant="outline" disabled={saving} onClick={() => void retry()}><RotateCcw className="mr-2 size-4" />Tentar novamente</Button>}
          {diagnostic?.canResolve && !derivedImportId && <Button disabled={saving || loading} onClick={() => void save()}>{saving ? "Salvando..." : "Salvar e recalcular"}</Button>}
          <Button variant="outline" onClick={() => onOpenChange(false)}>Fechar</Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function IssueEditor({ title, message, children }: { title: string; message: string; children?: ReactNode }) {
  return <div className="space-y-2 rounded-md bg-muted/30 p-3"><p className="text-sm font-medium">{title}</p><p className="text-sm text-muted-foreground">{message}</p>{children}</div>;
}

function MunicipalitySearch({ value, initialQuery, onChange }: { value: MunicipalityCandidate | null; initialQuery: string; onChange: (value: MunicipalityCandidate) => void }) {
  const [query, setQuery] = useState(initialQuery);
  const [items, setItems] = useState<MunicipalityCandidate[]>([]);
  const [loading, setLoading] = useState(false);
  const debouncedQuery = useDebouncedValue(query, TEXT_SEARCH_DEBOUNCE_MS);
  useEffect(() => {
    if (debouncedQuery.trim().length < 2) return setItems([]);
    setLoading(true);
    void searchMunicipalityCandidates(debouncedQuery).then(setItems).finally(() => setLoading(false));
  }, [debouncedQuery]);
  const label = useMemo(() => value ? `${value.name}/${value.stateCode}` : null, [value]);
  return <div className="space-y-2"><div className="relative"><Search className="absolute left-2.5 top-2.5 size-4 text-muted-foreground" /><Input aria-label={`Município oficial de ${initialQuery}`} className="pl-8" value={query} onChange={(event) => setQuery(event.target.value)} /></div>{label && <p className="flex items-center gap-1 text-sm text-primary"><MapPin className="size-4" />Selecionado: {label}</p>}{loading && <p className="text-xs text-muted-foreground">Pesquisando...</p>}{!loading && items.length > 0 && <div className="max-h-36 overflow-y-auto rounded-md border border-border">{items.map((item) => <button type="button" key={item.ibgeCode} className="block w-full px-3 py-2 text-left text-sm hover:bg-muted" onClick={() => { onChange(item); setItems([]); setQuery(`${item.name}/${item.stateCode}`); }}>{item.name}/{item.stateCode}</button>)}</div>}</div>;
}
