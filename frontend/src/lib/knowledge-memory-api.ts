import { authFetch } from "@/lib/auth";
import { buildGatewayUrl } from "@/lib/api-url";

export type KnowledgeMemory = {
  id: string; scope: "company" | "user"; ownerUserId: number | null; ownerUserName: string | null;
  createdByUserId: number; createdByUserName: string; subject: string; content: string; isActive: boolean;
  createdAt: string; updatedAt: string; supersedesMemoryId: string | null;
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await authFetch(buildGatewayUrl(`admin/knowledge-memories${path}`), init);
  if (!response.ok) throw new Error((await response.json().catch(() => null) as { detail?: string } | null)?.detail ?? "Não foi possível gerenciar as memórias.");
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}

export type KnowledgeMemoryFilters = {
  search?: string;
  ownerUserId?: number | null;
  includeInactive?: boolean;
  take?: number;
};

export const listKnowledgeMemories = ({ search = "", ownerUserId = null, includeInactive = false, take = 50 }: KnowledgeMemoryFilters = {}) => {
  const query = new URLSearchParams({ search, includeInactive: String(includeInactive), take: String(take) });
  if (ownerUserId !== null) query.set("ownerUserId", String(ownerUserId));
  return request<KnowledgeMemory[]>(`?${query.toString()}`);
};
export const updateKnowledgeMemory = (memory: Pick<KnowledgeMemory, "id" | "subject" | "content" | "isActive">) =>
  request<void>(`/${memory.id}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(memory) });
export const deleteKnowledgeMemory = (id: string) => request<void>(`/${id}`, { method: "DELETE" });
