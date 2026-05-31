import { extractHeadersInWorker } from "@/features/import-template-builder/utils/extract-headers-in-worker";
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
  | "Failed";

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
    const response = await fetch(`${API_URL}/api/files/upload`, {
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
  const response = await fetch(`${API_URL}/api/files/jobs?page=${page}&pageSize=${pageSize}`);
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
  const response = await fetch(`${API_URL}/api/files/jobs/${jobId}/errors?page=${page}&pageSize=${pageSize}`);
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

export async function fetchTemplateConfigs(): Promise<TemplateConfig[]> {
  const response = await fetch(`${API_URL}/api/template-configs`);
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
  const response = await fetch(`${API_URL}/api/template-configs`, {
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
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());

  const response = await fetch(`${API_URL}/api/commercial-transactions?${query.toString()}`);
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

export type ApiImportFileType = { id: string; name: string; code?: string };
export type ApiTargetField = { name: string; displayName: string; required: boolean; description?: string };
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

export async function fetchImportTemplateFileTypes(): Promise<ApiImportFileType[]> {
  try {
    const response = await fetch(`${API_URL}/api/import-templates/file-types`);
    if (response.ok) {
      const data = (await response.json()) as Array<Record<string, unknown>>;
      return (data ?? []).map((item) => ({
        id: String(item.id ?? item.Id ?? item.value ?? item.Value ?? ""),
        name: String(item.name ?? item.Name ?? item.label ?? item.Label ?? ""),
        code: String(item.code ?? item.Code ?? ""),
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
  if (input.dateFrom?.trim()) query.set("dateFrom", input.dateFrom.trim());
  if (input.dateTo?.trim()) query.set("dateTo", input.dateTo.trim());
  if (input.referenceDate?.trim()) query.set("referenceDate", input.referenceDate.trim());

  const response = await fetch(`${API_URL}/api/commercial-transactions/summary?${query.toString()}`);
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

  const response = await fetch(`${API_URL}/api/customer-analytics-v2/summary?${query.toString()}`);
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

  const response = await fetch(`${API_URL}/api/customer-analytics-v2/ranking?${query.toString()}`);
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

  const response = await fetch(`${API_URL}/api/customer-analytics-v2/new-customers-monthly?${query.toString()}`);
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

  const response = await fetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/summary?${query.toString()}`);
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

  const response = await fetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/timeline?${query.toString()}`);
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

  const response = await fetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/top-products?${query.toString()}`);
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

  const response = await fetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/purchase-history?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar histórico de compras."));
  return (await response.json()) as CustomerPurchaseHistoryResponse;
}

export async function fetchCustomerComparison(input: {
  customerId: string;
  referenceDate?: string;
}): Promise<CustomerComparisonResponse> {
  const query = new URLSearchParams();
  if (input.referenceDate?.trim()) query.set("referenceDate", input.referenceDate.trim());

  const response = await fetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/comparison?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar comparativo do cliente."));
  return (await response.json()) as CustomerComparisonResponse;
}

export async function fetchCustomerInsights(input: {
  customerId: string;
  movingAverageWindowMonths?: 3 | 6 | 12;
}): Promise<CustomerInsightsResponse> {
  const query = new URLSearchParams();
  query.set("movingAverageWindowMonths", String(input.movingAverageWindowMonths ?? 3));

  const response = await fetch(`${API_URL}/api/customers/${encodeURIComponent(input.customerId)}/insights?${query.toString()}`);
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao carregar insights do cliente."));
  return (await response.json()) as CustomerInsightsResponse;
}

export async function fetchImportTemplateTargetFields(importFileTypeId: string): Promise<ApiTargetField[]> {
  try {
    const response = await fetch(`${API_URL}/api/import-templates/file-types/${importFileTypeId}/fields`);
    if (response.ok) {
      const data = (await response.json()) as Array<Record<string, unknown>>;
      return (data ?? []).map((item) => ({
        name: String(item.name ?? item.Name ?? ""),
        displayName: String(item.displayName ?? item.DisplayName ?? item.name ?? item.Name ?? ""),
        required: Boolean(item.required ?? item.Required ?? false),
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

export async function extractSpreadsheetHeaders(file: File, importFileTypeId?: string): Promise<string[]> {
  const formData = new FormData();
  formData.append("file", file);
  if (importFileTypeId) formData.append("importFileTypeId", importFileTypeId);

  try {
    const response = await fetch(`${API_URL}/api/import-templates/extract-headers`, { method: "POST", body: formData });
    if (response.ok) {
      const payload = (await response.json()) as { headers?: string[]; Headers?: string[] } | string[];
      const apiHeaders = Array.isArray(payload) ? payload : payload.headers ?? payload.Headers ?? [];
      if (apiHeaders.length > 0) return apiHeaders;
    }
  } catch {
    // fallback
  }

  return extractHeadersInWorker(file);
}

export async function fetchTransformRules(): Promise<ApiTransformRule[]> {
  try {
    const response = await fetch(`${API_URL}/api/import-templates/transform-rules`);
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
  const response = await fetch(`${API_URL}/api/import-templates/${templateId}`);
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
  const response = await fetch(`${API_URL}/api/import-templates`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao criar template."));
  return (await response.json()) as ApiImportTemplate;
}

export async function updateImportTemplate(templateId: string, input: ApiImportTemplate): Promise<ApiImportTemplate> {
  const response = await fetch(`${API_URL}/api/import-templates/${templateId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });
  if (!response.ok) throw new Error(await parseApiError(response, "Falha ao atualizar template."));
  return (await response.json()) as ApiImportTemplate;
}


