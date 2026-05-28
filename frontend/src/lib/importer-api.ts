export type FileType = "Unknown" | "Customers" | "Orders" | "Products";

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
};

export type ImportError = {
  id: number;
  fileJobId: number;
  rowNumber: number;
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

const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5279";

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
};

const fileTypeMap: Record<number, FileType> = {
  0: "Unknown",
  1: "Customers",
  2: "Orders",
  3: "Products",
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

export async function uploadFile(file: File): Promise<number> {
  const form = new FormData();
  form.append("file", file);

  const response = await fetch(`${API_URL}/api/files/upload`, {
    method: "POST",
    body: form,
  });

  if (!response.ok) {
    throw new Error(await parseApiError(response, `Falha ao enviar '${file.name}'.`));
  }
  const data = (await response.json()) as { fileJobId: number };
  return data.fileJobId;
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
  ).map((job) => ({
    id: job.id ?? job.Id ?? 0,
    filePath: job.filePath ?? job.FilePath ?? "",
    fileType: normalizeFileType(job.fileType ?? job.FileType ?? "Unknown"),
    status: normalizeStatus(job.status ?? job.Status ?? "Failed"),
    createdAt: job.createdAt ?? job.CreatedAt ?? "",
    errorCount: job.errorCount ?? job.ErrorCount ?? 0,
    currentStep: job.currentStep ?? job.CurrentStep ?? "",
    progressPercent: job.progressPercent ?? job.ProgressPercent ?? 0,
    processedRows: job.processedRows ?? job.ProcessedRows ?? 0,
    totalRows: job.totalRows ?? job.TotalRows ?? 0,
  }));

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
