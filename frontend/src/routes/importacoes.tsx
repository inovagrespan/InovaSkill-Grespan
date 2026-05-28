import { useEffect, useMemo, useRef, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { FileUp, AlertTriangle, FolderUp, FileText, ChevronRight } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Progress } from "@/components/ui/progress";
import {
  fetchJobErrors,
  fetchJobs,
  type FileJob,
  type ImportError,
  type PagedResult,
  uploadFile,
} from "@/lib/importer-api";

export const Route = createFileRoute("/importacoes")({
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
    const interval = setInterval(() => void loadJobs(jobPageRef.current), 1000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    if (detailsOpen && selectedJobId != null) {
      void loadErrors(selectedJobId, 1);
    }
  }, [selectedJobId, detailsOpen]);

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
    <div className="p-12 space-y-6">
      <header className="animate-fade-in">
        <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-muted-foreground">Smart Core / Importações</span>
        <h1 className="text-4xl font-display tracking-tight mt-2">Importação de Arquivos</h1>
      </header>

      {message && (
        <Alert>
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <Card className="bg-surface border-primary/40 ring-2 ring-primary/10">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FolderUp className="size-5 text-primary" />
            Importar Arquivos (CSV/XLSX)
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleUpload} className="space-y-4">
            <label className="block cursor-pointer rounded-xl border-2 border-dashed border-primary/40 bg-primary/5 hover:bg-primary/10 transition-colors p-6">
              <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
                <div className="space-y-1">
                  <p className="text-sm font-semibold">Clique para selecionar um ou mais arquivos</p>
                  <p className="text-xs text-muted-foreground">Formatos aceitos: .csv e .xlsx</p>
                </div>
                <div className="inline-flex items-center rounded-md bg-primary text-primary-foreground px-4 py-2 text-sm font-semibold">
                  <FileUp className="size-4 mr-2" />
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
              <p className="text-xs uppercase tracking-wider text-muted-foreground mb-2">Arquivos selecionados</p>
              {selectedFiles.length === 0 ? (
                <p className="text-sm text-muted-foreground">Nenhum arquivo selecionado.</p>
              ) : (
                <div className="space-y-1">
                  {selectedFiles.map((file) => (
                    <p key={`${file.name}-${file.lastModified}`} className="text-sm flex items-center gap-2">
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

      <Card className="bg-surface border-border">
        <CardHeader>
          <CardTitle>Arquivos</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {jobs.length === 0 && <p className="text-sm text-muted-foreground">Nenhum arquivo encontrado.</p>}

          {jobs.map((job) => (
            <button
              key={job.id}
              type="button"
              onClick={() => {
                setSelectedJobId(job.id);
                setDetailsOpen(true);
                void loadErrors(job.id, 1);
              }}
              className="w-full text-left rounded-lg border border-border p-3 transition-colors hover:bg-white/[0.03]"
            >
              <div className="flex items-start justify-between gap-2">
                <p className="text-sm font-medium truncate">{job.filePath.split(/[/\\]/).pop() ?? `Job #${job.id}`}</p>
                <ChevronRight className="size-4 text-muted-foreground shrink-0" />
              </div>
              <div className="mt-2 flex items-center justify-between gap-2">
                <Badge variant={statusBadgeVariant(job.status)}>{statusLabel[job.status] ?? job.status}</Badge>
                <span className="text-xs text-muted-foreground font-mono">#{job.id}</span>
              </div>
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
        <DialogContent className="max-w-4xl bg-surface border-border">
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
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Arquivo</p>
                  <p className="font-medium break-all">{selectedJob.filePath}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Criado em</p>
                  <p className="font-medium">{formatDate(selectedJob.createdAt)}</p>
                </div>
              </div>

              <div className="rounded-lg border border-border p-3">
                <div className="flex items-center justify-between gap-3 mb-2">
                  <Badge variant={statusBadgeVariant(selectedJob.status)}>{statusLabel[selectedJob.status] ?? selectedJob.status}</Badge>
                  <span className="text-xs text-muted-foreground">Etapa: {selectedJob.currentStep || "-"}</span>
                </div>
                <div className="flex items-center gap-2">
                  <Progress value={Math.max(0, Math.min(100, Number(selectedJob.progressPercent ?? 0)))} className="h-2" />
                  <span className="text-xs font-mono w-9 text-right">{Math.max(0, Math.min(100, Number(selectedJob.progressPercent ?? 0)))}%</span>
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

              {errorsLoading && <p className="text-sm text-muted-foreground">Carregando erros...</p>}
              {!errorsLoading && errors.length === 0 && (
                <p className="text-sm text-muted-foreground">Sem erros para esse arquivo.</p>
              )}

              {errors.length > 0 && (
                <div className="space-y-2 max-h-[320px] overflow-auto pr-1">
                  {errors.map((err) => (
                    <div key={err.id} className="rounded-lg border border-border p-3">
                      <p className="text-sm font-medium">
                        Linha {err.rowNumber} - Campo: {err.column} - Registro: {err.recordIdentifier || "N/A"}
                      </p>
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
