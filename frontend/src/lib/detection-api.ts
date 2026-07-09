import { authFetch } from "@/lib/auth";
import { getApiServiceBaseUrl } from "@/lib/api-url";

const API_BASE = getApiServiceBaseUrl();

export type DetectorDto = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  status: "Active" | "Disabled";
  created_at: string;
  updated_at: string;
  lastRun: {
    id: string;
    status: string;
    requestedAt: string;
    findingsCount: number;
    analyzedItems: number;
  } | null;
};

export type DetectionRunSummaryDto = {
  id: string;
  status: string;
  trigger: string;
  requestedAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  attemptCount: number;
  analyzedItems: number;
  findingsCount: number;
  statusReason: string | null;
};

export type DetectionRunDetailDto = {
  id: string;
  detector: {
    id: string;
    code: string;
    name: string;
  };
  status: string;
  trigger: string;
  requestedAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  attemptCount: number;
  analyzedItems: number;
  findingsCount: number;
  statusReason: string | null;
  durationSeconds: number | null;
};

export type FindingDto = {
  id: string;
  fingerprint: string;
  title: string;
  description: string;
  subjectType: string;
  subjectId: string;
  subjectLabel: string | null;
  detectedAt: string;
};

export type FindingEvidenceDto = {
  name: string;
  value: string;
  referenceValue: string | null;
  unit: string | null;
  description: string | null;
  sourceType: string | null;
  sourceId: string | null;
  observedAt: string;
};

export type FindingDetailDto = FindingDto & {
  evidences: FindingEvidenceDto[];
};

export type PagedResponse<T> = {
  page: number;
  pageSize: number;
  total: number;
  items: T[];
};

export async function fetchDetectors(): Promise<DetectorDto[]> {
  const response = await authFetch(`${API_BASE}/api/detectors`);
  if (!response.ok) throw new Error("Falha ao buscar detectores.");
  return (await response.json()) as DetectorDto[];
}

export async function executeDetector(detectorId: string): Promise<{ runId: string; status: string }> {
  const response = await authFetch(
    `${API_BASE}/api/detectors/${detectorId}/runs`,
    { method: "POST" },
  );
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.message ?? "Falha ao executar detector.");
  }
  return (await response.json()) as { runId: string; status: string };
}

export async function fetchDetectorRuns(
  detectorId: string,
  page = 1,
  pageSize = 20,
): Promise<PagedResponse<DetectionRunSummaryDto>> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  const response = await authFetch(
    `${API_BASE}/api/detectors/${detectorId}/runs?${query.toString()}`,
  );
  if (!response.ok) throw new Error("Falha ao buscar execuções.");
  return (await response.json()) as PagedResponse<DetectionRunSummaryDto>;
}

export async function fetchDetectionRun(runId: string): Promise<DetectionRunDetailDto> {
  const response = await authFetch(`${API_BASE}/api/detection-runs/${runId}`);
  if (!response.ok) throw new Error("Falha ao buscar execução.");
  return (await response.json()) as DetectionRunDetailDto;
}

export async function fetchRunFindings(
  runId: string,
  page = 1,
  pageSize = 20,
): Promise<PagedResponse<FindingDto>> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  const response = await authFetch(
    `${API_BASE}/api/detection-runs/${runId}/findings?${query.toString()}`,
  );
  if (!response.ok) throw new Error("Falha ao buscar findings.");
  return (await response.json()) as PagedResponse<FindingDto>;
}

export async function fetchFinding(findingId: string): Promise<FindingDetailDto> {
  const response = await authFetch(`${API_BASE}/api/findings/${findingId}`);
  if (!response.ok) throw new Error("Falha ao buscar finding.");
  return (await response.json()) as FindingDetailDto;
}
