import { useEffect, useRef, useState } from "react";
import { createFileRoute, redirect } from "@tanstack/react-router";
import { Activity, AlertTriangle, CheckCircle2, Clock, Database, Loader2, RefreshCw, StopCircle } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { SkeletonMetricCard, SkeletonModalContent, SkeletonTable } from "@/components/ui/skeleton";
import {
  cancelAdminJob,
  fetchAdminJobDetail,
  fetchAdminJobs,
  fetchAdminJobsSummary,
  retryAdminJob,
  type AdminJobItem,
  type AdminJobSummary,
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
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [statusFilter, setStatusFilter] = useState<string>("");
  const pageSize = 20;
  const pageRef = useRef(1);
  const loadingRef = useRef(false);

  const [selectedJob, setSelectedJob] = useState<AdminJobItem | null>(null);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [retryingId, setRetryingId] = useState<string | null>(null);
  const [cancellingId, setCancellingId] = useState<string | null>(null);

  async function loadData(p: number = pageRef.current) {
    if (loadingRef.current) return;
    loadingRef.current = true;
    try {
      const [summaryData, jobsData] = await Promise.all([
        fetchAdminJobsSummary(),
        fetchAdminJobs(p, pageSize, statusFilter ? { status: statusFilter } : undefined),
      ]);
      setSummary(summaryData);
      setJobs(jobsData.items);
      setTotal(jobsData.total);
      setPage(jobsData.page);
      pageRef.current = jobsData.page;
    } catch (error) {
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
      setMessage("Job reenfileirado com sucesso.");
      await loadData(pageRef.current);
      if (selectedJob?.id === jobId) {
        const updated = await fetchAdminJobDetail(jobId);
        setSelectedJob(updated);
      }
    } catch (error) {
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
      setMessage("Job cancelado com sucesso.");
      await loadData(pageRef.current);
      if (selectedJob?.id === jobId || detailsOpen) {
        setDetailsOpen(false);
        setSelectedJob(null);
      }
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setCancellingId(null);
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
        { title: "Concluídos (24h)", value: formatInteger(summary.completedLast24Hours), icon: CheckCircle2 },
        { title: "Falhas (24h)", value: formatInteger(summary.failedLast24Hours), icon: AlertTriangle },
        { title: "Taxa de sucesso", value: `${summary.successRatePercent.toFixed(1)}%`, icon: Database },
        { title: "Tempo médio", value: formatDuration(summary.averageProcessingSeconds), icon: Clock },
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
              Monitore jobs de processamento de importações, taxas de sucesso e reenvie jobs com falha.
            </p>
          </div>
          <Button variant="outline" onClick={() => void loadData(pageRef.current)} disabled={loading}>
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

      <section className="metric-row">
        {loading && !summary
          ? Array.from({ length: 6 }).map((_, i) => <SkeletonMetricCard key={i} />)
          : summaryCards.map((card) => (
              <Card key={card.title} className="metric-card-item bg-surface border-border">
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
                        <Badge variant={statusVariant(job.status)}>{statusLabel(job.status)}</Badge>
                      </td>
                      <td className="py-3 pr-3 text-xs text-muted-foreground">{job.attempts}</td>
                      <td className="py-3 pr-3 text-xs text-muted-foreground whitespace-nowrap">
                        {formatDate(job.createdAt)}
                      </td>
                      <td className="py-3 text-xs text-muted-foreground">{formatDuration(job.durationSeconds)}</td>
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
            <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => void loadData(page - 1)}>
              Anterior
            </Button>
            <span className="text-xs text-muted-foreground">
              Página {page} de {pageCount}
            </span>
            <Button size="sm" variant="outline" disabled={page >= pageCount} onClick={() => void loadData(page + 1)}>
              Próxima
            </Button>
          </div>
        </CardContent>
      </Card>

      <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="max-w-2xl border-border bg-surface">
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
                  <p className="font-mono text-xs font-medium">{selectedJob.id}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Tipo</p>
                  <p className="font-medium">{selectedJob.jobType}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Arquivo</p>
                  <p className="font-medium">{selectedJob.importFileName}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Status</p>
                  <Badge variant={statusVariant(selectedJob.status)}>{statusLabel(selectedJob.status)}</Badge>
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
                  <p className="font-mono text-xs font-medium">{selectedJob.importId}</p>
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

              <div className="flex gap-2">
                {selectedJob.status === "Failed" && (
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
                    Reenviar Job
                  </Button>
                )}
                {(selectedJob.status === "Processing" || selectedJob.status === "Retrying" || selectedJob.status === "Queued") && (
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
