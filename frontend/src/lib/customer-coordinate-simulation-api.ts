import { authFetch } from "@/lib/auth";
import { buildGatewayUrl } from "@/lib/api-url";

export type CoordinateSimulationExecution = {
  id: string;
  status: string;
  progressPercent: number;
  progressMessage: string | null;
  errorMessage: string | null;
  createdAt: string;
  finishedAt: string | null;
  canRevert: boolean;
};

export type CoordinateSimulationItem = {
  customerId: string;
  externalCode: string;
  name: string;
  municipality: string;
  stateCode: string;
  registeredAddress: string;
  precision: string | null;
  source: string | null;
  latitude: number | null;
  longitude: number | null;
  isSimulated: boolean;
  baseLatitude: number | null;
  baseLongitude: number | null;
  baseSource: string | null;
  simulatedAddress: string | null;
  distanceMeters: number | null;
  appliedAt: string | null;
  jobExecutionId: string | null;
};

export type CoordinateSimulationResponse = {
  snapshotId: string | null;
  summary: { total: number; exact: number; simulated: number; pending: number };
  page: number;
  pageSize: number;
  total: number;
  items: CoordinateSimulationItem[];
  executions: CoordinateSimulationExecution[];
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await authFetch(
    buildGatewayUrl(`admin/customer-coordinate-simulations${path}`),
    init,
  );
  if (!response.ok) {
    const payload = (await response.json().catch(() => null)) as {
      message?: string;
      detail?: string;
    } | null;
    throw new Error(
      payload?.message ??
        payload?.detail ??
        "Não foi possível gerenciar as localizações simuladas.",
    );
  }
  return response.json() as Promise<T>;
}

export function fetchCoordinateSimulations(search = "", page = 1, pageSize = 20) {
  const query = new URLSearchParams({ search, page: String(page), pageSize: String(pageSize) });
  return request<CoordinateSimulationResponse>(`?${query.toString()}`);
}

export function runCoordinateSimulation(recalculateDependents: boolean) {
  return request<{ jobExecutionId: string; status: string }>("/run", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ recalculateDependents }),
  });
}

export function revertCoordinateSimulation(sourceJobExecutionId: string) {
  return request<{ jobExecutionId: string; status: string }>("/revert", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ sourceJobExecutionId }),
  });
}
