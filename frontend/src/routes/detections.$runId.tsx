import { Link, createFileRoute, useNavigate } from "@tanstack/react-router";
import { useCallback, useEffect, useRef, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import {
  type DetectionRunDetailDto,
  type FindingDetailDto,
  type FindingDto,
  fetchDetectionRun,
  fetchFinding,
  fetchRunFindings,
} from "@/lib/detection-api";
import { cn } from "@/lib/utils";
import { ArrowLeft, ChevronDown, ChevronRight, FileSearch, SearchX } from "lucide-react";

const POLLING_INTERVAL_MS = 3000;
const FINDINGS_PAGE_SIZE = 20;

export const Route = createFileRoute("/detections/$runId")({
  component: DetectionRunPage,
});

function StatusBadge({ status, pulse }: { status: string; pulse?: boolean }) {
  const variantMap: Record<string, "default" | "secondary" | "outline" | "destructive"> = {
    Queued: "secondary",
    Running: "default",
    Succeeded: "outline",
    Failed: "destructive",
  };

  return (
    <Badge
      variant={variantMap[status] ?? "outline"}
      className={cn(pulse && "animate-pulse")}
    >
      {status === "Queued" && "Na fila"}
      {status === "Running" && "Executando"}
      {status === "Succeeded" && "Sucesso"}
      {status === "Failed" && "Falha"}
    </Badge>
  );
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return "-";
  const d = new Date(dateStr);
  return d.toLocaleString("pt-BR");
}

function EvidenceDialog({
  finding,
  open,
  onOpenChange,
}: {
  finding: FindingDetailDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  if (!finding) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{finding.title}</DialogTitle>
          <DialogDescription>{finding.description}</DialogDescription>
        </DialogHeader>

        {finding.subjectLabel && (
          <div className="rounded-lg border bg-muted/30 px-4 py-3">
            <span className="text-xs text-muted-foreground">{finding.subjectType}</span>
            <p className="font-medium">{finding.subjectLabel}</p>
            <p className="text-xs text-muted-foreground">ID: {finding.subjectId}</p>
          </div>
        )}

        <div className="space-y-2">
          <h4 className="text-sm font-semibold">Evidências</h4>
          {finding.evidences.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nenhuma evidência registrada.</p>
          ) : (
            <div className="divide-y rounded-lg border">
              {finding.evidences.map((ev, idx) => (
                <div key={idx} className="grid grid-cols-2 gap-2 px-4 py-3 text-sm">
                  <div>
                    <span className="text-xs text-muted-foreground">{ev.name}</span>
                    <p className="font-medium">{ev.value}{ev.unit ? ` ${ev.unit}` : ""}</p>
                  </div>
                  <div>
                    {ev.referenceValue && (
                      <>
                        <span className="text-xs text-muted-foreground">Referência</span>
                        <p className="font-medium">{ev.referenceValue}{ev.unit ? ` ${ev.unit}` : ""}</p>
                      </>
                    )}
                    {ev.description && (
                      <p className="text-xs text-muted-foreground">{ev.description}</p>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

function DetectionRunPage() {
  const { runId } = Route.useParams();
  const navigate = useNavigate();
  const [run, setRun] = useState<DetectionRunDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [findings, setFindings] = useState<FindingDto[]>([]);
  const [findingsTotal, setFindingsTotal] = useState(0);
  const [findingsPage, setFindingsPage] = useState(1);
  const [findingsLoading, setFindingsLoading] = useState(false);
  const [selectedFinding, setSelectedFinding] = useState<FindingDetailDto | null>(null);
  const [evidenceDialogOpen, setEvidenceDialogOpen] = useState(false);
  const [evidenceLoading, setEvidenceLoading] = useState(false);
  const pollingRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const loadRun = useCallback(async () => {
    try {
      const data = await fetchDetectionRun(runId);
      setRun(data);
      return data;
    } catch {
      return null;
    } finally {
      setLoading(false);
    }
  }, [runId]);

  const loadFindings = useCallback(async (page: number) => {
    setFindingsLoading(true);
    try {
      const data = await fetchRunFindings(runId, page, FINDINGS_PAGE_SIZE);
      setFindings(data.items);
      setFindingsTotal(data.total);
      setFindingsPage(page);
    } catch {
      // ignore
    } finally {
      setFindingsLoading(false);
    }
  }, [runId]);

  useEffect(() => {
    void loadRun().then((data) => {
      if (data && (data.status === "Queued" || data.status === "Running")) {
        pollingRef.current = setInterval(async () => {
          const updated = await loadRun();
          if (updated && (updated.status === "Succeeded" || updated.status === "Failed")) {
            if (pollingRef.current) clearInterval(pollingRef.current);
            pollingRef.current = null;
            void loadFindings(1);
          }
        }, POLLING_INTERVAL_MS);
      }
    });

    return () => {
      if (pollingRef.current) {
        clearInterval(pollingRef.current);
        pollingRef.current = null;
      }
    };
  }, [loadRun, loadFindings]);

  useEffect(() => {
    if (run && (run.status === "Succeeded" || run.status === "Failed")) {
      void loadFindings(1);
    }
  }, [run?.status, loadFindings]);

  const handleOpenEvidence = useCallback(async (findingId: string) => {
    setEvidenceLoading(true);
    try {
      const data = await fetchFinding(findingId);
      setSelectedFinding(data);
      setEvidenceDialogOpen(true);
    } catch {
      // ignore
    } finally {
      setEvidenceLoading(false);
    }
  }, []);

  if (loading) {
    return (
      <div className="p-6 space-y-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-32 w-full" />
      </div>
    );
  }

  if (!run) {
    return (
      <div className="p-6">
        <Button variant="ghost" size="sm" onClick={() => navigate({ to: "/detections" })}>
          <ArrowLeft className="mr-1.5 size-3.5" />
          Voltar
        </Button>
        <div className="mt-6 text-center text-muted-foreground">
          Execução não encontrada.
        </div>
      </div>
    );
  }

  const isActive = run.status === "Queued" || run.status === "Running";
  const totalPages = Math.max(1, Math.ceil(findingsTotal / FINDINGS_PAGE_SIZE));

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="sm" onClick={() => navigate({ to: "/detections" })}>
          <ArrowLeft className="mr-1.5 size-3.5" />
          Voltar
        </Button>
      </div>

      <Card>
        <CardHeader>
          <div className="flex items-start justify-between gap-4">
            <div>
              <CardTitle className="text-lg">{run.detector.name}</CardTitle>
              <CardDescription>Execução</CardDescription>
            </div>
            <StatusBadge status={run.status} pulse={isActive} />
          </div>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 gap-x-8 gap-y-3 text-sm md:grid-cols-3">
            <div>
              <span className="text-xs text-muted-foreground">Solicitada</span>
              <p>{formatDate(run.requestedAt)}</p>
            </div>
            <div>
              <span className="text-xs text-muted-foreground">Iniciada</span>
              <p>{formatDate(run.startedAt)}</p>
            </div>
            <div>
              <span className="text-xs text-muted-foreground">Finalizada</span>
              <p>{formatDate(run.finishedAt)}</p>
            </div>
            <div>
              <span className="text-xs text-muted-foreground">Analisados</span>
              <p>{run.analyzedItems}</p>
            </div>
            <div>
              <span className="text-xs text-muted-foreground">Encontrados</span>
              <p>{run.findingsCount}</p>
            </div>
            <div>
              <span className="text-xs text-muted-foreground">Origem</span>
              <p>{run.trigger === "Manual" ? "Manual" : run.trigger}</p>
            </div>
          </div>
          {run.statusReason && (
            <div className="mt-4 rounded-lg border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-destructive">
              {run.statusReason}
            </div>
          )}
        </CardContent>
      </Card>

      {run.status === "Succeeded" && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              <FileSearch className="size-4" />
              O que foi encontrado
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {findingsLoading && findings.length === 0 ? (
              <div className="space-y-3">
                {Array.from({ length: 3 }).map((_, i) => (
                  <Skeleton key={i} className="h-20 w-full" />
                ))}
              </div>
            ) : findings.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-8 text-center">
                <SearchX className="size-8 text-muted-foreground" />
                <p className="text-sm text-muted-foreground">
                  Nenhuma inconformidade foi encontrada nesta execução.
                </p>
              </div>
            ) : (
              <>
                {findings.map((finding) => (
                  <Card key={finding.id} className="overflow-hidden">
                    <CardContent className="p-4">
                      <div className="flex items-start justify-between gap-4">
                        <div className="space-y-1 min-w-0">
                          <p className="font-medium truncate">
                            {finding.subjectLabel ?? finding.subjectId}
                          </p>
                          <p className="text-sm font-medium text-foreground">{finding.title}</p>
                          <p className="text-xs text-muted-foreground line-clamp-2">
                            {finding.description}
                          </p>
                        </div>
                        <Button
                          variant="outline"
                          size="sm"
                          className="shrink-0"
                          onClick={() => handleOpenEvidence(finding.id)}
                          disabled={evidenceLoading}
                        >
                          Ver evidências
                        </Button>
                      </div>
                    </CardContent>
                  </Card>
                ))}

                {totalPages > 1 && (
                  <div className="flex items-center justify-end gap-2 pt-2">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={findingsPage <= 1}
                      onClick={() => loadFindings(findingsPage - 1)}
                    >
                      Anterior
                    </Button>
                    <span className="text-xs text-muted-foreground">
                      Página {findingsPage} de {totalPages}
                    </span>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={findingsPage >= totalPages}
                      onClick={() => loadFindings(findingsPage + 1)}
                    >
                      Próxima
                    </Button>
                  </div>
                )}
              </>
            )}
          </CardContent>
        </Card>
      )}

      {run.status === "Failed" && (
        <Card>
          <CardContent className="flex flex-col items-center gap-2 py-8 text-center">
            <p className="text-sm text-muted-foreground">
              A execução falhou e não produziu resultados.
            </p>
          </CardContent>
        </Card>
      )}

      <EvidenceDialog
        finding={selectedFinding}
        open={evidenceDialogOpen}
        onOpenChange={setEvidenceDialogOpen}
      />
    </div>
  );
}
