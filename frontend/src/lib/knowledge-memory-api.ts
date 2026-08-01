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

export const listKnowledgeMemories = (search = "", includeInactive = false) =>
  request<KnowledgeMemory[]>(`?search=${encodeURIComponent(search)}&includeInactive=${includeInactive}`);
export const updateKnowledgeMemory = (memory: Pick<KnowledgeMemory, "id" | "subject" | "content" | "isActive">) =>
  request<void>(`/${memory.id}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(memory) });
export const deleteKnowledgeMemory = (id: string) => request<void>(`/${id}`, { method: "DELETE" });
