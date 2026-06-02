import fs from "node:fs";
import path from "node:path";
import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@/features/import-template-builder/utils/extract-headers-in-worker", () => ({
  extractHeadersInWorker: vi.fn(),
}));

vi.mock("@/lib/importer-progress", () => ({
  buildFallbackStages: vi.fn(() => []),
}));

describe("processing monitoring", () => {
  afterEach(() => {
    vi.restoreAllMocks();
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

  it("expõe a rota e o menu de Processamentos", () => {
    const routeSource = fs.readFileSync(path.resolve(process.cwd(), "src/routes/processamentos.tsx"), "utf8");
    const sidebarSource = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");

    expect(routeSource).toContain('createFileRoute("/processamentos")');
    expect(routeSource).toContain("Central de Processamentos");
    expect(sidebarSource).toContain('to: "/processamentos"');
    expect(sidebarSource).toContain('label: "Processamentos"');
  });

  it("usa skeletons nos principais fluxos assíncronos", () => {
    const processamentos = fs.readFileSync(path.resolve(process.cwd(), "src/routes/processamentos.tsx"), "utf8");
    const vendas = fs.readFileSync(path.resolve(process.cwd(), "src/routes/vendas.tsx"), "utf8");
    const clientes = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");
    const importacoes = fs.readFileSync(path.resolve(process.cwd(), "src/routes/importacoes.files.tsx"), "utf8");

    expect(processamentos).toContain("SkeletonTable");
    expect(processamentos).toContain("SkeletonChart");
    expect(vendas).toContain("SkeletonMetricCard");
    expect(vendas).toContain("!loading && items.length === 0");
    expect(clientes).toContain("SkeletonModalContent");
    expect(clientes).toContain("!loading && items.length === 0");
    expect(importacoes).toContain("jobsLoading && jobs.length === 0");
  });
});
