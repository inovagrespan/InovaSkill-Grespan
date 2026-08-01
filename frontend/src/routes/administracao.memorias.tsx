import { useCallback, useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Brain, Building2, Clock3, LockKeyhole, RotateCcw, Save, Search, Trash2, UserRound } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  deleteKnowledgeMemory,
  listKnowledgeMemories,
  updateKnowledgeMemory,
  type KnowledgeMemory,
} from "@/lib/knowledge-memory-api";
import { formatKnowledgeMemoryUpdatedAt, getKnowledgeMemoryScopeLabel } from "@/lib/knowledge-memory-ui";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/administracao/memorias")({ component: KnowledgeMemoriesPage });

function KnowledgeMemoriesPage() {
  const [items, setItems] = useState<KnowledgeMemory[]>([]);
  const [search, setSearch] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    try {
      setError("");
      setItems(await listKnowledgeMemories(search, includeInactive));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Falha ao carregar memórias.");
    }
  }, [search, includeInactive]);

  useEffect(() => {
    const timeout = window.setTimeout(() => void load(), 300);
    return () => window.clearTimeout(timeout);
  }, [load]);

  function change(id: string, patch: Partial<KnowledgeMemory>) {
    setItems((current) => current.map((item) => item.id === id ? { ...item, ...patch } : item));
  }

  return (
    <div className="page-shell">
      <div className="mx-auto w-full max-w-5xl space-y-6">
        <header>
          <span className="page-header-kicker">Administração</span>
          <div className="mt-3 flex items-center gap-3">
            <span className="grid size-11 place-items-center rounded-xl border border-primary/25 bg-primary/10 text-primary">
              <Brain className="size-5" />
            </span>
            <div>
              <h1 className="font-display text-3xl tracking-tight">Memórias da IA</h1>
              <p className="mt-1 text-sm text-muted-foreground">
                Revise as informações que contextualizam as respostas do chat.
              </p>
            </div>
          </div>
        </header>

        {error ? (
          <div role="alert" className="rounded-xl border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">
            {error}
          </div>
        ) : null}

        <section aria-label="Filtros de memórias" className="flex flex-col gap-4 rounded-xl border border-border bg-surface p-4 sm:flex-row sm:items-center">
          <div className="relative min-w-0 flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="h-11 bg-background pl-10"
              aria-label="Buscar memórias"
              placeholder="Buscar por assunto ou conteúdo"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
          <label className="flex shrink-0 cursor-pointer items-center gap-3 rounded-lg border border-border bg-background px-3 py-2.5 text-sm">
            <input
              type="checkbox"
              className="size-4 accent-primary"
              checked={includeInactive}
              onChange={(event) => setIncludeInactive(event.target.checked)}
            />
            Exibir memórias inativas
          </label>
        </section>

        <section aria-label="Lista de memórias" className="space-y-4">
          {items.length === 0 ? (
            <div className="grid min-h-48 place-items-center rounded-xl border border-dashed border-border bg-surface p-6 text-center">
              <div>
                <Brain className="mx-auto size-7 text-muted-foreground" />
                <p className="mt-3 text-sm font-medium">Nenhuma memória encontrada</p>
                <p className="mt-1 text-xs text-muted-foreground">Tente ajustar a busca ou exibir as memórias inativas.</p>
              </div>
            </div>
          ) : items.map((memory) => {
            const isCompanyMemory = memory.scope === "company";
            const ScopeIcon = isCompanyMemory ? Building2 : LockKeyhole;

            return (
              <article
                key={memory.id}
                className={cn(
                  "overflow-hidden rounded-xl border bg-surface transition-colors",
                  memory.isActive ? "border-border hover:border-primary/25" : "border-border/60 opacity-70",
                )}
              >
                <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-muted/25 px-5 py-3">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="inline-flex items-center gap-1.5 rounded-full border border-border bg-background px-2.5 py-1 text-xs font-medium">
                      <ScopeIcon className="size-3.5 text-primary" />
                      {getKnowledgeMemoryScopeLabel(memory)}
                    </span>
                    <span className={cn(
                      "rounded-full px-2.5 py-1 text-xs font-medium",
                      memory.isActive ? "bg-emerald-500/10 text-emerald-400" : "bg-muted text-muted-foreground",
                    )}>
                      {memory.isActive ? "Ativa" : "Inativa"}
                    </span>
                  </div>
                  <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                    <Clock3 className="size-3.5" />
                    Atualizada {formatKnowledgeMemoryUpdatedAt(memory.updatedAt)}
                  </span>
                </div>

                <div className="space-y-5 p-5">
                  <div className="space-y-2">
                    <Label htmlFor={`memory-subject-${memory.id}`} className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                      Assunto
                    </Label>
                    <Input
                      id={`memory-subject-${memory.id}`}
                      aria-label={`Assunto ${memory.id}`}
                      className="h-11 bg-background text-base font-medium"
                      value={memory.subject}
                      onChange={(event) => change(memory.id, { subject: event.target.value })}
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor={`memory-content-${memory.id}`} className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                      Conteúdo lembrado
                    </Label>
                    <Textarea
                      id={`memory-content-${memory.id}`}
                      aria-label={`Conteúdo ${memory.id}`}
                      className="min-h-28 resize-y bg-background leading-relaxed"
                      value={memory.content}
                      onChange={(event) => change(memory.id, { content: event.target.value })}
                    />
                  </div>
                </div>

                <footer className="flex flex-col gap-3 border-t border-border bg-muted/15 px-5 py-3 sm:flex-row sm:items-center sm:justify-between">
                  <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                    <UserRound className="size-3.5" />
                    Registrada por {memory.createdByUserName}
                  </span>
                  <div className="flex items-center gap-2">
                    {memory.isActive ? (
                      <>
                        <Button size="sm" onClick={async () => { await updateKnowledgeMemory(memory); await load(); }}>
                          <Save className="size-4" />Salvar alterações
                        </Button>
                        <Button size="sm" variant="outline" onClick={async () => { await deleteKnowledgeMemory(memory.id); await load(); }}>
                          <Trash2 className="size-4" />Desativar
                        </Button>
                      </>
                    ) : (
                      <Button size="sm" variant="outline" onClick={async () => { await updateKnowledgeMemory({ ...memory, isActive: true }); await load(); }}>
                        <RotateCcw className="size-4" />Reativar memória
                      </Button>
                    )}
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
