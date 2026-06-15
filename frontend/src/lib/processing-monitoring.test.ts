import fs from "node:fs";
import path from "node:path";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { saveAuthToken } from "./auth";

vi.mock("@/lib/importer-progress", () => ({
  buildFallbackStages: vi.fn(() => []),
}));

describe("processing monitoring", () => {
  const localStorageMap = new Map<string, string>();
  const sessionStorageMap = new Map<string, string>();

  function createToken(exp: number): string {
    const header = Buffer.from(JSON.stringify({ alg: "HS256", typ: "JWT" })).toString("base64url");
    const payload = Buffer.from(JSON.stringify({ sub: "1", exp })).toString("base64url");
    return `${header}.${payload}.signature`;
  }

  beforeEach(() => {
    localStorageMap.clear();
    sessionStorageMap.clear();
    vi.stubGlobal("window", {
      localStorage: {
        getItem: (key: string) => localStorageMap.get(key) ?? null,
        setItem: (key: string, value: string) => localStorageMap.set(key, value),
        removeItem: (key: string) => localStorageMap.delete(key),
      },
      sessionStorage: {
        getItem: (key: string) => sessionStorageMap.get(key) ?? null,
        setItem: (key: string, value: string) => sessionStorageMap.set(key, value),
        removeItem: (key: string) => sessionStorageMap.delete(key),
      },
      location: { assign: vi.fn(), pathname: "/", search: "" },
    });
    saveAuthToken(createToken(Math.floor(Date.now() / 1000) + 60));
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("normaliza snapshot operacional vindo da API", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({
      Summary: {
        RunningJobs: 2,
        QueuedJobs: 8,
        CompletedToday: 156,
        FailedJobs: 4,
        AverageProcessingSeconds: 134,
        ProcessedRowsToday: 2340000,
        StaleJobs: 1,
      },
      Jobs: [
        {
          Id: 10,
          Company: "-",
          FileName: "vendas.xlsx",
          Template: "SALES_INVOICE",
          Status: "Processando",
          StatusLabel: "Importacao",
          CurrentStep: "Importando dados",
          ProgressPercent: 66,
          CreatedAt: "2026-06-01T10:00:00Z",
          ElapsedSeconds: 90,
          ProcessedRows: 120,
          TotalRows: 200,
          ErrorCount: 3,
        },
      ],
      Daily: [{ Date: "2026-06-01T00:00:00Z", Jobs: 3, CompletedJobs: 2, FailedJobs: 1, ProcessedRows: 500, AverageProcessingSeconds: 20, SuccessRatePercent: 66.67 }],
      StageDurations: [{ Stage: "IMPORT", StageName: "Importacao", AverageDurationSeconds: 45, SharePercent: 55 }],
      Workers: [{ WorkerId: "worker-1", Status: "Online", LastSeenAt: "2026-06-01T10:01:00Z", SecondsSinceLastSeen: 12, ProcessedJobsToday: 7, IdleSeconds: 5, CurrentTask: "Aguardando job" }],
    }), { status: 200 })));
    const { fetchProcessingMonitoringDashboard } = await import("./importer-api");

    const dashboard = await fetchProcessingMonitoringDashboard();

    expect(dashboard.summary.runningJobs).toBe(2);
    expect(dashboard.summary.processedRowsToday).toBe(2340000);
    expect(dashboard.jobs[0]).toEqual(expect.objectContaining({ id: 10, progressPercent: 66, errorCount: 3 }));
    expect(dashboard.stageDurations[0]).toEqual(expect.objectContaining({ stage: "IMPORT", sharePercent: 55 }));
    expect(dashboard.workers[0]).toEqual(expect.objectContaining({ workerId: "worker-1", status: "Online" }));
  });

  it("usa dashboard demo quando a API de monitoramento esta indisponivel", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => {
      throw new Error("fetch failed");
    }));
    const { fetchProcessingMonitoringDashboard } = await import("./importer-api");

    const dashboard = await fetchProcessingMonitoringDashboard();

    expect(dashboard.summary.runningJobs).toBeGreaterThan(0);
    expect(dashboard.jobs[0]).toEqual(expect.objectContaining({ fileName: "clientes-base-demo.xlsx" }));
    expect(dashboard.workers[0]).toEqual(expect.objectContaining({ status: "Online" }));
  });

  it("normaliza detalhes do job preservando metricas e timeline criticas", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({
      Job: {
        Id: 20,
        FileName: "vendas.xlsx",
        Status: "Processando",
        StatusLabel: "Importacao",
        CurrentStep: "Gerando resumo",
        ProgressPercent: 100,
        CreatedAt: "2026-06-01T10:00:00Z",
        StartedAt: "2026-06-01T10:01:00Z",
        FinishedAt: null,
        ElapsedSeconds: 180,
        ProcessedRows: 90,
        TotalRows: 100,
        ErrorCount: 10,
      },
      Timeline: [
        { Step: "VALIDATION", StepName: "Validacao", StartedAt: "2026-06-01T10:01:00Z", FinishedAt: "2026-06-01T10:02:00Z", DurationSeconds: 60, Status: "completed", ProcessedRows: 100, ErrorCount: 10 },
      ],
      Metrics: {
        TotalRows: 100,
        ValidRows: 90,
        InvalidRows: 10,
        ImportedRows: 90,
        ErrorCount: 10,
        WarningCount: 0,
      },
      PerformanceByStage: [
        { Stage: "VALIDATION", StageName: "Validacao", AverageDurationSeconds: 60, SharePercent: 100 },
      ],
      Logs: [
        { Timestamp: "2026-06-01T10:02:00Z", FileJobId: 20, Stage: "VALIDATION", Level: "Warning", Message: "10 linhas invalidas" },
      ],
    }), { status: 200 })));
    const { fetchProcessingJobDetails } = await import("./importer-api");

    const details = await fetchProcessingJobDetails(20);

    expect(details.job).toEqual(expect.objectContaining({ id: 20, progressPercent: 100, totalRows: 100 }));
    expect(details.metrics).toEqual(expect.objectContaining({ totalRows: 100, validRows: 90, invalidRows: 10 }));
    expect(details.timeline[0]).toEqual(expect.objectContaining({ step: "VALIDATION", errorCount: 10 }));
    expect(details.performanceByStage[0]).toEqual(expect.objectContaining({ sharePercent: 100 }));
    expect(details.logs[0]).toEqual(expect.objectContaining({ stage: "VALIDATION", level: "Warning" }));
  });

  it("expoe a rota e o menu de Processamentos", () => {
    const routeSource = fs.readFileSync(path.resolve(process.cwd(), "src/routes/processamentos.tsx"), "utf8");
    const sidebarSource = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");

    expect(routeSource).toContain('createFileRoute("/processamentos")');
    expect(routeSource).toContain("processing-page-shell");
    expect(routeSource).toContain("Central de Processamentos");
    expect(sidebarSource).toContain('to: "/processamentos"');
    expect(sidebarSource).toContain('label: "Processamentos"');
  });

  it("usa skeletons nos principais fluxos assincronos", () => {
    const processamentos = fs.readFileSync(path.resolve(process.cwd(), "src/routes/processamentos.tsx"), "utf8");
    const vendas = fs.readFileSync(path.resolve(process.cwd(), "src/routes/vendas.tsx"), "utf8");
    const clientes = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");
    const importacoes = fs.readFileSync(path.resolve(process.cwd(), "src/routes/importacoes.files.tsx"), "utf8");

    expect(processamentos).toContain("SkeletonTable");
    expect(processamentos).toContain("SkeletonChart");
    expect(vendas).toContain("SkeletonMetricCard");
    expect(vendas).toContain("Filtros avançados");
    expect(vendas).toContain("Sem resultado");
    expect(vendas).toContain("periodOptions");
    expect(clientes).toContain("SkeletonModalContent");
    expect(clientes).toContain("!loading && items.length === 0");
    expect(importacoes).toContain("jobsLoading && jobs.length === 0");
  });
});
