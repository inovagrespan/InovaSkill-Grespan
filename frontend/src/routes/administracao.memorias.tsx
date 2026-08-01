import { useCallback, useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Brain, Save, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { deleteKnowledgeMemory, listKnowledgeMemories, updateKnowledgeMemory, type KnowledgeMemory } from "@/lib/knowledge-memory-api";

export const Route = createFileRoute("/administracao/memorias")({ component: KnowledgeMemoriesPage });

function KnowledgeMemoriesPage() {
  const [items, setItems] = useState<KnowledgeMemory[]>([]); const [search, setSearch] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false); const [error, setError] = useState("");
  const load = useCallback(async () => { try { setError(""); setItems(await listKnowledgeMemories(search, includeInactive)); } catch (reason) { setError(reason instanceof Error ? reason.message : "Falha ao carregar memórias."); } }, [search, includeInactive]);
  useEffect(() => { const timeout = window.setTimeout(() => void load(), 300); return () => window.clearTimeout(timeout); }, [load]);
  const change = (id: string, patch: Partial<KnowledgeMemory>) => setItems(current => current.map(item => item.id === id ? { ...item, ...patch } : item));
  return <div className="page-shell space-y-6">
    <header><span className="page-header-kicker">Administração</span><h1 className="mt-2 flex items-center gap-3 text-4xl font-display tracking-tight"><Brain className="size-8 text-primary"/>Memórias da IA</h1><p className="mt-2 text-sm text-muted-foreground">Revise o conhecimento empresarial e as memórias privadas usadas pelo chat.</p></header>
    {error ? <div role="alert" className="rounded-lg border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">{error}</div> : null}
    <section className="flex flex-wrap gap-3 rounded-xl border bg-surface p-4"><Input className="max-w-xl" aria-label="Buscar memórias" placeholder="Buscar por assunto ou conteúdo" value={search} onChange={event => setSearch(event.target.value)}/><label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={includeInactive} onChange={event => setIncludeInactive(event.target.checked)}/>Exibir histórico inativo</label></section>
    <section className="space-y-3">{items.length === 0 ? <p className="rounded-xl border bg-surface p-6 text-sm text-muted-foreground">Nenhuma memória encontrada.</p> : items.map(memory => <article key={memory.id} className={`rounded-xl border bg-surface p-5 ${memory.isActive ? "" : "opacity-60"}`}>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2"><span className="rounded-full border px-2 py-1 text-xs">{memory.scope === "company" ? "Empresa" : `Privada · ${memory.ownerUserName ?? "Usuário"}`}</span><span className="text-xs text-muted-foreground">Registrada por {memory.createdByUserName} · {new Date(memory.updatedAt).toLocaleString("pt-BR")}</span></div>
      <Input aria-label={`Assunto ${memory.id}`} value={memory.subject} onChange={event => change(memory.id, { subject: event.target.value })}/>
      <textarea aria-label={`Conteúdo ${memory.id}`} className="mt-3 min-h-24 w-full rounded-md border bg-background p-3 text-sm" value={memory.content} onChange={event => change(memory.id, { content: event.target.value })}/>
      <div className="mt-3 flex gap-2"><Button size="sm" onClick={async () => { await updateKnowledgeMemory(memory); await load(); }}><Save className="size-4"/>Salvar</Button>{memory.isActive ? <Button size="sm" variant="destructive" onClick={async () => { await deleteKnowledgeMemory(memory.id); await load(); }}><Trash2 className="size-4"/>Desativar</Button> : <Button size="sm" variant="outline" onClick={async () => { await updateKnowledgeMemory({ ...memory, isActive: true }); await load(); }}>Reativar</Button>}</div>
    </article>)}</section>
  </div>;
}
