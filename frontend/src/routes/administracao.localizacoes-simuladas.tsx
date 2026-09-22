import { useCallback, useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { AlertTriangle, Loader2, Play, RotateCcw, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import {
  fetchCoordinateSimulations,
  revertCoordinateSimulation,
  runCoordinateSimulation,
  type CoordinateSimulationResponse,
} from "@/lib/customer-coordinate-simulation-api";
import { isJobExecutionActive, JOB_STATUS_POLL_INTERVAL_MS } from "@/lib/job-runtime";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";

export const Route = createFileRoute("/administracao/localizacoes-simuladas")({
  component: SimulatedLocationsPage,
});

const formatter = new Intl.NumberFormat("pt-BR");

function SimulatedLocationsPage() {
  const [data, setData] = useState<CoordinateSimulationResponse | null>(null);
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, TEXT_SEARCH_DEBOUNCE_MS);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [action, setAction] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [runDialogOpen, setRunDialogOpen] = useState(false);

  const load = useCallback(async () => {
    try {
      setError("");
      setData(await fetchCoordinateSimulations(debouncedSearch, page));
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : "Falha ao carregar as localizações simuladas.",
      );
    } finally {
      setLoading(false);
    }
  }, [debouncedSearch, page]);

  useEffect(() => {
    setPage(1);
  }, [debouncedSearch]);
  useEffect(() => {
    void load();
  }, [load]);
  const hasActiveJob = data?.executions.some((job) => isJobExecutionActive(job.status)) ?? false;
  useEffect(() => {
    if (!hasActiveJob) return;
    const interval = window.setInterval(() => void load(), JOB_STATUS_POLL_INTERVAL_MS);
    return () => window.clearInterval(interval);
  }, [hasActiveJob, load]);

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil((data?.total ?? 0) / (data?.pageSize ?? 20))),
    [data],
  );

  async function run(recalculateDependents: boolean) {
    setRunDialogOpen(false);
    setAction("run");
    setError("");
    setMessage("");
    try {
      await runCoordinateSimulation(recalculateDependents);
      setMessage(
        recalculateDependents
          ? "Preenchimento e recálculos dependentes enviados para a Central de Processamentos."
          : "Preenchimento enviado sem solicitar recálculos dependentes.",
      );
      await load();
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setAction(null);
    }
  }

  async function revert(jobExecutionId: string) {
    setAction(jobExecutionId);
    setError("");
    setMessage("");
    try {
      await revertCoordinateSimulation(jobExecutionId);
      setMessage("Reversão enviada para a Central de Processamentos.");
      await load();
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setAction(null);
    }
  }

  return (
    <div className="page-shell">
      <div className="mx-auto w-full max-w-7xl space-y-6">
        <header className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <span className="page-header-kicker">Administração do sistema</span>
            <h1 className="mt-2 font-display text-3xl tracking-tight">Localizações simuladas</h1>
            <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
              Preencha clientes sem coordenada exata com um endereço predial real encontrado em até
              5 km do melhor ponto disponível.
            </p>
          </div>
          <Button
            disabled={action !== null || hasActiveJob || !data?.snapshotId}
            onClick={() => setRunDialogOpen(true)}
          >
            {action === "run" || hasActiveJob ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <Play className="size-4" />
            )}
            Preencher coordenadas ausentes
          </Button>
        </header>

        <Dialog open={runDialogOpen} onOpenChange={setRunDialogOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Atualizar cálculos dependentes?</DialogTitle>
              <DialogDescription>
                As coordenadas serão preenchidas nas duas opções. Escolha se, ao terminar, o sistema
                também deve executar os jobs que dependem delas. Isso otimiza as rotas usando
                as localizações exatas dos clientes e, em seguida, recalcula distância,
                combustível, pedágio e custo total.
              </DialogDescription>
            </DialogHeader>
            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <Button variant="outline" onClick={() => void run(false)}>
                Preencher sem recalcular
              </Button>
              <Button onClick={() => void run(true)}>Preencher e recalcular</Button>
            </div>
          </DialogContent>
        </Dialog>

        <div className="flex gap-3 rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-800 dark:text-amber-200">
          <AlertTriangle className="mt-0.5 size-5 shrink-0" />
          <p>
            Estes pontos são substitutos usados para validar os cálculos. Enquanto ativos,
            distância, combustível, pedágio, mapas e demais consumidores os tratam como coordenadas
            exatas. Toda alteração pode ser auditada e revertida abaixo.
          </p>
        </div>
        {error ? (
          <div
            role="alert"
            className="rounded-xl border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive"
          >
            {error}
          </div>
        ) : null}
        {message ? (
          <div
            role="status"
            className="rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-4 text-sm text-emerald-700 dark:text-emerald-300"
          >
            {message}
          </div>
        ) : null}

        <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {[
            ["Clientes", data?.summary.total ?? 0],
            ["Exatos", data?.summary.exact ?? 0],
            ["Simulados ativos", data?.summary.simulated ?? 0],
            ["Pendentes", data?.summary.pending ?? 0],
          ].map(([label, value]) => (
            <div key={String(label)} className="rounded-xl border border-border bg-surface p-4">
              <p className="text-xs uppercase tracking-wide text-muted-foreground">{label}</p>
              <p className="mt-2 text-2xl font-semibold">{formatter.format(Number(value))}</p>
            </div>
          ))}
        </section>

        <section className="space-y-3 rounded-xl border border-border bg-surface p-4">
          <h2 className="font-semibold">Execuções recentes</h2>
          {data?.executions.length ? (
            data.executions.map((job) => (
              <div
                key={job.id}
                className="flex flex-col gap-3 rounded-lg border border-border p-3 text-sm md:flex-row md:items-center md:justify-between"
              >
                <div>
                  <p className="font-medium">{job.progressMessage ?? job.status}</p>
                  <p className="text-xs text-muted-foreground">
                    {new Date(job.createdAt).toLocaleString("pt-BR")} · {job.progressPercent}%
                  </p>
                  {job.errorMessage ? (
                    <p className="mt-1 text-xs text-destructive">{job.errorMessage}</p>
                  ) : null}
                </div>
                {job.canRevert ? (
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={action !== null || hasActiveJob}
                    onClick={() => void revert(job.id)}
                  >
                    {action === job.id ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <RotateCcw className="size-4" />
                    )}
                    Reverter execução
                  </Button>
                ) : null}
              </div>
            ))
          ) : (
            <p className="text-sm text-muted-foreground">Nenhuma execução registrada.</p>
          )}
        </section>

        <section className="space-y-4">
          <div className="relative max-w-xl">
            <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              aria-label="Buscar clientes"
              className="pl-10"
              placeholder="Buscar cliente, código ou município"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
          <div className="overflow-x-auto rounded-xl border border-border bg-surface">
            <table className="w-full min-w-[1000px] text-sm">
              <thead className="bg-muted/40 text-left text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-3">Cliente</th>
                  <th className="px-4 py-3">Município</th>
                  <th className="px-4 py-3">Endereço cadastral</th>
                  <th className="px-4 py-3">Coordenada ativa</th>
                  <th className="px-4 py-3">Substituto / distância</th>
                  <th className="px-4 py-3">Situação</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {data?.items.map((item) => (
                  <tr key={item.customerId} className="align-top">
                    <td className="px-4 py-3">
                      <p className="font-medium">{item.name}</p>
                      <p className="text-xs text-muted-foreground">{item.externalCode}</p>
                    </td>
                    <td className="px-4 py-3">
                      {item.municipality}/{item.stateCode}
                    </td>
                    <td className="max-w-xs px-4 py-3 text-xs text-muted-foreground">
                      {item.registeredAddress}
                    </td>
                    <td className="px-4 py-3 text-xs">
                      {item.latitude !== null
                        ? `${item.latitude}, ${item.longitude}`
                        : "Indisponível"}
                      <p className="text-muted-foreground">
                        {item.source ?? item.precision ?? "Sem origem"}
                      </p>
                    </td>
                    <td className="max-w-sm px-4 py-3 text-xs">
                      {item.simulatedAddress ?? "—"}
                      {item.distanceMeters !== null ? (
                        <p className="text-muted-foreground">
                          {formatter.format(Math.round(item.distanceMeters))} m do ponto-base
                        </p>
                      ) : null}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={`inline-flex rounded-full px-2 py-1 text-xs font-medium ${item.isSimulated ? "bg-amber-500/10 text-amber-700 dark:text-amber-300" : item.precision === "EXACT" ? "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300" : "bg-muted text-muted-foreground"}`}
                      >
                        {item.isSimulated
                          ? "Simulada"
                          : item.precision === "EXACT"
                            ? "Exata"
                            : "Pendente"}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {!loading && !data?.items.length ? (
              <p className="p-6 text-center text-sm text-muted-foreground">
                Nenhum cliente encontrado.
              </p>
            ) : null}
          </div>
          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground">
              Página {page} de {totalPages}
            </span>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage((value) => value - 1)}
              >
                Anterior
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= totalPages}
                onClick={() => setPage((value) => value + 1)}
              >
                Próxima
              </Button>
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}
