import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Activity, AlertTriangle, CheckCircle2, Clock, Database, Download, ListChecks, PauseCircle, PlayCircle, RefreshCw, Server } from "lucide-react";
import { Bar, BarChart, CartesianGrid, Cell, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Progress } from "@/components/ui/progress";
import { SkeletonCard, SkeletonChart, SkeletonMetricCard, SkeletonModalContent, SkeletonTable } from "@/components/ui/skeleton";
import {
  cancelProcessingJob,
  fetchProcessingJobDetails,
  fetchProcessingMonitoringDashboard,
  retryProcessingJob,
  type ProcessingJobDetails,
  type ProcessingJobQueueItem,
  type ProcessingMonitoringDashboard,
} from "@/lib/importer-api";
import { clampProgressPercent } from "@/lib/importer-progress";

export const Route = createFileRoute("/processamentos")({
  component: ProcessamentosPage,
});

const chartColors = ["#b4232f", "#2563eb", "#16a34a", "#d97706", "#7c3aed", "#0891b2"];

function ProcessamentosPage() {
  const [dashboard, setDashboard] = useState<ProcessingMonitoringDashboard | null>(null);
  const [selectedJob, setSelectedJob] = useState<ProcessingJobDetails | null>(null);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(true);

  async function loadDashboard() {
    try {
      const data = await fetchProcessingMonitoringDashboard();
      setDashboard(data);
      setMessage("");
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  async function openJob(jobId: number) {
    try {
      const details = await fetchProcessingJobDetails(jobId);
      setSelectedJob(details);
      setDetailsOpen(true);
    } catch (error) {
      setMessage((error as Error).message);
    }
  }

  async function runAction(action: () => Promise<void>) {
    try {
      await action();
      await loadDashboard();
      if (selectedJob) {
        setSelectedJob(await fetchProcessingJobDetails(selectedJob.job.id));
      }
    } catch (error) {
      setMessage((error as Error).message);
    }
  }

  useEffect(() => {
    void loadDashboard();
    const interval = setInterval(() => void loadDashboard(), 3000);
    return () => clearInterval(interval);
  }, []);

  const summaryCards = useMemo(() => {
    const summary = dashboard?.summary;
    return [
      { title: "Jobs em execução", value: formatInteger(summary?.runningJobs ?? 0), icon: PlayCircle },
      { title: "Jobs na fila", value: formatInteger(summary?.queuedJobs ?? 0), icon: ListChecks },
      { title: "Concluídos hoje", value: formatInteger(summary?.completedToday ?? 0), icon: CheckCircle2 },
      { title: "Jobs com erro", value: formatInteger(summary?.failedJobs ?? 0), icon: AlertTriangle },
      { title: "Tempo médio", value: formatDuration(summary?.averageProcessingSeconds ?? 0), icon: Clock },
      { title: "Linhas hoje", value: formatInteger(summary?.processedRowsToday ?? 0), icon: Database },
    ];
  }, [dashboard]);

  return (
    <div className="page-shell">
      <header className="animate-soft-enter">
        <span className="page-header-kicker">Smart Core / Processamentos</span>
        <div className="mt-2 flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div>
            <h1 className="text-4xl font-display tracking-tight">Central de Processamentos</h1>
            <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
              Monitore filas, jobs em execução, falhas, performance por etapa e saúde dos workers.
            </p>
          </div>
          <Button variant="outline" onClick={() => void loadDashboard()} disabled={loading}>
            <RefreshCw className="mr-2 size-4" />
            Atualizar
          </Button>
        </div>
      </header>

      {message && (
        <Alert variant="destructive">
          <AlertTriangle className="size-4" />
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <section className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-6">
        {loading && !dashboard ? Array.from({ length: 6 }).map((_, index) => <SkeletonMetricCard key={index} />) : summaryCards.map((card) => (
          <Card key={card.title} className="bg-surface border-border">
            <CardContent className="p-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <p className="text-xs text-muted-foreground">{card.title}</p>
                  <p className="mt-1 text-2xl font-display font-semibold">{card.value}</p>
                </div>
                <span className="inline-flex size-9 items-center justify-center rounded-md bg-primary/10 text-primary">
                  <card.icon className="size-4" />
                </span>
              </div>
            </CardContent>
          </Card>
        ))}
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.4fr_0.8fr]">
        <Card className="bg-surface border-border">
          <CardHeader>
            <CardTitle>Fila de Processamento</CardTitle>
          </CardHeader>
          <CardContent>
            {loading && !dashboard ? (
              <SkeletonTable rows={8} columns={9} />
            ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[900px] text-sm">
                <thead className="text-xs text-muted-foreground">
                  <tr className="border-b border-border">
                    <th className="py-2 pr-3 text-left font-medium">Id Job</th>
                    <th className="py-2 pr-3 text-left font-medium">Empresa</th>
                    <th className="py-2 pr-3 text-left font-medium">Arquivo</th>
                    <th className="py-2 pr-3 text-left font-medium">Template</th>
                    <th className="py-2 pr-3 text-left font-medium">Status</th>
                    <th className="py-2 pr-3 text-left font-medium">Etapa atual</th>
                    <th className="py-2 pr-3 text-left font-medium">Progresso</th>
                    <th className="py-2 pr-3 text-left font-medium">Criado em</th>
                    <th className="py-2 text-left font-medium">Tempo</th>
                  </tr>
                </thead>
                <tbody>
                  {(dashboard?.jobs ?? []).map((job) => (
                    <tr key={job.id} className="cursor-pointer border-b border-border/60 hover:bg-muted/40" onClick={() => void openJob(job.id)}>
                      <td className="py-3 pr-3 font-mono text-xs">#{job.id}</td>
                      <td className="py-3 pr-3">{job.company}</td>
                      <td className="max-w-[220px] truncate py-3 pr-3">{job.fileName}</td>
                      <td className="py-3 pr-3">{job.template ?? "-"}</td>
                      <td className="py-3 pr-3"><Badge variant={statusVariant(job.status)}>{job.statusLabel || job.status}</Badge></td>
                      <td className="max-w-[220px] truncate py-3 pr-3 text-muted-foreground">{job.currentStep}</td>
                      <td className="w-[160px] py-3 pr-3">
                        <div className="flex items-center gap-2">
                          <Progress value={clampProgressPercent(job.progressPercent)} className="h-1.5" />
                          <span className="w-9 text-right font-mono text-xs text-muted-foreground">{clampProgressPercent(job.progressPercent)}%</span>
                        </div>
                      </td>
                      <td className="py-3 pr-3 text-xs text-muted-foreground">{formatDate(job.createdAt)}</td>
                      <td className="py-3 text-xs text-muted-foreground">{formatDuration(job.elapsedSeconds)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            )}
            {!loading && !dashboard?.jobs.length && <p className="py-6 text-sm text-muted-foreground">Nenhum job encontrado.</p>}
          </CardContent>
        </Card>

        <Card className="bg-surface border-border">
          <CardHeader>
            <CardTitle className="flex items-center gap-2"><Server className="size-5 text-primary" /> Worker Health</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading && !dashboard ? (
              <>
                <SkeletonCard lines={3} />
                <SkeletonCard lines={3} />
              </>
            ) : (dashboard?.workers ?? []).map((worker) => (
              <div key={worker.workerId} className="rounded-lg border border-border p-3">
                <div className="flex items-center justify-between gap-2">
                  <p className="font-medium">{worker.workerId}</p>
                  <Badge variant={worker.status === "Online" ? "default" : "destructive"}>{worker.status}</Badge>
                </div>
                <div className="mt-2 grid grid-cols-2 gap-2 text-xs text-muted-foreground">
                  <span>Último heartbeat: {formatDuration(worker.secondsSinceLastSeen)} atrás</span>
                  <span>Jobs hoje: {formatInteger(worker.processedJobsToday)}</span>
                  <span>Ocioso: {formatDuration(worker.idleSeconds)}</span>
                  <span>{worker.currentTask || "Sem atividade"}</span>
                </div>
              </div>
            ))}
            {!loading && !dashboard?.workers.length && <p className="text-sm text-muted-foreground">Nenhum heartbeat de worker registrado.</p>}
          </CardContent>
        </Card>
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <ChartCard title="Tempo médio de processamento">
          {loading && !dashboard ? <SkeletonChart /> : (
          <ResponsiveContainer width="100%" height={220}>
            <LineChart data={dashboard?.daily ?? []}>
              <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.25)" />
              <XAxis dataKey="date" tickFormatter={formatShortDate} tick={{ fontSize: 11 }} />
              <YAxis tickFormatter={(value) => formatDuration(Number(value))} tick={{ fontSize: 11 }} />
              <Tooltip formatter={(value) => formatDuration(Number(value))} labelFormatter={formatDate} />
              <Line type="monotone" dataKey="averageProcessingSeconds" stroke="#b4232f" strokeWidth={2} dot={false} />
            </LineChart>
          </ResponsiveContainer>
          )}
        </ChartCard>

        <ChartCard title="Jobs por dia">
          {loading && !dashboard ? <SkeletonChart /> : (
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={dashboard?.daily ?? []}>
              <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.25)" />
              <XAxis dataKey="date" tickFormatter={formatShortDate} tick={{ fontSize: 11 }} />
              <YAxis tick={{ fontSize: 11 }} />
              <Tooltip labelFormatter={formatDate} />
              <Bar dataKey="jobs" fill="#2563eb" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
          )}
        </ChartCard>

        <ChartCard title="Linhas processadas por dia">
          {loading && !dashboard ? <SkeletonChart /> : (
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={dashboard?.daily ?? []}>
              <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.25)" />
              <XAxis dataKey="date" tickFormatter={formatShortDate} tick={{ fontSize: 11 }} />
              <YAxis tickFormatter={(value) => formatCompact(Number(value))} tick={{ fontSize: 11 }} />
              <Tooltip formatter={(value) => formatInteger(Number(value))} labelFormatter={formatDate} />
              <Bar dataKey="processedRows" fill="#16a34a" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
          )}
        </ChartCard>

        <ChartCard title="Distribuição de tempo por etapa">
          {loading && !dashboard ? <SkeletonChart /> : (
          <ResponsiveContainer width="100%" height={220}>
            <PieChart>
              <Pie data={dashboard?.stageDurations ?? []} dataKey="sharePercent" nameKey="stageName" innerRadius={52} outerRadius={86} paddingAngle={2}>
                {(dashboard?.stageDurations ?? []).map((entry, index) => <Cell key={entry.stage} fill={chartColors[index % chartColors.length]} />)}
              </Pie>
              <Tooltip formatter={(value, _name, item) => [`${Number(value).toFixed(1)}%`, item.payload.stageName]} />
            </PieChart>
          </ResponsiveContainer>
          )}
        </ChartCard>
      </section>

      <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="max-w-5xl bg-surface border-border">
          <DialogHeader>
            <DialogTitle>Detalhes do Job</DialogTitle>
            <DialogDescription>Timeline, métricas, performance por etapa e logs estruturados.</DialogDescription>
          </DialogHeader>
          {!selectedJob && <SkeletonModalContent />}
          {selectedJob && (
            <JobDetails
              details={selectedJob}
              onRetry={() => runAction(() => retryProcessingJob(selectedJob.job.id))}
              onCancel={() => runAction(() => cancelProcessingJob(selectedJob.job.id))}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function JobDetails({ details, onRetry, onCancel }: { details: ProcessingJobDetails; onRetry: () => void; onCancel: () => void }) {
  const logUrl = `${import.meta.env.VITE_API_URL ?? "http://localhost:5279"}/api/processing-monitoring/jobs/${details.job.id}/logs/download`;
  return (
    <div className="max-h-[75vh] space-y-4 overflow-auto pr-1">
      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <Info label="Arquivo" value={details.job.fileName} />
        <Info label="Template" value={details.job.template ?? "-"} />
        <Info label="Tempo total" value={formatDuration(details.job.elapsedSeconds)} />
      </div>

      <div className="flex flex-wrap gap-2">
        <Button size="sm" onClick={onRetry}><RefreshCw className="mr-2 size-4" /> Reprocessar</Button>
        <Button size="sm" variant="outline" onClick={onCancel}><PauseCircle className="mr-2 size-4" /> Cancelar</Button>
        <Button size="sm" variant="outline" asChild><a href={logUrl}><Download className="mr-2 size-4" /> Baixar log</a></Button>
      </div>

      <div className="grid grid-cols-2 gap-3 md:grid-cols-6">
        <Info label="Linhas lidas" value={formatInteger(details.metrics.totalRows)} />
        <Info label="Válidas" value={formatInteger(details.metrics.validRows)} />
        <Info label="Inválidas" value={formatInteger(details.metrics.invalidRows)} />
        <Info label="Importadas" value={formatInteger(details.metrics.importedRows)} />
        <Info label="Erros" value={formatInteger(details.metrics.errorCount)} />
        <Info label="Warnings" value={formatInteger(details.metrics.warningCount)} />
      </div>

      <div className="rounded-lg border border-border p-3">
        <p className="mb-3 text-xs uppercase tracking-wider text-muted-foreground">Timeline</p>
        <div className="space-y-2">
          {details.timeline.map((step) => (
            <div key={step.step} className="grid grid-cols-1 gap-2 rounded-md bg-muted/30 p-2 text-sm md:grid-cols-[1fr_1fr_1fr_100px]">
              <span className="font-medium">{step.stepName}</span>
              <span className="text-muted-foreground">Início: {formatDate(step.startedAt)}</span>
              <span className="text-muted-foreground">Fim: {formatDate(step.finishedAt)}</span>
              <Badge variant={step.status === "failed" ? "destructive" : step.status === "completed" ? "default" : "outline"}>{step.status}</Badge>
            </div>
          ))}
        </div>
      </div>

      <div className="rounded-lg border border-border p-3">
        <p className="mb-3 text-xs uppercase tracking-wider text-muted-foreground">Performance por etapa</p>
        <div className="space-y-2">
          {details.performanceByStage.map((stage) => (
            <div key={stage.stage} className="grid grid-cols-[1fr_100px_80px] items-center gap-3 text-sm">
              <span>{stage.stageName}</span>
              <span className="text-muted-foreground">{formatDuration(stage.averageDurationSeconds)}</span>
              <span className="text-right text-muted-foreground">{stage.sharePercent.toFixed(1)}%</span>
            </div>
          ))}
        </div>
      </div>

      <div className="rounded-lg border border-border p-3">
        <p className="mb-3 text-xs uppercase tracking-wider text-muted-foreground">Logs</p>
        <div className="space-y-2">
          {details.logs.map((log, index) => (
            <div key={`${log.timestamp}-${index}`} className="grid grid-cols-1 gap-1 rounded-md bg-muted/30 p-2 text-xs md:grid-cols-[170px_100px_90px_1fr]">
              <span className="font-mono text-muted-foreground">{formatDate(log.timestamp)}</span>
              <span>{log.stage}</span>
              <Badge variant={log.level === "Error" ? "destructive" : log.level === "Warning" ? "secondary" : "outline"}>{log.level}</Badge>
              <span>{log.message}</span>
            </div>
          ))}
          {details.logs.length === 0 && <p className="text-sm text-muted-foreground">Sem logs estruturados para este job.</p>}
        </div>
      </div>
    </div>
  );
}

function ChartCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card className="bg-surface border-border">
      <CardHeader><CardTitle>{title}</CardTitle></CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 break-all font-medium">{value}</p>
    </div>
  );
}

function statusVariant(status: string): "default" | "secondary" | "destructive" | "outline" {
  if (status === "Concluido") return "default";
  if (status === "Falhou") return "destructive";
  if (status === "Processando") return "secondary";
  return "outline";
}

function formatDate(value?: string | null): string {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("pt-BR");
}

function formatShortDate(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString("pt-BR", { day: "2-digit", month: "2-digit" });
}

function formatDuration(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds <= 0) return "0s";
  const total = Math.round(seconds);
  const minutes = Math.floor(total / 60);
  const remainingSeconds = total % 60;
  if (minutes <= 0) return `${remainingSeconds}s`;
  const hours = Math.floor(minutes / 60);
  if (hours <= 0) return `${minutes}m ${remainingSeconds}s`;
  return `${hours}h ${minutes % 60}m`;
}

function formatInteger(value: number): string {
  return new Intl.NumberFormat("pt-BR").format(value);
}

function formatCompact(value: number): string {
  return new Intl.NumberFormat("pt-BR", { notation: "compact" }).format(value);
}
