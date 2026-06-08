import { useEffect, useMemo, useRef, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { FileUp, AlertTriangle, FolderUp, FileText, ChevronRight } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Progress } from "@/components/ui/progress";
import { SkeletonList, SkeletonModalContent } from "@/components/ui/skeleton";
import { subscribeToFileJobUpdates } from "@/lib/file-job-progress-realtime";
import {
  MAX_UPLOAD_SIZE_BYTES,
  fetchJobErrors,
  fetchJobs,
  type FileJob,
  type ImportError,
  type PagedResult,
  uploadFile,
} from "@/lib/importer-api";
import { clampProgressPercent, stageStatusLabel, type JobStageStatus } from "@/lib/importer-progress";

export const Route = createFileRoute("/importacoes/files")({
  component: ImportacoesPage,
});

const statusLabel: Record<string, string> = {
  WaitingProcessing: "Aguardando processamento",
  PreProcessing: "Pre-processando",
  Validating: "Validando",
  ValidationFailed: "Validação com erros",
  ReadyToImport: "Pronto para importar",
  Importing: "Importando",
  Completed: "Concluído",
  Failed: "Falha",
};

function statusBadgeVariant(status: string): "default" | "secondary" | "destructive" | "outline" {
  if (status === "Completed") return "default";
  if (status === "ValidationFailed" || status === "Failed") return "destructive";
  if (status === "ReadyToImport") return "secondary";
  return "outline";
}

function stageBadgeVariant(status: JobStageStatus): "default" | "secondary" | "destructive" | "outline" {
  if (status === "completed") return "default";
  if (status === "failed") return "destructive";
  if (status === "running") return "secondary";
  return "outline";
}

function formatDate(value: string): string {
  if (!value) return "-";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString("pt-BR");
}

function ImportacoesPage() {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");

  const [jobs, setJobs] = useState<FileJob[]>([]);
  const [jobsLoading, setJobsLoading] = useState(true);
  const [jobPage, setJobPage] = useState(1);
  const [jobTotal, setJobTotal] = useState(0);
  const jobPageSize = 10;
  const jobPageRef = useRef(1);
  const jobsRequestIdRef = useRef(0);

  const [selectedJobId, setSelectedJobId] = useState<number | null>(null);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [errors, setErrors] = useState<ImportError[]>([]);
  const [errorsLoading, setErrorsLoading] = useState(false);
  const [errorPage, setErrorPage] = useState(1);
  const [errorTotal, setErrorTotal] = useState(0);
  const errorPageSize = 50;
  const realtimeRefreshTimerRef = useRef<number | null>(null);
  const selectedJobIdRef = useRef<number | null>(null);
  const detailsOpenRef = useRef(false);
  const errorPageRef = useRef(1);

  async function loadJobs(page: number = jobPageRef.current) {
    const requestId = ++jobsRequestIdRef.current;
    try {
      const data = await fetchJobs(page, jobPageSize);
      if (requestId !== jobsRequestIdRef.current) return;
      setJobs(data.items);
      setJobTotal(data.total);
      setJobPage(data.page);
      jobPageRef.current = data.page;
    } catch (error) {
      if (requestId === jobsRequestIdRef.current) setMessage((error as Error).message);
    } finally {
      if (requestId === jobsRequestIdRef.current) setJobsLoading(false);
    }
  }

  async function loadErrors(jobId: number, page: number) {
    setErrorsLoading(true);
    try {
      const data: PagedResult<ImportError> = await fetchJobErrors(jobId, page, errorPageSize);
      setErrors(data.items);
      setErrorPage(data.page);
      setErrorTotal(data.total);
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setErrorsLoading(false);
    }
  }

  useEffect(() => {
    void loadJobs(1);
  }, []);

  useEffect(() => {
    let dispose: (() => void) | undefined;
    let cancelled = false;

    const scheduleRefresh = (jobId: number) => {
      if (realtimeRefreshTimerRef.current !== null) {
        window.clearTimeout(realtimeRefreshTimerRef.current);
      }

      realtimeRefreshTimerRef.current = window.setTimeout(() => {
        void loadJobs(jobPageRef.current);
        if (detailsOpenRef.current && selectedJobIdRef.current === jobId) {
          void loadErrors(jobId, errorPageRef.current);
        }
      }, 150);
    };

    void subscribeToFileJobUpdates(({ jobId }) => {
      if (!cancelled) {
        scheduleRefresh(jobId);
      }
    }).then((unsubscribe) => {
      if (cancelled) {
        unsubscribe();
        return;
      }

      dispose = unsubscribe;
    });

    return () => {
      cancelled = true;
      dispose?.();
      if (realtimeRefreshTimerRef.current !== null) {
        window.clearTimeout(realtimeRefreshTimerRef.current);
        realtimeRefreshTimerRef.current = null;
      }
    };
  }, []);

  useEffect(() => {
    if (detailsOpen && selectedJobId != null) {
      void loadErrors(selectedJobId, 1);
    }
  }, [selectedJobId, detailsOpen]);

  useEffect(() => {
    selectedJobIdRef.current = selectedJobId;
  }, [selectedJobId]);

  useEffect(() => {
    detailsOpenRef.current = detailsOpen;
  }, [detailsOpen]);

  useEffect(() => {
    errorPageRef.current = errorPage;
  }, [errorPage]);

  async function handleUpload(e: React.FormEvent) {
    e.preventDefault();
    if (selectedFiles.length === 0) {
      setMessage("Selecione ao menos um arquivo CSV ou XLSX.");
      return;
    }

    setLoading(true);
    setMessage("");
    try {
      const results: string[] = [];
      for (const file of selectedFiles) {
        if (file.size > MAX_UPLOAD_SIZE_BYTES) {
          throw new Error(`Arquivo '${file.name}' excede o limite de 500 MB.`);
        }

        const jobId = await uploadFile(file);
        results.push(`${file.name} -> Job #${jobId}`);
      }

      setMessage(`Upload concluído: ${results.join(" | ")}`);
      setSelectedFiles([]);
      await loadJobs(1);
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  const selectedJob = useMemo(() => jobs.find((job) => job.id === selectedJobId) ?? null, [jobs, selectedJobId]);

  return (
    <div className="page-shell">
      <header className="animate-soft-enter">
        <span className="page-header-kicker">Smart Core / Importações</span>
        <h1 className="mt-2 text-4xl font-display tracking-tight">Importação de Arquivos</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Envie arquivos, acompanhe o processamento em tempo real e revise erros com contexto. O backend identifica o
          layout automaticamente antes de validar e importar.
        </p>
      </header>

      {message && (
        <Alert>
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <Card className="animate-soft-enter border-primary/40 bg-surface ring-2 ring-primary/10">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FolderUp className="size-5 text-primary" />
            Importar Arquivos (CSV/XLSX)
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleUpload} className="space-y-4">
            <label className="block cursor-pointer rounded-xl border-2 border-dashed border-primary/40 bg-primary/5 p-6 transition-all duration-200 hover:bg-primary/10">
              <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                <div className="space-y-1">
                  <p className="text-sm font-semibold">Clique para selecionar um ou mais arquivos</p>
                  <p className="text-xs text-muted-foreground">Formatos aceitos: .csv e .xlsx</p>
                </div>
                <div className="inline-flex items-center rounded-md bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground shadow-sm shadow-primary/20">
                  <FileUp className="mr-2 size-4" />
                  Escolher arquivos
                </div>
              </div>
              <input
                type="file"
                multiple
                accept=".csv,.xlsx"
                className="hidden"
                onChange={(e) => setSelectedFiles(Array.from(e.target.files ?? []))}
              />
            </label>

            <div className="rounded-lg border border-border bg-background/50 p-3">
              <p className="mb-2 text-xs uppercase tracking-wider text-muted-foreground">Arquivos selecionados</p>
              {selectedFiles.length === 0 ? (
                <p className="text-sm text-muted-foreground">Nenhum arquivo selecionado.</p>
              ) : (
                <div className="space-y-1">
                  {selectedFiles.map((file) => (
                    <p key={`${file.name}-${file.lastModified}`} className="flex items-center gap-2 text-sm">
                      <FileText className="size-4 text-muted-foreground" />
                      {file.name}
                    </p>
                  ))}
                </div>
              )}
            </div>

            <div className="flex justify-end">
              <Button type="submit" disabled={loading || selectedFiles.length === 0} className="min-w-40">
                {loading ? "Enviando..." : `Importar ${selectedFiles.length > 0 ? `(${selectedFiles.length})` : ""}`}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card className="animate-soft-enter border-border bg-surface">
        <CardHeader>
          <CardTitle>Arquivos</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {jobsLoading && jobs.length === 0 && <SkeletonList rows={5} />}
          {!jobsLoading && jobs.length === 0 && <p className="text-sm text-muted-foreground">Nenhum arquivo encontrado.</p>}

          {jobs.map((job) => (
            <button
              key={job.id}
              type="button"
              onClick={() => {
                setSelectedJobId(job.id);
                setDetailsOpen(true);
                void loadErrors(job.id, 1);
              }}
              className="w-full rounded-lg border border-border/80 p-3 text-left transition-all duration-200 hover:border-border hover:bg-white/[0.03]"
            >
              <div className="flex items-start justify-between gap-2">
                <p className="truncate text-sm font-medium">{job.filePath.split(/[/\\]/).pop() ?? `Job #${job.id}`}</p>
                <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
              </div>
              <div className="mt-2 flex items-center justify-between gap-2">
                <Badge variant={statusBadgeVariant(job.status)}>{statusLabel[job.status] ?? job.status}</Badge>
                <span className="font-mono text-xs text-muted-foreground">#{job.id}</span>
              </div>
              <div className="mt-2 flex items-center gap-2">
                <Progress value={clampProgressPercent(job.progressPercent)} className="h-1.5" />
                <span className="w-9 text-right font-mono text-xs text-muted-foreground">
                  {clampProgressPercent(job.progressPercent)}%
                </span>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">
                {job.currentStageName ? `Etapa atual: ${job.currentStageName}` : job.currentStep || "Aguardando etapa"}
              </p>
            </button>
          ))}

          <div className="flex items-center justify-end gap-2 pt-1">
            <Button
              size="sm"
              variant="outline"
              disabled={jobPage <= 1}
              onClick={() => {
                const next = jobPage - 1;
                jobPageRef.current = next;
                setJobPage(next);
                void loadJobs(next);
              }}
            >
              Anterior
            </Button>
            <span className="text-xs text-muted-foreground">
              Página {jobPage} de {Math.max(1, Math.ceil(jobTotal / jobPageSize))}
            </span>
            <Button
              size="sm"
              variant="outline"
              disabled={jobPage >= Math.max(1, Math.ceil(jobTotal / jobPageSize))}
              onClick={() => {
                const next = jobPage + 1;
                jobPageRef.current = next;
                setJobPage(next);
                void loadJobs(next);
              }}
            >
              Próxima
            </Button>
          </div>
        </CardContent>
      </Card>

      <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="max-w-4xl border-border bg-surface">
          <DialogHeader>
            <DialogTitle>Detalhes do Arquivo</DialogTitle>
            <DialogDescription>Acompanhamento do processamento e validações.</DialogDescription>
          </DialogHeader>

          {!selectedJob && (
            <div className="rounded-lg border border-dashed border-border p-4">
              <p className="text-sm text-muted-foreground">Selecione um arquivo para ver os detalhes de processamento.</p>
            </div>
          )}

          {selectedJob && (
            <div className="space-y-4">
              <div className="grid grid-cols-1 gap-3 text-sm md:grid-cols-2">
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Arquivo</p>
                  <p className="break-all font-medium">{selectedJob.filePath}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Criado em</p>
                  <p className="font-medium">{formatDate(selectedJob.createdAt)}</p>
                </div>
              </div>

              <div className="rounded-lg border border-border p-3">
                <div className="mb-2 flex items-center justify-between gap-3">
                  <Badge variant={statusBadgeVariant(selectedJob.status)}>{statusLabel[selectedJob.status] ?? selectedJob.status}</Badge>
                  <span className="text-xs text-muted-foreground">
                    Etapa atual: {(selectedJob.currentStageName ?? selectedJob.currentStep) || "-"}
                  </span>
                </div>
                <div className="flex items-center gap-2">
                  <Progress value={clampProgressPercent(selectedJob.progressPercent)} className="h-2" />
                  <span className="w-9 text-right font-mono text-xs">{clampProgressPercent(selectedJob.progressPercent)}%</span>
                </div>
                <p className="mt-2 text-xs text-muted-foreground">{selectedJob.currentStep || "Aguardando processamento."}</p>
              </div>

              <div className="rounded-lg border border-border p-3">
                <p className="mb-3 text-xs uppercase tracking-wider text-muted-foreground">Etapas do processamento</p>
                <div className="space-y-3">
                  {selectedJob.stages.map((stage) => (
                    <div key={stage.code} className="space-y-1.5">
                      <div className="flex items-center justify-between gap-2">
                        <div className="flex items-center gap-2">
                          <span className="text-sm font-medium">{stage.name}</span>
                          <Badge variant={stageBadgeVariant(stage.status)}>{stageStatusLabel(stage.status)}</Badge>
                        </div>
                        <span className="font-mono text-xs text-muted-foreground">{clampProgressPercent(stage.progressPercent)}%</span>
                      </div>
                      <Progress value={clampProgressPercent(stage.progressPercent)} className="h-1.5" />
                      {stage.errorCount > 0 && <p className="text-xs text-destructive">{stage.errorCount} erro(s) nessa etapa.</p>}
                    </div>
                  ))}
                </div>
              </div>

              <Alert variant={selectedJob.errorCount > 0 ? "destructive" : "default"}>
                <AlertTriangle className="h-4 w-4" />
                <AlertDescription>
                  {selectedJob.errorCount > 0
                    ? `Arquivo com inconsistências. Total de erros: ${errorTotal}.`
                    : "Arquivo validado sem inconsistências."}
                </AlertDescription>
              </Alert>

              {errorsLoading && <SkeletonModalContent />}
              {!errorsLoading && errors.length === 0 && <p className="text-sm text-muted-foreground">Sem erros para esse arquivo.</p>}

              {errors.length > 0 && (
                <div className="max-h-[320px] space-y-2 overflow-auto pr-1">
                  {errors.map((err) => (
                    <div key={err.id} className="rounded-lg border border-border p-3">
                      <p className="text-sm font-medium">
                        Linha {err.rowNumber} - Campo: {err.column} - Registro: {err.recordIdentifier || "N/A"}
                      </p>
                      {err.stage && <p className="text-xs text-muted-foreground">Etapa: {err.stage}</p>}
                      <p className="text-sm text-muted-foreground">{err.message}</p>
                    </div>
                  ))}
                </div>
              )}

              <div className="flex items-center justify-end gap-2">
                <Button
                  size="sm"
                  variant="outline"
                  disabled={errorPage <= 1 || errorsLoading}
                  onClick={() => void loadErrors(selectedJob.id, errorPage - 1)}
                >
                  Anterior
                </Button>
                <span className="text-xs text-muted-foreground">
                  Página {errorPage} de {Math.max(1, Math.ceil(errorTotal / errorPageSize))}
                </span>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={errorPage >= Math.max(1, Math.ceil(errorTotal / errorPageSize)) || errorsLoading}
                  onClick={() => void loadErrors(selectedJob.id, errorPage + 1)}
                >
                  Próxima
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
