import { createFileRoute, Link } from "@tanstack/react-router";
import { useState, useEffect, useCallback } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from "@/components/ui/dialog";
import { AlertTriangle, CalendarDays, Plus, Users, X } from "lucide-react";
import { fetchMeetings, createMeeting, fetchUsers, KNOWN_AREAS, formatMeetingStatus, formatStage, type MeetingListDto, type UserListItem } from "@/lib/meetings-api";
import { getCurrentUserRole } from "@/lib/auth";

export const Route = createFileRoute("/reunioes/")({
  component: ReunioesPage,
});

const statusVariant: Record<string, "default" | "secondary" | "destructive" | "outline"> = {
  rascunho: "outline",
  em_andamento: "secondary",
  aguardando_respostas: "outline",
  em_analise_ia: "secondary",
  aguardando_conclusao: "outline",
  concluida: "default",
  cancelada: "destructive",
};

function ReunioesPage() {
  const [meetings, setMeetings] = useState<MeetingListDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [createOpen, setCreateOpen] = useState(false);
  const currentRole = getCurrentUserRole();
  const isDirector = currentRole === "diretor";

  const loadMeetings = useCallback(async () => {
    try {
      const data = await fetchMeetings();
      setMeetings(data);
    } catch { /* ignore */ } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void loadMeetings(); }, [loadMeetings]);

  return (
    <div className="page-shell">
      <header className="animate-soft-enter mb-8 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <span className="page-header-kicker">Smart Core / Reuniões</span>
          <h1 className="mt-2 mb-2 text-4xl font-display tracking-tight">Reuniões</h1>
          <p className="max-w-2xl text-sm text-muted-foreground">
            Gerencie reuniões, acompanhe decisões e ações definidas.
          </p>
        </div>
        {isDirector && (
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="mr-2 size-4" /> Nova reunião
          </Button>
        )}
      </header>

      <section className="space-y-3">
        {loading ? (
          <p className="text-sm text-muted-foreground">Carregando...</p>
        ) : meetings.length === 0 ? (
          <div className="rounded-xl border border-dashed border-border bg-surface p-8 text-center">
            <p className="text-sm text-muted-foreground">Nenhuma reunião encontrada.</p>
            {isDirector && (
              <Button variant="outline" className="mt-4" onClick={() => setCreateOpen(true)}>
                <Plus className="mr-2 size-4" /> Criar primeira reunião
              </Button>
            )}
          </div>
        ) : (
          meetings.map((m) => (
            <Link
              key={m.id}
              to="/reunioes/$id"
              params={{ id: String(m.id) }}
              className="flex items-center justify-between rounded-xl border border-border bg-surface p-4 transition-all hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-sm"
            >
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-3">
                  <h3 className="font-semibold truncate">{m.title}</h3>
                  <Badge variant={statusVariant[m.status] ?? "outline"}>{formatMeetingStatus(m.status)}</Badge>
                  {m.overdueActionCount > 0 && (
                    <Badge variant="destructive" className="gap-1">
                      <AlertTriangle className="size-3" /> {m.overdueActionCount} atrasadas
                    </Badge>
                  )}
                </div>
                <p className="mt-1 text-sm text-muted-foreground truncate">{m.description}</p>
                <div className="mt-2 flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
                  <span className="flex items-center gap-1"><CalendarDays className="size-3" /> {formatDate(m.createdAt)}</span>
                  <span className="flex items-center gap-1"><Users className="size-3" /> {m.participantCount} participantes</span>
                  <span>Etapa: {formatStage(m.currentStage)}</span>
                  <span>por {m.createdByName}</span>
                </div>
              </div>
            </Link>
          ))
        )}
      </section>

      <CreateMeetingDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onCreated={() => { setCreateOpen(false); void loadMeetings(); }}
      />
    </div>
  );
}

function CreateMeetingDialog({ open, onOpenChange, onCreated }: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  onCreated: () => void;
}) {
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [reason, setReason] = useState("");
  const [context, setContext] = useState("");
  const [selectedAreas, setSelectedAreas] = useState<string[]>([]);
  const [selectedParticipants, setSelectedParticipants] = useState<number[]>([]);
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (open) {
      void fetchUsers().then(setUsers).catch(() => {});
    }
  }, [open]);

  function toggleArea(area: string) {
    setSelectedAreas((prev) => prev.includes(area) ? prev.filter((a) => a !== area) : [...prev, area]);
  }

  function toggleParticipant(id: number) {
    setSelectedParticipants((prev) => prev.includes(id) ? prev.filter((p) => p !== id) : [...prev, id]);
  }

  async function handleCreate() {
    if (!title.trim() || !description.trim()) {
      setError("Preencha título e descrição.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      await createMeeting({
        title: title.trim(),
        description: description.trim(),
        reason: reason.trim(),
        participantUserIds: selectedParticipants,
        context: context.trim(),
        involvedAreasCsv: selectedAreas.join(","),
        initialProblems: [],
      });
      onCreated();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl bg-surface border-border">
        <DialogHeader>
          <DialogTitle>Nova reunião</DialogTitle>
          <DialogDescription>Preencha os dados para criar uma nova reunião.</DialogDescription>
        </DialogHeader>
        <div className="space-y-4 max-h-[70vh] overflow-y-auto pr-1">
          <div>
            <label className="text-sm font-medium">Título</label>
            <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Ex: Revisão de produção" />
          </div>
          <div>
            <label className="text-sm font-medium">Descricao</label>
            <textarea className="w-full rounded-lg border border-border bg-background p-2 text-sm" rows={2} value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Descreva o propósito da reunião" />
          </div>
          <div>
            <label className="text-sm font-medium">Motivo</label>
            <textarea className="w-full rounded-lg border border-border bg-background p-2 text-sm" rows={2} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Por que esta reunião é necessária?" />
          </div>
          <div>
            <label className="text-sm font-medium">Contexto inicial</label>
            <textarea className="w-full rounded-lg border border-border bg-background p-2 text-sm" rows={2} value={context} onChange={(e) => setContext(e.target.value)} placeholder="Contexto adicional para a reunião" />
          </div>

          <div>
            <label className="text-sm font-medium">Áreas envolvidas</label>
            <div className="mt-1 flex flex-wrap gap-2">
              {KNOWN_AREAS.map((area) => (
                <button
                  key={area}
                  type="button"
                  onClick={() => toggleArea(area)}
                  className={`rounded-full border px-3 py-1 text-xs font-medium transition-colors ${
                    selectedAreas.includes(area)
                      ? "border-primary bg-primary/10 text-primary"
                      : "border-border bg-background text-muted-foreground hover:border-primary/40"
                  }`}
                >
                  {area}
                  {selectedAreas.includes(area) && <X className="ml-1 inline size-3" />}
                </button>
              ))}
            </div>
          </div>

          <div>
            <label className="text-sm font-medium">Participantes</label>
            <div className="mt-1 flex flex-wrap gap-2">
              {users.map((user) => (
                <button
                  key={user.id}
                  type="button"
                  onClick={() => toggleParticipant(user.id)}
                  className={`rounded-full border px-3 py-1 text-xs font-medium transition-colors ${
                    selectedParticipants.includes(user.id)
                      ? "border-primary bg-primary/10 text-primary"
                      : "border-border bg-background text-muted-foreground hover:border-primary/40"
                  }`}
                >
                  {user.name}
                  {selectedParticipants.includes(user.id) && <X className="ml-1 inline size-3" />}
                </button>
              ))}
            </div>
          </div>

          {error && <p className="text-sm text-red-500">{error}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>Cancelar</Button>
            <Button onClick={() => void handleCreate()} disabled={saving}>{saving ? "Criando..." : "Criar reunião"}</Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function formatDate(value?: string | null): string {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("pt-BR");
}
