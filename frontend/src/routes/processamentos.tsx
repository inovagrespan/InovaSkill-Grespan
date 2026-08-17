import { useEffect, useRef, useState } from "react";
import { createFileRoute, redirect } from "@tanstack/react-router";
import {
  Activity,
  AlertTriangle,
  CheckCircle2,
  Clock,
  Database,
  CalendarClock,
  Copy,
  Loader2,
  Play,
  RefreshCw,
  StopCircle,
} from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { DashboardKpiCard } from "@/components/ui/dashboard-kpi-card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { FeedbackMessage } from "@/components/ui/feedback-message";
import { SkeletonMetricCard, SkeletonModalContent, SkeletonTable } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  cancelAdminJob,
  fetchAdminJobDetail,
  fetchAdminJobs,
  fetchAdminJobsSummary,
  fetchOperationalJobDefinitions,
  fetchJobSchedules,
  saveJobSchedule,
  setJobScheduleActive,
  deleteJobSchedule,
  retryAdminJob,
  retryAdminJobWithParameters,
  runOperationalJob,
  type AdminJobItem,
  type AdminJobSummary,
  type OperationalJobDefinition,
  type JobSchedule,
} from "@/lib/importer-api";
import { canCurrentUserAccessProcessingArea } from "@/lib/auth";

export const Route = createFileRoute("/processamentos")({
  beforeLoad: () => {
    if (!canCurrentUserAccessProcessingArea()) {
      throw redirect({ to: "/" });
    }
  },
  component: ProcessamentosPage,
});

const PROCESSING_REFRESH_INTERVAL_MS = 5_000;

function statusLabel(status: string): string {
  const labels: Record<string, string> = {
    Queued: "Na fila",
    Processing: "Processando",
    Retrying: "Re-tentando",
    Completed: "Concluído",
    Failed: "Falha",
    Cancelled: "Cancelado",
  };
  return labels[status] ?? status;
}

function statusVariant(status: string): "default" | "secondary" | "destructive" | "outline" {
  if (status === "Completed") return "default";
  if (status === "Failed") return "destructive";
  if (status === "Processing" || status === "Queued" || status === "Retrying") return "secondary";
  return "outline";
}

function formatDate(value?: string | null): string {
  if (!value) return "-";
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? value : d.toLocaleString("pt-BR");
}

function formatDuration(seconds: number | null): string {
  if (seconds == null || !Number.isFinite(seconds) || seconds <= 0) return "-";
  const total = Math.round(seconds);
  const m = Math.floor(total / 60);
  const s = total % 60;
  if (m <= 0) return `${s}s`;
  const h = Math.floor(m / 60);
  if (h <= 0) return `${m}m ${s}s`;
  return `${h}h ${m % 60}m`;
}

function formatInteger(value: number): string {
  return new Intl.NumberFormat("pt-BR").format(value);
}

function ProcessamentosPage() {
  const [summary, setSummary] = useState<AdminJobSummary | null>(null);
  const [jobs, setJobs] = useState<AdminJobItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const [messageType, setMessageType] = useState<"error" | "success">("success");
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [definitions, setDefinitions] = useState<OperationalJobDefinition[]>([]);
  const [schedules, setSchedules] = useState<JobSchedule[]>([]);
  const pageSize = 20;
  const pageRef = useRef(1);
  const loadingRef = useRef(false);

  const [selectedJob, setSelectedJob] = useState<AdminJobItem | null>(null);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [retryingId, setRetryingId] = useState<string | null>(null);
  const [cancellingId, setCancellingId] = useState<string | null>(null);
  const [runningDefinitionType, setRunningDefinitionType] = useState<string | null>(null);
  const [definitionToRun, setDefinitionToRun] = useState<OperationalJobDefinition | null>(null);
  const [parametersJson, setParametersJson] = useState("{}");
  const [jsonError, setJsonError] = useState("");
  const [retrySourceId, setRetrySourceId] = useState<string | null>(null);
  const [scheduleOpen, setScheduleOpen] = useState(false);
  const [editingSchedule, setEditingSchedule] = useState<JobSchedule | null>(null);
  const [scheduleName, setScheduleName] = useState("");
  const [scheduleCron, setScheduleCron] = useState("0 6 * * *");
  const [scheduleTimeZone, setScheduleTimeZone] = useState("America/Sao_Paulo");
  const [scheduleDefinition, setScheduleDefinition] = useState<OperationalJobDefinition | null>(
    null,
  );
  const [scheduleParameters, setScheduleParameters] = useState("{}");

  async function loadData(p: number = pageRef.current) {
    if (loadingRef.current) return;
    loadingRef.current = true;
    try {
      const [summaryData, jobsData, definitionsData, schedulesData] = await Promise.all([
        fetchAdminJobsSummary(),
        fetchAdminJobs(p, pageSize, statusFilter ? { status: statusFilter } : undefined),
        fetchOperationalJobDefinitions(),
        fetchJobSchedules(),
      ]);
      setSummary(summaryData);
      setJobs(jobsData.items);
      setDefinitions(definitionsData);
      setSchedules(schedulesData);
      setTotal(jobsData.total);
      setPage(jobsData.page);
      pageRef.current = jobsData.page;
    } catch (error) {
      setMessageType("error");
      setMessage((error as Error).message);
    } finally {
      loadingRef.current = false;
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadData(1);
  }, [statusFilter]);

  useEffect(() => {
    const refreshTimer = window.setInterval(() => {
      void loadData(pageRef.current);
    }, PROCESSING_REFRESH_INTERVAL_MS);
    return () => window.clearInterval(refreshTimer);
  }, [statusFilter]);

  async function handleRetry(jobId: string) {
    setRetryingId(jobId);
    setMessage("");
    try {
      await retryAdminJob(jobId);
      setMessageType("success");
      setMessage("Job reenfileirado com sucesso.");
      await loadData(pageRef.current);
      if (selectedJob?.id === jobId) {
        const updated = await fetchAdminJobDetail(jobId);
        setSelectedJob(updated);
      }
    } catch (error) {
      setMessageType("error");
      setMessage((error as Error).message);
    } finally {
      setRetryingId(null);
    }
  }

  async function handleCancel(jobId: string) {
    setCancellingId(jobId);
    setMessage("");
    try {
      await cancelAdminJob(jobId);
      setMessageType("success");
      setMessage("Job cancelado com sucesso.");
      await loadData(pageRef.current);
      if (selectedJob?.id === jobId || detailsOpen) {
        setDetailsOpen(false);
        setSelectedJob(null);
      }
    } catch (error) {
      setMessageType("error");
      setMessage((error as Error).message);
    } finally {
      setCancellingId(null);
    }
  }

  async function handleRunDefinition() {
    if (!definitionToRun) return;
    let parameters: unknown;
    try {
      parameters = JSON.parse(parametersJson);
      setJsonError("");
    } catch {
      setJsonError("Informe um objeto JSON válido.");
      return;
    }
    setRunningDefinitionType(definitionToRun.jobType);
    setMessage("");
    try {
      if (retrySourceId)
        await retryAdminJobWithParameters(
          retrySourceId,
          definitionToRun.contractVersion,
          parameters,
        );
      else
        await runOperationalJob(
          definitionToRun.jobType,
          definitionToRun.contractVersion,
          parameters,
        );
      setMessageType("success");
      setMessage("Job operacional enfileirado com sucesso.");
      await loadData(pageRef.current);
      setDefinitionToRun(null);
      setRetrySourceId(null);
    } catch (error) {
      setMessageType("error");
      setMessage((error as Error).message);
    } finally {
      setRunningDefinitionType(null);
    }
  }

  function openRunDialog(definition: OperationalJobDefinition) {
    setDefinitionToRun(definition);
    setRetrySourceId(null);
    setParametersJson(formatJson(definition.exampleParametersJson));
    setJsonError("");
  }

  function openScheduleDialog(schedule?: JobSchedule) {
    const definition = schedule
      ? (definitions.find((item) => item.jobType === schedule.jobType) ?? null)
      : (definitions.find((item) => item.scheduleAllowed) ?? null);
    setEditingSchedule(schedule ?? null);
    setScheduleDefinition(definition);
    setScheduleName(schedule?.name ?? "");
    setScheduleCron(schedule?.cronExpression ?? "0 6 * * *");
    setScheduleTimeZone(schedule?.timeZoneId ?? "America/Sao_Paulo");
    setScheduleParameters(
      formatJson(schedule?.parametersJson ?? definition?.exampleParametersJson ?? "{}"),
    );
    setScheduleOpen(true);
  }

  async function handleSaveSchedule() {
    if (!scheduleDefinition) return;
    try {
      JSON.parse(scheduleParameters);
      await saveJobSchedule(
        {
          name: scheduleName,
          jobType: scheduleDefinition.jobType,
          contractVersion: scheduleDefinition.contractVersion,
          parametersJson: scheduleParameters,
          cronExpression: scheduleCron,
          timeZoneId: scheduleTimeZone,
          isActive: editingSchedule?.isActive ?? true,
        },
        editingSchedule?.id,
      );
      setScheduleOpen(false);
      setMessageType("success");
      setMessage("Agendamento salvo com sucesso.");
      await loadData(pageRef.current);
    } catch (error) {
      setMessageType("error");
      setMessage((error as Error).message);
    }
  }

  async function openJob(job: AdminJobItem) {
    setSelectedJob(job);
    setDetailsOpen(true);
    try {
      const detail = await fetchAdminJobDetail(job.id);
      setSelectedJob(detail);
    } catch {
      // keep basic info
    }
  }

  const pageCount = Math.max(1, Math.ceil(total / pageSize));

  const summaryCards = summary
    ? [
        { title: "Na fila", value: formatInteger(summary.queuedNow), icon: Clock },
        { title: "Processando", value: formatInteger(summary.processingNow), icon: Activity },
        {
          title: "Concluídos (24h)",
          value: formatInteger(summary.completedLast24Hours),
          icon: CheckCircle2,
        },
        {
          title: "Falhas (24h)",
          value: formatInteger(summary.failedLast24Hours),
          icon: AlertTriangle,
        },
        {
          title: "Taxa de sucesso",
          value: `${summary.successRatePercent.toFixed(1)}%`,
          icon: Database,
        },
        {
          title: "Tempo médio",
          value: formatDuration(summary.averageProcessingSeconds),
          icon: Clock,
        },
      ]
    : [];

  return (
    <div className="page-shell">
      <header className="animate-soft-enter space-y-2">
        <span className="page-header-kicker">Processamentos</span>
        <div className="mt-2 flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div>
            <h1 className="text-4xl font-display tracking-tight">Central de Processamentos</h1>
            <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
              Monitore jobs de processamento de importações, taxas de sucesso e reenvie jobs com
              falha.
            </p>
          </div>
          <Button
            variant="outline"
            onClick={() => void loadData(pageRef.current)}
            disabled={loading}
          >
            <RefreshCw className="mr-2 size-4" />
            Atualizar
          </Button>
        </div>
      </header>

      <FeedbackMessage message={message} type={messageType} />

      <Tabs defaultValue="monitoring" className="space-y-6">
        <TabsList>
          <TabsTrigger value="monitoring">Monitoramento</TabsTrigger>
          <TabsTrigger value="services">Serviços disponíveis</TabsTrigger>
        </TabsList>

        <TabsContent value="monitoring" className="space-y-6">
          <section className="metric-row">
            {loading && !summary
              ? Array.from({ length: 6 }).map((_, i) => <SkeletonMetricCard key={i} />)
              : summaryCards.map((card) => (
                  <DashboardKpiCard
                    key={card.title}
                    title={card.title}
                    value={card.value}
                    icon={card.icon}
                    className="metric-card-item bg-surface border-border"
                  />
                ))}
          </section>

          <Card className="bg-surface border-border">
            <CardHeader>
              <CardTitle>Jobs de Processamento</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="mb-4 flex items-center gap-2">
                <span className="text-xs text-muted-foreground">Filtrar por status:</span>
                {["", "Queued", "Processing", "Completed", "Failed"].map((s) => (
                  <Button
                    key={s}
                    size="sm"
                    variant={statusFilter === s ? "default" : "outline"}
                    onClick={() => {
                      setStatusFilter(s);
                      setPage(1);
                      pageRef.current = 1;
                    }}
                  >
                    {s === "" ? "Todos" : statusLabel(s)}
                  </Button>
                ))}
              </div>

              {loading && !jobs.length ? (
                <SkeletonTable rows={8} columns={7} />
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[700px] text-sm">
                    <thead className="text-xs text-muted-foreground">
                      <tr className="border-b border-border">
                        <th className="py-2 pr-3 text-left font-medium">Job</th>
                        <th className="py-2 pr-3 text-left font-medium">Tipo</th>
                        <th className="py-2 pr-3 text-left font-medium">Arquivo</th>
                        <th className="py-2 pr-3 text-left font-medium">Status</th>
                        <th className="py-2 pr-3 text-left font-medium">Tentativas</th>
                        <th className="py-2 pr-3 text-left font-medium">Criado em</th>
                        <th className="py-2 text-left font-medium">Duração</th>
                      </tr>
                    </thead>
                    <tbody>
                      {jobs.map((job) => (
                        <tr
                          key={job.id}
                          className="cursor-pointer border-b border-border/60 hover:bg-muted/40"
                          onClick={() => void openJob(job)}
                        >
                          <td className="py-3 pr-3 font-mono text-xs">{job.id.slice(0, 8)}</td>
                          <td className="py-3 pr-3 text-xs">{job.jobType}</td>
                          <td className="max-w-[220px] truncate py-3 pr-3">{job.importFileName}</td>
                          <td className="py-3 pr-3">
                            <Badge variant={statusVariant(job.status)}>
                              {statusLabel(job.status)}
                            </Badge>
                          </td>
                          <td className="py-3 pr-3 text-xs text-muted-foreground">
                            {job.attempts}
                          </td>
                          <td className="py-3 pr-3 text-xs text-muted-foreground whitespace-nowrap">
                            {formatDate(job.createdAt)}
                          </td>
                          <td className="py-3 text-xs text-muted-foreground">
                            {formatDuration(job.durationSeconds)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {!loading && jobs.length === 0 && (
                <p className="py-6 text-sm text-muted-foreground">Nenhum job encontrado.</p>
              )}

              <div className="flex items-center justify-end gap-2 pt-4">
                <Button
                  size="sm"
                  variant="outline"
                  disabled={page <= 1}
                  onClick={() => void loadData(page - 1)}
                >
                  Anterior
                </Button>
                <span className="text-xs text-muted-foreground">
                  Página {page} de {pageCount}
                </span>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={page >= pageCount}
                  onClick={() => void loadData(page + 1)}
                >
                  Próxima
                </Button>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="services">
          <Card className="bg-surface border-border">
            <CardHeader>
              <CardTitle>Serviços que podem ser executados</CardTitle>
              <p className="text-sm text-muted-foreground">
                Inicie manualmente serviços operacionais disponíveis para processamento em segundo
                plano.
              </p>
            </CardHeader>
            <CardContent>
              {definitions.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  Nenhum serviço disponível para execução.
                </p>
              ) : (
                <div className="grid gap-3 md:grid-cols-2">
                  {definitions.map((definition) => (
                    <article
                      key={definition.jobType}
                      className="rounded-lg border border-border p-4"
                    >
                      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <p className="font-semibold">{definition.displayName}</p>
                            {definition.currentlyRunning && (
                              <Badge variant="secondary">Em andamento</Badge>
                            )}
                          </div>
                          <p className="mt-1 text-sm text-muted-foreground">
                            {definition.description}
                          </p>
                          <p className="mt-2 font-mono text-xs text-muted-foreground">
                            {definition.jobType}
                          </p>
                        </div>
                        <Button
                          size="sm"
                          disabled={
                            !definition.manualRunAllowed ||
                            runningDefinitionType === definition.jobType
                          }
                          onClick={() => openRunDialog(definition)}
                        >
                          {runningDefinitionType === definition.jobType ? (
                            <Loader2 className="mr-2 size-4 animate-spin" />
                          ) : (
                            <Play className="mr-2 size-4" />
                          )}
                          Executar agora
                        </Button>
                      </div>
                    </article>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          <Card className="mt-6 bg-surface border-border">
            <CardHeader className="flex flex-row items-center justify-between">
              <div>
                <CardTitle>Agendamentos</CardTitle>
                <p className="mt-1 text-sm text-muted-foreground">
                  Execuções recorrentes em cron, com fuso horário explícito.
                </p>
              </div>
              <Button size="sm" onClick={() => openScheduleDialog()}>
                <CalendarClock className="mr-2 size-4" /> Novo agendamento
              </Button>
            </CardHeader>
            <CardContent className="space-y-3">
              {schedules.length === 0 && (
                <p className="text-sm text-muted-foreground">Nenhum agendamento configurado.</p>
              )}
              {schedules.map((schedule) => (
                <article
                  key={schedule.id}
                  className="flex flex-col gap-3 rounded-lg border border-border p-4 md:flex-row md:items-center md:justify-between"
                >
                  <div>
                    <div className="flex items-center gap-2">
                      <p className="font-semibold">{schedule.name}</p>
                      <Badge variant={schedule.isActive ? "default" : "outline"}>
                        {schedule.isActive ? "Ativo" : "Pausado"}
                      </Badge>
                    </div>
                    <p className="mt-1 font-mono text-xs text-muted-foreground">
                      {schedule.jobType} · {schedule.cronExpression} · {schedule.timeZoneId}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Próxima execução: {formatDate(schedule.nextExecutionAt)}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => openScheduleDialog(schedule)}
                    >
                      Editar
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={async () => {
                        await setJobScheduleActive(schedule.id, !schedule.isActive);
                        await loadData(pageRef.current);
                      }}
                    >
                      {schedule.isActive ? "Pausar" : "Ativar"}
                    </Button>
                    <Button
                      size="sm"
                      variant="destructive"
                      onClick={async () => {
                        await deleteJobSchedule(schedule.id);
                        await loadData(pageRef.current);
                      }}
                    >
                      Excluir
                    </Button>
                  </div>
                </article>
              ))}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <Dialog
        open={definitionToRun !== null}
        onOpenChange={(open) => !open && setDefinitionToRun(null)}
      >
        <DialogContent className="max-w-2xl border-border bg-surface">
          <DialogHeader>
            <DialogTitle>Executar {definitionToRun?.displayName}</DialogTitle>
            <DialogDescription>
              Contrato v{definitionToRun?.contractVersion}. O JSON será validado e persistido sem
              mascaramento.
            </DialogDescription>
          </DialogHeader>
          <textarea
            aria-label="Parâmetros JSON"
            className="min-h-64 w-full rounded-md border border-border bg-background p-3 font-mono text-sm"
            value={parametersJson}
            onChange={(event) => setParametersJson(event.target.value)}
          />
          {jsonError && <p className="text-sm text-destructive">{jsonError}</p>}
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setDefinitionToRun(null)}>
              Cancelar
            </Button>
            <Button
              onClick={() => void handleRunDefinition()}
              disabled={runningDefinitionType !== null}
            >
              Executar agora
            </Button>
          </div>
        </DialogContent>
      </Dialog>

      <Dialog open={scheduleOpen} onOpenChange={setScheduleOpen}>
        <DialogContent className="max-w-2xl border-border bg-surface">
          <DialogHeader>
            <DialogTitle>{editingSchedule ? "Editar" : "Novo"} agendamento</DialogTitle>
            <DialogDescription>
              Use uma expressão cron de cinco campos. Horários perdidos não serão recuperados.
            </DialogDescription>
          </DialogHeader>
          <label className="space-y-1 text-sm">
            <span>Nome</span>
            <input
              className="w-full rounded-md border border-border bg-background p-2"
              value={scheduleName}
              onChange={(event) => setScheduleName(event.target.value)}
            />
          </label>
          <label className="space-y-1 text-sm">
            <span>Serviço</span>
            <select
              className="w-full rounded-md border border-border bg-background p-2"
              value={scheduleDefinition?.jobType ?? ""}
              onChange={(event) => {
                const definition =
                  definitions.find((item) => item.jobType === event.target.value) ?? null;
                setScheduleDefinition(definition);
                setScheduleParameters(formatJson(definition?.exampleParametersJson ?? "{}"));
              }}
            >
              {definitions
                .filter((item) => item.scheduleAllowed)
                .map((item) => (
                  <option key={item.jobType} value={item.jobType}>
                    {item.displayName}
                  </option>
                ))}
            </select>
          </label>
          <div className="grid gap-3 md:grid-cols-2">
            <label className="space-y-1 text-sm">
              <span>Cron</span>
              <input
                className="w-full rounded-md border border-border bg-background p-2 font-mono"
                value={scheduleCron}
                onChange={(event) => setScheduleCron(event.target.value)}
              />
            </label>
            <label className="space-y-1 text-sm">
              <span>Fuso horário</span>
              <input
                className="w-full rounded-md border border-border bg-background p-2"
                value={scheduleTimeZone}
                onChange={(event) => setScheduleTimeZone(event.target.value)}
              />
            </label>
          </div>
          <label className="space-y-1 text-sm">
            <span>Parâmetros JSON</span>
            <textarea
              className="min-h-48 w-full rounded-md border border-border bg-background p-3 font-mono text-sm"
              value={scheduleParameters}
              onChange={(event) => setScheduleParameters(event.target.value)}
            />
          </label>
          <p className="text-xs text-muted-foreground">
            Não inclua segredos sem necessidade: o conteúdo é armazenado em texto claro.
          </p>
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => setScheduleOpen(false)}>
              Cancelar
            </Button>
            <Button onClick={() => void handleSaveSchedule()}>Salvar</Button>
          </div>
        </DialogContent>
      </Dialog>

      <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="max-h-[calc(100dvh-2rem)] max-w-2xl overflow-x-hidden overflow-y-auto border-border bg-surface">
          <DialogHeader>
            <DialogTitle>Detalhes do Job</DialogTitle>
            <DialogDescription>Informações completas do job de processamento.</DialogDescription>
          </DialogHeader>
          {!selectedJob && <SkeletonModalContent />}
          {selectedJob && (
            <div className="space-y-4">
              <div className="grid grid-cols-1 gap-3 text-sm md:grid-cols-2">
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">ID do Job</p>
                  <p className="break-all font-mono text-xs font-medium">{selectedJob.id}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Tipo</p>
                  <p className="break-all font-medium">{selectedJob.jobType}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Arquivo</p>
                  <p className="font-medium">{selectedJob.importFileName}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Status</p>
                  <Badge variant={statusVariant(selectedJob.status)}>
                    {statusLabel(selectedJob.status)}
                  </Badge>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Tentativas</p>
                  <p className="font-medium">{selectedJob.attempts}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Duração</p>
                  <p className="font-medium">{formatDuration(selectedJob.durationSeconds)}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Criado em</p>
                  <p className="font-medium">{formatDate(selectedJob.createdAt)}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Iniciado em</p>
                  <p className="font-medium">{formatDate(selectedJob.startedAt)}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Finalizado em</p>
                  <p className="font-medium">{formatDate(selectedJob.finishedAt)}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">ID da Importação</p>
                  <p className="break-all font-mono text-xs font-medium">
                    {selectedJob.importId}
                  </p>
                </div>
              </div>

              {selectedJob.errorMessage && (
                <Alert variant="destructive">
                  <AlertTriangle className="size-4" />
                  <AlertDescription className="whitespace-pre-wrap text-sm">
                    {selectedJob.errorMessage}
                  </AlertDescription>
                </Alert>
              )}

              {selectedJob.parametersJson && (
                <div className="space-y-2 rounded-lg border border-border p-3">
                  <div className="flex items-center justify-between">
                    <p className="text-sm font-medium">
                      Parâmetros · contrato v{selectedJob.contractVersion}
                    </p>
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() =>
                        void navigator.clipboard.writeText(selectedJob.parametersJson ?? "")
                      }
                    >
                      <Copy className="mr-2 size-4" />
                      Copiar
                    </Button>
                  </div>
                  <pre className="max-h-48 overflow-auto whitespace-pre-wrap text-xs">
                    {formatJson(selectedJob.parametersJson)}
                  </pre>
                </div>
              )}
              {selectedJob.resultJson && (
                <div className="space-y-2 rounded-lg border border-border p-3">
                  <p className="text-sm font-medium">Resultado</p>
                  <pre className="max-h-48 overflow-auto whitespace-pre-wrap text-xs">
                    {formatJson(selectedJob.resultJson)}
                  </pre>
                </div>
              )}

              <div className="flex gap-2">
                {selectedJob.status === "Failed" && (
                  <>
                    <Button
                      size="sm"
                      onClick={() => handleRetry(selectedJob.id)}
                      disabled={retryingId === selectedJob.id}
                    >
                      {retryingId === selectedJob.id ? (
                        <Loader2 className="mr-2 size-4 animate-spin" />
                      ) : (
                        <RefreshCw className="mr-2 size-4" />
                      )}
                      Reenviar igual
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => {
                        const definition = definitions.find(
                          (item) => item.jobType === selectedJob.jobType,
                        );
                        if (!definition) return;
                        setRetrySourceId(selectedJob.id);
                        setParametersJson(
                          formatJson(
                            selectedJob.parametersJson ?? definition.exampleParametersJson,
                          ),
                        );
                        setDefinitionToRun(definition);
                        setDetailsOpen(false);
                      }}
                    >
                      Editar e executar novamente
                    </Button>
                  </>
                )}
                {(selectedJob.status === "Processing" ||
                  selectedJob.status === "Retrying" ||
                  selectedJob.status === "Queued") && (
                  <Button
                    size="sm"
                    variant="destructive"
                    onClick={() => handleCancel(selectedJob.id)}
                    disabled={cancellingId === selectedJob.id}
                  >
                    {cancellingId === selectedJob.id ? (
                      <Loader2 className="mr-2 size-4 animate-spin" />
                    ) : (
                      <StopCircle className="mr-2 size-4" />
                    )}
                    Cancelar Job
                  </Button>
                )}
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function formatJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}
