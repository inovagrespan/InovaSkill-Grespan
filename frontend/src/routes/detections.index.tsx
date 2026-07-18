import { Link, createFileRoute, useNavigate } from "@tanstack/react-router";
import { useCallback, useEffect, useRef, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  type DetectionRunSummaryDto,
  type DetectorDto,
  executeDetector,
  fetchDetectionRun,
  fetchDetectors,
} from "@/lib/detection-api";
import { cn } from "@/lib/utils";
import { Eye, Play, RefreshCw } from "lucide-react";

const POLLING_INTERVAL_MS = 3000;

export const Route = createFileRoute("/detections/")({
  component: DetectionsIndexPage,
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

function DetectorCard({
  detector,
  onExecute,
  executing,
}: {
  detector: DetectorDto;
  onExecute: () => void;
  executing: boolean;
}) {
  const lastRunId = detector.lastRun?.id;

  return (
    <Card className="overflow-hidden">
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div className="space-y-1">
            <CardTitle className="text-lg">{detector.name}</CardTitle>
            <CardDescription className="text-xs font-mono text-muted-foreground">
              {detector.code}
            </CardDescription>
          </div>
          <Badge variant={detector.status === "Active" ? "default" : "secondary"}>
            {detector.status === "Active" ? "Ativo" : "Desativado"}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {detector.description && (
          <p className="text-sm text-muted-foreground">{detector.description}</p>
        )}
        {detector.lastRun && (
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            <RefreshCw className="size-3 shrink-0" />
            <span>
              {detector.lastRun.analyzedItems} analisados · {detector.lastRun.findingsCount} encontrados
            </span>
            <span className="ml-auto">
              <StatusBadge status={detector.lastRun.status} />
            </span>
          </div>
        )}
      </CardContent>
      <CardFooter className="flex gap-2 border-t bg-muted/30 px-6 py-3">
        <Button
          size="sm"
          disabled={executing || detector.status !== "Active"}
          onClick={onExecute}
        >
          <Play className="mr-1.5 size-3.5" />
          {executing ? "Executando..." : "Executar agora"}
        </Button>
        {lastRunId && (
          <Button variant="outline" size="sm" asChild>
            <Link to="/detections/$runId" params={{ runId: lastRunId }}>
              <Eye className="mr-1.5 size-3.5" />
              Ver execuções
            </Link>
          </Button>
        )}
      </CardFooter>
    </Card>
  );
}

function DetectorGridSkeleton() {
  return (
    <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
      {Array.from({ length: 3 }).map((_, i) => (
        <Card key={i}>
          <CardHeader>
            <Skeleton className="h-5 w-48" />
            <Skeleton className="mt-1 h-3 w-32" />
          </CardHeader>
          <CardContent>
            <Skeleton className="h-4 w-full" />
            <Skeleton className="mt-2 h-3 w-36" />
          </CardContent>
          <CardFooter className="border-t">
            <Skeleton className="h-9 w-28" />
          </CardFooter>
        </Card>
      ))}
    </div>
  );
}

function EmptyState() {
  return (
    <Card>
      <CardContent className="flex flex-col items-center gap-2 py-12">
        <p className="text-sm text-muted-foreground">Nenhum detector disponível.</p>
      </CardContent>
    </Card>
  );
}

function DetectionsIndexPage() {
  const navigate = useNavigate();
  const [detectors, setDetectors] = useState<DetectorDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [executingId, setExecutingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const activePolls = useRef<Map<string, string>>(new Map());
  const pollTimers = useRef<Map<string, ReturnType<typeof setInterval>>>(new Map());

  const loadDetectors = useCallback(async () => {
    try {
      setError(null);
      const data = await fetchDetectors();
      setDetectors(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao carregar detectores.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadDetectors();
  }, [loadDetectors]);

  useEffect(() => {
    return () => {
      pollTimers.current.forEach((timer) => clearInterval(timer));
      pollTimers.current.clear();
    };
  }, []);

  const startPolling = useCallback(
    (detectorId: string, runId: string) => {
      const key = detectorId;

      if (pollTimers.current.has(key)) {
        clearInterval(pollTimers.current.get(key)!);
      }

      activePolls.current.set(key, runId);

      const timer = setInterval(async () => {
        try {
          const currentRunId = activePolls.current.get(key);
          if (!currentRunId) return;

          const run = await fetchDetectionRun(currentRunId);

          if (run.status === "Succeeded" || run.status === "Failed") {
            clearInterval(timer);
            pollTimers.current.delete(key);
            activePolls.current.delete(key);
            setExecutingId((prev) => (prev === detectorId ? null : prev));
            void loadDetectors();
          }
        } catch {
          // ignore polling errors
        }
      }, POLLING_INTERVAL_MS);

      pollTimers.current.set(key, timer);
    },
    [loadDetectors],
  );

  const handleExecute = useCallback(
    async (detectorId: string) => {
      setExecutingId(detectorId);
      try {
        const result = await executeDetector(detectorId);
        startPolling(detectorId, result.runId);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Falha ao executar detector.");
        setExecutingId(null);
      }
    },
    [startPolling],
  );

  if (loading) {
    return (
      <div className="p-6 space-y-6">
        <div>
          <h1 className="text-2xl font-bold">Detecção de Inconformidades</h1>
          <p className="text-sm text-muted-foreground">
            Execute os detectores e visualize o que foi encontrado.
          </p>
        </div>
        <DetectorGridSkeleton />
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Detecção de Inconformidades</h1>
          <p className="text-sm text-muted-foreground">
            Execute os detectores e visualize o que foi encontrado.
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => loadDetectors()}>
          <RefreshCw className="mr-1.5 size-3.5" />
          Atualizar
        </Button>
      </div>

      {error && (
        <div className="rounded-lg border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-destructive">
          {error}
        </div>
      )}

      {detectors.length === 0 ? (
        <EmptyState />
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {detectors.map((detector) => (
            <DetectorCard
              key={detector.id}
              detector={detector}
              onExecute={() => handleExecute(detector.id)}
              executing={executingId === detector.id}
            />
          ))}
        </div>
      )}
    </div>
  );
}
