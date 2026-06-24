import { createFileRoute } from "@tanstack/react-router";
import { useState, useEffect, useCallback } from "react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { ScrollArea, ScrollBar } from "@/components/ui/scroll-area";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Skeleton, SkeletonCard } from "@/components/ui/skeleton";
import { AlertTriangle, CheckCircle2, ChevronRight, ClipboardList, MessageSquare, PlayCircle, Plus, Users, Lightbulb, Target, ArrowRight, ListChecks, Sparkles, FileText } from "lucide-react";
import {
  fetchMeeting, updateMeetingStage, addMeetingComment, addMeetingProblem, addMeetingQuestion,
  answerMeetingQuestion, createMeetingDecision, createMeetingAction, updateActionStatus,
  concludeMeeting, fetchPreMeetingBriefing, fetchUnresolvedPendencies, addPendingToMeeting,
  startMeeting, generateSuggestedMeetingProblems, approveMeetingProblem, generateMeetingAiAnalysis,
  type MeetingDetailDto, type MeetingCommentDto, type MeetingProblemDto,
  type MeetingQuestionDto, type MeetingActionDto, type MeetingDecisionDto, type MeetingParticipantDto,
  type CriticalPendingSummaryDto, type PreMeetingBriefingDto, type MeetingAiAnalysisDto,
  formatMeetingStatus, formatStage,
} from "@/lib/meetings-api";
import { getCurrentUserRole, getCurrentUser } from "@/lib/auth";
import { Alert, AlertDescription } from "@/components/ui/alert";

export const Route = createFileRoute("/reunioes/$id")({
  component: MeetingDetailPage,
});

const STAGES = ["contexto", "discussao", "problemas", "perguntas_e_respostas", "analise_ia", "conclusao", "acoes", "acompanhamento"];

function MeetingDetailPage() {
  const { id } = Route.useParams();
  const meetingId = Number(id);
  const [meeting, setMeeting] = useState<MeetingDetailDto | null>(null);
  const [briefing, setBriefing] = useState<PreMeetingBriefingDto | null>(null);
  const [unfittedPendencies, setUnfittedPendencies] = useState<CriticalPendingSummaryDto[]>([]);
  const [commentText, setCommentText] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const currentRole = getCurrentUserRole();
  const isDirector = currentRole === "diretor";
  const currentUserId = Number(getCurrentUser()?.sub ?? 0);

  const loadMeeting = useCallback(async () => {
    try {
      const data = await fetchMeeting(meetingId);
      setMeeting(data);
      if (data.status === "em_andamento") {
        try {
          const b = await fetchPreMeetingBriefing(meetingId);
          setBriefing(b);
        } catch { /* ignore */ }
        try {
          const p = await fetchUnresolvedPendencies();
          setUnfittedPendencies(p.filter(pend => !data.relatedPendencies.some(rp => rp.id === pend.id)));
        } catch { /* ignore */ }
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, [meetingId]);

  useEffect(() => { void loadMeeting(); }, [loadMeeting]);

  async function changeStage(direction: 1 | -1) {
    if (!meeting) return;
    const currentIndex = STAGES.indexOf(meeting.currentStage);
    const nextStage = STAGES[currentIndex + direction];
    if (nextStage) {
      try {
        const updated = await updateMeetingStage(meetingId, nextStage, { force: true, justification: "Avanço confirmado pelo Diretor no fluxo guiado." });
        setMeeting(updated);
      } catch (e) {
        setError((e as Error).message);
      }
    }
  }

  async function handleStartMeeting() {
    try {
      const updated = await startMeeting(meetingId);
      setMeeting(updated);
    } catch (e) {
      setError((e as Error).message);
    }
  }

  async function handleAddComment() {
    if (!commentText.trim() || !meeting) return;
    try {
      const comment = await addMeetingComment(meetingId, commentText.trim(), meeting.currentStage);
      setMeeting((prev) => prev ? { ...prev, comments: [...prev.comments, comment] } : prev);
      setCommentText("");
    } catch (e) {
      setError((e as Error).message);
    }
  }

  async function handleAddPendingToDiscussion(pendingId: number) {
    try {
      await addPendingToMeeting(meetingId, pendingId);
      setUnfittedPendencies((prev) => prev.filter((p) => p.id !== pendingId));
      void loadMeeting();
    } catch { /* ignore */ }
  }

  async function handleConclude() {
    try {
      await concludeMeeting(meetingId);
      void loadMeeting();
    } catch (e) {
      setError((e as Error).message);
    }
  }

  if (loading) return (
    <div className="page-shell">
      <div className="mb-6 space-y-3">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-4 w-48" />
      </div>
      <SkeletonCard lines={6} />
    </div>
  );
  if (!meeting) return <div className="page-shell"><p className="text-sm text-red-500">{error || "Reunião não encontrada."}</p></div>;

  const currentStageIndex = STAGES.indexOf(meeting.currentStage);
  const canAdvance = isDirector && currentStageIndex < STAGES.length - 1 && meeting.status !== "concluida" && meeting.status !== "cancelada";
  const canBack = isDirector && currentStageIndex > 0 && meeting.status !== "concluida" && meeting.status !== "cancelada";

  return (
    <div className="page-shell">
        <header className="animate-soft-enter mb-6 space-y-3">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-3">
              <h1 className="text-3xl font-display tracking-tight truncate">{meeting.title}</h1>
              <Badge variant={meeting.status === "concluida" ? "default" : meeting.status === "cancelada" ? "destructive" : "secondary"} className="shrink-0">
                {formatMeetingStatus(meeting.status)}
              </Badge>
            </div>
            <p className="mt-1 text-sm text-muted-foreground">por {meeting.createdByName} &bull; {formatDate(meeting.createdAt)}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {isDirector && meeting.status === "rascunho" && (
              <Button size="sm" onClick={() => void handleStartMeeting()}>
                <PlayCircle className="mr-2 size-4" /> Iniciar
              </Button>
            )}
            {canBack && (
              <Button variant="outline" size="sm" onClick={() => void changeStage(-1)}>
                Voltar etapa
              </Button>
            )}
            {canAdvance && meeting.currentStage !== "acompanhamento" && (
              <Button size="sm" onClick={() => void changeStage(1)}>
                Avançar etapa <ChevronRight className="ml-2 size-4" />
              </Button>
            )}
            {isDirector && meeting.status !== "concluida" && meeting.status !== "cancelada" && (
              <Button variant="outline" size="sm" onClick={() => void handleConclude()}>
                <CheckCircle2 className="mr-2 size-4" /> Concluir
              </Button>
            )}
          </div>
        </div>
      </header>

      {briefing && briefing.totalPendencies > 0 && meeting.currentStage === "contexto" && (
        <Alert className="mb-6 border-amber-500/30 bg-amber-50/80 dark:bg-amber-950/20">
          <AlertTriangle className="size-4 text-amber-600" />
          <AlertDescription className="text-amber-800 dark:text-amber-300">{briefing.aiSummary}</AlertDescription>
        </Alert>
      )}

      {error && (
        <Alert variant="destructive" className="mb-4">
          <AlertTriangle className="size-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[1fr_300px]">
        <div className="space-y-4">
          {/* Stage indicator */}
          <div className="rounded-xl border border-border bg-surface">
            <ScrollArea className="w-full">
              <div className="flex items-center gap-1 p-3">
                {STAGES.map((stage, index) => (
                  <div key={stage} className={`flex items-center gap-1 text-xs whitespace-nowrap ${index <= currentStageIndex ? "text-primary font-semibold" : "text-muted-foreground"}`}>
                    <span className={`inline-flex size-5 items-center justify-center rounded-full text-[10px] font-bold ${index <= currentStageIndex ? "bg-primary/10 text-primary" : "bg-muted text-muted-foreground"}`}>
                      {index + 1}
                    </span>
                    <span>{formatStage(stage)}</span>
                    {index < STAGES.length - 1 && <ChevronRight className="size-3 shrink-0" />}
                  </div>
                ))}
              </div>
              <ScrollBar orientation="horizontal" />
            </ScrollArea>
          </div>

          {/* Stage content */}
          {meeting.currentStage === "contexto" && <StageContext meeting={meeting} />}
          {meeting.currentStage === "discussao" && (
            <StageDiscussion
              comments={meeting.comments.filter(c => c.stage === "discussao")}
              commentText={commentText}
              onCommentTextChange={setCommentText}
              onAddComment={handleAddComment}
              onAddProblem={async (sector, description, severity, origin) => {
                try {
                  const problem = await addMeetingProblem(meetingId, { sector, description, severity, origin });
                  setMeeting(prev => prev ? { ...prev, problems: [...prev.problems, problem] } : prev);
                } catch (e) { setError((e as Error).message); }
              }}
            />
          )}
          {meeting.currentStage === "problemas" && (
            <StageProblems
              problems={meeting.problems}
              participants={meeting.participants}
              meetingId={meetingId}
              isDirector={isDirector}
              onProblemChanged={() => void loadMeeting()}
              onQuestionAdded={() => void loadMeeting()}
            />
          )}
          {meeting.currentStage === "perguntas_e_respostas" && (
            <StageSolutions
              problems={meeting.problems}
              meetingId={meetingId}
              currentUserId={currentUserId}
              onAnswered={() => void loadMeeting()}
            />
          )}
          {meeting.currentStage === "analise_ia" && <StageAiAnalysis meetingId={meetingId} analyses={meeting.aiAnalyses} isDirector={isDirector} onGenerated={() => void loadMeeting()} />}
          {meeting.currentStage === "conclusao" && (
            <StageConclusion
              problems={meeting.problems}
              participants={meeting.participants}
              meetingId={meetingId}
              onDecisionCreated={() => void loadMeeting()}
            />
          )}
          {meeting.currentStage === "acoes" && (
            <StageActions
              actions={meeting.actions}
              participants={meeting.participants}
              meetingId={meetingId}
              isDirector={isDirector}
              currentUserId={currentUserId}
              onActionCreated={() => void loadMeeting()}
              onActionUpdated={() => void loadMeeting()}
            />
          )}
          {meeting.currentStage === "acompanhamento" && (
            <StageFollowUp
              actions={meeting.actions}
              decisions={meeting.decisions}
            />
          )}
        </div>

        {/* Right panel */}
        <aside className="space-y-4">
          <div className="rounded-xl border border-border bg-surface p-4">
            <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold"><Users className="size-4 text-primary" /> Participantes</h3>
            <div className="space-y-2">
              {meeting.participants.map((p) => (
                <div key={p.id} className="flex items-center gap-2 text-sm">
                  <Avatar className="size-7">
                    <AvatarFallback className="text-[10px]">{p.userName.charAt(0).toUpperCase()}</AvatarFallback>
                  </Avatar>
                  <div className="flex-1 min-w-0">
                    <p className="truncate font-medium">{p.userName}</p>
                    <p className="text-[10px] text-muted-foreground">{p.roleInMeeting}</p>
                  </div>
                  <Badge variant="outline" className="text-[10px] capitalize">{p.participationStatus}</Badge>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-xl border border-border bg-surface p-4">
            <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold"><ClipboardList className="size-4 text-primary" /> Histórico</h3>
            <div className="space-y-0">
              {meeting.history?.slice(-6).reverse().map((h, i) => (
                <div key={h.id} className={`relative flex gap-3 pb-3 pl-4 ${i < Math.min(meeting.history.length, 6) - 1 ? "border-l-2 border-border" : ""}`}>
                  <div className="absolute left-[-4.5px] top-1.5 size-2 rounded-full bg-primary/30" />
                  <div className="min-w-0">
                    <p className="text-xs font-medium">{h.description}</p>
                    <p className="mt-0.5 text-[10px] text-muted-foreground">{h.userName} &bull; {formatDate(h.createdAt)}</p>
                  </div>
                </div>
              ))}
              {(!meeting.history || meeting.history.length === 0) && <p className="text-xs text-muted-foreground">Sem eventos registrados.</p>}
            </div>
          </div>

          {unfittedPendencies.length > 0 && (meeting.currentStage === "discussao" || meeting.currentStage === "problemas") && (
            <div className="rounded-xl border border-amber-500/30 bg-amber-50/80 dark:bg-amber-950/20 p-4">
              <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold text-amber-800 dark:text-amber-300">
                <AlertTriangle className="size-4" /> Pendências não encaixadas
              </h3>
              <div className="space-y-2">
                {unfittedPendencies.slice(0, 5).map((p) => (
                  <div key={p.id} className="rounded-lg border border-amber-500/20 bg-background p-2 text-xs">
                    <p className="font-medium">{p.title}</p>
                    <p className="mt-1 text-muted-foreground">{p.sector} &bull; {p.status}</p>
                    {isDirector && (
                      <button
                        className="mt-2 text-xs font-medium text-primary hover:underline"
                        onClick={() => void handleAddPendingToDiscussion(p.id)}
                      >
                        Adicionar a pauta
                      </button>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          <div className="rounded-xl border border-border bg-surface p-4">
            <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold"><ListChecks className="size-4 text-primary" /> Ações</h3>
            <div className="space-y-2">
              {meeting.actions.length === 0 && <p className="text-xs text-muted-foreground">Nenhuma ação registrada.</p>}
              {meeting.actions.slice(0, 5).map((a) => (
                <div key={a.id} className="flex items-center justify-between gap-2 text-xs">
                  <span className="truncate">{a.title}</span>
                  <Badge variant={a.status === "atrasada" ? "destructive" : a.status === "concluida" ? "default" : "outline"} className="shrink-0 text-[10px]">{a.status}</Badge>
                </div>
              ))}
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
}

// Stage sub-components

function StageContext({ meeting }: { meeting: MeetingDetailDto }) {
  return (
    <div className="rounded-xl border border-border bg-surface p-5 space-y-4">
      <div className="flex items-center gap-2">
        <FileText className="size-5 text-primary" />
        <h2 className="text-xl font-display">Contexto</h2>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <div className="rounded-lg border border-border/60 bg-background p-3">
          <p className="text-xs uppercase tracking-wide text-muted-foreground">Motivo</p>
          <p className="mt-1 text-sm">{meeting.reason || "Não informado."}</p>
        </div>
        {meeting.context && (
          <div className="rounded-lg border border-border/60 bg-background p-3">
            <p className="text-xs uppercase tracking-wide text-muted-foreground">Contexto inicial</p>
            <p className="mt-1 text-sm">{meeting.context}</p>
          </div>
        )}
      </div>
      {meeting.aiSummary && (
        <div className="rounded-lg border border-primary/20 bg-primary/5 p-3">
          <div className="flex items-center gap-2 mb-1">
            <Sparkles className="size-4 text-primary" />
            <p className="text-xs uppercase tracking-wide text-primary font-medium">Observações da IA</p>
          </div>
          <p className="text-sm">{meeting.aiSummary}</p>
        </div>
      )}
      <div>
        <p className="text-xs uppercase tracking-wide text-muted-foreground mb-2">Áreas envolvidas</p>
        <div className="flex flex-wrap gap-2">
          {meeting.involvedAreasCsv ? meeting.involvedAreasCsv.split(",").map((area) => (
            <Badge key={area} variant="outline">{area.trim()}</Badge>
          )) : <span className="text-sm text-muted-foreground">Nenhuma</span>}
        </div>
      </div>
      {meeting.relatedPendencies.length > 0 && (
        <div>
          <p className="text-xs uppercase tracking-wide text-amber-600 mb-2">Pendências relacionadas</p>
          <div className="space-y-2">
            {meeting.relatedPendencies.map((p) => (
              <div key={p.id} className="rounded-lg border border-amber-500/20 bg-amber-50/50 dark:bg-amber-950/10 p-2.5 text-sm">
                <p className="font-medium">{p.title}</p>
                <p className="text-xs text-muted-foreground">{p.sector} &bull; {p.status}</p>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function StageDiscussion({ comments, commentText, onCommentTextChange, onAddComment, onAddProblem }: {
  comments: MeetingCommentDto[];
  commentText: string;
  onCommentTextChange: (v: string) => void;
  onAddComment: () => void;
  onAddProblem: (sector: string, description: string, severity: string, origin: string) => Promise<void>;
}) {
  const [showNewProblem, setShowNewProblem] = useState(false);
  const [problemSector, setProblemSector] = useState("");
  const [problemDesc, setProblemDesc] = useState("");

  return (
    <div className="rounded-xl border border-border bg-surface p-5 space-y-4">
      <div className="flex items-center gap-2">
        <MessageSquare className="size-5 text-primary" />
        <h2 className="text-xl font-display">Discussão</h2>
      </div>
      <p className="text-sm text-muted-foreground">Compartilhe observações e levante pontos importantes.</p>

      <ScrollArea className="max-h-[420px]">
        <div className="space-y-3 pr-3">
          {comments.length === 0 && <p className="text-sm text-muted-foreground">Nenhum comentário ainda.</p>}
          {comments.map((c) => (
            <div key={c.id} className="rounded-lg border border-border bg-background p-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Avatar className="size-6">
                    <AvatarFallback className="text-[9px]">{c.userName.charAt(0).toUpperCase()}</AvatarFallback>
                  </Avatar>
                  <span className="text-sm font-medium">{c.userName}</span>
                </div>
                <span className="text-xs text-muted-foreground">{formatDate(c.createdAt)}</span>
              </div>
              <p className="mt-2 text-sm leading-relaxed">{c.message}</p>
            </div>
          ))}
        </div>
      </ScrollArea>

      <div className="flex gap-2">
        <textarea
          className="flex-1 rounded-lg border border-border bg-background p-2.5 text-sm resize-none"
          rows={2}
          value={commentText}
          onChange={(e) => onCommentTextChange(e.target.value)}
          placeholder="Digite seu comentário..."
        />
        <div className="flex flex-col justify-end gap-1">
          <Button size="sm" onClick={() => void onAddComment()} disabled={!commentText.trim()}>
            <MessageSquare className="mr-2 size-4" /> Comentar
          </Button>
        </div>
      </div>

      <Button variant="outline" size="sm" onClick={() => setShowNewProblem(!showNewProblem)}>
        <AlertTriangle className="mr-2 size-4" /> {showNewProblem ? "Cancelar" : "Adicionar problema"}
      </Button>

      {showNewProblem && (
        <div className="space-y-3 rounded-lg border border-border bg-muted/30 p-4">
          <Input placeholder="Setor" value={problemSector} onChange={(e) => setProblemSector(e.target.value)} />
          <textarea className="w-full rounded-lg border border-border bg-background p-2.5 text-sm resize-none" rows={2} placeholder="Descreva o problema" value={problemDesc} onChange={(e) => setProblemDesc(e.target.value)} />
          <div className="flex justify-end">
            <Button size="sm" onClick={() => { void onAddProblem(problemSector, problemDesc, "media", "discussao_atual"); setProblemSector(""); setProblemDesc(""); setShowNewProblem(false); }}>
              Adicionar
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

function StageProblems({ problems, participants, meetingId, isDirector, onProblemChanged, onQuestionAdded }: {
  problems: MeetingProblemDto[];
  participants: MeetingParticipantDto[];
  meetingId: number;
  isDirector: boolean;
  onProblemChanged: () => void;
  onQuestionAdded: () => void;
}) {
  const [newQuestion, setNewQuestion] = useState<{ problemId: number; question: string; responsible: string } | null>(null);

  const grouped = problems.reduce<Record<string, MeetingProblemDto[]>>((acc, p) => {
    (acc[p.sector] ??= []).push(p);
    return acc;
  }, {});

  return (
    <div className="rounded-xl border border-border bg-surface p-5 space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <Target className="size-5 text-primary" />
            <h2 className="text-xl font-display">Problemas por setor</h2>
          </div>
          <p className="text-sm text-muted-foreground mt-1">Apenas problemas aprovados pelo Diretor seguem para perguntas e respostas.</p>
        </div>
        {isDirector && problems.length > 0 && (
          <Button variant="outline" size="sm" onClick={async () => { await generateSuggestedMeetingProblems(meetingId); onProblemChanged(); }}>
            <Sparkles className="mr-2 size-4" /> Sugerir problemas
          </Button>
        )}
      </div>
      {Object.entries(grouped).length === 0 && (
        <div className="flex flex-col items-center gap-2 py-6 text-sm text-muted-foreground">
          <Target className="size-8 opacity-30" />
          <p>Nenhum problema registrado.</p>
        </div>
      )}
      {Object.entries(grouped).map(([sector, sectorProblems]) => (
        <div key={sector}>
          <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground mb-2">{sector}</h3>
          <div className="space-y-3">
            {sectorProblems.map((p) => (
              <div key={p.id} className="rounded-lg border border-border bg-background p-3">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium">{p.description}</p>
                    <p className="mt-0.5 text-xs text-muted-foreground">Origem: {p.origin}</p>
                  </div>
                  <div className="flex shrink-0 items-center gap-2">
                    <Badge variant={p.approvedByDirector ? "default" : "outline"}>{p.approvedByDirector ? "Aprovado" : "Pendente"}</Badge>
                    <Badge variant={p.severity === "critica" ? "destructive" : p.severity === "alta" ? "secondary" : "outline"}>{p.severity}</Badge>
                  </div>
                </div>
                {p.questions.length > 0 && (
                  <div className="mt-2 space-y-1">
                    {p.questions.map((q) => (
                      <div key={q.id} className="flex items-center justify-between text-xs bg-muted/30 rounded p-2">
                        <span className="truncate mr-2">{q.question}</span>
                        <Badge variant="outline" className="shrink-0 text-[10px]">{q.status}</Badge>
                      </div>
                    ))}
                  </div>
                )}
                {isDirector && (
                  <div className="mt-2 flex flex-wrap gap-2">
                    {!p.approvedByDirector && (
                      <Button variant="outline" size="sm" className="text-xs" onClick={async () => { await approveMeetingProblem(p.id); onProblemChanged(); }}>
                        Aprovar
                      </Button>
                    )}
                    <Button variant="ghost" size="sm" className="text-xs" onClick={() => setNewQuestion(newQuestion?.problemId === p.id ? null : { problemId: p.id, question: "", responsible: "" })}>
                      {newQuestion?.problemId === p.id ? "Cancelar" : "Perguntar"}
                    </Button>
                  </div>
                )}
                {newQuestion?.problemId === p.id && (
                  <div className="mt-2 space-y-2 rounded-lg border border-primary/20 bg-primary/5 p-3">
                    <Input placeholder="Pergunta" value={newQuestion.question} onChange={(e) => setNewQuestion({ ...newQuestion, question: e.target.value })} />
                    <div className="space-y-1">
                      <label className="text-xs text-muted-foreground">Responsável pela resposta</label>
                      <select
                        className="w-full rounded-lg border border-border bg-background p-2 text-sm"
                        value={newQuestion.responsible}
                        onChange={(e) => setNewQuestion({ ...newQuestion, responsible: e.target.value })}
                      >
                        <option value="">Selecione um participante</option>
                        {participants.map((part) => (
                          <option key={part.userId} value={part.userId}>{part.userName}</option>
                        ))}
                      </select>
                    </div>
                    <div className="flex justify-end">
                      <Button size="sm" onClick={async () => {
                        const respId = Number(newQuestion.responsible);
                        if (newQuestion.question && respId) {
                          await addMeetingQuestion(meetingId, { problemId: p.id, question: newQuestion.question, responsibleUserId: respId, sector: p.sector, isRequired: true });
                          setNewQuestion(null);
                          onQuestionAdded();
                        }
                      }} disabled={!newQuestion.question || !newQuestion.responsible}>Adicionar pergunta</Button>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

function StageSolutions({ problems, meetingId, currentUserId, onAnswered }: {
  problems: MeetingProblemDto[];
  meetingId: number;
  currentUserId: number;
  onAnswered: () => void;
}) {
  const [answers, setAnswers] = useState<Record<number, string>>({});

  const pendingQuestions = problems.flatMap(p => p.questions.filter(q => q.status === "pendente" && q.responsibleUserId === currentUserId));

  return (
    <div className="rounded-xl border border-border bg-surface p-5 space-y-4">
      <div className="flex items-center gap-2">
        <MessageSquare className="size-5 text-primary" />
        <h2 className="text-xl font-display">Perguntas e respostas</h2>
      </div>
      {pendingQuestions.length > 0 && (
        <div className="rounded-lg border border-amber-500/20 bg-amber-50/50 dark:bg-amber-950/10 p-3 text-sm">
          <p className="font-medium text-amber-800 dark:text-amber-300">Você tem {pendingQuestions.length} pergunta(s) pendente(s) de resposta.</p>
        </div>
      )}
      {problems.length === 0 && <p className="text-sm text-muted-foreground">Nenhum problema para responder.</p>}
      {problems.map((p) => (
        <div key={p.id} className="space-y-3">
          <h3 className="text-sm font-medium">{p.description}</h3>
          {p.questions.length === 0 && <p className="text-xs text-muted-foreground ml-4">Nenhuma pergunta para este problema.</p>}
          {p.questions.map((q) => (
            <div key={q.id} className="ml-4 rounded-lg border border-border bg-background p-3">
              <div className="flex items-center justify-between">
                <p className="text-sm font-medium">{q.question}</p>
                <Badge variant={q.status === "respondida" ? "default" : "outline"}>{q.status === "respondida" ? "Respondida" : "Pendente"}</Badge>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">Responsável: {q.responsibleName}</p>
              {q.answer ? (
                <div className="mt-2 rounded-lg bg-muted/30 p-3 text-sm">
                  <div className="flex items-center gap-2 mb-1">
                    <Avatar className="size-5">
                      <AvatarFallback className="text-[8px]">{q.answer.userName.charAt(0).toUpperCase()}</AvatarFallback>
                    </Avatar>
                    <p className="text-xs text-muted-foreground font-medium">{q.answer.userName}</p>
                  </div>
                  <p className="leading-relaxed">{q.answer.answer}</p>
                </div>
              ) : (
                q.responsibleUserId === currentUserId && (
                  <div className="mt-2 flex gap-2">
                    <textarea className="flex-1 rounded-lg border border-border bg-background p-2.5 text-sm resize-none" rows={2} placeholder="Sua resposta..." value={answers[q.id] ?? ""} onChange={(e) => setAnswers({ ...answers, [q.id]: e.target.value })} />
                    <Button size="sm" className="self-end" onClick={async () => {
                      const text = answers[q.id];
                      if (text?.trim()) {
                        await answerMeetingQuestion(q.id, text.trim());
                        setAnswers({ ...answers, [q.id]: "" });
                        onAnswered();
                      }
                    }}>Responder</Button>
                  </div>
                )
              )}
            </div>
          ))}
        </div>
      ))}
    </div>
  );
}

function StageAiAnalysis({ meetingId, analyses, isDirector, onGenerated }: { meetingId: number; analyses: MeetingAiAnalysisDto[]; isDirector: boolean; onGenerated: () => void }) {
  return (
    <div className="rounded-xl border border-border bg-surface p-5 space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <Sparkles className="size-5 text-primary" />
            <h2 className="text-xl font-display">Análise da IA</h2>
          </div>
          <p className="text-sm text-muted-foreground mt-1">A IA apoia a decisão, mas a escolha final continua com o Diretor.</p>
        </div>
        {isDirector && (
          <Button variant="outline" size="sm" onClick={async () => { await generateMeetingAiAnalysis(meetingId, { force: true, justification: "Análise solicitada pelo Diretor no MVP." }); onGenerated(); }}>
            <Sparkles className="mr-2 size-4" /> Gerar análise
          </Button>
        )}
      </div>
      {analyses.length === 0 && (
        <div className="flex flex-col items-center gap-2 py-6 text-sm text-muted-foreground">
          <Sparkles className="size-8 opacity-30" />
          <p>Nenhuma análise gerada ainda.</p>
        </div>
      )}
      {analyses.map((analysis) => (
        <div key={analysis.id} className="rounded-lg border border-border bg-background p-4 space-y-3">
          <div className="flex items-center gap-2">
            <Lightbulb className="size-4 text-primary" />
            <h3 className="text-sm font-semibold">{analysis.problemDescription}</h3>
          </div>
          <p className="text-sm text-muted-foreground ml-6">Solução considerada: {analysis.proposedSolution}</p>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <div className="rounded-lg border border-emerald-500/20 bg-emerald-50/50 dark:bg-emerald-950/10 p-3">
              <p className="text-xs font-medium text-emerald-700 dark:text-emerald-300">Pontos positivos</p>
              <p className="mt-1 text-sm">{analysis.positivePoints}</p>
            </div>
            <div className="rounded-lg border border-red-500/20 bg-red-50/50 dark:bg-red-950/10 p-3">
              <p className="text-xs font-medium text-red-700 dark:text-red-300">Riscos</p>
              <p className="mt-1 text-sm">{analysis.risks}</p>
            </div>
          </div>
          <div className="rounded-lg border border-primary/20 bg-primary/5 p-3">
            <p className="text-xs font-medium text-primary">Recomendação da IA</p>
            <p className="mt-1 text-sm">{analysis.recommendation}</p>
            <p className="mt-2 text-xs text-muted-foreground">Alternativa: {analysis.alternativeSolution}</p>
          </div>
        </div>
      ))}
    </div>
  );
}

function StageConclusion({ problems, participants, meetingId, onDecisionCreated }: {
  problems: MeetingProblemDto[];
  participants: MeetingParticipantDto[];
  meetingId: number;
  onDecisionCreated: () => void;
}) {
  const [selectedProblem, setSelectedProblem] = useState<number | null>(null);
  const [solution, setSolution] = useState("");
  const [origin, setOrigin] = useState("gestor_mais_ia");
  const [justification, setJustification] = useState("");
  const [responsibleId, setResponsibleId] = useState("");
  const [deadlineDays, setDeadlineDays] = useState(5);
  const [priority, setPriority] = useState("media");

  async function handleCreateDecision() {
    if (!selectedProblem || !solution.trim()) return;
    try {
      await createMeetingDecision(meetingId, {
        problemId: selectedProblem,
        chosenSolution: solution.trim(),
        solutionOrigin: origin,
        justification: justification.trim(),
        responsibleUserId: Number(responsibleId) || 0,
        deadlineDays,
        priority,
        trackingMetric: "",
        acceptedRisk: "",
        nextSteps: "",
        closedPendencies: "",
      });
      setSelectedProblem(null);
      setSolution("");
      setJustification("");
      onDecisionCreated();
    } catch { /* ignore */ }
  }

  return (
    <div className="rounded-xl border border-border bg-surface p-5 space-y-4">
      <div className="flex items-center gap-2">
        <CheckCircle2 className="size-5 text-primary" />
        <h2 className="text-xl font-display">Conclusão</h2>
      </div>
      <p className="text-sm text-muted-foreground">Registre as decisões finais para cada problema.</p>

      <div className="space-y-3">
        {problems.map((p) => (
          <div key={p.id} className="rounded-lg border border-border bg-background p-3">
            <div className="flex items-center justify-between">
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium truncate">{p.description}</p>
                <p className="text-xs text-muted-foreground">{p.sector}</p>
              </div>
              <Button variant="outline" size="sm" className="shrink-0" onClick={() => setSelectedProblem(p.id)}>
                <CheckCircle2 className="mr-2 size-3" /> Decidir
              </Button>
            </div>
          </div>
        ))}
      </div>

      {selectedProblem && (
        <div className="rounded-lg border border-primary/20 bg-primary/5 p-4 space-y-3">
          <h3 className="text-sm font-semibold">Registrar decisão</h3>
          <textarea className="w-full rounded-lg border border-border bg-background p-2.5 text-sm resize-none" rows={2} placeholder="Solução escolhida" value={solution} onChange={(e) => setSolution(e.target.value)} />
          <select className="w-full rounded-lg border border-border bg-background p-2.5 text-sm" value={origin} onChange={(e) => setOrigin(e.target.value)}>
            <option value="gestor">Gestor</option>
            <option value="ia">IA</option>
            <option value="gestor_mais_ia">Gestor + IA</option>
            <option value="diretor">Diretor</option>
            <option value="consenso_da_reuniao">Consenso</option>
          </select>
          <textarea className="w-full rounded-lg border border-border bg-background p-2.5 text-sm resize-none" rows={2} placeholder="Justificativa" value={justification} onChange={(e) => setJustification(e.target.value)} />
          <div>
            <label className="text-xs text-muted-foreground">Responsável pela execução</label>
            <select className="w-full rounded-lg border border-border bg-background p-2.5 text-sm mt-1" value={responsibleId} onChange={(e) => setResponsibleId(e.target.value)}>
              <option value="">Selecione um participante</option>
              {participants.map((part) => (
                <option key={part.userId} value={part.userId}>{part.userName}</option>
              ))}
            </select>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="text-xs text-muted-foreground">Prazo (dias)</label>
              <input className="w-full rounded-lg border border-border bg-background p-2.5 text-sm mt-1" type="number" min={1} value={deadlineDays} onChange={(e) => setDeadlineDays(Number(e.target.value))} />
            </div>
            <div>
              <label className="text-xs text-muted-foreground">Prioridade</label>
              <select className="w-full rounded-lg border border-border bg-background p-2.5 text-sm mt-1" value={priority} onChange={(e) => setPriority(e.target.value)}>
                <option value="baixa">Baixa</option>
                <option value="media">Média</option>
                <option value="alta">Alta</option>
                <option value="critica">Crítica</option>
              </select>
            </div>
          </div>
          <div className="flex justify-end">
            <Button onClick={() => void handleCreateDecision()} disabled={!solution.trim()}>
              <CheckCircle2 className="mr-2 size-4" /> Registrar decisão
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

function StageActions({ actions, participants, meetingId, isDirector, currentUserId, onActionCreated, onActionUpdated }: {
  actions: MeetingActionDto[];
  participants: MeetingParticipantDto[];
  meetingId: number;
  isDirector: boolean;
  currentUserId: number;
  onActionCreated: () => void;
  onActionUpdated: () => void;
}) {
  const [showCreate, setShowCreate] = useState(false);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [respId, setRespId] = useState("");
  const [sector, setSector] = useState("");
  const [deadlineDays, setDeadlineDays] = useState(5);
  const [priority, setPriority] = useState("media");

  async function handleCreate() {
    if (!title.trim()) return;
    try {
      await createMeetingAction(meetingId, {
        title: title.trim(), description: description.trim(),
        responsibleUserId: Number(respId) || 0, sector, deadlineDays, priority,
      });
      setTitle(""); setDescription(""); setRespId(""); setSector(""); setDeadlineDays(5);
      setShowCreate(false);
      onActionCreated();
    } catch { /* ignore */ }
  }

  return (
    <div className="rounded-xl border border-border bg-surface p-5 space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <ListChecks className="size-5 text-primary" />
          <h2 className="text-xl font-display">Ações</h2>
        </div>
        {isDirector && <Button size="sm" onClick={() => setShowCreate(!showCreate)}><Plus className="mr-2 size-4" /> Nova ação</Button>}
      </div>

      {showCreate && (
        <div className="rounded-lg border border-primary/20 bg-primary/5 p-4 space-y-3">
          <Input placeholder="Título da ação" value={title} onChange={(e) => setTitle(e.target.value)} />
          <textarea className="w-full rounded-lg border border-border bg-background p-2.5 text-sm resize-none" rows={2} placeholder="Descrição" value={description} onChange={(e) => setDescription(e.target.value)} />
          <div className="space-y-1">
            <label className="text-xs text-muted-foreground">Responsável</label>
            <select className="w-full rounded-lg border border-border bg-background p-2.5 text-sm" value={respId} onChange={(e) => setRespId(e.target.value)}>
              <option value="">Selecione um participante</option>
              {participants.map((part) => (
                <option key={part.userId} value={part.userId}>{part.userName}</option>
              ))}
            </select>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <Input placeholder="Setor" value={sector} onChange={(e) => setSector(e.target.value)} />
            <div>
              <label className="text-xs text-muted-foreground">Prazo (dias)</label>
              <input className="w-full rounded-lg border border-border bg-background p-2.5 text-sm mt-1" type="number" min={1} value={deadlineDays} onChange={(e) => setDeadlineDays(Number(e.target.value))} />
            </div>
            <div>
              <label className="text-xs text-muted-foreground">Prioridade</label>
              <select className="w-full rounded-lg border border-border bg-background p-2.5 text-sm mt-1" value={priority} onChange={(e) => setPriority(e.target.value)}>
                <option value="baixa">Baixa</option><option value="media">Média</option><option value="alta">Alta</option><option value="critica">Crítica</option>
              </select>
            </div>
          </div>
          <div className="flex justify-end">
            <Button size="sm" onClick={() => void handleCreate()} disabled={!title.trim() || !respId}>Criar ação</Button>
          </div>
        </div>
      )}

      <div className="space-y-2">
        {actions.length === 0 && (
          <div className="flex flex-col items-center gap-2 py-6 text-sm text-muted-foreground">
            <ListChecks className="size-8 opacity-30" />
            <p>Nenhuma ação registrada.</p>
          </div>
        )}
        {actions.map((a) => (
          <ActionCard key={a.id} action={a} currentUserId={currentUserId} isDirector={isDirector} onUpdated={onActionUpdated} />
        ))}
      </div>
    </div>
  );
}

function ActionCard({ action, currentUserId, isDirector, onUpdated }: {
  action: MeetingActionDto;
  currentUserId: number;
  isDirector: boolean;
  onUpdated: () => void;
}) {
  const [updating, setUpdating] = useState(false);
  const canUpdate = action.responsibleUserId === currentUserId || isDirector;

  async function markCompleted() {
    setUpdating(true);
    try {
      await updateActionStatus(action.id, { status: "concluida", completionEvidence: "", comments: "" });
      onUpdated();
    } catch { /* ignore */ } finally { setUpdating(false); }
  }

  const isOverdue = action.status === "atrasada";
  const isDone = action.status === "concluida";

  return (
    <div className={`rounded-lg border p-3 transition-colors ${isOverdue ? "border-red-300 bg-red-50/50 dark:bg-red-950/10" : isDone ? "border-emerald-200 bg-emerald-50/50 dark:bg-emerald-950/10" : "border-border bg-background"}`}>
      <div className="flex items-center justify-between gap-3">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <p className={`text-sm font-medium truncate ${isDone ? "line-through text-muted-foreground" : ""}`}>{action.title}</p>
            <Badge variant={isOverdue ? "destructive" : isDone ? "default" : "outline"} className="shrink-0">{action.status}</Badge>
          </div>
          <p className="text-xs text-muted-foreground mt-1">{action.responsibleName} &bull; {action.sector} &bull; {isDone ? "Concluída" : `Prazo: ${action.deadlineDays}d`}</p>
        </div>
        {!isDone && canUpdate && (
          <Button size="sm" variant="outline" disabled={updating} onClick={() => void markCompleted()} className="shrink-0">
            <CheckCircle2 className="size-4" />
          </Button>
        )}
      </div>
    </div>
  );
}

function StageFollowUp({ actions, decisions }: { actions: MeetingActionDto[]; decisions: MeetingDecisionDto[] }) {
  const overdue = actions.filter(a => a.status === "atrasada");
  const completed = actions.filter(a => a.status === "concluida");
  const pending = actions.filter(a => a.status !== "atrasada" && a.status !== "concluida");

  return (
    <div className="rounded-xl border border-border bg-surface p-5 space-y-4">
      <div className="flex items-center gap-2">
        <ArrowRight className="size-5 text-primary" />
        <h2 className="text-xl font-display">Acompanhamento</h2>
      </div>
      <p className="text-sm text-muted-foreground">Ações definidas nesta reunião para acompanhamento futuro.</p>

      <div className="space-y-4">
        {overdue.length > 0 && (
          <div>
            <h3 className="text-sm font-semibold text-red-600 flex items-center gap-2 mb-2"><AlertTriangle className="size-4" /> Atrasadas ({overdue.length})</h3>
            <div className="space-y-2">
              {overdue.map(a => (
                <div key={a.id} className="rounded-lg border border-red-200 bg-red-50/50 dark:bg-red-950/10 p-2.5 text-sm">
                  <p className="font-medium">{a.title}</p>
                  <p className="text-xs text-muted-foreground mt-0.5">{a.responsibleName}</p>
                </div>
              ))}
            </div>
          </div>
        )}
        {pending.length > 0 && (
          <div>
            <h3 className="text-sm font-semibold flex items-center gap-2 mb-2"><PlayCircle className="size-4 text-primary" /> Pendentes ({pending.length})</h3>
            <div className="space-y-2">
              {pending.map(a => (
                <div key={a.id} className="rounded-lg border border-border bg-background p-2.5 text-sm">
                  <p className="font-medium">{a.title}</p>
                  <p className="text-xs text-muted-foreground mt-0.5">{a.responsibleName} &bull; {a.deadlineDays}d</p>
                </div>
              ))}
            </div>
          </div>
        )}
        {completed.length > 0 && (
          <div>
            <h3 className="text-sm font-semibold flex items-center gap-2 mb-2 text-emerald-600"><CheckCircle2 className="size-4" /> Concluídas ({completed.length})</h3>
            <div className="space-y-2">
              {completed.map(a => (
                <div key={a.id} className="rounded-lg border border-emerald-200 bg-emerald-50/50 dark:bg-emerald-950/10 p-2.5 text-sm">
                  <p className="font-medium line-through text-muted-foreground">{a.title}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {actions.length === 0 && (
          <div className="flex flex-col items-center gap-2 py-6 text-sm text-muted-foreground">
            <ListChecks className="size-8 opacity-30" />
            <p>Nenhuma ação registrada nesta reunião.</p>
          </div>
        )}
      </div>

      {decisions.length > 0 && (
        <div className="pt-4 border-t border-border">
          <h3 className="text-sm font-semibold mb-3">Decisões registradas</h3>
          <div className="space-y-2">
            {decisions.map(d => (
              <div key={d.id} className="rounded-lg border border-border bg-background p-3 text-sm">
                <p className="font-medium">{d.problemDescription}</p>
                <p className="text-xs text-muted-foreground mt-1">Solução: {d.chosenSolution}</p>
                <p className="text-xs text-muted-foreground">Responsável: {d.responsibleName} &bull; Prazo: {d.deadlineDays}d &bull; Prioridade: {d.priority}</p>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function formatDate(value?: string | null): string {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("pt-BR");
}
