import type { KnowledgeMemory } from "./knowledge-memory-api";

export type KnowledgeMemoryOwnerOption = { id: number; name: string };

export function getKnowledgeMemoryOwnerOptions(memories: KnowledgeMemory[]): KnowledgeMemoryOwnerOption[] {
  const owners = new Map<number, string>();
  for (const memory of memories) {
    if (memory.ownerUserId !== null) owners.set(memory.ownerUserId, memory.ownerUserName?.trim() || "Usuário sem nome");
  }
  return [...owners.entries()]
    .map(([id, name]) => ({ id, name }))
    .sort((left, right) => left.name.localeCompare(right.name, "pt-BR"));
}

export function getKnowledgeMemoryScopeLabel(memory: Pick<KnowledgeMemory, "scope" | "ownerUserName">): string {
  if (memory.scope === "company") return "Empresa";
  return `Privada · ${memory.ownerUserName?.trim() || "Usuário"}`;
}

export function formatKnowledgeMemoryUpdatedAt(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "em data não informada";
  return date.toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "short" });
}
