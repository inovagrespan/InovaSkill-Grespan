import { useCallback, useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Brain, Building2, Clock3, Edit3, LockKeyhole, RotateCcw, Save, Search, Trash2, UserRound, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { deleteKnowledgeMemory, listKnowledgeMemories, updateKnowledgeMemory, type KnowledgeMemory } from "@/lib/knowledge-memory-api";
import { formatKnowledgeMemoryUpdatedAt, getKnowledgeMemoryOwnerOptions, getKnowledgeMemoryScopeLabel } from "@/lib/knowledge-memory-ui";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/administracao/memorias")({ component: KnowledgeMemoriesPage });

function KnowledgeMemoriesPage() {
  const [items, setItems] = useState<KnowledgeMemory[]>([]);
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, TEXT_SEARCH_DEBOUNCE_MS);
  const [ownerUserId, setOwnerUserId] = useState("all");
  const [ownerOptions, setOwnerOptions] = useState<ReturnType<typeof getKnowledgeMemoryOwnerOptions>>([]);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    try {
      setError("");
      setItems(await listKnowledgeMemories({
        search: debouncedSearch,
        ownerUserId: ownerUserId === "all" ? null : Number(ownerUserId),
        includeInactive,
      }));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Falha ao carregar memórias.");
    }
  }, [debouncedSearch, ownerUserId, includeInactive]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    listKnowledgeMemories({ includeInactive: true, take: 100 })
      .then((memories) => setOwnerOptions(getKnowledgeMemoryOwnerOptions(memories)))
      .catch(() => undefined);
  }, []);

  function change(id: string, patch: Partial<KnowledgeMemory>) {
    setItems((current) => current.map((item) => item.id === id ? { ...item, ...patch } : item));
  }

  return (
    <div className="page-shell">
      <div className="mx-auto w-full max-w-7xl space-y-6">
        <header>
          <span className="page-header-kicker">Administração</span>
          <div className="mt-3 flex items-center gap-3">
            <span className="grid size-11 place-items-center rounded-xl border border-primary/25 bg-primary/10 text-primary"><Brain className="size-5" /></span>
            <div>
              <h1 className="font-display text-3xl tracking-tight">Memórias da IA</h1>
              <p className="mt-1 text-sm text-muted-foreground">Encontre, revise e organize as informações que contextualizam as respostas do chat.</p>
            </div>
          </div>
        </header>

        {error ? <div role="alert" className="rounded-xl border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">{error}</div> : null}

        <section aria-label="Filtros de memórias" className="grid gap-3 rounded-xl border border-border bg-surface p-4 md:grid-cols-[minmax(0,1fr)_minmax(13rem,0.35fr)_auto] md:items-center">
          <div className="relative min-w-0">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input className="h-11 bg-background pl-10" aria-label="Buscar memórias" placeholder="Buscar por assunto ou conteúdo" value={search} onChange={(event) => setSearch(event.target.value)} />
          </div>
          <Select value={ownerUserId} onValueChange={setOwnerUserId}>
            <SelectTrigger className="h-11 bg-background" aria-label="Filtrar por usuário">
              <span className="flex min-w-0 items-center gap-2"><UserRound className="size-4 shrink-0 text-muted-foreground" /><SelectValue placeholder="Todos os usuários" /></span>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Todos os usuários</SelectItem>
              {ownerOptions.map((owner) => <SelectItem key={owner.id} value={String(owner.id)}>{owner.name}</SelectItem>)}
            </SelectContent>
          </Select>
          <label className="flex h-11 shrink-0 cursor-pointer items-center gap-3 rounded-lg border border-border bg-background px-3 text-sm">
            <input type="checkbox" className="size-4 accent-primary" checked={includeInactive} onChange={(event) => setIncludeInactive(event.target.checked)} />
            Exibir inativas
          </label>
        </section>

        <div className="flex items-center justify-between gap-3">
          <p className="text-sm text-muted-foreground"><span className="font-medium text-foreground">{items.length}</span> {items.length === 1 ? "memória encontrada" : "memórias encontradas"}</p>
          {(search || ownerUserId !== "all" || includeInactive) ? (
            <Button variant="ghost" size="sm" onClick={() => { setSearch(""); setOwnerUserId("all"); setIncludeInactive(false); }}><X className="size-4" />Limpar filtros</Button>
          ) : null}
        </div>

        <section aria-label="Lista de memórias" className="grid gap-4 md:grid-cols-2">
          {items.length === 0 ? (
            <div className="grid min-h-48 place-items-center rounded-xl border border-dashed border-border bg-surface p-6 text-center md:col-span-2">
              <div><Brain className="mx-auto size-7 text-muted-foreground" /><p className="mt-3 text-sm font-medium">Nenhuma memória encontrada</p><p className="mt-1 text-xs text-muted-foreground">Tente ajustar os filtros ou exibir as memórias inativas.</p></div>
            </div>
          ) : items.map((memory) => {
            const ScopeIcon = memory.scope === "company" ? Building2 : LockKeyhole;
            const isEditing = editingId === memory.id;
            return (
              <article key={memory.id} className={cn("flex min-w-0 flex-col overflow-hidden rounded-xl border bg-surface transition-colors", memory.isActive ? "border-border hover:border-primary/25" : "border-border/60 opacity-70")}>
                <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border bg-muted/25 px-4 py-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="inline-flex items-center gap-1.5 rounded-full border border-border bg-background px-2.5 py-1 text-xs font-medium"><ScopeIcon className="size-3.5 text-primary" />{getKnowledgeMemoryScopeLabel(memory)}</span>
                    <span className={cn("rounded-full px-2.5 py-1 text-xs font-medium", memory.isActive ? "bg-emerald-500/10 text-emerald-400" : "bg-muted text-muted-foreground")}>{memory.isActive ? "Ativa" : "Inativa"}</span>
                  </div>
                  <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground"><Clock3 className="size-3.5" />Atualizada {formatKnowledgeMemoryUpdatedAt(memory.updatedAt)}</span>
                </div>

                {isEditing ? (
                  <div className="space-y-4 p-4">
                    <div className="space-y-2"><Label htmlFor={`memory-subject-${memory.id}`} className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Assunto</Label><Input id={`memory-subject-${memory.id}`} aria-label={`Assunto ${memory.id}`} className="h-10 bg-background font-medium" value={memory.subject} onChange={(event) => change(memory.id, { subject: event.target.value })} /></div>
                    <div className="space-y-2"><Label htmlFor={`memory-content-${memory.id}`} className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Conteúdo lembrado</Label><Textarea id={`memory-content-${memory.id}`} aria-label={`Conteúdo ${memory.id}`} className="min-h-24 resize-y bg-background leading-relaxed" value={memory.content} onChange={(event) => change(memory.id, { content: event.target.value })} /></div>
                  </div>
                ) : (
                  <div className="min-h-36 flex-1 p-4"><h2 className="line-clamp-2 text-base font-semibold leading-snug">{memory.subject}</h2><p className="mt-2 line-clamp-4 whitespace-pre-wrap text-sm leading-relaxed text-muted-foreground">{memory.content}</p></div>
                )}

                <footer className="flex flex-col gap-3 border-t border-border bg-muted/15 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                  <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground"><UserRound className="size-3.5" />{memory.createdByUserName}</span>
                  <div className="flex items-center justify-end gap-2">
                    {memory.isActive ? (
                      <>
                        {isEditing ? <><Button size="sm" variant="ghost" onClick={() => { setEditingId(null); void load(); }}>Cancelar</Button><Button size="sm" onClick={async () => { await updateKnowledgeMemory(memory); setEditingId(null); await load(); }}><Save className="size-4" />Salvar</Button></> : <Button size="sm" variant="outline" onClick={() => setEditingId(memory.id)}><Edit3 className="size-4" />Editar</Button>}
                        <Button size="sm" variant="outline" onClick={async () => { await deleteKnowledgeMemory(memory.id); await load(); }}><Trash2 className="size-4" />Desativar</Button>
                      </>
                    ) : <Button size="sm" variant="outline" onClick={async () => { await updateKnowledgeMemory({ ...memory, isActive: true }); await load(); }}><RotateCcw className="size-4" />Reativar memória</Button>}
                  </div>
                </footer>
              </article>
            );
          })}
        </section>
      </div>
    </div>
  );
}
