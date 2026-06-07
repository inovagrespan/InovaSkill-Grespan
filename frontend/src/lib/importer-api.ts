import { extractHeadersInWorker } from "@/features/import-template-builder/utils/extract-headers-in-worker";
import { authFetch } from "@/lib/auth";
import { buildFallbackStages, type FileJobStageProgress } from "@/lib/importer-progress";

export type FileType = "Unknown" | "Customers" | "Orders" | "Products" | "CommercialTransaction";
export type UploadDestinationMode = "auto" | "manual";
export type UploadDestinationCode = "SALES_INVOICE" | "CUSTOMER_LIST" | "PRODUCT_LIST" | "FINANCIAL_ENTRY";

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

export type TemplateAlias = { from: string; to: string };
export type TemplateConfig = {
  id: number;
  fileType: FileType;
  name: string;
  isActive: boolean;
  requiredHeadersCsv: string;
  aliases: TemplateAlias[];
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
const DEMO_PAGE = 1;
const DEMO_PAGE_SIZE = 20;
const DEMO_HISTORY_PAGE_SIZE = 10;
const DEMO_TOTAL_ROWS = 48_000;
const DEMO_DATE_TODAY = "2026-06-07";
const DEMO_UPLOAD_JOB_ID_BASE = 900;

function shouldUseDemoData(error: unknown): boolean {
  return error instanceof Error && /Failed to fetch|NetworkError|Load failed|ECONNREFUSED|fetch failed/i.test(error.message);
}

function demoUploadJobId(fileName: string): number {
  const hash = Array.from(fileName).reduce((total, char) => total + char.charCodeAt(0), 0);
  return DEMO_UPLOAD_JOB_ID_BASE + (hash % 100);
}

function demoCommercialTransactions(): CommercialTransaction[] {
  return [
    {
      id: 1001,
      documentNumber: "NF-2026-001",
      transactionDate: "2026-06-03",
      customerCode: "CLI-001",
      customerName: "Mercado São Bento",
      productCode: "PRD-104",
      productDescription: "Arroz Tipo 1 5kg",
      quantity: 240,
      unitPrice: 27.9,
      totalAmount: 6_696,
      transactionType: "Venda",
      city: "Campinas",
      productGroup: "Mercearia",
      grossWeightKg: 1_200,
      sourceFileJobId: 501,
    },
    {
      id: 1002,
      documentNumber: "NF-2026-002",
      transactionDate: "2026-06-04",
      customerCode: "CLI-002",
      customerName: "Atacado Primavera",
      productCode: "PRD-221",
      productDescription: "Feijão Carioca 1kg",
      quantity: 520,
      unitPrice: 8.7,
      totalAmount: 4_524,
      transactionType: "Venda",
      city: "Ribeirão Preto",
      productGroup: "Mercearia",
      grossWeightKg: 520,
      sourceFileJobId: 501,
    },
    {
      id: 1003,
      documentNumber: "NF-2026-003",
      transactionDate: "2026-06-05",
      customerCode: "CLI-003",
      customerName: "Super Lopes",
      productCode: "PRD-318",
      productDescription: "Óleo de Soja 900ml",
      quantity: 360,
      unitPrice: 6.4,
      totalAmount: 2_304,
      transactionType: "Venda",
      city: "Sorocaba",
      productGroup: "Alimentos",
      grossWeightKg: 340,
      sourceFileJobId: 502,
    },
    {
      id: 1004,
      documentNumber: "NF-2026-004",
      transactionDate: "2026-06-06",
      customerCode: "CLI-004",
      customerName: "Distribuidora Central",
      productCode: "PRD-411",
      productDescription: "Café Tradicional 500g",
      quantity: 180,
      unitPrice: 18.5,
      totalAmount: 3_330,
      transactionType: "Venda",
      city: "São Paulo",
      productGroup: "Bebidas",
      grossWeightKg: 90,
      sourceFileJobId: 502,
    },
    {
      id: 1005,
      documentNumber: "DEV-2026-001",
      transactionDate: DEMO_DATE_TODAY,
      customerCode: "CLI-002",
      customerName: "Atacado Primavera",
      productCode: "PRD-104",
      productDescription: "Arroz Tipo 1 5kg",
      quantity: -24,
      unitPrice: 27.9,
      totalAmount: -669.6,
      transactionType: "Devolução",
      city: "Ribeirão Preto",
      productGroup: "Mercearia",
      grossWeightKg: -120,
      sourceFileJobId: 503,
    },
  ];
}

function demoCommercialSummary(input: {
  page?: number;
  pageSize?: number;
  granularity?: SummaryGranularity;
  sortBy?: SummarySortBy;
}): CommercialTransactionSummaryResponse {
  const items: CommercialTransactionCompanySummary[] = [
    {
      companyName: "Mercado São Bento",
      totalAmount: 64_850,
      totalQuantity: 2_420,
      totalWeightKg: 12_800,
      currentPeriodAmount: 64_850,
      previousPeriodAmount: 57_600,
      growthPercent: 12.6,
    },
    {
      companyName: "Atacado Primavera",
      totalAmount: 52_300,
      totalQuantity: 3_180,
      totalWeightKg: 9_750,
      currentPeriodAmount: 52_300,
      previousPeriodAmount: 55_900,
      growthPercent: -6.4,
    },
    {
      companyName: "Super Lopes",
      totalAmount: 38_940,
      totalQuantity: 1_760,
      totalWeightKg: 4_980,
      currentPeriodAmount: 38_940,
      previousPeriodAmount: 31_200,
      growthPercent: 24.8,
    },
    {
      companyName: "Distribuidora Central",
      totalAmount: 31_500,
      totalQuantity: 980,
      totalWeightKg: 2_400,
      currentPeriodAmount: 31_500,
      previousPeriodAmount: 29_800,
      growthPercent: 5.7,
    },
  ];

  return {
    page: input.page ?? DEMO_PAGE,
    pageSize: input.pageSize ?? DEMO_PAGE_SIZE,
    totalItems: items.length,
    granularity: input.granularity ?? "weekly",
    currentPeriodStart: "2026-06-01",
    previousPeriodStart: "2026-05-01",
    currentPeriodTotalAmount: 187_590,
    previousPeriodTotalAmount: 174_100,
    totalGrowthPercent: 7.7,
    totalRecords: 128,
    totalAmount: 187_590,
    totalQuantity: 8_340,
    totalWeightKg: 29_930,
    totalCompanies: items.length,
    items,
  };
}

function demoFileJobs(page = DEMO_PAGE, pageSize = 10): PagedResult<FileJob> {
  const jobs: FileJob[] = [
    {
      id: 501,
      filePath: "uploads/vendas-junho-demo.xlsx",
      fileType: "CommercialTransaction",
      status: "Completed",
      createdAt: "2026-06-07T08:40:00-03:00",
      errorCount: 0,
      currentStep: "Importação concluída",
      progressPercent: 100,
      processedRows: 12_480,
      totalRows: 12_480,
      currentStageCode: "DONE",
      currentStageName: "Concluído",
      stages: buildFallbackStages({ status: "Completed", progressPercent: 100, errorCount: 0 }),
    },
    {
      id: 502,
      filePath: "uploads/clientes-base-demo.xlsx",
      fileType: "Customers",
      status: "Importing",
      createdAt: "2026-06-07T09:15:00-03:00",
      errorCount: 3,
      currentStep: "Gravando clientes",
      progressPercent: 68,
      processedRows: 8_160,
      totalRows: 12_000,
      currentStageCode: "IMPORT",
      currentStageName: "Importação",
      stages: buildFallbackStages({ status: "Importing", progressPercent: 68, errorCount: 3 }),
    },
    {
      id: 503,
      filePath: "uploads/produtos-custos-demo.xlsx",
      fileType: "Products",
      status: "ValidationFailed",
      createdAt: "2026-06-07T10:05:00-03:00",
      errorCount: 12,
      currentStep: "Validação encontrou inconsistências",
      progressPercent: 42,
      processedRows: 1_420,
      totalRows: 3_400,
      currentStageCode: "VALIDATE",
      currentStageName: "Validação",
      stages: buildFallbackStages({ status: "ValidationFailed", progressPercent: 42, errorCount: 12 }),
    },
  ];

  return { page, pageSize, total: jobs.length, items: jobs.slice(0, pageSize) };
}

function demoProcessingDashboard(): ProcessingMonitoringDashboard {
  return {
    summary: {
      runningJobs: 2,
      queuedJobs: 4,
      completedToday: 18,
      failedJobs: 1,
      averageProcessingSeconds: 96,
      processedRowsToday: 42_860,
      staleJobs: 0,
    },
    jobs: [
      {
        id: 502,
        company: "Grespan",
        fileName: "clientes-base-demo.xlsx",
        template: "Clientes padrão",
        status: "Importing",
        statusLabel: "Importando",
        currentStep: "Gravando clientes",
        progressPercent: 68,
        createdAt: "2026-06-07T09:15:00-03:00",
        startedAt: "2026-06-07T09:18:00-03:00",
        finishedAt: null,
        elapsedSeconds: 420,
        processedRows: 8_160,
        totalRows: 12_000,
        errorCount: 3,
      },
      {
        id: 501,
        company: "Grespan",
        fileName: "vendas-junho-demo.xlsx",
        template: "Vendas NF",
        status: "Completed",
        statusLabel: "Concluído",
        currentStep: "Finalizado",
        progressPercent: 100,
        createdAt: "2026-06-07T08:40:00-03:00",
        startedAt: "2026-06-07T08:41:00-03:00",
        finishedAt: "2026-06-07T08:45:00-03:00",
        elapsedSeconds: 240,
        processedRows: 12_480,
        totalRows: 12_480,
        errorCount: 0,
      },
    ],
    daily: [
      { date: "2026-06-01", jobs: 8, completedJobs: 7, failedJobs: 1, processedRows: 18_900, averageProcessingSeconds: 102, successRatePercent: 87.5 },
      { date: "2026-06-02", jobs: 11, completedJobs: 11, failedJobs: 0, processedRows: 24_200, averageProcessingSeconds: 94, successRatePercent: 100 },
      { date: "2026-06-03", jobs: 9, completedJobs: 8, failedJobs: 1, processedRows: 21_650, averageProcessingSeconds: 110, successRatePercent: 88.9 },
      { date: "2026-06-04", jobs: 13, completedJobs: 13, failedJobs: 0, processedRows: 31_400, averageProcessingSeconds: 89, successRatePercent: 100 },
      { date: "2026-06-05", jobs: 10, completedJobs: 9, failedJobs: 1, processedRows: 27_900, averageProcessingSeconds: 97, successRatePercent: 90 },
      { date: DEMO_DATE_TODAY, jobs: 24, completedJobs: 18, failedJobs: 1, processedRows: 42_860, averageProcessingSeconds: 96, successRatePercent: 94.7 },
    ],
    stageDurations: [
      { stage: "READ", stageName: "Leitura", averageDurationSeconds: 24, sharePercent: 25 },
      { stage: "VALIDATE", stageName: "Validação", averageDurationSeconds: 31, sharePercent: 32 },
      { stage: "IMPORT", stageName: "Importação", averageDurationSeconds: 39, sharePercent: 41 },
      { stage: "SUMMARY", stageName: "Resumo", averageDurationSeconds: 2, sharePercent: 2 },
    ],
    workers: [
      { workerId: "worker-demo-01", status: "Online", lastSeenAt: "2026-06-07T10:14:00-03:00", secondsSinceLastSeen: 8, processedJobsToday: 12, idleSeconds: 0, currentJobId: 502, currentTask: "Importando clientes" },
      { workerId: "worker-demo-02", status: "Online", lastSeenAt: "2026-06-07T10:14:05-03:00", secondsSinceLastSeen: 3, processedJobsToday: 6, idleSeconds: 78, currentJobId: null, currentTask: "Aguardando fila" },
    ],
  };
}

function demoProcessingJobDetails(jobId: number): ProcessingJobDetails {
  const dashboard = demoProcessingDashboard();
  const job = dashboard.jobs.find((item) => item.id === jobId) ?? dashboard.jobs[0];
  return {
    job,
    timeline: [
      { step: "READ", stepName: "Leitura do arquivo", startedAt: "2026-06-07T09:18:00-03:00", finishedAt: "2026-06-07T09:18:24-03:00", durationSeconds: 24, status: "completed", processedRows: DEMO_TOTAL_ROWS / 4, errorCount: 0 },
      { step: "VALIDATE", stepName: "Validação de colunas", startedAt: "2026-06-07T09:18:24-03:00", finishedAt: "2026-06-07T09:19:10-03:00", durationSeconds: 46, status: "completed", processedRows: DEMO_TOTAL_ROWS / 4, errorCount: 3 },
      { step: "IMPORT", stepName: "Persistência", startedAt: "2026-06-07T09:19:10-03:00", finishedAt: null, durationSeconds: 340, status: "running", processedRows: 8_160, errorCount: 3 },
    ],
    metrics: {
      totalRows: 12_000,
      validRows: 11_720,
      invalidRows: 280,
      importedRows: 8_160,
      errorCount: 3,
      warningCount: 18,
    },
    performanceByStage: dashboard.stageDurations,
    logs: [
      { timestamp: "2026-06-07T09:18:24-03:00", fileJobId: job.id, stage: "READ", level: "Info", message: "Arquivo demo carregado com sucesso." },
      { timestamp: "2026-06-07T09:19:10-03:00", fileJobId: job.id, stage: "VALIDATE", level: "Warning", message: "3 linhas possuem cidade vazia; aplicado valor padrão para demonstração." },
      { timestamp: "2026-06-07T09:22:00-03:00", fileJobId: job.id, stage: "IMPORT", level: "Info", message: "Importação em andamento com dados fictícios." },
    ],
  };
}

function demoCustomerSummary(): CustomerAnalyticsSummary {
  return {
    activeCustomers: 84,
    totalRevenue: 187_590,
    totalOrders: 128,
    averageTicket: 1_465.55,
    averageRevenuePerCustomer: 2_232.02,
    newCustomers: 9,
    inactiveCustomers: 14,
    currentPeriodStart: "2026-06-01",
    currentPeriodEnd: DEMO_DATE_TODAY,
    previousPeriodStart: "2026-05-01",
    previousPeriodEnd: "2026-05-31",
  };
}

function demoCustomerRanking(input: { page?: number; pageSize?: number }): CustomerRankingResponse {
  const items: CustomerRankingItem[] = [
    { customerCode: "CLI-001", customerName: "Mercado São Bento", revenue: 64_850, quantity: 2_420, weight: 12_800, orders: 28, averageTicket: 2_316.07, variationPercent: 12.6 },
    { customerCode: "CLI-002", customerName: "Atacado Primavera", revenue: 52_300, quantity: 3_180, weight: 9_750, orders: 21, averageTicket: 2_490.48, variationPercent: -6.4 },
    { customerCode: "CLI-003", customerName: "Super Lopes", revenue: 38_940, quantity: 1_760, weight: 4_980, orders: 18, averageTicket: 2_163.33, variationPercent: 24.8 },
    { customerCode: "CLI-004", customerName: "Distribuidora Central", revenue: 31_500, quantity: 980, weight: 2_400, orders: 12, averageTicket: 2_625, variationPercent: 5.7 },
  ];
  return { page: input.page ?? DEMO_PAGE, pageSize: input.pageSize ?? DEMO_PAGE_SIZE, totalItems: items.length, items };
}

function demoCustomerDetails(customerId: string): CustomerDetailSummary {
  const ranking = demoCustomerRanking({}).items;
  const customer = ranking.find((item) => item.customerCode === customerId) ?? ranking[0];
  return {
    customerCode: customer.customerCode,
    customerName: customer.customerName,
    city: customer.customerCode === "CLI-002" ? "Ribeirão Preto" : "Campinas",
    linkedCompany: "Grespan Distribuição",
    lastPurchaseDate: DEMO_DATE_TODAY,
    status: customer.variationPercent != null && customer.variationPercent < 0 ? "Em queda" : "Ativo",
    totalRevenue: customer.revenue,
    averageTicket: customer.averageTicket,
    averageRevenueMonthly: customer.revenue / 6,
    averageRevenueWeekly: customer.revenue / 24,
    totalQuantity: customer.quantity,
    totalWeight: customer.weight,
    totalOrders: customer.orders,
    averageDaysBetweenPurchases: 11,
  };
}

function demoCustomerTimeline(input: {
  granularity?: "daily" | "weekly" | "monthly";
  metric?: "revenue" | "quantity" | "weight" | "orders";
}): CustomerTimelineResponse {
  const points: CustomerTimelinePoint[] = [
    { periodStart: "2026-01-01", value: 21_400, revenue: 21_400, quantity: 880, weight: 3_200, orders: 9 },
    { periodStart: "2026-02-01", value: 24_900, revenue: 24_900, quantity: 940, weight: 3_520, orders: 10 },
    { periodStart: "2026-03-01", value: 19_700, revenue: 19_700, quantity: 790, weight: 2_980, orders: 8 },
    { periodStart: "2026-04-01", value: 28_600, revenue: 28_600, quantity: 1_120, weight: 4_100, orders: 12 },
    { periodStart: "2026-05-01", value: 31_200, revenue: 31_200, quantity: 1_280, weight: 4_450, orders: 13 },
    { periodStart: "2026-06-01", value: 38_940, revenue: 38_940, quantity: 1_760, weight: 4_980, orders: 18 },
  ];
  return {
    granularity: input.granularity ?? "monthly",
    metric: input.metric ?? "revenue",
    points: points.map((point) => ({ ...point, value: point[input.metric ?? "revenue"] })),
  };
}

function demoCustomerTopProducts(): CustomerTopProductItem[] {
  return [
    { productCode: "PRD-104", productDescription: "Arroz Tipo 1 5kg", quantity: 820, revenue: 22_878, sharePercent: 35.2 },
    { productCode: "PRD-221", productDescription: "Feijão Carioca 1kg", quantity: 1_100, revenue: 9_570, sharePercent: 14.7 },
    { productCode: "PRD-411", productDescription: "Café Tradicional 500g", quantity: 480, revenue: 8_880, sharePercent: 13.7 },
  ];
}

function demoCustomerCommercialHealth(customerId: string): CustomerCommercialHealthReport {
  const details = demoCustomerDetails(customerId);
  return {
    header: {
      customerCode: details.customerCode,
      customerName: details.customerName,
      city: details.city,
      linkedCompany: details.linkedCompany,
      lastPurchaseDate: details.lastPurchaseDate,
      daysWithoutPurchase: 3,
      averageDaysBetweenPurchases: details.averageDaysBetweenPurchases,
      commercialStatus: details.status,
    },
    score: { value: 82, label: "Saudável", explanation: "Cliente fictício com compra recente, frequência estável e bom ticket médio." },
    health: { status: "Bom", tone: "success", summary: "Cliente ativo e recorrente.", detail: "Use este bloco como base para mensagens de saúde comercial." },
    trend: { status: "Crescimento", tone: "success", summary: "Faturamento subiu no último mês.", detail: "A variação fictícia ajuda a testar os cards positivos." },
    potential: { expectedRevenue: 42_000, expectedQuantity: 1_920, label: "Potencial alto", explanation: "Projeção demo baseada em histórico mensal simulado." },
    dependency: { status: "Mix concentrado", explanation: "Top 3 produtos representam parte relevante da receita.", productsToReachEightyPercent: 5, topProductSharePercent: 35.2 },
    products: demoCustomerTopProducts(),
    timeline: demoCustomerTimeline({}).points.map((point) => ({ date: point.periodStart, orders: point.orders, revenue: point.revenue, quantity: point.quantity })),
    evolution: demoCustomerTimeline({}).points.map((point) => ({ periodStart: point.periodStart, revenue: point.revenue, quantity: point.quantity, orders: point.orders, averageTicket: point.revenue / Math.max(point.orders, 1) })),
    comparisons: [
      { label: "Mês atual", revenue: 38_940, previousRevenue: 31_200, quantity: 1_760, previousQuantity: 1_280, orders: 18, previousOrders: 13, averageTicket: 2_163.33, previousAverageTicket: 2_400, revenueVariationPercent: 24.8, quantityVariationPercent: 37.5, ordersVariationPercent: 38.5, averageTicketVariationPercent: -9.9 },
    ],
    recommendations: [
      { priority: "Alta", title: "Reforçar mix principal", detail: "Oferecer reposição dos produtos de maior participação antes da próxima janela de compra." },
      { priority: "Média", title: "Testar produto complementar", detail: "Usar a base fictícia para decidir quais sugestões comerciais entram na tela final." },
    ],
    alerts: [
      { severity: "info", title: "Dados de demonstração", detail: "Esses números são fictícios e servem apenas como base visual." },
    ],
  };
}

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

export async function uploadFile(file: File, importFileTypeCode?: UploadDestinationCode): Promise<number> {
  const form = new FormData();
  form.append("file", file);
  if (importFileTypeCode) {
    form.append("importFileTypeCode", importFileTypeCode);
  }

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
    if (shouldUseDemoData(error)) {
      return demoUploadJobId(file.name);
    }

    throw error;
  }
}

export async function fetchJobs(page = 1, pageSize = 10): Promise<PagedResult<FileJob>> {
  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/files/jobs?page=${page}&pageSize=${pageSize}`);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoFileJobs(page, pageSize);
    throw error;
  }
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
  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/files/jobs/${jobId}/errors?page=${page}&pageSize=${pageSize}`);
  } catch (error) {
    if (shouldUseDemoData(error)) {
      return {
        page,
        pageSize,
        total: 2,
        items: [
          { id: 1, fileJobId: jobId, rowNumber: 18, stage: "VALIDATE", column: "Cidade", message: "Cidade vazia na linha demo.", recordIdentifier: "CLI-002" },
          { id: 2, fileJobId: jobId, rowNumber: 42, stage: "VALIDATE", column: "Valor Total", message: "Valor total divergente da quantidade x unitário.", recordIdentifier: "NF-2026-042" },
        ],
      };
    }
    throw error;
  }
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
  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/processing-monitoring/dashboard`);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoProcessingDashboard();
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar monitoramento de jobs."));
  return normalizeProcessingDashboard(await response.json());
}

export async function fetchProcessingJobDetails(jobId: number): Promise<ProcessingJobDetails> {
  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/processing-monitoring/jobs/${jobId}`);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoProcessingJobDetails(jobId);
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar detalhes do job."));
  return normalizeProcessingJobDetails(await response.json());
}

export async function retryProcessingJob(jobId: number): Promise<void> {
  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/processing-monitoring/jobs/${jobId}/retry`, { method: "POST" });
  } catch (error) {
    if (shouldUseDemoData(error)) return;
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao reprocessar job."));
}

export async function cancelProcessingJob(jobId: number): Promise<void> {
  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/processing-monitoring/jobs/${jobId}/cancel`, { method: "POST" });
  } catch (error) {
    if (shouldUseDemoData(error)) return;
    throw error;
  }
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
  return {
    id: raw.id ?? raw.Id ?? 0,
    company: raw.company ?? raw.Company ?? "-",
    fileName: raw.fileName ?? raw.FileName ?? "",
    template: raw.template ?? raw.Template ?? null,
    status: raw.status ?? raw.Status ?? "",
    statusLabel: raw.statusLabel ?? raw.StatusLabel ?? "",
    currentStep: raw.currentStep ?? raw.CurrentStep ?? "",
    progressPercent: raw.progressPercent ?? raw.ProgressPercent ?? 0,
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

export async function fetchTemplateConfigs(): Promise<TemplateConfig[]> {
  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/template-configs`);
  } catch (error) {
    if (shouldUseDemoData(error)) {
      return [
        {
          id: 1,
          fileType: "CommercialTransaction",
          name: "Template demo de vendas",
          isActive: true,
          requiredHeadersCsv: "Documento,Data,Cliente,Produto,Quantidade,Valor Total",
          aliases: [
            { from: "NF", to: "Documento" },
            { from: "Vlr Total", to: "Valor Total" },
          ],
        },
      ];
    }
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar configurações de template."));
  const data = (await response.json()) as Array<{
    id?: number;
    fileType?: number | string;
    name?: string;
    isActive?: boolean;
    requiredHeadersCsv?: string;
    aliases?: TemplateAlias[];
  }>;
  return (data ?? []).map((item) => ({
    id: item.id ?? 0,
    fileType: normalizeFileType(item.fileType ?? "Unknown"),
    name: item.name ?? "",
    isActive: item.isActive ?? true,
    requiredHeadersCsv: item.requiredHeadersCsv ?? "",
    aliases: item.aliases ?? [],
  }));
}

export async function saveTemplateConfig(input: Omit<TemplateConfig, "id"> & { id?: number }): Promise<TemplateConfig> {
  const response = await authFetch(`${API_URL}/api/template-configs`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao salvar configuração de template."));
  return (await response.json()) as TemplateConfig;
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

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/commercial-transactions?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) {
      const items = demoCommercialTransactions();
      return { page: input.page ?? DEMO_PAGE, pageSize: input.pageSize ?? DEMO_PAGE_SIZE, total: items.length, items };
    }
    throw error;
  }
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

export type ApiImportFileType = { id: string; name: string; code?: string; description?: string; allowedExtensions?: string };
export type ApiTargetField = { name: string; displayName: string; required: boolean; dataType?: "text" | "number" | "date" | "currency"; description?: string };
export type ApiTransformRule = {
  id: string;
  name: string;
  code: string;
  description?: string;
  requiresParameters?: boolean;
};
export type ApiImportTemplateRule = {
  transformRuleId: string;
  order: number;
  parametersJson: unknown;
};
export type ApiImportTemplateMapping = {
  sourceColumnName: string;
  targetFieldName: string;
  isRequired: boolean;
  defaultValue: string | null;
  transformRules: ApiImportTemplateRule[];
};
export type ApiImportTemplate = {
  id?: string;
  name: string;
  description: string;
  importFileTypeId: string;
  columnMappings: ApiImportTemplateMapping[];
};
export type ApiSpreadsheetSample = {
  headers: string[];
  previewRows: Array<Record<string, string>>;
};

export async function fetchImportTemplateFileTypes(): Promise<ApiImportFileType[]> {
  try {
    const response = await authFetch(`${API_URL}/api/import-templates/file-types`);
    if (response.ok) {
      const data = (await response.json()) as Array<Record<string, unknown>>;
      return (data ?? []).map((item) => ({
        id: String(item.id ?? item.Id ?? item.value ?? item.Value ?? ""),
        name: String(item.name ?? item.Name ?? item.label ?? item.Label ?? ""),
        code: String(item.code ?? item.Code ?? ""),
        description: String(item.description ?? item.Description ?? ""),
        allowedExtensions: String(item.allowedExtensions ?? item.AllowedExtensions ?? ""),
      }));
    }
  } catch {
    // fallback
  }

  return [
    { id: "Customers", name: "Clientes", code: "CUSTOMERS" },
    { id: "Products", name: "Produtos", code: "PRODUCTS" },
    { id: "CommercialTransaction", name: "Vendas", code: "COMMERCIAL_TRANSACTION" },
    { id: "Orders", name: "Pedidos", code: "ORDERS" },
  ];
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

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/commercial-transactions/summary?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoCommercialSummary(input);
    throw error;
  }
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

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/customer-analytics-v2/summary?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoCustomerSummary();
    throw error;
  }
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

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/customer-analytics-v2/ranking?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoCustomerRanking(input);
    throw error;
  }
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

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/customer-analytics-v2/new-customers-monthly?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) {
      return {
        periodStart: "2026-01-01",
        periodEnd: DEMO_DATE_TODAY,
        totalNewCustomers: 27,
        activeMonths: 6,
        points: [
          { monthStart: "2026-01-01", newCustomers: 3 },
          { monthStart: "2026-02-01", newCustomers: 4 },
          { monthStart: "2026-03-01", newCustomers: 2 },
          { monthStart: "2026-04-01", newCustomers: 5 },
          { monthStart: "2026-05-01", newCustomers: 7 },
          { monthStart: "2026-06-01", newCustomers: 6 },
        ],
      };
    }
    throw error;
  }
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

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/summary?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoCustomerDetails(input.customerId);
    throw error;
  }
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

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/timeline?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoCustomerTimeline(input);
    throw error;
  }
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

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/top-products?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoCustomerTopProducts();
    throw error;
  }
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

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/purchase-history?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) {
      const items: CustomerPurchaseHistoryItem[] = demoCommercialTransactions().map((item) => ({
        date: item.transactionDate,
        document: item.documentNumber,
        product: item.productDescription,
        quantity: item.quantity,
        unitPrice: item.unitPrice,
        total: item.totalAmount,
        weight: item.grossWeightKg,
        operationType: item.transactionType,
      }));
      return { page: input.page ?? DEMO_PAGE, pageSize: input.pageSize ?? DEMO_HISTORY_PAGE_SIZE, totalItems: items.length, items };
    }
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar histórico de compras."));
  return (await response.json()) as CustomerPurchaseHistoryResponse;
}

export async function fetchCustomerComparison(input: {
  customerId: string;
  referenceDate?: string;
}): Promise<CustomerComparisonResponse> {
  const query = new URLSearchParams();
  if (input.referenceDate?.trim()) query.set("referenceDate", input.referenceDate.trim());

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/comparison?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) {
      return {
        items: [
          { label: "Mês atual", currentValue: 38_940, previousValue: 31_200, variationPercent: 24.8 },
          { label: "Últimos 3 meses", currentValue: 98_500, previousValue: 91_000, variationPercent: 8.2 },
          { label: "Últimos 6 meses", currentValue: 165_800, previousValue: 172_400, variationPercent: -3.8 },
        ],
      };
    }
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar comparativo do cliente."));
  return (await response.json()) as CustomerComparisonResponse;
}

export async function fetchCustomerInsights(input: {
  customerId: string;
  movingAverageWindowMonths?: 3 | 6 | 12;
}): Promise<CustomerInsightsResponse> {
  const query = new URLSearchParams();
  query.set("movingAverageWindowMonths", String(input.movingAverageWindowMonths ?? 3));

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/insights?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) {
      return {
        averagePurchaseFrequencyDays: 11,
        estimatedNextPurchaseDate: "2026-06-18",
        predictedRevenue: 42_000,
        predictedQuantity: 1_920,
        consumptionTrend: "Crescimento",
        riskLevel: "Sem risco",
        daysWithoutPurchase: 3,
        riskScore: 12,
        frequencyReason: "Frequência fictícia baseada em compras mensais.",
        nextPurchaseReason: "Data estimada apenas para demonstração.",
        revenuePredictionReason: "Projeção demo para validar o layout.",
        quantityPredictionReason: "Quantidade prevista com dados fictícios.",
        riskReason: "Cliente com compra recente na base demo.",
        monthlyHistoryPeriods: 6,
      };
    }
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar insights do cliente."));
  return (await response.json()) as CustomerInsightsResponse;
}

export async function fetchCustomerCommercialHealth(input: {
  customerId: string;
}): Promise<CustomerCommercialHealthReport> {
  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/commercial-health`);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoCustomerCommercialHealth(input.customerId);
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar análise comercial do cliente."));
  return (await response.json()) as CustomerCommercialHealthReport;
}

function normalizeTargetFieldType(value: unknown): ApiTargetField["dataType"] {
  const normalized = String(value ?? "text").toLowerCase();
  if (normalized === "number" || normalized === "date" || normalized === "currency") {
    return normalized;
  }

  return "text";
}

export async function fetchImportTemplateTargetFields(importFileTypeId: string): Promise<ApiTargetField[]> {
  try {
    const response = await authFetch(`${API_URL}/api/import-templates/file-types/${importFileTypeId}/fields`);
    if (response.ok) {
      const data = (await response.json()) as Array<Record<string, unknown>>;
      return (data ?? []).map((item) => ({
        name: String(item.name ?? item.Name ?? ""),
        displayName: String(item.displayName ?? item.DisplayName ?? item.name ?? item.Name ?? ""),
        required: Boolean(item.required ?? item.Required ?? false),
        dataType: normalizeTargetFieldType(item.dataType ?? item.DataType),
        description: String(item.description ?? item.Description ?? ""),
      }));
    }
  } catch {
    // fallback
  }

  const fallbackFields: Record<string, ApiTargetField[]> = {
    Customers: [
      { name: "customercode", displayName: "Codigo do Cliente", required: true },
      { name: "name", displayName: "Nome", required: true },
      { name: "email", displayName: "E-mail", required: false },
    ],
    Products: [
      { name: "sku", displayName: "SKU", required: true },
      { name: "name", displayName: "Nome", required: true },
      { name: "price", displayName: "Preço", required: false },
    ],
    Orders: [
      { name: "ordernumber", displayName: "Número do Pedido", required: true },
      { name: "customeremail", displayName: "E-mail do Cliente", required: true },
      { name: "productsku", displayName: "SKU do Produto", required: true },
      { name: "quantity", displayName: "Quantidade", required: true },
      { name: "orderdate", displayName: "Data do Pedido", required: false },
    ],
    CommercialTransaction: [
      { name: "documentnumber", displayName: "Documento", required: true },
      { name: "transactiondate", displayName: "Data", required: true },
      { name: "customercode", displayName: "Código Cliente", required: true },
      { name: "customername", displayName: "Nome Cliente", required: true },
      { name: "productcode", displayName: "Código Produto", required: true },
      { name: "productdescription", displayName: "Descrição Produto", required: false },
      { name: "quantity", displayName: "Quantidade", required: true },
      { name: "unitprice", displayName: "Valor Unitário", required: false },
      { name: "totalamount", displayName: "Valor Total", required: false },
      { name: "transactiontype", displayName: "Tipo", required: false },
      { name: "city", displayName: "Cidade", required: false },
      { name: "productgroup", displayName: "Grupo Produto", required: false },
      { name: "grossweightkg", displayName: "Peso Bruto (Kg)", required: false },
    ],
  };

  return fallbackFields[importFileTypeId] ?? [];
}

export async function extractSpreadsheetSample(file: File, importFileTypeId?: string): Promise<ApiSpreadsheetSample> {
  const formData = new FormData();
  formData.append("file", file);
  if (importFileTypeId) formData.append("importFileTypeId", importFileTypeId);

  try {
    const response = await authFetch(`${API_URL}/api/import-templates/extract-headers`, { method: "POST", body: formData });
    if (response.ok) {
      const payload = (await response.json()) as
        | { headers?: string[]; Headers?: string[]; previewRows?: Array<Record<string, string>>; PreviewRows?: Array<Record<string, string>> }
        | string[];
      const apiHeaders = Array.isArray(payload) ? payload : payload.headers ?? payload.Headers ?? [];
      if (apiHeaders.length > 0) {
        return {
          headers: apiHeaders,
          previewRows: Array.isArray(payload) ? [] : payload.previewRows ?? payload.PreviewRows ?? [],
        };
      }
    }
  } catch {
    // fallback
  }

  return { headers: await extractHeadersInWorker(file), previewRows: [] };
}

export async function extractSpreadsheetHeaders(file: File, importFileTypeId?: string): Promise<string[]> {
  return (await extractSpreadsheetSample(file, importFileTypeId)).headers;
}

export async function fetchTransformRules(): Promise<ApiTransformRule[]> {
  try {
    const response = await authFetch(`${API_URL}/api/import-templates/transform-rules`);
    if (response.ok) {
      const data = (await response.json()) as Array<Record<string, unknown>>;
      return (data ?? []).map((item) => ({
        id: String(item.id ?? item.Id ?? ""),
        name: String(item.name ?? item.Name ?? item.code ?? item.Code ?? ""),
        code: String(item.code ?? item.Code ?? ""),
        description: String(item.description ?? item.Description ?? ""),
        requiresParameters: Boolean(item.requiresParameters ?? item.RequiresParameters ?? false),
      }));
    }
  } catch {
    // fallback
  }

  return [
    { id: "trim", name: "Trim", code: "Trim", requiresParameters: false },
    { id: "onlydigits", name: "OnlyDigits", code: "OnlyDigits", requiresParameters: false },
    { id: "brcurrency", name: "BrazilianCurrency", code: "BrazilianCurrency", requiresParameters: true },
    { id: "brdate", name: "BrazilianDate", code: "BrazilianDate", requiresParameters: true },
    { id: "uppercase", name: "UpperCase", code: "UpperCase", requiresParameters: false },
    { id: "lowercase", name: "LowerCase", code: "LowerCase", requiresParameters: false },
  ];
}
export async function fetchImportTemplateById(templateId: string): Promise<ApiImportTemplate> {
  const response = await authFetch(`${API_URL}/api/import-templates/${templateId}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar template."));
  const item = (await response.json()) as Record<string, any>;
  return {
    id: String(item.id ?? item.Id ?? ""),
    name: String(item.name ?? item.Name ?? ""),
    description: String(item.description ?? item.Description ?? ""),
    importFileTypeId: String(item.importFileTypeId ?? item.ImportFileTypeId ?? ""),
    columnMappings: (item.columnMappings ?? item.ColumnMappings ?? []).map((mapping: any) => ({
      sourceColumnName: String(mapping.sourceColumnName ?? mapping.SourceColumnName ?? ""),
      targetFieldName: String(mapping.targetFieldName ?? mapping.TargetFieldName ?? ""),
      isRequired: Boolean(mapping.isRequired ?? mapping.IsRequired ?? false),
      defaultValue: mapping.defaultValue ?? mapping.DefaultValue ?? null,
      transformRules: (mapping.transformRules ?? mapping.TransformRules ?? []).map((rule: any) => ({
        transformRuleId: String(rule.transformRuleId ?? rule.TransformRuleId ?? ""),
        order: Number(rule.order ?? rule.Order ?? 0),
        parametersJson: rule.parametersJson ?? rule.ParametersJson ?? null,
      })),
    })),
  };
}

export async function createImportTemplate(input: ApiImportTemplate): Promise<ApiImportTemplate> {
  const response = await authFetch(`${API_URL}/api/import-templates`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao criar template."));
  return (await response.json()) as ApiImportTemplate;
}

export async function updateImportTemplate(templateId: string, input: ApiImportTemplate): Promise<ApiImportTemplate> {
  const response = await authFetch(`${API_URL}/api/import-templates/${templateId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao atualizar template."));
  return (await response.json()) as ApiImportTemplate;
}


