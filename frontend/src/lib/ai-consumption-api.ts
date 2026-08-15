import { authFetch } from "@/lib/auth";
import { buildGatewayUrl } from "@/lib/api-url";

export type AiConsumptionReport = {
  from: string; to: string;
  total: { inputTokens: number; outputTokens: number; totalTokens: number; estimatedCostUsd: number; calls: number; responses: number };
  detailPage: number; detailPageSize: number; detailTotal: number;
  details: Array<{ id: string; responseExecutionId: string; userId: number; userName: string; model: string; purpose: string; status: string; inputTokens: number; outputTokens: number; totalTokens: number; estimatedCostUsd: number; createdAt: string }>;
};

export type AiConsumptionConfiguration = {
  model: string; defaultMonthlyTokenLimit: number; defaultAlertPercentage: number;
  prices: Array<{ id: number; model: string; inputPricePerMillionUsd: number; outputPricePerMillionUsd: number; effectiveFrom: string }>;
};

export type AiConsumptionUser = { userId: number; name: string; email: string; role: string; monthlyTokenLimit: number | null; alertPercentage: number | null };
export type AiConsumptionUsersPage = { page: number; pageSize: number; total: number; items: AiConsumptionUser[] };

export type AiConsumptionAlert = { id: string; userId: number; userName: string; periodMonth: string; level: string; consumedTokens: number; tokenLimit: number; createdAt: string; readAt: string | null };

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await authFetch(buildGatewayUrl(`admin/ai-consumption${path}`), init);
  if (!response.ok) {
    const body = await response.json().catch(() => null) as { detail?: string } | null;
    throw new Error(body?.detail ?? "Não foi possível concluir a operação.");
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}

export const getAiConsumptionReport = (from: string, to: string, userId?: string, detailPage = 1, detailPageSize = 25) => api<AiConsumptionReport>(`/report?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}${userId ? `&userId=${userId}` : ""}&detailPage=${detailPage}&detailPageSize=${detailPageSize}`);
export const getAiConsumptionConfiguration = () => api<AiConsumptionConfiguration>("/configuration");
export const listAiConsumptionUsers = ({ search = "", page = 1, pageSize = 20 }: { search?: string; page?: number; pageSize?: number } = {}) =>
  api<AiConsumptionUsersPage>(`/users?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}`);
export const updateAiConsumptionConfiguration = (body: { model: string; defaultMonthlyTokenLimit: number; defaultAlertPercentage: number }) => api<void>("/configuration", { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
export const updateAiUserLimit = (userId: number, body: { monthlyTokenLimit: number | null; alertPercentage: number | null }) => api<void>(`/users/${userId}/limit`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
export const addAiModelPrice = (body: { model: string; inputPricePerMillionUsd: number; outputPricePerMillionUsd: number; effectiveFrom: string }) => api<void>("/prices", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
export const listAiConsumptionAlerts = () => api<AiConsumptionAlert[]>("/alerts");
export const readAiConsumptionAlert = (id: string) => api<void>(`/alerts/${id}/read`, { method: "PUT" });
