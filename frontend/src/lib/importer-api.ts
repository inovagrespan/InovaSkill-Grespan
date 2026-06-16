import { authFetch } from "@/lib/auth";
import {
  buildFinanceCustomerRevenueRanking,
  buildFinanceRevenueTrend,
  calculateFinanceMetrics,
  financeDemoTransactions,
  listFinanceCustomers,
} from "@/lib/finance-demo-metrics";
import { buildFallbackStages, type FileJobStageProgress } from "@/lib/importer-progress";
import { getApiServiceBaseUrl } from "@/lib/api-url";

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

export type Product = {
  id: number;
  sku: string;
  name: string;
  price: number;
  createdAt: string;
  sourceFileJobId: number;
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
  documentCount: number;
  singleDocumentNumber: string | null;
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
export type CommercialInvoiceSummary = {
  documentNumber: string;
  transactionDate: string;
  customerCode: string;
  customerName: string;
  city: string;
  transactionType: string;
  totalAmount: number;
  totalQuantity: number;
  totalWeightKg: number;
  totalItems: number;
};
export type CommercialInvoiceSummaryResponse = {
  page: number;
  pageSize: number;
  totalItems: number;
  totalAmount: number;
  totalQuantity: number;
  totalWeightKg: number;
  items: CommercialInvoiceSummary[];
};
export type CommercialInvoiceDetails = {
  documentNumber: string;
  transactionDate: string;
  customerCode: string;
  customerName: string;
  city: string;
  transactionType: string;
  totalAmount: number;
  totalQuantity: number;
  totalWeightKg: number;
  totalItems: number;
  items: CommercialTransaction[];
};
export type CommercialInvoiceAnalyticsGranularity = "day" | "week" | "month";
export type CommercialInvoiceAnalyticsTrendPoint = {
  periodStart: string;
  invoiceCount: number;
  totalAmount: number;
  totalWeightKg: number;
};
export type CommercialInvoiceAnalyticsRankingItem = {
  customerCode: string;
  customerName: string;
  totalAmount: number;
  invoiceCount: number;
  totalItems: number;
  totalWeightKg: number;
};
export type CommercialInvoiceAnalyticsSummary = {
  totalInvoices: number;
  totalAmount: number;
  totalWeightKg: number;
  totalCustomers: number;
  totalItems: number;
  totalQuantity: number;
};
export type CommercialInvoiceAnalyticsResponse = {
  granularity: CommercialInvoiceAnalyticsGranularity;
  summary: CommercialInvoiceAnalyticsSummary;
  trend: CommercialInvoiceAnalyticsTrendPoint[];
  ranking: CommercialInvoiceAnalyticsRankingItem[];
};
export type CommercialTransactionTimelineGranularity = "hour" | "day" | "week" | "month" | "quarter";
export type CommercialTransactionTimelinePoint = {
  periodStart: string;
  totalAmount: number;
  totalQuantity: number;
  totalWeightKg: number;
  recordCount: number;
};
export type CommercialTransactionTimelineResponse = {
  granularity: CommercialTransactionTimelineGranularity;
  items: CommercialTransactionTimelinePoint[];
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
  canRunManualActions: boolean;
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
  averageTicket: number;
};

export type CustomerTimelineResponse = {
  granularity: "daily" | "weekly" | "monthly";
  metric: "revenue" | "quantity" | "weight" | "orders" | "averageTicket";
  points: CustomerTimelinePoint[];
};

export type CustomerIndividualAnalysisScope = "historical" | "current";

export type CustomerIndividualAnalysisResponse = {
  scope: CustomerIndividualAnalysisScope;
  periodStart: string;
  periodEnd: string;
  granularity: "weekly" | "monthly";
  metric: "revenue" | "quantity" | "weight" | "orders" | "averageTicket";
  summary: CustomerDetailSummary;
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

export type FinanceRevenueGranularity = "weekly" | "monthly" | "yearly";

export type FinanceDashboardSummary = {
  totalRevenue: number;
  totalOrders: number;
  totalQuantity: number;
  averageTicket: number;
};

export type FinanceDashboardItem = {
  customer: string;
  date: string;
  revenue: number;
  orders: number;
  quantity: number;
};

export type FinanceRevenueTrendPoint = {
  period: string;
  label: string;
  revenue: number;
};

export type FinanceCustomerRevenuePoint = {
  customer: string;
  revenue: number;
};

export type FinanceCustomerOption = {
  id: string;
  nome: string;
};

export type FinanceDashboardResponse = {
  customers: string[];
  summary: FinanceDashboardSummary;
  revenueTrend: FinanceRevenueTrendPoint[];
  customerRanking: FinanceCustomerRevenuePoint[];
  items: FinanceDashboardItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
};

const API_URL = getApiServiceBaseUrl();
export const MAX_UPLOAD_SIZE_BYTES = 524_288_000;
const DEMO_PAGE = 1;
const DEMO_PAGE_SIZE = 20;
const DEMO_HISTORY_PAGE_SIZE = 10;
const DEMO_TOTAL_ROWS = 48_000;
const DEMO_DATE_TODAY = "2026-06-07";
const DEMO_SALES_DATE_TODAY = "2026-06-08";
const DEMO_UPLOAD_JOB_ID_BASE = 900;
const DEMO_PRODUCT_JOB_ID = 501;

function shouldUseDemoData(error: unknown): boolean {
  return error instanceof Error && /Failed to fetch|NetworkError|Load failed|ECONNREFUSED|fetch failed/i.test(error.message);
}

function hasItems<T extends { items?: unknown[]; total?: number; totalItems?: number }>(value: T): boolean {
  return (value.items?.length ?? 0) > 0 || (value.total ?? value.totalItems ?? 0) > 0;
}

function hasFinanceData(value: FinanceDashboardResponse): boolean {
  return value.summary.totalRevenue !== 0 || value.items.length > 0 || value.revenueTrend.length > 0 || value.customerRanking.length > 0;
}

function hasProcessingData(value: ProcessingMonitoringDashboard): boolean {
  return value.jobs.length > 0 || value.daily.length > 0 || value.workers.length > 0 || value.summary.completedToday > 0 || value.summary.runningJobs > 0;
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
      customerName: "Padaria São Bento",
      productCode: "PAN-104",
      productDescription: "Pão Francês Congelado 60g",
      quantity: 240,
      unitPrice: 27.9,
      totalAmount: 6_696,
      transactionType: "Venda",
      city: "Campinas",
      productGroup: "Pães Congelados",
      grossWeightKg: 1_200,
      sourceFileJobId: 501,
    },
    {
      id: 1002,
      documentNumber: "NF-2026-002",
      transactionDate: "2026-06-04",
      customerCode: "CLI-002",
      customerName: "Supermercado Primavera",
      productCode: "PAN-221",
      productDescription: "Pão de Queijo Congelado 1kg",
      quantity: 520,
      unitPrice: 8.7,
      totalAmount: 4_524,
      transactionType: "Venda",
      city: "Ribeirão Preto",
      productGroup: "Salgados Congelados",
      grossWeightKg: 520,
      sourceFileJobId: 501,
    },
    {
      id: 1003,
      documentNumber: "NF-2026-003",
      transactionDate: "2026-06-05",
      customerCode: "CLI-003",
      customerName: "Cafeteria Grão & Massa",
      productCode: "PAN-318",
      productDescription: "Croissant Congelado 80g",
      quantity: 360,
      unitPrice: 6.4,
      totalAmount: 2_304,
      transactionType: "Venda",
      city: "Sorocaba",
      productGroup: "Folhados",
      grossWeightKg: 340,
      sourceFileJobId: 502,
    },
    {
      id: 1004,
      documentNumber: "NF-2026-004",
      transactionDate: "2026-06-06",
      customerCode: "CLI-004",
      customerName: "Rede Conveniência Rota 12",
      productCode: "EQP-411",
      productDescription: "Freezer Comercial Expositor 410L",
      quantity: 180,
      unitPrice: 18.5,
      totalAmount: 3_330,
      transactionType: "Venda",
      city: "São Paulo",
      productGroup: "Equipamentos",
      grossWeightKg: 90,
      sourceFileJobId: 502,
    },
    {
      id: 1005,
      documentNumber: "DEV-2026-001",
      transactionDate: DEMO_SALES_DATE_TODAY,
      customerCode: "CLI-002",
      customerName: "Supermercado Primavera",
      productCode: "PAN-104",
      productDescription: "Pão Francês Congelado 60g",
      quantity: -24,
      unitPrice: 27.9,
      totalAmount: -669.6,
      transactionType: "Devolução",
      city: "Ribeirão Preto",
      productGroup: "Pães Congelados",
      grossWeightKg: -120,
      sourceFileJobId: 503,
    },
  ];
}

type DemoCommercialTransactionFilters = {
  documentNumber?: string;
  customerCode?: string;
  customerName?: string;
  productCode?: string;
  city?: string;
  productGroup?: string;
  transactionType?: string;
  dateFrom?: string;
  dateTo?: string;
};

function includesNormalized(value: string, filter?: string): boolean {
  const normalizedFilter = filter?.trim().toLowerCase();
  if (!normalizedFilter) return true;
  return value.toLowerCase().includes(normalizedFilter);
}

function filterDemoCommercialTransactions(input: DemoCommercialTransactionFilters): CommercialTransaction[] {
  const filtered = demoCommercialTransactions().filter((item) => {
    if (!includesNormalized(item.documentNumber, input.documentNumber)) return false;
    if (!includesNormalized(item.customerCode, input.customerCode)) return false;
    if (!includesNormalized(item.customerName, input.customerName)) return false;
    if (!includesNormalized(`${item.productCode} ${item.productDescription}`, input.productCode)) return false;
    if (!includesNormalized(item.city, input.city)) return false;
    if (!includesNormalized(item.productGroup, input.productGroup)) return false;
    if (!includesNormalized(item.transactionType, input.transactionType)) return false;
    if (input.dateFrom?.trim() && item.transactionDate < input.dateFrom.trim()) return false;
    if (input.dateTo?.trim() && item.transactionDate > input.dateTo.trim()) return false;
    return true;
  });

  return filtered.length > 0 ? filtered : demoCommercialTransactions();
}

function paginateDemoItems<T>(items: T[], page = DEMO_PAGE, pageSize = DEMO_PAGE_SIZE): T[] {
  const start = Math.max(0, page - 1) * pageSize;
  return items.slice(start, start + pageSize);
}

function demoProducts(): Product[] {
  return [
    { id: 2001, sku: "PAN-104", name: "Pão Francês Congelado 60g", price: 27.9, createdAt: "2026-06-01T00:00:00Z", sourceFileJobId: DEMO_PRODUCT_JOB_ID },
    { id: 2002, sku: "PAN-221", name: "Pão de Queijo Congelado 1kg", price: 8.7, createdAt: "2026-06-01T00:00:00Z", sourceFileJobId: DEMO_PRODUCT_JOB_ID },
    { id: 2003, sku: "PAN-318", name: "Croissant Congelado 80g", price: 6.4, createdAt: "2026-06-02T00:00:00Z", sourceFileJobId: 502 },
    { id: 2004, sku: "PAN-512", name: "Massa para Pizza Congelada 400g", price: 12.9, createdAt: "2026-06-02T00:00:00Z", sourceFileJobId: 502 },
    { id: 2005, sku: "EQP-411", name: "Freezer Comercial Expositor 410L", price: 18.5, createdAt: "2026-06-03T00:00:00Z", sourceFileJobId: 503 },
    { id: 2006, sku: "EQP-620", name: "Armário de Crescimento 20 Esteiras", price: 7_890, createdAt: "2026-06-03T00:00:00Z", sourceFileJobId: 503 },
  ];
}

function filterDemoProducts(search?: string): Product[] {
  const normalizedSearch = search?.trim().toLowerCase();
  const filtered = demoProducts()
    .filter((item) => {
      if (!normalizedSearch) return true;
      return item.sku.toLowerCase().includes(normalizedSearch) || item.name.toLowerCase().includes(normalizedSearch);
    })
    .sort((left, right) => left.name.localeCompare(right.name, "pt-BR") || left.sku.localeCompare(right.sku, "pt-BR"));

  return filtered.length > 0 ? filtered : demoProducts();
}

function demoCommercialSummary(input: {
  page?: number;
  pageSize?: number;
  granularity?: SummaryGranularity;
  sortBy?: SummarySortBy;
} & DemoCommercialTransactionFilters): CommercialTransactionSummaryResponse {
  const filteredItems = filterDemoCommercialTransactions(input);
  const groups = new Map<string, CommercialTransactionCompanySummary>();

  for (const item of filteredItems) {
    const current = groups.get(item.customerName) ?? {
      companyName: item.customerName,
      documentCount: 0,
      singleDocumentNumber: null,
      totalAmount: 0,
      totalQuantity: 0,
      totalWeightKg: 0,
      currentPeriodAmount: 0,
      previousPeriodAmount: 0,
      growthPercent: null,
    };

    current.totalAmount += item.totalAmount;
    current.totalQuantity += item.quantity;
    current.totalWeightKg += item.grossWeightKg;
    current.currentPeriodAmount += item.totalAmount;
    const documents = filteredItems
      .filter((candidate) => candidate.customerName === item.customerName)
      .map((candidate) => candidate.documentNumber);
    const uniqueDocuments = Array.from(new Set(documents));
    current.documentCount = uniqueDocuments.length;
    current.singleDocumentNumber = uniqueDocuments.length === 1 ? uniqueDocuments[0] : null;
    groups.set(item.customerName, current);
  }

  const sortBy = input.sortBy ?? "amount";
  const sortedItems = Array.from(groups.values()).sort((left, right) => {
    if (sortBy === "quantity") return right.totalQuantity - left.totalQuantity;
    if (sortBy === "weight") return right.totalWeightKg - left.totalWeightKg;
    if (sortBy === "growth") return (right.growthPercent ?? Number.NEGATIVE_INFINITY) - (left.growthPercent ?? Number.NEGATIVE_INFINITY);
    return right.totalAmount - left.totalAmount;
  });
  const page = input.page ?? DEMO_PAGE;
  const pageSize = input.pageSize ?? DEMO_PAGE_SIZE;
  const totalAmount = filteredItems.reduce((total, item) => total + item.totalAmount, 0);
  const totalQuantity = filteredItems.reduce((total, item) => total + item.quantity, 0);
  const totalWeightKg = filteredItems.reduce((total, item) => total + item.grossWeightKg, 0);

  return {
    page,
    pageSize,
    totalItems: sortedItems.length,
    granularity: input.granularity ?? "weekly",
    currentPeriodStart: input.dateFrom ?? "2026-06-01",
    previousPeriodStart: "",
    currentPeriodTotalAmount: totalAmount,
    previousPeriodTotalAmount: 0,
    totalGrowthPercent: null,
    totalRecords: filteredItems.length,
    totalAmount,
    totalQuantity,
    totalWeightKg,
    totalCompanies: sortedItems.length,
    items: paginateDemoItems(sortedItems, page, pageSize),
  };
}

function demoCommercialInvoices(input: {
  page?: number;
  pageSize?: number;
} & DemoCommercialTransactionFilters): CommercialInvoiceSummaryResponse {
  const filteredItems = filterDemoCommercialTransactions(input);
  const groups = new Map<string, CommercialInvoiceSummary>();

  for (const item of filteredItems) {
    const current = groups.get(item.documentNumber) ?? {
      documentNumber: item.documentNumber,
      transactionDate: item.transactionDate,
      customerCode: item.customerCode,
      customerName: item.customerName,
      city: item.city,
      transactionType: item.transactionType,
      totalAmount: 0,
      totalQuantity: 0,
      totalWeightKg: 0,
      totalItems: 0,
    };

    current.totalAmount += item.totalAmount;
    current.totalQuantity += item.quantity;
    current.totalWeightKg += item.grossWeightKg;
    current.totalItems += 1;
    groups.set(item.documentNumber, current);
  }

  const items = Array.from(groups.values()).sort((left, right) =>
    right.transactionDate.localeCompare(left.transactionDate, "pt-BR") ||
    right.documentNumber.localeCompare(left.documentNumber, "pt-BR"),
  );
  const page = input.page ?? DEMO_PAGE;
  const pageSize = input.pageSize ?? DEMO_PAGE_SIZE;

  return {
    page,
    pageSize,
    totalItems: items.length,
    totalAmount: items.reduce((total, item) => total + item.totalAmount, 0),
    totalQuantity: items.reduce((total, item) => total + item.totalQuantity, 0),
    totalWeightKg: items.reduce((total, item) => total + item.totalWeightKg, 0),
    items: paginateDemoItems(items, page, pageSize),
  };
}

function demoCommercialInvoiceDetails(documentNumber: string): CommercialInvoiceDetails {
  const items = filterDemoCommercialTransactions({ documentNumber }).filter((item) => item.documentNumber === documentNumber);
  const firstItem = items[0];

  return {
    documentNumber,
    transactionDate: firstItem?.transactionDate ?? "",
    customerCode: firstItem?.customerCode ?? "",
    customerName: firstItem?.customerName ?? "",
    city: firstItem?.city ?? "",
    transactionType: firstItem?.transactionType ?? "",
    totalAmount: items.reduce((total, item) => total + item.totalAmount, 0),
    totalQuantity: items.reduce((total, item) => total + item.quantity, 0),
    totalWeightKg: items.reduce((total, item) => total + item.grossWeightKg, 0),
    totalItems: items.length,
    items,
  };
}

function demoCommercialInvoiceAnalytics(input: {
  granularity?: CommercialInvoiceAnalyticsGranularity;
} & DemoCommercialTransactionFilters): CommercialInvoiceAnalyticsResponse {
  const invoices = demoCommercialInvoices(input).items;
  const granularity = input.granularity ?? "month";
  const trendGroups = new Map<string, CommercialInvoiceAnalyticsTrendPoint>();
  const rankingGroups = new Map<string, CommercialInvoiceAnalyticsRankingItem>();

  for (const invoice of invoices) {
    const periodStart = resolveDemoTimelinePeriodStart(invoice.transactionDate, granularity);
    const trendPoint = trendGroups.get(periodStart) ?? {
      periodStart,
      invoiceCount: 0,
      totalAmount: 0,
      totalWeightKg: 0,
    };
    trendPoint.invoiceCount += 1;
    trendPoint.totalAmount += invoice.totalAmount;
    trendPoint.totalWeightKg += invoice.totalWeightKg;
    trendGroups.set(periodStart, trendPoint);

    const rankingPoint = rankingGroups.get(invoice.customerCode) ?? {
      customerCode: invoice.customerCode,
      customerName: invoice.customerName,
      totalAmount: 0,
      invoiceCount: 0,
      totalItems: 0,
      totalWeightKg: 0,
    };
    rankingPoint.totalAmount += invoice.totalAmount;
    rankingPoint.invoiceCount += 1;
    rankingPoint.totalItems += invoice.totalItems;
    rankingPoint.totalWeightKg += invoice.totalWeightKg;
    rankingGroups.set(invoice.customerCode, rankingPoint);
  }

  return {
    granularity,
    summary: {
      totalInvoices: invoices.length,
      totalAmount: invoices.reduce((total, item) => total + item.totalAmount, 0),
      totalWeightKg: invoices.reduce((total, item) => total + item.totalWeightKg, 0),
      totalCustomers: new Set(invoices.map((item) => item.customerCode)).size,
      totalItems: invoices.reduce((total, item) => total + item.totalItems, 0),
      totalQuantity: invoices.reduce((total, item) => total + item.totalQuantity, 0),
    },
    trend: Array.from(trendGroups.values()).sort((left, right) => left.periodStart.localeCompare(right.periodStart, "pt-BR")),
    ranking: Array.from(rankingGroups.values()).sort((left, right) => right.totalAmount - left.totalAmount || left.customerName.localeCompare(right.customerName, "pt-BR")),
  };
}

function demoCommercialTimeline(input: {
  granularity?: CommercialTransactionTimelineGranularity;
} & DemoCommercialTransactionFilters): CommercialTransactionTimelineResponse {
  const filteredItems = filterDemoCommercialTransactions(input);

  const granularity = input.granularity ?? "month";
  const grouped = new Map<string, CommercialTransactionTimelinePoint>();

  for (const item of filteredItems) {
    const periodStart = resolveDemoTimelinePeriodStart(item.transactionDate, granularity);
    const current = grouped.get(periodStart) ?? {
      periodStart,
      totalAmount: 0,
      totalQuantity: 0,
      totalWeightKg: 0,
      recordCount: 0,
    };

    current.totalAmount += item.totalAmount;
    current.totalQuantity += item.quantity;
    current.totalWeightKg += item.grossWeightKg;
    current.recordCount += 1;
    grouped.set(periodStart, current);
  }

  return {
    granularity,
    items: Array.from(grouped.values()).sort((left, right) => left.periodStart.localeCompare(right.periodStart)),
  };
}

function resolveDemoTimelinePeriodStart(
  transactionDate: string,
  granularity: CommercialTransactionTimelineGranularity,
): string {
  const date = new Date(`${transactionDate}T00:00:00Z`);
  if (Number.isNaN(date.getTime())) return transactionDate;

  if (granularity === "hour") {
    return `${transactionDate}T00:00:00Z`;
  }

  if (granularity === "day") {
    return transactionDate;
  }

  if (granularity === "week") {
    const dayOffset = (date.getUTCDay() + 6) % 7;
    date.setUTCDate(date.getUTCDate() - dayOffset);
    return date.toISOString().slice(0, 10);
  }

  if (granularity === "quarter") {
    const quarterStartMonth = Math.floor(date.getUTCMonth() / 3) * 3;
    date.setUTCMonth(quarterStartMonth, 1);
    return date.toISOString().slice(0, 10);
  }

  return `${transactionDate.slice(0, 7)}-01`;
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
        canRunManualActions: false,
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
        canRunManualActions: true,
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
    { customerCode: "CLI-001", customerName: "Padaria São Bento", revenue: 64_850, quantity: 2_420, weight: 12_800, orders: 28, averageTicket: 2_316.07, variationPercent: 12.6 },
    { customerCode: "CLI-002", customerName: "Supermercado Primavera", revenue: 52_300, quantity: 3_180, weight: 9_750, orders: 21, averageTicket: 2_490.48, variationPercent: -6.4 },
    { customerCode: "CLI-003", customerName: "Cafeteria Grão & Massa", revenue: 38_940, quantity: 1_760, weight: 4_980, orders: 18, averageTicket: 2_163.33, variationPercent: 24.8 },
    { customerCode: "CLI-004", customerName: "Rede Conveniência Rota 12", revenue: 31_500, quantity: 980, weight: 2_400, orders: 12, averageTicket: 2_625, variationPercent: 5.7 },
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
  metric?: "revenue" | "quantity" | "weight" | "orders" | "averageTicket";
}): CustomerTimelineResponse {
  const points: CustomerTimelinePoint[] = [
    { periodStart: "2025-07-01", value: 18_500, revenue: 18_500, quantity: 760, weight: 2_940, orders: 8, averageTicket: 2_312.5 },
    { periodStart: "2025-08-01", value: 0, revenue: 0, quantity: 0, weight: 0, orders: 0, averageTicket: 0 },
    { periodStart: "2025-09-01", value: 20_200, revenue: 20_200, quantity: 810, weight: 3_040, orders: 9, averageTicket: 2_244.44 },
    { periodStart: "2025-10-01", value: 21_400, revenue: 21_400, quantity: 880, weight: 3_200, orders: 9, averageTicket: 2_377.78 },
    { periodStart: "2025-11-01", value: 24_900, revenue: 24_900, quantity: 940, weight: 3_520, orders: 10, averageTicket: 2_490 },
    { periodStart: "2025-12-01", value: 19_700, revenue: 19_700, quantity: 790, weight: 2_980, orders: 8, averageTicket: 2_462.5 },
    { periodStart: "2026-01-01", value: 28_600, revenue: 28_600, quantity: 1_120, weight: 4_100, orders: 12, averageTicket: 2_383.33 },
    { periodStart: "2026-02-01", value: 0, revenue: 0, quantity: 0, weight: 0, orders: 0, averageTicket: 0 },
    { periodStart: "2026-03-01", value: 31_200, revenue: 31_200, quantity: 1_280, weight: 4_450, orders: 13, averageTicket: 2_400 },
    { periodStart: "2026-04-01", value: 29_400, revenue: 29_400, quantity: 1_140, weight: 4_280, orders: 12, averageTicket: 2_450 },
    { periodStart: "2026-05-01", value: 33_800, revenue: 33_800, quantity: 1_430, weight: 4_780, orders: 15, averageTicket: 2_253.33 },
    { periodStart: "2026-06-01", value: 38_940, revenue: 38_940, quantity: 1_760, weight: 4_980, orders: 18, averageTicket: 2_163.33 },
  ];
  const metric = input.metric ?? "revenue";
  return {
    granularity: input.granularity ?? "monthly",
    metric,
    points: points.map((point) => ({ ...point, value: point[metric] })),
  };
}

function demoCustomerIndividualAnalysis(input: {
  customerId: string;
  scope?: CustomerIndividualAnalysisScope;
  metric?: "revenue" | "quantity" | "weight" | "orders" | "averageTicket";
}): CustomerIndividualAnalysisResponse {
  const summary = demoCustomerDetails(input.customerId);
  const timeline = demoCustomerTimeline({ granularity: "monthly", metric: input.metric ?? "revenue" });
  return {
    scope: input.scope ?? "historical",
    periodStart: "2025-07-01",
    periodEnd: DEMO_DATE_TODAY,
    granularity: "monthly",
    metric: timeline.metric,
    summary,
    points: timeline.points,
  };
}

function demoCustomerTopProducts(): CustomerTopProductItem[] {
  return [
    { productCode: "PAN-104", productDescription: "Pão Francês Congelado 60g", quantity: 820, revenue: 22_878, sharePercent: 35.2 },
    { productCode: "PAN-221", productDescription: "Pão de Queijo Congelado 1kg", quantity: 1_100, revenue: 9_570, sharePercent: 14.7 },
    { productCode: "PAN-318", productDescription: "Croissant Congelado 80g", quantity: 480, revenue: 8_880, sharePercent: 13.7 },
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

  if (items.length === 0) {
    return demoFileJobs(page, pageSize);
  }

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
  const dashboard = normalizeProcessingDashboard(await response.json());
  return hasProcessingData(dashboard) ? dashboard : demoProcessingDashboard();
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

export async function runProcessingManualAction(input: {
  action: "sales-summary" | "customer-summary";
  jobIds: number[];
}): Promise<void> {
  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/processing-monitoring/jobs/manual-actions`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  } catch (error) {
    if (shouldUseDemoData(error)) return;
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao executar ação manual."));
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
    canRunManualActions: raw.canRunManualActions ?? raw.CanRunManualActions ?? false,
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

function demoFinanceDashboard(input: {
  customer?: string;
  dateFrom?: string;
  dateTo?: string;
  allTime?: boolean;
  revenueGranularity?: FinanceRevenueGranularity;
}): FinanceDashboardResponse {
  const filteredMetrics = calculateFinanceMetrics(
    {
      customer: input.customer ?? "",
      dateFrom: input.dateFrom ?? "",
      dateTo: input.dateTo ?? "",
      allTime: input.allTime ?? true,
    },
    financeDemoTransactions,
  );
  const metrics = filteredMetrics.items.length > 0
    ? filteredMetrics
    : calculateFinanceMetrics({ customer: "", dateFrom: "", dateTo: "", allTime: true }, financeDemoTransactions);

  return {
    customers: listFinanceCustomers(financeDemoTransactions),
    summary: {
      totalRevenue: metrics.totalRevenue,
      totalOrders: metrics.totalOrders,
      totalQuantity: metrics.totalQuantity,
      averageTicket: metrics.averageTicket,
    },
    revenueTrend: buildFinanceRevenueTrend(metrics.items, input.revenueGranularity ?? "monthly"),
    customerRanking: buildFinanceCustomerRevenueRanking(metrics.items),
    items: metrics.items.slice(((input.page ?? 1) - 1) * (input.pageSize ?? 20), ((input.page ?? 1) - 1) * (input.pageSize ?? 20) + (input.pageSize ?? 20)),
    page: input.page ?? 1,
    pageSize: input.pageSize ?? 20,
    totalItems: metrics.items.length,
    totalPages: Math.max(1, Math.ceil(metrics.items.length / (input.pageSize ?? 20))),
  };
}

function demoFinanceCustomers(input: { search?: string; limit?: number }): FinanceCustomerOption[] {
  const normalizedSearch = input.search?.trim().toLowerCase() ?? "";
  return listFinanceCustomers(financeDemoTransactions)
    .filter((customer) => !normalizedSearch || customer.toLowerCase().includes(normalizedSearch))
    .slice(0, input.limit ?? 20)
    .map((customer) => ({ id: customer, nome: customer }));
}

function normalizeFinanceDashboard(raw: any): FinanceDashboardResponse {
  return {
    customers: raw.customers ?? raw.Customers ?? [],
    summary: {
      totalRevenue: raw.summary?.totalRevenue ?? raw.Summary?.TotalRevenue ?? 0,
      totalOrders: raw.summary?.totalOrders ?? raw.Summary?.TotalOrders ?? 0,
      totalQuantity: raw.summary?.totalQuantity ?? raw.Summary?.TotalQuantity ?? 0,
      averageTicket: raw.summary?.averageTicket ?? raw.Summary?.AverageTicket ?? 0,
    },
    revenueTrend: (raw.revenueTrend ?? raw.RevenueTrend ?? []).map((item: any) => ({
      period: item.period ?? item.Period ?? "",
      label: item.label ?? item.Label ?? "",
      revenue: item.revenue ?? item.Revenue ?? 0,
    })),
    customerRanking: (raw.customerRanking ?? raw.CustomerRanking ?? []).map((item: any) => ({
      customer: item.customer ?? item.Customer ?? "",
      revenue: item.revenue ?? item.Revenue ?? 0,
    })),
    items: (raw.items ?? raw.Items ?? []).map((item: any) => ({
      customer: item.customer ?? item.Customer ?? "",
      date: String(item.date ?? item.Date ?? "").split("T")[0] ?? "",
      revenue: item.revenue ?? item.Revenue ?? 0,
      orders: item.orders ?? item.Orders ?? 0,
      quantity: item.quantity ?? item.Quantity ?? 0,
    })),
    page: raw.page ?? raw.Page ?? 1,
    pageSize: raw.pageSize ?? raw.PageSize ?? 20,
    totalItems: raw.totalItems ?? raw.TotalItems ?? (raw.items ?? raw.Items ?? []).length ?? 0,
    totalPages: raw.totalPages ?? raw.TotalPages ?? 1,
  };
}

export async function fetchFinanceDashboard(input: {
  customer?: string;
  dateFrom?: string;
  dateTo?: string;
  allTime?: boolean;
  revenueGranularity?: FinanceRevenueGranularity;
  page?: number;
  pageSize?: number;
}): Promise<FinanceDashboardResponse> {
  const query = new URLSearchParams();
  if (input.customer?.trim()) query.set("customer", input.customer.trim());
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());
  query.set("allTime", String(input.allTime ?? true));
  query.set("revenueGranularity", input.revenueGranularity ?? "monthly");
  query.set("page", String(input.page ?? 1));
  query.set("pageSize", String(input.pageSize ?? 20));

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/finance/dashboard?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) {
      return demoFinanceDashboard(input);
    }
    throw error;
  }

  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar painel de finanças."));
  const dashboard = normalizeFinanceDashboard(await response.json());
  return hasFinanceData(dashboard) ? dashboard : demoFinanceDashboard(input);
}

export async function fetchFinanceCustomers(input: {
  search?: string;
  limit?: number;
  signal?: AbortSignal;
} = {}): Promise<FinanceCustomerOption[]> {
  const query = new URLSearchParams();
  if (input.search?.trim()) query.set("search", input.search.trim());
  query.set("limit", String(input.limit ?? 20));

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/finance/customers?${query.toString()}`, {
      signal: input.signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }

    if (shouldUseDemoData(error)) {
      return demoFinanceCustomers(input);
    }

    throw error;
  }

  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao buscar clientes."));
  const customers = ((await response.json()) as any[]).map((item) => ({
    id: item.id ?? item.Id ?? item.nome ?? item.Nome ?? "",
    nome: item.nome ?? item.Nome ?? item.name ?? item.Name ?? "",
  }));
  return customers.length > 0 ? customers : demoFinanceCustomers(input);
}

export async function fetchProducts(input: {
  page?: number;
  pageSize?: number;
  search?: string;
  signal?: AbortSignal;
} = {}): Promise<PagedResult<Product>> {
  const query = new URLSearchParams();
  const page = input.page ?? DEMO_PAGE;
  const pageSize = input.pageSize ?? DEMO_PAGE_SIZE;
  query.set("page", String(page));
  query.set("pageSize", String(pageSize));
  if (input.search?.trim()) query.set("search", input.search.trim());

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/products?${query.toString()}`, {
      signal: input.signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }

    if (shouldUseDemoData(error)) {
      const items = filterDemoProducts(input.search);
      return { page, pageSize, total: items.length, items: paginateDemoItems(items, page, pageSize) };
    }

    throw error;
  }

  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar produtos."));

  const raw = (await response.json()) as
    | PagedResult<Product>
    | { Page?: number; PageSize?: number; Total?: number; Items?: Array<Product | { Id?: number; Sku?: string; Name?: string; Price?: number; CreatedAt?: string; SourceFileJobId?: number }> };
  const rawItems = (raw as PagedResult<Product>).items ?? (raw as { Items?: Product[] }).Items ?? [];

  const result = {
    page: (raw as PagedResult<Product>).page ?? (raw as { Page?: number }).Page ?? page,
    pageSize: (raw as PagedResult<Product>).pageSize ?? (raw as { PageSize?: number }).PageSize ?? pageSize,
    total: (raw as PagedResult<Product>).total ?? (raw as { Total?: number }).Total ?? 0,
    items: rawItems.map((item: any) => ({
      id: item.id ?? item.Id ?? 0,
      sku: item.sku ?? item.Sku ?? "",
      name: item.name ?? item.Name ?? "",
      price: item.price ?? item.Price ?? 0,
      createdAt: item.createdAt ?? item.CreatedAt ?? "",
      sourceFileJobId: item.sourceFileJobId ?? item.SourceFileJobId ?? 0,
    })),
  };
  if (!hasItems(result)) {
    const items = filterDemoProducts(input.search);
    return { page, pageSize, total: items.length, items: paginateDemoItems(items, page, pageSize) };
  }
  return result;
}

function demoCustomerNewCustomersMonthly(): CustomerNewCustomersMonthlyResponse {
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

function demoCustomerPurchaseHistory(input: {
  page?: number;
  pageSize?: number;
}): CustomerPurchaseHistoryResponse {
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
  const page = input.page ?? DEMO_PAGE;
  const pageSize = input.pageSize ?? DEMO_HISTORY_PAGE_SIZE;
  return { page, pageSize, totalItems: items.length, items: paginateDemoItems(items, page, pageSize) };
}

function demoCustomerComparison(): CustomerComparisonResponse {
  return {
    items: [
      { label: "Mês atual", currentValue: 38_940, previousValue: 31_200, variationPercent: 24.8 },
      { label: "Últimos 3 meses", currentValue: 98_500, previousValue: 91_000, variationPercent: 8.2 },
      { label: "Últimos 6 meses", currentValue: 165_800, previousValue: 172_400, variationPercent: -3.8 },
    ],
  };
}

function demoCustomerInsights(): CustomerInsightsResponse {
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
  signal?: AbortSignal;
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
    response = await authFetch(`${API_URL}/api/commercial-transactions?${query.toString()}`, {
      signal: input.signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }

    if (shouldUseDemoData(error)) {
      const page = input.page ?? DEMO_PAGE;
      const pageSize = input.pageSize ?? DEMO_PAGE_SIZE;
      const items = filterDemoCommercialTransactions(input);
      return { page, pageSize, total: items.length, items: paginateDemoItems(items, page, pageSize) };
    }
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar vendas."));

  const raw = (await response.json()) as
    | PagedResult<CommercialTransaction>
    | { Page?: number; PageSize?: number; Total?: number; Items?: CommercialTransaction[] };

  const items = (raw as PagedResult<CommercialTransaction>).items ?? (raw as { Items?: CommercialTransaction[] }).Items ?? [];

  const result = {
    page: (raw as PagedResult<CommercialTransaction>).page ?? (raw as { Page?: number }).Page ?? 1,
    pageSize: (raw as PagedResult<CommercialTransaction>).pageSize ?? (raw as { PageSize?: number }).PageSize ?? 20,
    total: (raw as PagedResult<CommercialTransaction>).total ?? (raw as { Total?: number }).Total ?? 0,
    items,
  };
  if (!hasItems(result)) {
    const page = input.page ?? DEMO_PAGE;
    const pageSize = input.pageSize ?? DEMO_PAGE_SIZE;
    const demoItems = filterDemoCommercialTransactions(input);
    return { page, pageSize, total: demoItems.length, items: paginateDemoItems(demoItems, page, pageSize) };
  }
  return result;
}

function normalizeCommercialInvoiceSummary(raw: any): CommercialInvoiceSummaryResponse {
  return {
    page: raw.page ?? raw.Page ?? 1,
    pageSize: raw.pageSize ?? raw.PageSize ?? 20,
    totalItems: raw.totalItems ?? raw.TotalItems ?? 0,
    totalAmount: raw.totalAmount ?? raw.TotalAmount ?? 0,
    totalQuantity: raw.totalQuantity ?? raw.TotalQuantity ?? 0,
    totalWeightKg: raw.totalWeightKg ?? raw.TotalWeightKg ?? 0,
    items: (raw.items ?? raw.Items ?? []).map((item: any) => ({
      documentNumber: item.documentNumber ?? item.DocumentNumber ?? "",
      transactionDate: String(item.transactionDate ?? item.TransactionDate ?? "").split("T")[0] ?? "",
      customerCode: item.customerCode ?? item.CustomerCode ?? "",
      customerName: item.customerName ?? item.CustomerName ?? "",
      city: item.city ?? item.City ?? "",
      transactionType: item.transactionType ?? item.TransactionType ?? "",
      totalAmount: item.totalAmount ?? item.TotalAmount ?? 0,
      totalQuantity: item.totalQuantity ?? item.TotalQuantity ?? 0,
      totalWeightKg: item.totalWeightKg ?? item.TotalWeightKg ?? 0,
      totalItems: item.totalItems ?? item.TotalItems ?? 0,
    })),
  };
}

function normalizeCommercialInvoiceDetails(raw: any): CommercialInvoiceDetails {
  return {
    documentNumber: raw.documentNumber ?? raw.DocumentNumber ?? "",
    transactionDate: String(raw.transactionDate ?? raw.TransactionDate ?? "").split("T")[0] ?? "",
    customerCode: raw.customerCode ?? raw.CustomerCode ?? "",
    customerName: raw.customerName ?? raw.CustomerName ?? "",
    city: raw.city ?? raw.City ?? "",
    transactionType: raw.transactionType ?? raw.TransactionType ?? "",
    totalAmount: raw.totalAmount ?? raw.TotalAmount ?? 0,
    totalQuantity: raw.totalQuantity ?? raw.TotalQuantity ?? 0,
    totalWeightKg: raw.totalWeightKg ?? raw.TotalWeightKg ?? 0,
    totalItems: raw.totalItems ?? raw.TotalItems ?? 0,
    items: (raw.items ?? raw.Items ?? []).map((item: any) => ({
      id: item.id ?? item.Id ?? 0,
      documentNumber: item.documentNumber ?? item.DocumentNumber ?? "",
      transactionDate: String(item.transactionDate ?? item.TransactionDate ?? "").split("T")[0] ?? "",
      customerCode: item.customerCode ?? item.CustomerCode ?? "",
      customerName: item.customerName ?? item.CustomerName ?? "",
      productCode: item.productCode ?? item.ProductCode ?? "",
      productDescription: item.productDescription ?? item.ProductDescription ?? "",
      quantity: item.quantity ?? item.Quantity ?? 0,
      unitPrice: item.unitPrice ?? item.UnitPrice ?? 0,
      totalAmount: item.totalAmount ?? item.TotalAmount ?? 0,
      transactionType: item.transactionType ?? item.TransactionType ?? "",
      city: item.city ?? item.City ?? "",
      productGroup: item.productGroup ?? item.ProductGroup ?? "",
      grossWeightKg: item.grossWeightKg ?? item.GrossWeightKg ?? 0,
      sourceFileJobId: item.sourceFileJobId ?? item.SourceFileJobId ?? 0,
    })),
  };
}

function normalizeCommercialInvoiceAnalytics(raw: any): CommercialInvoiceAnalyticsResponse {
  return {
    granularity: raw.granularity ?? raw.Granularity ?? "month",
    summary: {
      totalInvoices: raw.summary?.totalInvoices ?? raw.Summary?.TotalInvoices ?? 0,
      totalAmount: raw.summary?.totalAmount ?? raw.Summary?.TotalAmount ?? 0,
      totalWeightKg: raw.summary?.totalWeightKg ?? raw.Summary?.TotalWeightKg ?? 0,
      totalCustomers: raw.summary?.totalCustomers ?? raw.Summary?.TotalCustomers ?? 0,
      totalItems: raw.summary?.totalItems ?? raw.Summary?.TotalItems ?? 0,
      totalQuantity: raw.summary?.totalQuantity ?? raw.Summary?.TotalQuantity ?? 0,
    },
    trend: (raw.trend ?? raw.Trend ?? []).map((item: any) => ({
      periodStart: String(item.periodStart ?? item.PeriodStart ?? "").split("T")[0] ?? "",
      invoiceCount: item.invoiceCount ?? item.InvoiceCount ?? 0,
      totalAmount: item.totalAmount ?? item.TotalAmount ?? 0,
      totalWeightKg: item.totalWeightKg ?? item.TotalWeightKg ?? 0,
    })),
    ranking: (raw.ranking ?? raw.Ranking ?? []).map((item: any) => ({
      customerCode: item.customerCode ?? item.CustomerCode ?? "",
      customerName: item.customerName ?? item.CustomerName ?? "",
      totalAmount: item.totalAmount ?? item.TotalAmount ?? 0,
      invoiceCount: item.invoiceCount ?? item.InvoiceCount ?? 0,
      totalItems: item.totalItems ?? item.TotalItems ?? 0,
      totalWeightKg: item.totalWeightKg ?? item.TotalWeightKg ?? 0,
    })),
  };
}

export async function fetchCommercialInvoiceAnalytics(input: {
  granularity?: CommercialInvoiceAnalyticsGranularity;
  documentNumber?: string;
  customerCode?: string;
  customerName?: string;
  productCode?: string;
  city?: string;
  productGroup?: string;
  transactionType?: string;
  dateFrom?: string;
  dateTo?: string;
  signal?: AbortSignal;
}): Promise<CommercialInvoiceAnalyticsResponse> {
  const query = new URLSearchParams();
  query.set("granularity", input.granularity ?? "month");

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
    response = await authFetch(`${API_URL}/api/commercial-transactions/invoice-analytics?${query.toString()}`, {
      signal: input.signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }

    if (shouldUseDemoData(error)) return demoCommercialInvoiceAnalytics(input);
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar análise de notas fiscais."));

  return normalizeCommercialInvoiceAnalytics(await response.json());
}

export async function fetchCommercialInvoices(input: {
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
  signal?: AbortSignal;
}): Promise<CommercialInvoiceSummaryResponse> {
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
    response = await authFetch(`${API_URL}/api/commercial-transactions/invoices?${query.toString()}`, {
      signal: input.signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }

    if (shouldUseDemoData(error)) return demoCommercialInvoices(input);
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar notas fiscais."));

  return normalizeCommercialInvoiceSummary(await response.json());
}

export async function fetchCommercialInvoiceDetails(documentNumber: string, signal?: AbortSignal): Promise<CommercialInvoiceDetails> {
  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/commercial-transactions/invoices/${encodeURIComponent(documentNumber)}`, {
      signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }

    if (shouldUseDemoData(error)) return demoCommercialInvoiceDetails(documentNumber);
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar detalhes da nota fiscal."));

  return normalizeCommercialInvoiceDetails(await response.json());
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
  signal?: AbortSignal;
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
    response = await authFetch(`${API_URL}/api/commercial-transactions/summary?${query.toString()}`, {
      signal: input.signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }

    if (shouldUseDemoData(error)) return demoCommercialSummary(input);
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar resumo de vendas."));

  const summary = (await response.json()) as CommercialTransactionSummaryResponse;
  return summary.totalRecords > 0 || summary.items.length > 0 ? summary : demoCommercialSummary(input);
}

export async function fetchCommercialTransactionsTimeline(input: {
  granularity?: CommercialTransactionTimelineGranularity;
  documentNumber?: string;
  customerCode?: string;
  customerName?: string;
  productCode?: string;
  city?: string;
  productGroup?: string;
  transactionType?: string;
  dateFrom?: string;
  dateTo?: string;
  signal?: AbortSignal;
}): Promise<CommercialTransactionTimelineResponse> {
  const query = new URLSearchParams();
  query.set("groupBy", input.granularity ?? "month");

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
    response = await authFetch(`${API_URL}/api/commercial-transactions/timeline?${query.toString()}`, {
      signal: input.signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }

    if (shouldUseDemoData(error)) return demoCommercialTimeline(input);
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar a evolução de vendas."));

  const timeline = (await response.json()) as CommercialTransactionTimelineResponse;
  return timeline.items.length > 0 ? timeline : demoCommercialTimeline(input);
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
  const summary = (await response.json()) as CustomerAnalyticsSummary;
  return summary.activeCustomers > 0 || summary.totalRevenue > 0 || summary.totalOrders > 0 ? summary : demoCustomerSummary();
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
  const ranking = (await response.json()) as CustomerRankingResponse;
  return ranking.items.length > 0 || ranking.totalItems > 0 ? ranking : demoCustomerRanking(input);
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
    if (shouldUseDemoData(error)) return demoCustomerNewCustomersMonthly();
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar evolução mensal de novos clientes."));
  const monthly = (await response.json()) as CustomerNewCustomersMonthlyResponse;
  return monthly.points.length > 0 || monthly.totalNewCustomers > 0 ? monthly : demoCustomerNewCustomersMonthly();
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
  const details = (await response.json()) as CustomerDetailSummary;
  return details.totalOrders > 0 || details.totalRevenue > 0 ? details : demoCustomerDetails(input.customerId);
}

export async function fetchCustomerIndividualAnalysis(input: {
  customerId: string;
  scope?: CustomerIndividualAnalysisScope;
  metric?: "revenue" | "quantity" | "weight" | "orders" | "averageTicket";
  dateFrom?: string;
  dateTo?: string;
}): Promise<CustomerIndividualAnalysisResponse> {
  const query = new URLSearchParams();
  query.set("scope", input.scope ?? "historical");
  query.set("metric", input.metric ?? "revenue");
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());

  let response: Response;
  try {
    response = await authFetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/individual-analysis?${query.toString()}`);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoCustomerIndividualAnalysis(input);
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar análise individual do cliente."));

  const raw = await response.json() as any;
  return {
    scope: (raw.scope ?? raw.Scope ?? "historical") as CustomerIndividualAnalysisScope,
    periodStart: String(raw.periodStart ?? raw.PeriodStart ?? ""),
    periodEnd: String(raw.periodEnd ?? raw.PeriodEnd ?? ""),
    granularity: (raw.granularity ?? raw.Granularity ?? "monthly") as "weekly" | "monthly",
    metric: (raw.metric ?? raw.Metric ?? "revenue") as CustomerIndividualAnalysisResponse["metric"],
    summary: {
      customerCode: String(raw.summary?.customerCode ?? raw.Summary?.CustomerCode ?? ""),
      customerName: String(raw.summary?.customerName ?? raw.Summary?.CustomerName ?? ""),
      city: String(raw.summary?.city ?? raw.Summary?.City ?? ""),
      linkedCompany: String(raw.summary?.linkedCompany ?? raw.Summary?.LinkedCompany ?? ""),
      lastPurchaseDate: raw.summary?.lastPurchaseDate ?? raw.Summary?.LastPurchaseDate ?? null,
      status: String(raw.summary?.status ?? raw.Summary?.Status ?? "Ativo") as CustomerDetailSummary["status"],
      totalRevenue: Number(raw.summary?.totalRevenue ?? raw.Summary?.TotalRevenue ?? 0),
      averageTicket: raw.summary?.averageTicket == null && raw.Summary?.AverageTicket == null ? null : Number(raw.summary?.averageTicket ?? raw.Summary?.AverageTicket ?? 0),
      averageRevenueMonthly: raw.summary?.averageRevenueMonthly == null && raw.Summary?.AverageRevenueMonthly == null ? null : Number(raw.summary?.averageRevenueMonthly ?? raw.Summary?.AverageRevenueMonthly ?? 0),
      averageRevenueWeekly: raw.summary?.averageRevenueWeekly == null && raw.Summary?.AverageRevenueWeekly == null ? null : Number(raw.summary?.averageRevenueWeekly ?? raw.Summary?.AverageRevenueWeekly ?? 0),
      totalQuantity: Number(raw.summary?.totalQuantity ?? raw.Summary?.TotalQuantity ?? 0),
      totalWeight: Number(raw.summary?.totalWeight ?? raw.Summary?.TotalWeight ?? 0),
      totalOrders: Number(raw.summary?.totalOrders ?? raw.Summary?.TotalOrders ?? 0),
      averageDaysBetweenPurchases: raw.summary?.averageDaysBetweenPurchases == null && raw.Summary?.AverageDaysBetweenPurchases == null ? null : Number(raw.summary?.averageDaysBetweenPurchases ?? raw.Summary?.AverageDaysBetweenPurchases ?? 0),
    },
    points: (raw.points ?? raw.Points ?? []).map((point: any) => ({
      periodStart: String(point.periodStart ?? point.PeriodStart ?? ""),
      value: Number(point.value ?? point.Value ?? 0),
      revenue: Number(point.revenue ?? point.Revenue ?? 0),
      quantity: Number(point.quantity ?? point.Quantity ?? 0),
      weight: Number(point.weight ?? point.Weight ?? 0),
      orders: Number(point.orders ?? point.Orders ?? 0),
      averageTicket: Number(point.averageTicket ?? point.AverageTicket ?? 0),
    })),
  };
}

export async function fetchCustomerTimeline(input: {
  customerId: string;
  granularity?: "daily" | "weekly" | "monthly";
  metric?: "revenue" | "quantity" | "weight" | "orders" | "averageTicket";
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
  const timeline = (await response.json()) as CustomerTimelineResponse;
  return timeline.points.length > 0 ? timeline : demoCustomerTimeline(input);
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
  const products = (await response.json()) as CustomerTopProductItem[];
  return products.length > 0 ? products : demoCustomerTopProducts();
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
    if (shouldUseDemoData(error)) return demoCustomerPurchaseHistory(input);
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar histórico de compras."));
  const history = (await response.json()) as CustomerPurchaseHistoryResponse;
  return history.items.length > 0 || history.totalItems > 0 ? history : demoCustomerPurchaseHistory(input);
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
    if (shouldUseDemoData(error)) return demoCustomerComparison();
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar comparativo do cliente."));
  const comparison = (await response.json()) as CustomerComparisonResponse;
  return comparison.items.length > 0 ? comparison : demoCustomerComparison();
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
    if (shouldUseDemoData(error)) return demoCustomerInsights();
    throw error;
  }
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar insights do cliente."));
  const insights = (await response.json()) as CustomerInsightsResponse;
  return insights.monthlyHistoryPeriods > 0 || insights.predictedRevenue != null ? insights : demoCustomerInsights();
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
  const report = (await response.json()) as CustomerCommercialHealthReport;
  return report.evolution.length > 0 || report.products.length > 0 || report.timeline.length > 0 ? report : demoCustomerCommercialHealth(input.customerId);
}

