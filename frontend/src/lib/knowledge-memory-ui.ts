import type { KnowledgeMemory } from "./knowledge-memory-api";

export function getKnowledgeMemoryScopeLabel(memory: Pick<KnowledgeMemory, "scope" | "ownerUserName">): string {
  if (memory.scope === "company") return "Empresa";
  return `Privada · ${memory.ownerUserName?.trim() || "Usuário"}`;
}

export function formatKnowledgeMemoryUpdatedAt(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "em data não informada";
  return date.toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "short" });
}
