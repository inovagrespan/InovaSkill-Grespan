import { authFetch } from "@/lib/auth";
import { buildFallbackStages, type FileJobStageProgress } from "@/lib/importer-progress";

export type FileType = "Unknown" | "Customers" | "Orders" | "Products" | "CommercialTransaction";

export type JobStatus =
  | "WaitingProcessing"
  | "PreProcessing"
  | "Validating"
  | "ValidationFailed"
  | "ReadyToImport"
  | "Importing"
  | "Completed"
  | "Failed"
  | "Cancelled";

export type FileJob = {
  id: number;
  filePath: string;
  fileType: FileType;
  status: JobStatus;
  createdAt: string;
  errorCount: number;
  currentStep: string;
  progressPercent: number;
  processedRows: number;
  totalRows: number;
  currentStageCode: string | null;
  currentStageName: string | null;
  stages: FileJobStageProgress[];
};

export type ImportError = {
  id: number;
  fileJobId: number;
  rowNumber: number;
  stage: string;
  column: string;
  message: string;
  recordIdentifier: string;
};

export type PagedResult<T> = {
  page: number;
  pageSize: number;
  total: number;
  items: T[];
};

export type CommercialTransaction = {
  id: number;
  documentNumber: string;
  transactionDate: string;
  customerCode: string;
  customerName: string;
  productCode: string;
  productDescription: string;
  quantity: number;
  unitPrice: number;
  totalAmount: number;
  transactionType: string;
  city: string;
  productGroup: string;
  grossWeightKg: number;
  sourceFileJobId: number;
};

export type SummaryGranularity = "daily" | "weekly" | "monthly";
export type SummarySortBy = "growth" | "amount" | "weight" | "quantity";
export type CommercialTransactionCompanySummary = {
  companyName: string;
  totalAmount: number;
  totalQuantity: number;
  totalWeightKg: number;
  currentPeriodAmount: number;
  previousPeriodAmount: number;
  growthPercent: number | null;
};
export type CommercialTransactionSummaryResponse = {
  page: number;
  pageSize: number;
  totalItems: number;
  granularity: SummaryGranularity;
  currentPeriodStart: string;
  previousPeriodStart: string;
  currentPeriodTotalAmount: number;
  previousPeriodTotalAmount: number;
  totalGrowthPercent: number | null;
  totalRecords: number;
  totalAmount: number;
  totalQuantity: number;
  totalWeightKg: number;
  totalCompanies: number;
  items: CommercialTransactionCompanySummary[];
};

export type ProcessingMonitoringDashboard = {
  summary: ProcessingMonitoringSummary;
  jobs: ProcessingJobQueueItem[];
  daily: ProcessingDailyPoint[];
  stageDurations: ProcessingStageDuration[];
  workers: WorkerHealth[];
};

export type ProcessingMonitoringSummary = {
  runningJobs: number;
  queuedJobs: number;
  completedToday: number;
  failedJobs: number;
  averageProcessingSeconds: number;
  processedRowsToday: number;
  staleJobs: number;
};

export type ProcessingJobQueueItem = {
  id: number;
  company: string;
  fileName: string;
  template: string | null;
  status: string;
  statusLabel: string;
  currentStep: string;
  progressPercent: number;
  currentStageCode: string | null;
  currentStageName: string | null;
  stages: FileJobStageProgress[];
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  elapsedSeconds: number;
  processedRows: number;
  totalRows: number;
  errorCount: number;
};

export type ProcessingDailyPoint = {
  date: string;
  jobs: number;
  completedJobs: number;
  failedJobs: number;
  processedRows: number;
  averageProcessingSeconds: number;
  successRatePercent: number;
};

export type ProcessingStageDuration = {
  stage: string;
  stageName: string;
  averageDurationSeconds: number;
  sharePercent: number;
};

export type WorkerHealth = {
  workerId: string;
  status: string;
  lastSeenAt: string;
  secondsSinceLastSeen: number;
  processedJobsToday: number;
  idleSeconds: number;
  currentJobId: number | null;
  currentTask: string;
};

export type ProcessingJobDetails = {
  job: ProcessingJobQueueItem;
  timeline: ProcessingStep[];
  metrics: ProcessingJobMetrics;
  performanceByStage: ProcessingStageDuration[];
  logs: ProcessingLog[];
};

export type ProcessingStep = {
  step: string;
  stepName: string;
  startedAt: string | null;
  finishedAt: string | null;
  durationSeconds: number;
  status: string;
  processedRows: number;
  errorCount: number;
};

export type ProcessingJobMetrics = {
  totalRows: number;
  validRows: number;
  invalidRows: number;
  importedRows: number;
  errorCount: number;
  warningCount: number;
};

export type ProcessingLog = {
  timestamp: string;
  fileJobId: number;
  stage: string;
  level: string;
  message: string;
};

export type CustomerAnalyticsSummary = {
  activeCustomers: number;
  totalRevenue: number;
  totalOrders: number;
  averageTicket: number;
  averageRevenuePerCustomer: number;
  newCustomers: number;
  inactiveCustomers: number;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  previousPeriodStart: string;
  previousPeriodEnd: string;
};

export type CustomerRankingItem = {
  customerCode: string;
  customerName: string;
  revenue: number;
  quantity: number;
  weight: number;
  orders: number;
  averageTicket: number;
  variationPercent: number | null;
};

export type CustomerRankingResponse = {
  page: number;
  pageSize: number;
  totalItems: number;
  items: CustomerRankingItem[];
};

export type CustomerNewCustomersMonthlyPoint = {
  monthStart: string;
  newCustomers: number;
};

export type CustomerNewCustomersMonthlyResponse = {
  periodStart: string;
  periodEnd: string;
  totalNewCustomers: number;
  activeMonths: number;
  points: CustomerNewCustomersMonthlyPoint[];
};

export type CustomerDetailSummary = {
  customerCode: string;
  customerName: string;
  city: string;
  linkedCompany: string;
  lastPurchaseDate: string | null;
  status: "Ativo" | "Inativo" | "Em queda" | "Em crescimento";
  totalRevenue: number;
  averageTicket: number | null;
  averageRevenueMonthly: number | null;
  averageRevenueWeekly: number | null;
  totalQuantity: number;
  totalWeight: number;
  totalOrders: number;
  averageDaysBetweenPurchases: number | null;
};

export type CustomerTimelinePoint = {
  periodStart: string;
  value: number;
  revenue: number;
  quantity: number;
  weight: number;
  orders: number;
};

export type CustomerTimelineResponse = {
  granularity: "daily" | "weekly" | "monthly";
  metric: "revenue" | "quantity" | "weight" | "orders";
  points: CustomerTimelinePoint[];
};

export type CustomerComparisonItem = {
  label: string;
  currentValue: number;
  previousValue: number;
  variationPercent: number | null;
};

export type CustomerComparisonResponse = {
  items: CustomerComparisonItem[];
};

export type CustomerTopProductItem = {
  productCode: string;
  productDescription: string;
  quantity: number;
  revenue: number;
  sharePercent: number;
};

export type CustomerPurchaseHistoryItem = {
  date: string;
  document: string;
  product: string;
  quantity: number;
  unitPrice: number;
  total: number;
  weight: number;
  operationType: string;
};

export type CustomerPurchaseHistoryResponse = {
  page: number;
  pageSize: number;
  totalItems: number;
  items: CustomerPurchaseHistoryItem[];
};

export type CustomerInsightsResponse = {
  averagePurchaseFrequencyDays: number | null;
  estimatedNextPurchaseDate: string | null;
  predictedRevenue: number | null;
  predictedQuantity: number | null;
  consumptionTrend: "Crescimento" | "Estabilidade" | "Queda" | string;
  riskLevel: "Sem risco" | "Atenção" | "Em risco" | "Crítico" | "Sem histórico suficiente" | string;
  daysWithoutPurchase: number;
  riskScore: number | null;
  frequencyReason: string | null;
  nextPurchaseReason: string | null;
  revenuePredictionReason: string | null;
  quantityPredictionReason: string | null;
  riskReason: string | null;
  monthlyHistoryPeriods: number;
};

export type CustomerCommercialHealthReport = {
  header: CustomerCommercialHealthHeader;
  score: CustomerCommercialHealthScore;
  health: CustomerCommercialHealthBlock;
  trend: CustomerCommercialHealthBlock;
  potential: CustomerCommercialHealthPotential;
  dependency: CustomerCommercialHealthDependency;
  products: CustomerCommercialHealthProduct[];
  timeline: CustomerCommercialHealthTimelineItem[];
  evolution: CustomerCommercialHealthEvolutionPoint[];
  comparisons: CustomerCommercialHealthComparison[];
  recommendations: CustomerCommercialHealthRecommendation[];
  alerts: CustomerCommercialHealthAlert[];
};

export type CustomerCommercialHealthHeader = {
  customerCode: string;
  customerName: string;
  city: string;
  linkedCompany: string;
  lastPurchaseDate: string | null;
  daysWithoutPurchase: number;
  averageDaysBetweenPurchases: number | null;
  commercialStatus: string;
};

export type CustomerCommercialHealthScore = {
  value: number;
  label: string;
  explanation: string;
};

export type CustomerCommercialHealthBlock = {
  status: string;
  tone: "success" | "warning" | "danger" | "neutral" | string;
  summary: string;
  detail: string;
};

export type CustomerCommercialHealthPotential = {
  expectedRevenue: number | null;
  expectedQuantity: number | null;
  label: string;
  explanation: string;
};

export type CustomerCommercialHealthDependency = {
  status: string;
  explanation: string;
  productsToReachEightyPercent: number;
  topProductSharePercent: number;
};

export type CustomerCommercialHealthProduct = {
  productCode: string;
  productDescription: string;
  quantity: number;
  revenue: number;
  sharePercent: number;
};

export type CustomerCommercialHealthTimelineItem = {
  date: string;
  orders: number;
  revenue: number;
  quantity: number;
};

export type CustomerCommercialHealthEvolutionPoint = {
  periodStart: string;
  revenue: number;
  quantity: number;
  orders: number;
  averageTicket: number;
};

export type CustomerCommercialHealthComparison = {
  label: string;
  revenue: number;
  previousRevenue: number;
  quantity: number;
  previousQuantity: number;
  orders: number;
  previousOrders: number;
  averageTicket: number;
  previousAverageTicket: number;
  revenueVariationPercent: number | null;
  quantityVariationPercent: number | null;
  ordersVariationPercent: number | null;
  averageTicketVariationPercent: number | null;
};

export type CustomerCommercialHealthRecommendation = {
  priority: string;
  title: string;
  detail: string;
};

export type CustomerCommercialHealthAlert = {
  severity: "critical" | "warning" | "info" | string;
  title: string;
  detail: string;
};

const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5279";
export const MAX_UPLOAD_SIZE_BYTES = 524_288_000;

async function parseApiError(response: Response, fallbackMessage: string): Promise<string> {
  try {
    const contentType = response.headers.get("content-type") ?? "";
    if (contentType.includes("application/json")) {
      const payload = (await response.json()) as {
        detail?: string;
        Detail?: string;
        title?: string;
        Title?: string;
        message?: string;
        Message?: string;
      };

      return (
        payload.detail ??
        payload.Detail ??
        payload.message ??
        payload.Message ??
        payload.title ??
        payload.Title ??
        fallbackMessage
      );
    }

    const text = (await response.text()).trim();
    return text || fallbackMessage;
  } catch {
    return fallbackMessage;
  }
}

type BackendFileJob = {
  id?: number;
  Id?: number;
  filePath?: string;
  FilePath?: string;
  fileType?: number | string;
  FileType?: number | string;
  status?: number | string;
  Status?: number | string;
  createdAt?: string;
  CreatedAt?: string;
  errorCount?: number;
  ErrorCount?: number;
  currentStep?: string;
  CurrentStep?: string;
  progressPercent?: number;
  ProgressPercent?: number;
  processedRows?: number;
  ProcessedRows?: number;
  totalRows?: number;
  TotalRows?: number;
  currentStageCode?: string | null;
  CurrentStageCode?: string | null;
  currentStageName?: string | null;
  CurrentStageName?: string | null;
  stages?: BackendFileJobStage[];
  Stages?: BackendFileJobStage[];
};

type BackendFileJobStage = {
  code?: string;
  Code?: string;
  name?: string;
  Name?: string;
  status?: string;
  Status?: string;
  progressPercent?: number;
  ProgressPercent?: number;
  errorCount?: number;
  ErrorCount?: number;
};

const fileTypeMap: Record<number, FileType> = {
  0: "Unknown",
  1: "Customers",
  2: "Orders",
  3: "Products",
  4: "CommercialTransaction",
};

const statusMap: Record<number, JobStatus> = {
  0: "WaitingProcessing",
  1: "PreProcessing",
  2: "Validating",
  3: "ValidationFailed",
  4: "ReadyToImport",
  5: "Importing",
  6: "Completed",
  7: "Failed",
  8: "Cancelled",
};

function normalizeFileType(value: number | string): FileType {
  if (typeof value === "number") return fileTypeMap[value] ?? "Unknown";
  return (value as FileType) ?? "Unknown";
}

function normalizeStatus(value: number | string): JobStatus {
  if (typeof value === "number") return statusMap[value] ?? "Failed";
  return (value as JobStatus) ?? "Failed";
}

export async function uploadFile(file: File): Promise<number> {
  const form = new FormData();
  form.append("file", file);

  try {
    const response = await authFetch(`${API_URL}/api/files/upload`, {
      method: "POST",
      body: form,
    });

    if (!response.ok) {
      if (response.status === 413) {
        throw new Error("Arquivo muito grande. O limite atual é 500 MB.");
      }
      throw new Error(await parseApiError(response, `Falha ao enviar '${file.name}'.`));
    }

    const data = (await response.json()) as { fileJobId: number };
    return data.fileJobId;
  } catch (error) {
    if (error instanceof Error && /Failed to fetch|NetworkError|Load failed/i.test(error.message)) {
      throw new Error("Não foi possível conectar com a API. Verifique se os containers estão ativos e tente novamente.");
    }

    throw error;
  }
}

export async function fetchJobs(page = 1, pageSize = 10): Promise<PagedResult<FileJob>> {
  const response = await authFetch(`${API_URL}/api/files/jobs?page=${page}&pageSize=${pageSize}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar arquivos."));

  const raw = (await response.json()) as
    | PagedResult<BackendFileJob>
    | { Page?: number; PageSize?: number; Total?: number; Items?: BackendFileJob[] };

  const items = (
    (raw as PagedResult<BackendFileJob>).items ??
    (raw as { Items?: BackendFileJob[] }).Items ??
    []
  ).map((job) => {
    const status = normalizeStatus(job.status ?? job.Status ?? "Failed");
    const progressPercent = job.progressPercent ?? job.ProgressPercent ?? 0;
    const errorCount = job.errorCount ?? job.ErrorCount ?? 0;
    const stages = normalizeStages(job.stages ?? job.Stages, status, progressPercent, errorCount);
    const runningStage = stages.find((stage) => stage.status === "running");

    return {
      id: job.id ?? job.Id ?? 0,
      filePath: job.filePath ?? job.FilePath ?? "",
      fileType: normalizeFileType(job.fileType ?? job.FileType ?? "Unknown"),
      status,
      createdAt: job.createdAt ?? job.CreatedAt ?? "",
      errorCount,
      currentStep: job.currentStep ?? job.CurrentStep ?? "",
      progressPercent,
      processedRows: job.processedRows ?? job.ProcessedRows ?? 0,
      totalRows: job.totalRows ?? job.TotalRows ?? 0,
      currentStageCode: job.currentStageCode ?? job.CurrentStageCode ?? runningStage?.code ?? null,
      currentStageName: job.currentStageName ?? job.CurrentStageName ?? runningStage?.name ?? null,
      stages,
    };
  });

  return {
    page: (raw as PagedResult<BackendFileJob>).page ?? (raw as { Page?: number }).Page ?? page,
    pageSize:
      (raw as PagedResult<BackendFileJob>).pageSize ??
      (raw as { PageSize?: number }).PageSize ??
      pageSize,
    total: (raw as PagedResult<BackendFileJob>).total ?? (raw as { Total?: number }).Total ?? 0,
    items,
  };
}

function normalizeStages(
  stages: BackendFileJobStage[] | undefined,
  status: JobStatus,
  progressPercent: number,
  errorCount: number,
): FileJobStageProgress[] {
  if (!stages || stages.length === 0) {
    return buildFallbackStages({ status, progressPercent, errorCount });
  }

  return stages.map((stage) => ({
    code: stage.code ?? stage.Code ?? "",
    name: stage.name ?? stage.Name ?? "",
    status: normalizeStageStatus(stage.status ?? stage.Status ?? "pending"),
    progressPercent: stage.progressPercent ?? stage.ProgressPercent ?? 0,
    errorCount: stage.errorCount ?? stage.ErrorCount ?? 0,
  }));
}

function normalizeStageStatus(value: string): FileJobStageProgress["status"] {
  if (value === "running" || value === "completed" || value === "failed") {
    return value;
  }

  return "pending";
}

export async function fetchJobErrors(
  jobId: number,
  page = 1,
  pageSize = 50,
): Promise<PagedResult<ImportError>> {
  const response = await authFetch(`${API_URL}/api/files/jobs/${jobId}/errors?page=${page}&pageSize=${pageSize}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar erros do arquivo."));

  const rawJson = await response.json();
  if (Array.isArray(rawJson)) {
    const legacyItems = rawJson.map((e: any) => ({
      id: e.id ?? e.Id ?? 0,
      fileJobId: e.fileJobId ?? e.FileJobId ?? jobId,
      rowNumber: e.rowNumber ?? e.RowNumber ?? 0,
      stage: e.stage ?? e.Stage ?? "",
      column: e.column ?? e.Column ?? "",
      message: e.message ?? e.Message ?? "",
      recordIdentifier: e.recordIdentifier ?? e.RecordIdentifier ?? "",
    })) as ImportError[];

    const total = legacyItems.length;
    const start = (page - 1) * pageSize;
    return { page, pageSize, total, items: legacyItems.slice(start, start + pageSize) };
  }

  const raw = rawJson as
    | PagedResult<ImportError>
    | { Page?: number; PageSize?: number; Total?: number; Items?: ImportError[] };
  const items = ((raw as PagedResult<ImportError>).items ?? (raw as { Items?: ImportError[] }).Items ?? []).map(
    (e: any) => ({
      id: e.id ?? e.Id ?? 0,
      fileJobId: e.fileJobId ?? e.FileJobId ?? jobId,
      rowNumber: e.rowNumber ?? e.RowNumber ?? 0,
      stage: e.stage ?? e.Stage ?? "",
      column: e.column ?? e.Column ?? "",
      message: e.message ?? e.Message ?? "",
      recordIdentifier: e.recordIdentifier ?? e.RecordIdentifier ?? "",
    }),
  );

  return {
    page: (raw as PagedResult<ImportError>).page ?? (raw as { Page?: number }).Page ?? 1,
    pageSize:
      (raw as PagedResult<ImportError>).pageSize ??
      (raw as { PageSize?: number }).PageSize ??
      pageSize,
    total: (raw as PagedResult<ImportError>).total ?? (raw as { Total?: number }).Total ?? 0,
    items,
  };
}

export async function fetchProcessingMonitoringDashboard(): Promise<ProcessingMonitoringDashboard> {
  const response = await fetch(`${API_URL}/api/processing-monitoring/dashboard`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar monitoramento de jobs."));
  return normalizeProcessingDashboard(await response.json());
}

export async function fetchProcessingJobDetails(jobId: number): Promise<ProcessingJobDetails> {
  const response = await fetch(`${API_URL}/api/processing-monitoring/jobs/${jobId}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar detalhes do job."));
  return normalizeProcessingJobDetails(await response.json());
}

export async function retryProcessingJob(jobId: number): Promise<void> {
  const response = await fetch(`${API_URL}/api/processing-monitoring/jobs/${jobId}/retry`, { method: "POST" });
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao reprocessar job."));
}

export async function cancelProcessingJob(jobId: number): Promise<void> {
  const response = await fetch(`${API_URL}/api/processing-monitoring/jobs/${jobId}/cancel`, { method: "POST" });
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao cancelar job."));
}

function normalizeProcessingDashboard(raw: any): ProcessingMonitoringDashboard {
  return {
    summary: normalizeProcessingSummary(raw.summary ?? raw.Summary ?? {}),
    jobs: (raw.jobs ?? raw.Jobs ?? []).map(normalizeProcessingJobItem),
    daily: (raw.daily ?? raw.Daily ?? []).map(normalizeProcessingDailyPoint),
    stageDurations: (raw.stageDurations ?? raw.StageDurations ?? []).map(normalizeProcessingStageDuration),
    workers: (raw.workers ?? raw.Workers ?? []).map(normalizeWorkerHealth),
  };
}

function normalizeProcessingJobDetails(raw: any): ProcessingJobDetails {
  return {
    job: normalizeProcessingJobItem(raw.job ?? raw.Job ?? {}),
    timeline: (raw.timeline ?? raw.Timeline ?? []).map(normalizeProcessingStep),
    metrics: normalizeProcessingMetrics(raw.metrics ?? raw.Metrics ?? {}),
    performanceByStage: (raw.performanceByStage ?? raw.PerformanceByStage ?? []).map(normalizeProcessingStageDuration),
    logs: (raw.logs ?? raw.Logs ?? []).map(normalizeProcessingLog),
  };
}

function normalizeProcessingSummary(raw: any): ProcessingMonitoringSummary {
  return {
    runningJobs: raw.runningJobs ?? raw.RunningJobs ?? 0,
    queuedJobs: raw.queuedJobs ?? raw.QueuedJobs ?? 0,
    completedToday: raw.completedToday ?? raw.CompletedToday ?? 0,
    failedJobs: raw.failedJobs ?? raw.FailedJobs ?? 0,
    averageProcessingSeconds: raw.averageProcessingSeconds ?? raw.AverageProcessingSeconds ?? 0,
    processedRowsToday: raw.processedRowsToday ?? raw.ProcessedRowsToday ?? 0,
    staleJobs: raw.staleJobs ?? raw.StaleJobs ?? 0,
  };
}

function normalizeProcessingJobItem(raw: any): ProcessingJobQueueItem {
  const stages = normalizeStages(
    raw.stages ?? raw.Stages,
    normalizeStatus(raw.status ?? raw.Status ?? "Failed"),
    raw.progressPercent ?? raw.ProgressPercent ?? 0,
    raw.errorCount ?? raw.ErrorCount ?? 0,
  );
  const runningStage = stages.find((stage) => stage.status === "running");

  return {
    id: raw.id ?? raw.Id ?? 0,
    company: raw.company ?? raw.Company ?? "-",
    fileName: raw.fileName ?? raw.FileName ?? "",
    template: raw.template ?? raw.Template ?? null,
    status: raw.status ?? raw.Status ?? "",
    statusLabel: raw.statusLabel ?? raw.StatusLabel ?? "",
    currentStep: raw.currentStep ?? raw.CurrentStep ?? "",
    progressPercent: raw.progressPercent ?? raw.ProgressPercent ?? 0,
    currentStageCode: raw.currentStageCode ?? raw.CurrentStageCode ?? runningStage?.code ?? null,
    currentStageName: raw.currentStageName ?? raw.CurrentStageName ?? runningStage?.name ?? null,
    stages,
    createdAt: raw.createdAt ?? raw.CreatedAt ?? "",
    startedAt: raw.startedAt ?? raw.StartedAt ?? null,
    finishedAt: raw.finishedAt ?? raw.FinishedAt ?? null,
    elapsedSeconds: raw.elapsedSeconds ?? raw.ElapsedSeconds ?? 0,
    processedRows: raw.processedRows ?? raw.ProcessedRows ?? 0,
    totalRows: raw.totalRows ?? raw.TotalRows ?? 0,
    errorCount: raw.errorCount ?? raw.ErrorCount ?? 0,
  };
}

function normalizeProcessingDailyPoint(raw: any): ProcessingDailyPoint {
  return {
    date: raw.date ?? raw.Date ?? "",
    jobs: raw.jobs ?? raw.Jobs ?? 0,
    completedJobs: raw.completedJobs ?? raw.CompletedJobs ?? 0,
    failedJobs: raw.failedJobs ?? raw.FailedJobs ?? 0,
    processedRows: raw.processedRows ?? raw.ProcessedRows ?? 0,
    averageProcessingSeconds: raw.averageProcessingSeconds ?? raw.AverageProcessingSeconds ?? 0,
    successRatePercent: raw.successRatePercent ?? raw.SuccessRatePercent ?? 0,
  };
}

function normalizeProcessingStageDuration(raw: any): ProcessingStageDuration {
  return {
    stage: raw.stage ?? raw.Stage ?? "",
    stageName: raw.stageName ?? raw.StageName ?? "",
    averageDurationSeconds: raw.averageDurationSeconds ?? raw.AverageDurationSeconds ?? 0,
    sharePercent: raw.sharePercent ?? raw.SharePercent ?? 0,
  };
}

function normalizeWorkerHealth(raw: any): WorkerHealth {
  return {
    workerId: raw.workerId ?? raw.WorkerId ?? "",
    status: raw.status ?? raw.Status ?? "Offline",
    lastSeenAt: raw.lastSeenAt ?? raw.LastSeenAt ?? "",
    secondsSinceLastSeen: raw.secondsSinceLastSeen ?? raw.SecondsSinceLastSeen ?? 0,
    processedJobsToday: raw.processedJobsToday ?? raw.ProcessedJobsToday ?? 0,
    idleSeconds: raw.idleSeconds ?? raw.IdleSeconds ?? 0,
    currentJobId: raw.currentJobId ?? raw.CurrentJobId ?? null,
    currentTask: raw.currentTask ?? raw.CurrentTask ?? "",
  };
}

function normalizeProcessingStep(raw: any): ProcessingStep {
  return {
    step: raw.step ?? raw.Step ?? "",
    stepName: raw.stepName ?? raw.StepName ?? "",
    startedAt: raw.startedAt ?? raw.StartedAt ?? null,
    finishedAt: raw.finishedAt ?? raw.FinishedAt ?? null,
    durationSeconds: raw.durationSeconds ?? raw.DurationSeconds ?? 0,
    status: raw.status ?? raw.Status ?? "pending",
    processedRows: raw.processedRows ?? raw.ProcessedRows ?? 0,
    errorCount: raw.errorCount ?? raw.ErrorCount ?? 0,
  };
}

function normalizeProcessingMetrics(raw: any): ProcessingJobMetrics {
  return {
    totalRows: raw.totalRows ?? raw.TotalRows ?? 0,
    validRows: raw.validRows ?? raw.ValidRows ?? 0,
    invalidRows: raw.invalidRows ?? raw.InvalidRows ?? 0,
    importedRows: raw.importedRows ?? raw.ImportedRows ?? 0,
    errorCount: raw.errorCount ?? raw.ErrorCount ?? 0,
    warningCount: raw.warningCount ?? raw.WarningCount ?? 0,
  };
}

function normalizeProcessingLog(raw: any): ProcessingLog {
  return {
    timestamp: raw.timestamp ?? raw.Timestamp ?? "",
    fileJobId: raw.fileJobId ?? raw.FileJobId ?? 0,
    stage: raw.stage ?? raw.Stage ?? "",
    level: raw.level ?? raw.Level ?? "",
    message: raw.message ?? raw.Message ?? "",
  };
}

export async function fetchCommercialTransactions(input: {
  page?: number;
  pageSize?: number;
  documentNumber?: string;
  customerCode?: string;
  customerName?: string;
  productCode?: string;
  city?: string;
  productGroup?: string;
  transactionType?: string;
  dateFrom?: string;
  dateTo?: string;
}): Promise<PagedResult<CommercialTransaction>> {
  const query = new URLSearchParams();
  query.set("page", String(input.page ?? 1));
  query.set("pageSize", String(input.pageSize ?? 20));

  if (input.documentNumber?.trim()) query.set("documentNumber", input.documentNumber.trim());
  if (input.customerCode?.trim()) query.set("customerCode", input.customerCode.trim());
  if (input.customerName?.trim()) query.set("customerName", input.customerName.trim());
  if (input.productCode?.trim()) query.set("productCode", input.productCode.trim());
  if (input.city?.trim()) query.set("city", input.city.trim());
  if (input.productGroup?.trim()) query.set("productGroup", input.productGroup.trim());
  if (input.transactionType?.trim()) query.set("transactionType", input.transactionType.trim());
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());

  const response = await authFetch(`${API_URL}/api/commercial-transactions?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar vendas."));

  const raw = (await response.json()) as
    | PagedResult<CommercialTransaction>
    | { Page?: number; PageSize?: number; Total?: number; Items?: CommercialTransaction[] };

  const items = (raw as PagedResult<CommercialTransaction>).items ?? (raw as { Items?: CommercialTransaction[] }).Items ?? [];

  return {
    page: (raw as PagedResult<CommercialTransaction>).page ?? (raw as { Page?: number }).Page ?? 1,
    pageSize: (raw as PagedResult<CommercialTransaction>).pageSize ?? (raw as { PageSize?: number }).PageSize ?? 20,
    total: (raw as PagedResult<CommercialTransaction>).total ?? (raw as { Total?: number }).Total ?? 0,
    items,
  };
}

export async function fetchCommercialTransactionsSummary(input: {
  page?: number;
  pageSize?: number;
  granularity?: SummaryGranularity;
  sortBy?: SummarySortBy;
  documentNumber?: string;
  customerCode?: string;
  customerName?: string;
  productCode?: string;
  city?: string;
  productGroup?: string;
  transactionType?: string;
  dateFrom?: string;
  dateTo?: string;
  referenceDate?: string;
}): Promise<CommercialTransactionSummaryResponse> {
  const query = new URLSearchParams();
  query.set("page", String(input.page ?? 1));
  query.set("pageSize", String(input.pageSize ?? 20));
  query.set("granularity", input.granularity ?? "weekly");
  query.set("sortBy", input.sortBy ?? "growth");

  if (input.documentNumber?.trim()) query.set("documentNumber", input.documentNumber.trim());
  if (input.customerCode?.trim()) query.set("customerCode", input.customerCode.trim());
  if (input.customerName?.trim()) query.set("customerName", input.customerName.trim());
  if (input.productCode?.trim()) query.set("productCode", input.productCode.trim());
  if (input.city?.trim()) query.set("city", input.city.trim());
  if (input.productGroup?.trim()) query.set("productGroup", input.productGroup.trim());
  if (input.transactionType?.trim()) query.set("transactionType", input.transactionType.trim());
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());
  if (input.referenceDate?.trim()) query.set("referenceDate", input.referenceDate.trim());

  const response = await authFetch(`${API_URL}/api/commercial-transactions/summary?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar resumo de vendas."));

  return (await response.json()) as CommercialTransactionSummaryResponse;
}

export async function fetchCustomerAnalyticsSummary(input: {
  dateFrom?: string;
  dateTo?: string;
  customer?: string;
  city?: string;
  productGroup?: string;
  productCode?: string;
  transactionType?: string;
}): Promise<CustomerAnalyticsSummary> {
  const query = new URLSearchParams();
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());
  if (input.customer?.trim()) query.set("customer", input.customer.trim());
  if (input.city?.trim()) query.set("city", input.city.trim());
  if (input.productGroup?.trim()) query.set("productGroup", input.productGroup.trim());
  if (input.productCode?.trim()) query.set("productCode", input.productCode.trim());
  if (input.transactionType?.trim()) query.set("transactionType", input.transactionType.trim());

  const response = await authFetch(`${API_URL}/api/customer-analytics-v2/summary?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar resumo de clientes."));
  return (await response.json()) as CustomerAnalyticsSummary;
}

export async function fetchCustomerRanking(input: {
  page?: number;
  pageSize?: number;
  sortBy?: "revenue" | "growth" | "drop" | "quantity" | "weight" | "ticket";
  dateFrom?: string;
  dateTo?: string;
  customer?: string;
  city?: string;
  productGroup?: string;
  productCode?: string;
  transactionType?: string;
}): Promise<CustomerRankingResponse> {
  const query = new URLSearchParams();
  query.set("page", String(input.page ?? 1));
  query.set("pageSize", String(input.pageSize ?? 20));
  query.set("sortBy", input.sortBy ?? "revenue");

  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());
  if (input.customer?.trim()) query.set("customer", input.customer.trim());
  if (input.city?.trim()) query.set("city", input.city.trim());
  if (input.productGroup?.trim()) query.set("productGroup", input.productGroup.trim());
  if (input.productCode?.trim()) query.set("productCode", input.productCode.trim());
  if (input.transactionType?.trim()) query.set("transactionType", input.transactionType.trim());

  const response = await authFetch(`${API_URL}/api/customer-analytics-v2/ranking?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar ranking de clientes."));
  return (await response.json()) as CustomerRankingResponse;
}

export async function fetchCustomerNewCustomersMonthly(input: {
  dateFrom?: string;
  dateTo?: string;
  customer?: string;
  city?: string;
  productGroup?: string;
  productCode?: string;
  transactionType?: string;
}): Promise<CustomerNewCustomersMonthlyResponse> {
  const query = new URLSearchParams();
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());
  if (input.customer?.trim()) query.set("customer", input.customer.trim());
  if (input.city?.trim()) query.set("city", input.city.trim());
  if (input.productGroup?.trim()) query.set("productGroup", input.productGroup.trim());
  if (input.productCode?.trim()) query.set("productCode", input.productCode.trim());
  if (input.transactionType?.trim()) query.set("transactionType", input.transactionType.trim());

  const response = await authFetch(`${API_URL}/api/customer-analytics-v2/new-customers-monthly?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar evolução mensal de novos clientes."));
  return (await response.json()) as CustomerNewCustomersMonthlyResponse;
}

export async function fetchCustomerDetailsSummary(input: {
  customerId: string;
  dateFrom?: string;
  dateTo?: string;
}): Promise<CustomerDetailSummary> {
  const query = new URLSearchParams();
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());

  const response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/summary?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar resumo do cliente."));
  return (await response.json()) as CustomerDetailSummary;
}

export async function fetchCustomerTimeline(input: {
  customerId: string;
  granularity?: "daily" | "weekly" | "monthly";
  metric?: "revenue" | "quantity" | "weight" | "orders";
  dateFrom?: string;
  dateTo?: string;
}): Promise<CustomerTimelineResponse> {
  const query = new URLSearchParams();
  query.set("granularity", input.granularity ?? "monthly");
  query.set("metric", input.metric ?? "revenue");
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());

  const response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/timeline?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar evolução temporal do cliente."));
  return (await response.json()) as CustomerTimelineResponse;
}

export async function fetchCustomerTopProducts(input: {
  customerId: string;
  dateFrom?: string;
  dateTo?: string;
}): Promise<CustomerTopProductItem[]> {
  const query = new URLSearchParams();
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());

  const response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/top-products?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar produtos mais comprados."));
  return (await response.json()) as CustomerTopProductItem[];
}

export async function fetchCustomerPurchaseHistory(input: {
  customerId: string;
  page?: number;
  pageSize?: number;
  dateFrom?: string;
  dateTo?: string;
}): Promise<CustomerPurchaseHistoryResponse> {
  const query = new URLSearchParams();
  query.set("page", String(input.page ?? 1));
  query.set("pageSize", String(input.pageSize ?? 10));
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());

  const response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/purchase-history?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar histórico de compras."));
  return (await response.json()) as CustomerPurchaseHistoryResponse;
}

export async function fetchCustomerComparison(input: {
  customerId: string;
  referenceDate?: string;
}): Promise<CustomerComparisonResponse> {
  const query = new URLSearchParams();
  if (input.referenceDate?.trim()) query.set("referenceDate", input.referenceDate.trim());

  const response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/comparison?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar comparativo do cliente."));
  return (await response.json()) as CustomerComparisonResponse;
}

export async function fetchCustomerInsights(input: {
  customerId: string;
  movingAverageWindowMonths?: 3 | 6 | 12;
}): Promise<CustomerInsightsResponse> {
  const query = new URLSearchParams();
  query.set("movingAverageWindowMonths", String(input.movingAverageWindowMonths ?? 3));

  const response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/insights?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar insights do cliente."));
  return (await response.json()) as CustomerInsightsResponse;
}

export async function fetchCustomerCommercialHealth(input: {
  customerId: string;
}): Promise<CustomerCommercialHealthReport> {
  const response = await fetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/commercial-health`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar análise comercial do cliente."));
  return (await response.json()) as CustomerCommercialHealthReport;
}

