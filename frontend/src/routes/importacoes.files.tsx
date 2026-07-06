import { useEffect, useRef, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { AlertTriangle, CheckCircle2, FileText, FolderUp, Loader2, RefreshCw } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Progress } from "@/components/ui/progress";
import { SkeletonList, SkeletonModalContent } from "@/components/ui/skeleton";
import {
  MAX_UPLOAD_SIZE_BYTES,
  fetchImportErrors,
  fetchImports,
  reprocessImport,
  resolveImportError,
  uploadImport,
  type ImportErrorItem,
  type ImportItem,
} from "@/lib/importer-api";

export const Route = createFileRoute("/importacoes/files")({
  component: ImportacoesPage,
});

function statusLabel(status: string): string {
  const labels: Record<string, string> = {
    Queued: "Na fila",
    Processing: "Processando",
    NeedsReview: "Revisão necessária",
    Completed: "Concluído",
    Failed: "Falha",
  };
  return labels[status] ?? status;
}

function statusVariant(status: string): "default" | "secondary" | "destructive" | "outline" {
  if (status === "Completed") return "default";
  if (status === "Failed") return "destructive";
  if (status === "Processing" || status === "Queued") return "secondary";
  return "outline";
}

function formatDate(value: string): string {
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

function ImportacoesPage() {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [uploading, setUploading] = useState(false);
  const [sourceCode, setSourceCode] = useState("ROUTES_BY_CITY");
  const [message, setMessage] = useState("");

  const [imports, setImports] = useState<ImportItem[]>([]);
  const [importsLoading, setImportsLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const pageSize = 20;
  const pageRef = useRef(1);

  const [selectedImport, setSelectedImport] = useState<ImportItem | null>(null);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [errors, setErrors] = useState<ImportErrorItem[]>([]);
  const [errorsLoading, setErrorsLoading] = useState(false);
  const [resolvingErrorId, setResolvingErrorId] = useState<string | null>(null);
  const [reprocessingId, setReprocessingId] = useState<string | null>(null);

  async function loadImports(p: number = pageRef.current) {
    try {
      const data = await fetchImports(p, pageSize);
      setImports(data.items);
      setTotal(data.total);
      setPage(data.page);
      pageRef.current = data.page;
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setImportsLoading(false);
    }
  }

  async function loadErrors(importId: string) {
    setErrorsLoading(true);
    try {
      const data = await fetchImportErrors(importId);
      setErrors(data);
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setErrorsLoading(false);
    }
  }

  useEffect(() => {
    void loadImports(1);
  }, []);

  async function handleUpload(e: React.FormEvent) {
    e.preventDefault();
    if (selectedFiles.length === 0) {
      setMessage("Selecione ao menos um arquivo XLSX.");
      return;
    }
    setUploading(true);
    setMessage("");
    try {
      for (const file of selectedFiles) {
        if (file.size > 50 * 1024 * 1024) {
          throw new Error(`Arquivo '${file.name}' excede o limite de 50 MB.`);
        }
        if (!file.name.toLowerCase().endsWith(".xlsx")) {
          throw new Error(`Arquivo '${file.name}' não é um XLSX válido.`);
        }
        await uploadImport(file, sourceCode);
      }
      setMessage("Upload concluído com sucesso.");
      setSelectedFiles([]);
      await loadImports(1);
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setUploading(false);
    }
  }

  async function handleReprocess(importId: string) {
    setReprocessingId(importId);
    setMessage("");
    try {
      await reprocessImport(importId);
      setMessage("Importação reenfileirada para reprocessamento.");
      await loadImports(pageRef.current);
      if (selectedImport?.id === importId) {
        await loadErrors(importId);
      }
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setReprocessingId(null);
    }
  }

  async function handleResolveError(errorId: string, correctedValue: string) {
    setResolvingErrorId(errorId);
    setMessage("");
    try {
      await resolveImportError(errorId, correctedValue);
      setMessage("Erro resolvido com sucesso.");
      if (selectedImport) {
        await loadErrors(selectedImport.id);
      }
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setResolvingErrorId(null);
    }
  }

  const pageCount = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div className="page-shell">
      <header className="animate-soft-enter">
        <span className="page-header-kicker">Importações</span>
        <h1 className="mt-2 text-4xl font-display tracking-tight">Importação de Arquivos</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Envie arquivos, acompanhe o processamento e revise erros antes de concluir.
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
            Importar arquivo XLSX
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleUpload} className="space-y-4">
            <label className="block space-y-2 text-sm font-medium">
              Fonte de dados
              <select
                value={sourceCode}
                onChange={(event) => setSourceCode(event.target.value)}
                className="block h-10 w-full max-w-sm rounded-md border border-input bg-background px-3"
              >
                <option value="ROUTES_BY_CITY">Rotas por cidades</option>
                <option value="CUSTOMERS">Cadastro de clientes</option>
              </select>
            </label>
            <label className="block cursor-pointer rounded-xl border-2 border-dashed border-primary/40 bg-primary/5 p-6 transition-all duration-200 hover:bg-primary/10">
              <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                <div className="space-y-1">
                  <p className="text-sm font-semibold">Clique para selecionar o arquivo</p>
                  <p className="text-xs text-muted-foreground">Formato aceito: .xlsx (até 50 MB)</p>
                </div>
                <div className="inline-flex items-center rounded-md bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground shadow-sm shadow-primary/20">
                  <FolderUp className="mr-2 size-4" />
                  Escolher arquivo
                </div>
              </div>
              <input
                type="file"
                accept=".xlsx"
                className="hidden"
                onChange={(e) => setSelectedFiles(Array.from(e.target.files ?? []))}
              />
            </label>

            {selectedFiles.length > 0 && (
              <div className="rounded-lg border border-border bg-background/50 p-3">
                <p className="mb-2 text-xs uppercase tracking-wider text-muted-foreground">Arquivos selecionados</p>
                <div className="space-y-1">
                  {selectedFiles.map((file) => (
                    <p key={`${file.name}-${file.lastModified}`} className="flex items-center gap-2 text-sm">
                      <FileText className="size-4 text-muted-foreground" />
                      {file.name}
                    </p>
                  ))}
                </div>
              </div>
            )}

            <div className="flex justify-end">
              <Button type="submit" disabled={uploading || selectedFiles.length === 0} className="min-w-40">
                {uploading ? "Enviando..." : "Importar"}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card className="animate-soft-enter border-border bg-surface">
        <CardHeader>
          <CardTitle>Importações realizadas</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {importsLoading && imports.length === 0 && <SkeletonList rows={5} />}
          {!importsLoading && imports.length === 0 && (
            <p className="text-sm text-muted-foreground">Nenhuma importação encontrada.</p>
          )}

          {imports.map((imp) => (
            <button
              key={imp.id}
              type="button"
              onClick={() => {
                setSelectedImport(imp);
                setDetailsOpen(true);
                void loadErrors(imp.id);
              }}
              className="w-full rounded-lg border border-border/80 p-3 text-left transition-all duration-200 hover:border-border hover:bg-white/[0.03]"
            >
              <div className="flex items-start justify-between gap-2">
                <p className="truncate text-sm font-medium">
                  v{imp.version} · {imp.fileName}
                </p>
                <Badge variant={statusVariant(imp.status)}>{statusLabel(imp.status)}</Badge>
              </div>
              <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
                <span>Criado em: {formatDate(imp.createdAt)}</span>
                {imp.totalRows != null && <span>Linhas: {imp.totalRows}</span>}
                {imp.importedRows != null && <span>Importadas: {imp.importedRows}</span>}
                {imp.errorCount > 0 && <span className="text-destructive">Erros: {imp.errorCount}</span>}
                {imp.durationSeconds != null && <span>Duração: {formatDuration(imp.durationSeconds)}</span>}
              </div>
              {imp.status === "Processing" && (
                <div className="mt-2">
                  <Progress value={null} className="h-1.5 animate-pulse" />
                </div>
              )}
            </button>
          ))}

          <div className="flex items-center justify-end gap-2 pt-1">
            <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => void loadImports(page - 1)}>
              Anterior
            </Button>
            <span className="text-xs text-muted-foreground">
              Página {page} de {pageCount}
            </span>
            <Button size="sm" variant="outline" disabled={page >= pageCount} onClick={() => void loadImports(page + 1)}>
              Próxima
            </Button>
          </div>
        </CardContent>
      </Card>

      <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="max-w-4xl border-border bg-surface max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Detalhes da Importação</DialogTitle>
            <DialogDescription>Informações do arquivo, erros de validação e ações disponíveis.</DialogDescription>
          </DialogHeader>

          {!selectedImport && (
            <div className="rounded-lg border border-dashed border-border p-4">
              <p className="text-sm text-muted-foreground">Selecione uma importação para ver os detalhes.</p>
            </div>
          )}

          {selectedImport && (
            <div className="space-y-4">
              <div className="grid grid-cols-1 gap-3 text-sm md:grid-cols-2">
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Arquivo</p>
                  <p className="break-all font-medium">
                    v{selectedImport.version} · {selectedImport.fileName}
                  </p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Criado em</p>
                  <p className="font-medium">{formatDate(selectedImport.createdAt)}</p>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Status</p>
                  <Badge variant={statusVariant(selectedImport.status)}>{statusLabel(selectedImport.status)}</Badge>
                </div>
                <div className="rounded-lg border border-border p-3">
                  <p className="text-xs text-muted-foreground">Duração</p>
                  <p className="font-medium">{formatDuration(selectedImport.durationSeconds)}</p>
                </div>
                {selectedImport.totalRows != null && (
                  <div className="rounded-lg border border-border p-3">
                    <p className="text-xs text-muted-foreground">Total de linhas</p>
                    <p className="font-medium">{selectedImport.totalRows}</p>
                  </div>
                )}
                {selectedImport.importedRows != null && (
                  <div className="rounded-lg border border-border p-3">
                    <p className="text-xs text-muted-foreground">Linhas importadas</p>
                    <p className="font-medium">{selectedImport.importedRows}</p>
                  </div>
                )}
              </div>

              {selectedImport.status === "NeedsReview" && (
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    onClick={() => handleReprocess(selectedImport.id)}
                    disabled={reprocessingId === selectedImport.id || errors.some((e) => e.status === "Pending")}
                  >
                    {reprocessingId === selectedImport.id ? (
                      <Loader2 className="mr-2 size-4 animate-spin" />
                    ) : (
                      <RefreshCw className="mr-2 size-4" />
                    )}
                    Reprocessar
                  </Button>
                  {errors.some((e) => e.status === "Pending") && (
                    <p className="text-xs text-destructive self-center">
                      Resolva todos os erros pendentes antes de reprocessar.
                    </p>
                  )}
                </div>
              )}

              <Alert variant={selectedImport.errorCount > 0 ? "destructive" : "default"}>
                <AlertTriangle className="h-4 w-4" />
                <AlertDescription>
                  {selectedImport.errorCount > 0
                    ? `Foram encontrados ${selectedImport.errorCount} erro(s) durante a validação.`
                    : "Nenhum erro encontrado."}
                </AlertDescription>
              </Alert>

              {errorsLoading && <SkeletonModalContent />}

              {!errorsLoading && errors.length === 0 && selectedImport.errorCount > 0 && (
                <p className="text-sm text-muted-foreground">Nenhum erro registrado no momento.</p>
              )}

              {!errorsLoading && errors.length === 0 && selectedImport.errorCount === 0 && (
                <p className="text-sm text-muted-foreground">Esta importação não possui erros.</p>
              )}

              {errors.length > 0 && (
                <div className="space-y-2">
                  <p className="text-xs uppercase tracking-wider text-muted-foreground">
                    Erros ({errors.length})
                  </p>
                  <div className="max-h-[320px] space-y-2 overflow-auto pr-1">
                    {errors.map((err) => (
                      <div key={err.id} className="rounded-lg border border-border p-3">
                        <div className="flex items-start justify-between gap-2">
                          <div className="space-y-1">
                            <p className="text-sm font-medium">
                              {err.sheetName && `${err.sheetName} > `}Linha {err.rowNumber} - {err.field}
                            </p>
                            <p className="text-xs text-muted-forebreak">
                              Valor: <code className="rounded bg-muted px-1">{err.rawValue}</code>
                            </p>
                            <p className="text-sm text-muted-foreground">{err.message}</p>
                            {err.correctedValue && (
                              <p className="text-xs text-green-600 dark:text-green-400">
                                Corrigido para: {err.correctedValue}
                              </p>
                            )}
                          </div>
                          <Badge variant={err.status === "Resolved" ? "default" : "outline"}>
                            {err.status === "Resolved" ? "Resolvido" : "Pendente"}
                          </Badge>
                        </div>
                        {err.status === "Pending" && (
                          <div className="mt-2 space-y-1">
                            <div className="flex items-center gap-2">
                              <input
                                type="text"
                                placeholder="Valor corrigido..."
                                className="flex h-8 w-full max-w-xs rounded-md border border-input bg-background px-3 text-xs"
                                id={`resolve-${err.id}`}
                                onKeyDown={async (e) => {
                                  if (e.key === "Enter") {
                                    const value = (e.target as HTMLInputElement).value.trim();
                                    if (value) await handleResolveError(err.id, value);
                                  }
                                }}
                              />
                              <Button
                                size="sm"
                                variant="outline"
                                disabled={resolvingErrorId === err.id}
                                onClick={async () => {
                                  const input = document.getElementById(`resolve-${err.id}`) as HTMLInputElement | null;
                                  const value = input?.value?.trim();
                                  if (value) await handleResolveError(err.id, value);
                                }}
                              >
                                {resolvingErrorId === err.id ? (
                                  <Loader2 className="size-3 animate-spin" />
                                ) : (
                                  <CheckCircle2 className="size-3" />
                                )}
                                Resolver
                              </Button>
                            </div>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
