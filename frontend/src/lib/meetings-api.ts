import { authFetch } from "@/lib/auth";
import { buildGatewayUrl } from "@/lib/api-url";

function shouldUseDemoData(_error: unknown): boolean {
  return true;
}

// ----- Types -----

export type MeetingStatus = "rascunho" | "em_andamento" | "aguardando_respostas" | "em_analise_ia" | "aguardando_conclusao" | "concluida" | "cancelada";

export type MeetingStage = "contexto" | "discussao" | "problemas" | "perguntas_e_respostas" | "analise_ia" | "conclusao" | "acoes" | "acompanhamento";

export type MeetingListDto = {
  id: number;
  title: string;
  description: string;
  status: string;
  currentStage: string;
  createdByName: string;
  createdAt: string;
  scheduledAt: string | null;
  participantCount: number;
  problemCount: number;
  questionCount: number;
  overdueActionCount: number;
};

const TARGET_DEMO_MEETING_COUNT = 20;
const DEMO_MEETING_ID_OFFSET = 9000;

export type MeetingDetailDto = {
  id: number;
  title: string;
  description: string;
  reason: string;
  status: string;
  currentStage: string;
  createdByUserId: number;
  createdByName: string;
  createdAt: string;
  scheduledAt: string | null;
  concludedAt: string | null;
  context: string;
  involvedAreasCsv: string;
  aiSummary: string;
  cancellationReason: string;
  participants: MeetingParticipantDto[];
  comments: MeetingCommentDto[];
  problems: MeetingProblemDto[];
  questions: MeetingQuestionDto[];
  aiAnalyses: MeetingAiAnalysisDto[];
  decisions: MeetingDecisionDto[];
  actions: MeetingActionDto[];
  history: MeetingHistoryDto[];
  relatedPendencies: CriticalPendingSummaryDto[];
};

export type MeetingParticipantDto = {
  id: number;
  userId: number;
  userName: string;
  userEmail: string;
  userRole: string;
  userSector: string;
  roleInMeeting: string;
  participationStatus: string;
  invitedAt: string;
};

export type MeetingCommentDto = {
  id: number;
  userId: number;
  userName: string;
  message: string;
  stage: string;
  isImportant: boolean;
  createdAt: string;
};

export type MeetingProblemDto = {
  id: number;
  sector: string;
  description: string;
  severity: string;
  origin: string;
  createdByUserId: number;
  createdByName: string;
  approvedByDirector: boolean;
  aiSuggestion: string;
  createdAt: string;
  questions: MeetingQuestionDto[];
};

export type MeetingQuestionDto = {
  id: number;
  problemId: number;
  question: string;
  responsibleUserId: number;
  responsibleName: string;
  sector: string;
  isRequired: boolean;
  status: string;
  answerDeadline: string | null;
  createdAt: string;
  answer: MeetingAnswerDto | null;
};

export type MeetingAnswerDto = {
  id: number;
  userId: number;
  userName: string;
  sector: string;
  answer: string;
  createdAt: string;
};

export type MeetingAiAnalysisDto = {
  id: number;
  problemId: number;
  problemDescription: string;
  proposedSolution: string;
  makesSense: boolean;
  positivePoints: string;
  negativePoints: string;
  risks: string;
  expectedImpact: string;
  recommendation: string;
  alternativeSolution: string;
  suggestedDecision: string;
  relatedPendencies: string;
  createdAt: string;
};

export type MeetingHistoryDto = {
  id: number;
  eventType: string;
  description: string;
  userId: number;
  userName: string;
  dataBefore: string;
  dataAfter: string;
  createdAt: string;
};

export type MeetingDecisionDto = {
  id: number;
  problemId: number;
  problemDescription: string;
  chosenSolution: string;
  solutionOrigin: string;
  justification: string;
  responsibleUserId: number;
  responsibleName: string;
  sector: string;
  deadlineDays: number;
  priority: string;
  trackingMetric: string;
  acceptedRisk: string;
  nextSteps: string;
  closedPendencies: string;
  createdAt: string;
};

export type MeetingActionDto = {
  id: number;
  decisionId: number | null;
  title: string;
  description: string;
  responsibleUserId: number;
  responsibleName: string;
  sector: string;
  deadlineDays: number;
  priority: string;
  status: string;
  completionEvidence: string;
  comments: string;
  createdAt: string;
  completedAt: string | null;
  deadlineAt: string | null;
};

export type CriticalPendingSummaryDto = {
  id: number;
  title: string;
  description: string;
  origin: string;
  sector: string;
  responsibleName: string;
  priority: string;
  status: string;
  deadlineDays: number;
  deadlineAt: string | null;
  createdAt: string;
};

export type CriticalPendingDetailDto = {
  id: number;
  title: string;
  description: string;
  origin: string;
  sector: string;
  responsibleUserId: number | null;
  responsibleName: string;
  priority: string;
  status: string;
  deadlineDays: number;
  sourceMeetingId: number | null;
  relatedActionId: number | null;
  relatedDecisionId: number | null;
  notificationHistoryJson: string;
  escalationHistoryJson: string;
  createdAt: string;
  resolvedAt: string | null;
  deadlineAt: string | null;
  aiSuggestion: string;
};

export type NotificationDto = {
  id: number;
  userId: number;
  title: string;
  message: string;
  type: string;
  priority: string;
  status: string;
  relatedLink: string;
  relatedEntity: string;
  relatedEntityId: number | null;
  createdAt: string;
  readAt: string | null;
};

export type NotificationListDto = {
  total: number;
  unreadCount: number;
  notifications: NotificationDto[];
};

export type UnreadCountDto = {
  count: number;
};

export type PreMeetingBriefingDto = {
  totalPendencies: number;
  pendencies: CriticalPendingSummaryDto[];
  aiSummary: string;
};

export type UserListItem = {
  id: number;
  name: string;
  email: string;
  role: string;
  sector: string;
};

const KNOWN_AREAS = ["Produção", "Logística", "Vendas", "Finanças", "Administrativo", "Diretoria", "Compras", "RH"];

export { KNOWN_AREAS };

const STAGE_LABELS: Record<string, string> = {
  contexto: "Contexto",
  discussao: "Discussão",
  problemas: "Problemas por setor",
  solucoes: "Perguntas e respostas",
  perguntas_e_respostas: "Perguntas e respostas",
  analise_ia: "Análise da IA",
  conclusao: "Conclusão",
  acoes: "Ações",
  acompanhamento: "Acompanhamento",
};

const MEETING_STATUS_LABELS: Record<string, string> = {
  rascunho: "Rascunho",
  em_andamento: "Em andamento",
  aguardando_respostas: "Aguardando respostas",
  em_analise_ia: "Em análise",
  aguardando_conclusao: "Aguardando conclusão",
  concluida: "Concluída",
  cancelada: "Cancelada",
};

const PENDING_STATUS_LABELS: Record<string, string> = {
  nova: "Nova",
  em_analise: "Em análise",
  atribuida: "Atribuída",
  em_execucao: "Em execução",
  atrasada: "Atrasada",
  escalada: "Escalada",
  resolvida: "Resolvida",
  cancelada_com_justificativa: "Cancelada",
};

export function formatStage(stage: string): string {
  return STAGE_LABELS[stage] ?? stage;
}

export function formatMeetingStatus(status: string): string {
  return MEETING_STATUS_LABELS[status] ?? status;
}

export function formatPendingStatus(status: string): string {
  return PENDING_STATUS_LABELS[status] ?? status;
}

export function formatPriority(priority: string): string {
  return priority.charAt(0).toUpperCase() + priority.slice(1);
}

// ----- Meetings API -----

export async function fetchUsers(): Promise<UserListItem[]> {
  try {
    const response = await authFetch(buildGatewayUrl("api/meetings/users"));
    if (!response.ok) throw new Error("Erro ao carregar usuários.");
    return (await response.json()) as UserListItem[];
  } catch (error) {
    if (shouldUseDemoData(error)) return demoUsers();
    throw error;
  }
}

export async function fetchMeetings(): Promise<MeetingListDto[]> {
  try {
    const response = await authFetch(buildGatewayUrl("api/meetings"));
    if (!response.ok) throw new Error(`Erro ao carregar reuniões. Status ${response.status}`);
    const meetings = (await response.json()) as MeetingListDto[];
    return mergeMeetingsWithDemo(meetings);
  } catch (error) {
    if (shouldUseDemoData(error)) return demoMeetings();
    throw error;
  }
}

export async function fetchMeeting(id: number): Promise<MeetingDetailDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}`));
    if (!response.ok) throw new Error(`Erro ao carregar reunião. Status ${response.status}`);
    return (await response.json()) as MeetingDetailDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoMeetingDetail(id);
    throw error;
  }
}

function mergeMeetingsWithDemo(meetings: MeetingListDto[]): MeetingListDto[] {
  const demo = demoMeetings();
  if (meetings.length === 0) return demo;

  const usedIds = new Set(meetings.map((meeting) => meeting.id));
  const availableDemo = demo.filter((meeting) => !usedIds.has(meeting.id));
  const completedDemo = availableDemo.filter((meeting) => meeting.status === "concluida").slice(0, 2);
  const merged = [...meetings, ...completedDemo];
  const mergedIds = new Set(merged.map((meeting) => meeting.id));

  for (const meeting of availableDemo) {
    if (merged.length >= TARGET_DEMO_MEETING_COUNT) break;
    if (mergedIds.has(meeting.id)) continue;
    merged.push(meeting);
    mergedIds.add(meeting.id);
  }

  return merged;
}

export async function createMeeting(data: {
  title: string;
  description: string;
  reason: string;
  participantUserIds: number[];
  scheduledAt?: string;
  context: string;
  involvedAreasCsv: string;
  initialProblems: string[];
}): Promise<MeetingDetailDto> {
  try {
    const response = await authFetch(buildGatewayUrl("api/meetings"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error("Erro ao criar reunião.");
    return (await response.json()) as MeetingDetailDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoCreateMeeting(data);
    throw error;
  }
}

export async function startMeeting(id: number): Promise<MeetingDetailDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/start`), { method: "POST" });
    if (!response.ok) throw new Error("Erro ao iniciar reunião.");
    return (await response.json()) as MeetingDetailDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoStartMeeting(id);
    throw error;
  }
}

export async function updateMeetingStage(id: number, stage: string, options: { force?: boolean; justification?: string } = {}): Promise<MeetingDetailDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/stage`), {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ stage, force: options.force ?? false, justification: options.justification ?? "" }),
    });
    if (!response.ok) throw new Error("Erro ao alterar etapa.");
    return (await response.json()) as MeetingDetailDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoUpdateStage(id, stage, options);
    throw error;
  }
}

export async function generateSuggestedMeetingProblems(id: number): Promise<MeetingProblemDto[]> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/problems/suggest`), { method: "POST" });
    if (!response.ok) throw new Error("Erro ao gerar problemas sugeridos.");
    return (await response.json()) as MeetingProblemDto[];
  } catch (error) {
    if (shouldUseDemoData(error)) return demoSuggestProblems(id);
    throw error;
  }
}

export async function approveMeetingProblem(problemId: number): Promise<MeetingProblemDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/problems/${problemId}/approve`), { method: "POST" });
    if (!response.ok) throw new Error("Erro ao aprovar problema.");
    return (await response.json()) as MeetingProblemDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoApproveProblem(problemId);
    throw error;
  }
}

export async function generateMeetingAiAnalysis(id: number, options: { force?: boolean; justification?: string } = {}): Promise<MeetingAiAnalysisDto[]> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/ai-analysis/generate`), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ force: options.force ?? false, justification: options.justification ?? "" }),
    });
    if (!response.ok) throw new Error("Erro ao gerar análise da IA.");
    return (await response.json()) as MeetingAiAnalysisDto[];
  } catch (error) {
    if (shouldUseDemoData(error)) return demoGenerateAiAnalysis(id);
    throw error;
  }
}

export async function addMeetingComment(id: number, message: string, stage: string): Promise<MeetingCommentDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/comments`), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message, stage }),
    });
    if (!response.ok) throw new Error("Erro ao adicionar comentário.");
    return (await response.json()) as MeetingCommentDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoAddComment(id, message, stage);
    throw error;
  }
}

export async function addMeetingProblem(id: number, data: {
  sector: string;
  description: string;
  severity: string;
  origin: string;
}): Promise<MeetingProblemDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/problems`), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error("Erro ao adicionar problema.");
    return (await response.json()) as MeetingProblemDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoAddProblem(id, data);
    throw error;
  }
}

export async function addMeetingQuestion(id: number, data: {
  problemId: number;
  question: string;
  responsibleUserId: number;
  sector: string;
  isRequired: boolean;
}): Promise<MeetingQuestionDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/questions`), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error("Erro ao adicionar pergunta.");
    return (await response.json()) as MeetingQuestionDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoAddQuestion(id, data);
    throw error;
  }
}

export async function answerMeetingQuestion(questionId: number, answer: string): Promise<MeetingAnswerDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/questions/${questionId}/answer`), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ answer }),
    });
    if (!response.ok) throw new Error("Erro ao responder pergunta.");
    return (await response.json()) as MeetingAnswerDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoAnswerQuestion(questionId, answer);
    throw error;
  }
}

export async function createMeetingDecision(id: number, data: {
  problemId: number;
  chosenSolution: string;
  solutionOrigin: string;
  justification: string;
  responsibleUserId: number;
  deadlineDays: number;
  priority: string;
  trackingMetric: string;
  acceptedRisk: string;
  nextSteps: string;
  closedPendencies: string;
}): Promise<MeetingDecisionDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/decisions`), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error("Erro ao registrar decisão.");
    return (await response.json()) as MeetingDecisionDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoCreateDecision(id, data);
    throw error;
  }
}

export async function createMeetingAction(id: number, data: {
  decisionId?: number | null;
  title: string;
  description: string;
  responsibleUserId: number;
  sector: string;
  deadlineDays: number;
  priority: string;
}): Promise<MeetingActionDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/actions`), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ...data, decisionId: data.decisionId ?? null }),
    });
    if (!response.ok) throw new Error("Erro ao criar ação.");
    return (await response.json()) as MeetingActionDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoCreateAction(id, data);
    throw error;
  }
}

export async function updateActionStatus(actionId: number, data: {
  status: string;
  completionEvidence: string;
  comments: string;
}): Promise<MeetingActionDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/actions/${actionId}/status`), {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error("Erro ao atualizar ação.");
    return (await response.json()) as MeetingActionDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoUpdateActionStatus(actionId, data);
    throw error;
  }
}

export async function concludeMeeting(id: number): Promise<void> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/conclude`), {
      method: "POST",
    });
    if (!response.ok) throw new Error("Erro ao concluir reunião.");
  } catch (error) {
    if (shouldUseDemoData(error)) { demoConcludeMeeting(id); return; }
    throw error;
  }
}

export async function fetchPreMeetingBriefing(id: number): Promise<PreMeetingBriefingDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/briefing`));
    if (!response.ok) throw new Error("Erro ao carregar briefing.");
    return (await response.json()) as PreMeetingBriefingDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoBriefing();
    throw error;
  }
}

export async function addPendingToMeeting(id: number, pendingId: number): Promise<void> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/meetings/${id}/add-pending`), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ pendingId }),
    });
    if (!response.ok) throw new Error("Erro ao adicionar pendencia.");
  } catch (error) {
    if (shouldUseDemoData(error)) return;
    throw error;
  }
}

// ----- Critical Pendencies API -----

export async function fetchCriticalPendencies(status?: string, sector?: string): Promise<CriticalPendingSummaryDto[]> {
  try {
    const params = new URLSearchParams();
    if (status) params.set("status", status);
    if (sector) params.set("sector", sector);
    const qs = params.toString();
    const response = await authFetch(buildGatewayUrl(`api/critical-pendencies${qs ? "?" + qs : ""}`));
    if (!response.ok) throw new Error("Erro ao carregar pendências.");
    return (await response.json()) as CriticalPendingSummaryDto[];
  } catch (error) {
    if (shouldUseDemoData(error)) return demoPendencies();
    throw error;
  }
}

export async function fetchCriticalPending(id: number): Promise<CriticalPendingDetailDto> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/critical-pendencies/${id}`));
    if (!response.ok) throw new Error("Erro ao carregar pendencia.");
    return (await response.json()) as CriticalPendingDetailDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoPendingDetail();
    throw error;
  }
}

export async function createCriticalPending(data: {
  title: string;
  description: string;
  origin: string;
  sector: string;
  responsibleUserId?: number | null;
  priority: string;
  deadlineDays: number;
  sourceMeetingId?: number | null;
  relatedActionId?: number | null;
  relatedDecisionId?: number | null;
}): Promise<CriticalPendingDetailDto> {
  try {
    const response = await authFetch(buildGatewayUrl("api/critical-pendencies"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });
    if (!response.ok) throw new Error("Erro ao criar pendencia.");
    return (await response.json()) as CriticalPendingDetailDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return { id: Date.now(), title: data.title, description: data.description, origin: data.origin, sector: data.sector, responsibleUserId: data.responsibleUserId ?? null, responsibleName: "", priority: data.priority, status: "nova", deadlineDays: data.deadlineDays, sourceMeetingId: data.sourceMeetingId ?? null, relatedActionId: data.relatedActionId ?? null, relatedDecisionId: data.relatedDecisionId ?? null, notificationHistoryJson: "[]", escalationHistoryJson: "[]", createdAt: new Date().toISOString(), resolvedAt: null, deadlineAt: null, aiSuggestion: "" };
    throw error;
  }
}

export async function updatePendingStatus(id: number, status: string, justification = ""): Promise<void> {
  try {
    const response = await authFetch(buildGatewayUrl(`api/critical-pendencies/${id}/status`), {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ status, justification }),
    });
    if (!response.ok) throw new Error("Erro ao atualizar status.");
  } catch (error) {
    if (shouldUseDemoData(error)) return;
    throw error;
  }
}

export async function fetchUnresolvedPendencies(): Promise<CriticalPendingSummaryDto[]> {
  try {
    const response = await authFetch(buildGatewayUrl("api/critical-pendencies/unresolved"));
    if (!response.ok) throw new Error("Erro ao carregar pendências não resolvidas.");
    return (await response.json()) as CriticalPendingSummaryDto[];
  } catch (error) {
    if (shouldUseDemoData(error)) return demoPendencies().filter((p) => p.status !== "resolvida");
    throw error;
  }
}

// ----- Notifications API -----

export async function fetchNotifications(status?: string, page = 1): Promise<NotificationListDto> {
  try {
    const params = new URLSearchParams();
    if (status) params.set("status", status);
    params.set("page", String(page));
    const response = await authFetch(buildGatewayUrl(`api/notifications?${params.toString()}`));
    if (!response.ok) throw new Error("Erro ao carregar notificações.");
    return (await response.json()) as NotificationListDto;
  } catch (error) {
    if (shouldUseDemoData(error)) return demoNotifications();
    throw error;
  }
}

export async function fetchUnreadCount(): Promise<number> {
  try {
    const response = await authFetch(buildGatewayUrl("api/notifications/unread-count"));
    if (!response.ok) return 0;
    const data = (await response.json()) as UnreadCountDto;
    return data.count;
  } catch (error) {
    if (shouldUseDemoData(error)) return 3;
    throw error;
  }
}

export async function markNotificationAsRead(id: number): Promise<void> {
  await authFetch(buildGatewayUrl(`api/notifications/${id}/read`), { method: "PUT" });
}

export async function markAllNotificationsAsRead(): Promise<void> {
  await authFetch(buildGatewayUrl("api/notifications/read-all"), { method: "PUT" });
}

export async function archiveNotification(id: number): Promise<void> {
  await authFetch(buildGatewayUrl(`api/notifications/${id}/archive`), { method: "PUT" });
}

// ----- Stateful Demo Store -----

const _store = new Map<number, MeetingDetailDto>();
let _nextId = 100;
let _itemIdSeq = 100;

function _ts(offsetMinutes = 0): string {
  return new Date(Date.now() + offsetMinutes * 60000).toISOString();
}

function _daysAgo(days: number): string {
  return new Date(Date.now() - days * 86400000).toISOString();
}

function _clone<T>(obj: T): T {
  return JSON.parse(JSON.stringify(obj));
}

function _prepareStore(): void {
  if (_store.size > 0) return;

  const m1: MeetingDetailDto = {
    id: DEMO_MEETING_ID_OFFSET + 1, title: "Comitê de ruptura e abastecimento", description: "Produção, Logística e Vendas definem o plano de reposição dos três SKUs críticos.", reason: "O estoque disponível de três SKUs está abaixo da demanda confirmada para os próximos embarques.", status: "em_andamento", currentStage: "discussao", createdByUserId: 1, createdByName: "Diretor", createdAt: _daysAgo(1), scheduledAt: _ts(-60), concludedAt: null,
    context: "Pão Francês Congelado 60g, Pão de Queijo Congelado 1kg e Croissant Congelado 80g exigem reposição coordenada para preservar as entregas.",
    involvedAreasCsv: "Produção, Logística, Vendas",
    aiSummary: "Há uma ação de abastecimento vencida e três SKUs com risco de ruptura. A prioridade é confirmar insumos, sequenciar a produção e proteger as rotas com pedidos já confirmados.",
    cancellationReason: "",
    participants: [
      { id: 1, userId: 1, userName: "Diretor", userEmail: "diretor@empresa.com", userRole: "diretor", userSector: "Diretoria", roleInMeeting: "diretor", participationStatus: "confirmado", invitedAt: _daysAgo(1) },
      { id: 2, userId: 2, userName: "Gestor Produção", userEmail: "producao@empresa.com", userRole: "producao", userSector: "Produção", roleInMeeting: "gestor", participationStatus: "confirmado", invitedAt: _daysAgo(1) },
      { id: 3, userId: 3, userName: "Gestor Logística", userEmail: "logistica@empresa.com", userRole: "logistica", userSector: "Logística", roleInMeeting: "gestor", participationStatus: "confirmado", invitedAt: _daysAgo(1) },
      { id: 4, userId: 4, userName: "Analista", userEmail: "analista@empresa.com", userRole: "participante", userSector: "Produção", roleInMeeting: "participante", participationStatus: "convidado", invitedAt: _daysAgo(1) },
    ],
    comments: [
      { id: 1, userId: 2, userName: "Gestor Produção", message: "A produção está abaixo do esperado. A equipe teve dificuldade com matéria-prima.", stage: "discussao", isImportant: false, createdAt: _ts(-120) },
      { id: 2, userId: 3, userName: "Gestor Logística", message: "O prazo do cliente está próximo. Precisamos saber se a entrega será mantida.", stage: "discussao", isImportant: true, createdAt: _ts(-90) },
    ],
    problems: [
      { id: 1, sector: "Produção", description: "Três SKUs críticos não possuem cobertura suficiente para os pedidos confirmados.", severity: "alta", origin: "discussao_atual", createdByUserId: 1, createdByName: "Diretor", approvedByDirector: true, aiSuggestion: "Priorizar os SKUs pela data de embarque e confirmar insumos com Compras.", createdAt: _ts(-60), questions: [
        { id: 1, problemId: 1, question: "Quais insumos limitam a reposição dos três SKUs?", responsibleUserId: 2, responsibleName: "Gestor Produção", sector: "Produção", isRequired: true, status: "respondida", answerDeadline: null, createdAt: _ts(-50), answer: { id: 1, userId: 2, userName: "Gestor Produção", sector: "Produção", answer: "A farinha especial e o queijo ainda dependem da confirmação dos fornecedores.", createdAt: _ts(-30) } },
        { id: 2, problemId: 1, question: "Qual sequência de produção protege os embarques mais próximos?", responsibleUserId: 2, responsibleName: "Gestor Produção", sector: "Produção", isRequired: true, status: "pendente", answerDeadline: null, createdAt: _ts(-40), answer: null },
      ]},
      { id: 2, sector: "Logística", description: "Não há confirmação se os caminhões serão usados com boa ocupação.", severity: "media", origin: "discussao_atual", createdByUserId: 1, createdByName: "Diretor", approvedByDirector: true, aiSuggestion: "", createdAt: _ts(-55), questions: [] },
    ],
    questions: [],
    aiAnalyses: [
      { id: 1, problemId: 1, problemDescription: "Três SKUs críticos não possuem cobertura suficiente para os pedidos confirmados.", proposedSolution: "Confirmar os insumos e priorizar a produção pela data dos embarques.", makesSense: true, positivePoints: "Alinha abastecimento, produção e logística em uma única sequência operacional.", negativePoints: "Depende da confirmação dos fornecedores antes de liberar turno adicional.", risks: "Abrir turno sem insumo confirmado aumenta custo sem recuperar o nível de serviço.", expectedImpact: "Redução do risco de ruptura e maior previsibilidade das entregas.", recommendation: "Confirmar farinha especial e queijo antes de alterar a programação da fábrica.", alternativeSolution: "Comprar lote emergencial ou realocar estoque entre centros de distribuição.", suggestedDecision: "Confirmar insumos, priorizar os três SKUs críticos e reservar capacidade nas rotas dos pedidos confirmados.", relatedPendencies: "Resposta do gestor de Produção e ação de abastecimento vencida.", createdAt: _ts(-10) },
    ],
    decisions: [],
    actions: [
      { id: 1, decisionId: null, title: "Confirmar disponibilidade de matéria-prima", description: "Verificar com fornecedores o prazo de entrega.", responsibleUserId: 2, responsibleName: "Gestor Produção", sector: "Produção", deadlineDays: 5, priority: "alta", status: "pendente", completionEvidence: "", comments: "", createdAt: _daysAgo(3), completedAt: null, deadlineAt: _daysAgo(2) },
    ],
    relatedPendencies: [{ id: 1, title: "Ação atrasada: Confirmar matéria-prima", description: "", origin: "acao_atrasada", sector: "Produção", responsibleName: "Gestor Produção", priority: "critica", status: "atrasada", deadlineDays: 5, deadlineAt: _daysAgo(2), createdAt: _daysAgo(5) }],
    history: [
      { id: 1, eventType: "meeting.created", description: "Reunião criada em rascunho.", userId: 1, userName: "Diretor", dataBefore: "{}", dataAfter: "{}", createdAt: _daysAgo(1) },
      { id: 2, eventType: "meeting.stage_changed", description: "Etapa alterada de Contexto para Discussão.", userId: 1, userName: "Diretor", dataBefore: "{}", dataAfter: "{}", createdAt: _ts(-180) },
    ],
  };
  _store.set(m1.id, m1);

  const m2 = _clone(m1);
  m2.id = DEMO_MEETING_ID_OFFSET + 2;
  m2.title = "Revisão de metas comerciais";
  m2.description = "Avaliar desempenho de vendas do trimestre.";
  m2.reason = "Desempenho de vendas abaixo do esperado no trimestre.";
  m2.status = "concluida";
  m2.currentStage = "acompanhamento";
  m2.createdAt = _daysAgo(7);
  m2.concludedAt = _daysAgo(3);
  m2.context = "Vendas fecharam 20% abaixo da meta.";
  m2.involvedAreasCsv = "Vendas, Finanças, Diretoria";
  m2.aiSummary = "";
  m2.participants = [
    { id: 1, userId: 1, userName: "Diretor", userEmail: "diretor@empresa.com", userRole: "diretor", userSector: "Diretoria", roleInMeeting: "diretor", participationStatus: "confirmado", invitedAt: _daysAgo(7) },
    { id: 5, userId: 5, userName: "Vendedor", userEmail: "vendas@empresa.com", userRole: "vendas", userSector: "Vendas", roleInMeeting: "gestor", participationStatus: "confirmado", invitedAt: _daysAgo(7) },
    { id: 6, userId: 6, userName: "Financeiro", userEmail: "financeiro@empresa.com", userRole: "financeiro", userSector: "Finanças", roleInMeeting: "gestor", participationStatus: "confirmado", invitedAt: _daysAgo(7) },
    { id: 3, userId: 3, userName: "Gestor Logística", userEmail: "logistica@empresa.com", userRole: "logistica", userSector: "Logística", roleInMeeting: "gestor", participationStatus: "confirmado", invitedAt: _daysAgo(7) },
    { id: 4, userId: 4, userName: "Analista", userEmail: "analista@empresa.com", userRole: "participante", userSector: "Produção", roleInMeeting: "participante", participationStatus: "convidado", invitedAt: _daysAgo(7) },
  ];
  m2.comments = [
    { id: 1, userId: 5, userName: "Vendedor", message: "O mercado está retraído, perdemos 3 clientes grandes.", stage: "discussao", isImportant: false, createdAt: _ts(-4000) },
    { id: 2, userId: 6, userName: "Financeiro", message: "Precisamos revisar as metas para o próximo trimestre.", stage: "discussao", isImportant: true, createdAt: _ts(-3900) },
  ];
  m2.problems = [
    { id: 1, sector: "Vendas", description: "Meta de vendas não atingida no trimestre.", severity: "alta", origin: "discussao_atual", createdByUserId: 1, createdByName: "Diretor", approvedByDirector: true, aiSuggestion: "", createdAt: _ts(-3800), questions: [
      { id: 1, problemId: 1, question: "Qual foi o principal motivo?", responsibleUserId: 5, responsibleName: "Vendedor", sector: "Vendas", isRequired: true, status: "respondida", answerDeadline: null, createdAt: _ts(-3700), answer: { id: 1, userId: 5, userName: "Vendedor", sector: "Vendas", answer: "Mercado retraído e falta de novos leads.", createdAt: _ts(-3600) } },
    ]},
  ];
  m2.questions = [];
  m2.aiAnalyses = [];
  m2.decisions = [
    { id: 1, problemId: 1, problemDescription: "Meta de vendas não atingida no trimestre.", chosenSolution: "Intensificar prospecção e revisar metas.", solutionOrigin: "gestor_mais_ia", justification: "Equipe comercial precisa de mais leads.", responsibleUserId: 5, responsibleName: "Vendedor", sector: "Vendas", deadlineDays: 30, priority: "alta", trackingMetric: "Número de reuniões por semana", acceptedRisk: "Curto prazo pode sacrificar margem", nextSteps: "Contratar agência de leads", closedPendencies: "", createdAt: _daysAgo(4) },
  ];
  m2.actions = [
    { id: 1, decisionId: null, title: "Revisar metas comerciais", description: "Ajustar metas do próximo trimestre.", responsibleUserId: 5, responsibleName: "Vendedor", sector: "Vendas", deadlineDays: 7, priority: "alta", status: "concluida", completionEvidence: "Metas revisadas.", comments: "", createdAt: _daysAgo(4), completedAt: _daysAgo(2), deadlineAt: _daysAgo(-3) },
    { id: 2, decisionId: 1, title: "Contratar prospecção externa", description: "Avaliar agência de leads B2B.", responsibleUserId: 5, responsibleName: "Vendedor", sector: "Vendas", deadlineDays: 15, priority: "media", status: "pendente", completionEvidence: "", comments: "", createdAt: _daysAgo(4), completedAt: null, deadlineAt: null },
  ];
  m2.relatedPendencies = [];
  m2.history = [
    { id: 1, eventType: "meeting.created", description: "Reunião criada em rascunho.", userId: 1, userName: "Diretor", dataBefore: "{}", dataAfter: "{}", createdAt: _daysAgo(7) },
    { id: 2, eventType: "meeting.stage_changed", description: "Etapa alterada de Contexto para Discussão.", userId: 1, userName: "Diretor", dataBefore: "{}", dataAfter: "{}", createdAt: _daysAgo(6) },
    { id: 3, eventType: "meeting.completed", description: "Reunião encerrada pelo Diretor.", userId: 1, userName: "Diretor", dataBefore: "{}", dataAfter: "{}", createdAt: _daysAgo(3) },
  ];
  _store.set(m2.id, m2);

  const m3 = _clone(m1);
  m3.id = DEMO_MEETING_ID_OFFSET + 3;
  m3.title = "Planejamento de rotas críticas";
  m3.description = "Logística e Produção revisam ocupação da frota, custos e três rotas com risco de atraso.";
  m3.reason = "Ocupação abaixo de 70% na frota e aumento de custos operacionais.";
  m3.status = "rascunho";
  m3.currentStage = "contexto";
  m3.createdAt = _daysAgo(2);
  m3.scheduledAt = _ts(1440);
  m3.context = "O relatório de frota aponta ocupação de 65% e 3 rotas com custo acima do esperado.";
  m3.involvedAreasCsv = "Logística, Produção";
  m3.aiSummary = "";
  m3.participants = [
    { id: 1, userId: 1, userName: "Diretor", userEmail: "diretor@empresa.com", userRole: "diretor", userSector: "Diretoria", roleInMeeting: "diretor", participationStatus: "confirmado", invitedAt: _daysAgo(2) },
    { id: 3, userId: 3, userName: "Gestor Logística", userEmail: "logistica@empresa.com", userRole: "logistica", userSector: "Logística", roleInMeeting: "gestor", participationStatus: "confirmado", invitedAt: _daysAgo(2) },
    { id: 2, userId: 2, userName: "Gestor Produção", userEmail: "producao@empresa.com", userRole: "producao", userSector: "Produção", roleInMeeting: "gestor", participationStatus: "confirmado", invitedAt: _daysAgo(2) },
  ];
  m3.comments = [];
  m3.problems = [];
  m3.questions = [];
  m3.aiAnalyses = [];
  m3.decisions = [];
  m3.actions = [];
  m3.relatedPendencies = [];
  m3.history = [
    { id: 1, eventType: "meeting.created", description: "Reunião criada em rascunho.", userId: 1, userName: "Diretor", dataBefore: "{}", dataAfter: "{}", createdAt: _daysAgo(2) },
  ];
  _store.set(m3.id, m3);

  const demoMeetingScenarios: Array<{
    id: number;
    title: string;
    description: string;
    reason: string;
    status: MeetingStatus;
    currentStage: MeetingStage;
    areas: string;
    createdDaysAgo: number;
    scheduledOffsetMinutes?: number;
    concludedDaysAgo?: number;
    problemCount: number;
    actionStatus?: "pendente" | "em_execucao" | "atrasada" | "concluida";
  }> = [
    { id: DEMO_MEETING_ID_OFFSET + 4, title: "Revisão de margem por cliente", description: "Analisar clientes com margem abaixo do planejado.", reason: "Margem comprimida em clientes estratégicos.", status: "em_andamento", currentStage: "problemas", areas: "Vendas, Finanças", createdDaysAgo: 3, problemCount: 2, actionStatus: "pendente" },
    { id: DEMO_MEETING_ID_OFFSET + 5, title: "Plano de recuperação de entregas", description: "Reorganizar entregas críticas da semana.", reason: "Atrasos acumulados nas rotas do Sudeste.", status: "em_andamento", currentStage: "perguntas_e_respostas", areas: "Logística, Vendas", createdDaysAgo: 4, problemCount: 3, actionStatus: "atrasada" },
    { id: DEMO_MEETING_ID_OFFSET + 6, title: "Capacidade produtiva do mês", description: "Validar gargalos de linha e disponibilidade de turno.", reason: "Demanda prevista acima da capacidade atual.", status: "rascunho", currentStage: "contexto", areas: "Produção, Diretoria", createdDaysAgo: 1, scheduledOffsetMinutes: 2880, problemCount: 1 },
    { id: DEMO_MEETING_ID_OFFSET + 7, title: "Acompanhamento de ações comerciais", description: "Checar execução das ações da última reunião.", reason: "Ações comerciais críticas próximas do prazo.", status: "em_andamento", currentStage: "acoes", areas: "Vendas, Diretoria", createdDaysAgo: 8, problemCount: 1, actionStatus: "em_execucao" },
    { id: DEMO_MEETING_ID_OFFSET + 8, title: "Qualidade de dados importados", description: "Investigar divergências nos arquivos importados.", reason: "Indicadores financeiros com bases inconsistentes.", status: "em_andamento", currentStage: "discussao", areas: "Administrativo, Finanças", createdDaysAgo: 2, problemCount: 2, actionStatus: "pendente" },
    { id: DEMO_MEETING_ID_OFFSET + 9, title: "Priorização de pedidos especiais", description: "Definir pedidos que entram na janela produtiva.", reason: "Pedidos especiais competem com produção recorrente.", status: "rascunho", currentStage: "contexto", areas: "Vendas, Produção", createdDaysAgo: 5, scheduledOffsetMinutes: 4320, problemCount: 1 },
    { id: DEMO_MEETING_ID_OFFSET + 10, title: "Risco de ruptura de estoque", description: "Avaliar produtos com cobertura baixa.", reason: "Itens de alto giro abaixo do estoque mínimo.", status: "em_andamento", currentStage: "solucoes", areas: "Produção, Logística", createdDaysAgo: 6, problemCount: 2, actionStatus: "pendente" },
    { id: DEMO_MEETING_ID_OFFSET + 11, title: "Fechamento de pendências antigas", description: "Encerrar decisões sem evidência de execução.", reason: "Pendências de reuniões anteriores seguem abertas.", status: "em_andamento", currentStage: "acompanhamento", areas: "Diretoria, Administrativo", createdDaysAgo: 12, problemCount: 2, actionStatus: "atrasada" },
    { id: DEMO_MEETING_ID_OFFSET + 12, title: "Revisão de atendimento ao cliente", description: "Tratar reclamações recorrentes de entrega.", reason: "Aumento de chamados por atraso e avaria.", status: "concluida", currentStage: "acompanhamento", areas: "Logística, Vendas", createdDaysAgo: 15, concludedDaysAgo: 9, problemCount: 2, actionStatus: "concluida" },
    { id: DEMO_MEETING_ID_OFFSET + 13, title: "Alinhamento de compras emergenciais", description: "Validar fornecedores alternativos para insumos.", reason: "Risco de falta de matéria-prima crítica.", status: "em_andamento", currentStage: "analise_ia", areas: "Produção, Administrativo", createdDaysAgo: 7, problemCount: 3, actionStatus: "pendente" },
    { id: DEMO_MEETING_ID_OFFSET + 14, title: "Revisão de custos logísticos", description: "Comparar custos por rota e ocupação de carga.", reason: "Custo por rota acima do limite aceitável.", status: "rascunho", currentStage: "contexto", areas: "Logística, Finanças", createdDaysAgo: 3, scheduledOffsetMinutes: 5760, problemCount: 1 },
    { id: DEMO_MEETING_ID_OFFSET + 15, title: "Comitê de inadimplência", description: "Definir tratativas para clientes em atraso.", reason: "Recebíveis vencidos pressionam o caixa.", status: "em_andamento", currentStage: "conclusao", areas: "Finanças, Vendas", createdDaysAgo: 10, problemCount: 2, actionStatus: "em_execucao" },
    { id: DEMO_MEETING_ID_OFFSET + 16, title: "Roteirização de entregas críticas", description: "Replanejar rotas com maior risco operacional.", reason: "Rotas críticas concentram atrasos e custo alto.", status: "em_andamento", currentStage: "problemas", areas: "Logística, Diretoria", createdDaysAgo: 4, problemCount: 2, actionStatus: "pendente" },
    { id: DEMO_MEETING_ID_OFFSET + 17, title: "Revisão do plano semanal", description: "Ajustar plano produtivo com base nas vendas.", reason: "Carteira mudou após novos pedidos prioritários.", status: "rascunho", currentStage: "contexto", areas: "Produção, Vendas", createdDaysAgo: 1, scheduledOffsetMinutes: 1440, problemCount: 1 },
    { id: DEMO_MEETING_ID_OFFSET + 18, title: "Auditoria de processos administrativos", description: "Validar aprovações e documentação pendente.", reason: "Processos sem evidência atrasam fechamento mensal.", status: "em_andamento", currentStage: "acompanhamento", areas: "Administrativo, Diretoria", createdDaysAgo: 20, problemCount: 2, actionStatus: "em_execucao" },
    { id: DEMO_MEETING_ID_OFFSET + 19, title: "Tratativa de alertas críticos", description: "Priorizar alertas escalados para diretoria.", reason: "Alertas críticos exigem decisão executiva.", status: "em_andamento", currentStage: "discussao", areas: "Diretoria, Produção, Logística", createdDaysAgo: 2, problemCount: 3, actionStatus: "atrasada" },
    { id: DEMO_MEETING_ID_OFFSET + 20, title: "Acompanhamento de implantação", description: "Monitorar decisões já aprovadas e seus responsáveis.", reason: "Ações aprovadas precisam de cadência de acompanhamento.", status: "em_andamento", currentStage: "acompanhamento", areas: "Diretoria, Vendas, Produção", createdDaysAgo: 11, problemCount: 2, actionStatus: "em_execucao" },
  ];

  for (const scenario of demoMeetingScenarios) {
    const template = scenario.status === "concluida" ? m2 : scenario.areas.includes("Logística") ? m3 : m1;
    const meeting = _clone(template);
    meeting.id = scenario.id;
    meeting.title = scenario.title;
    meeting.description = scenario.description;
    meeting.reason = scenario.reason;
    meeting.status = scenario.status;
    meeting.currentStage = scenario.currentStage;
    meeting.createdAt = _daysAgo(scenario.createdDaysAgo);
    meeting.scheduledAt = scenario.scheduledOffsetMinutes == null ? null : _ts(scenario.scheduledOffsetMinutes);
    meeting.concludedAt = scenario.concludedDaysAgo == null ? null : _daysAgo(scenario.concludedDaysAgo);
    meeting.context = `${scenario.description} Áreas envolvidas: ${scenario.areas}.`;
    meeting.involvedAreasCsv = scenario.areas;
    meeting.aiSummary = "";
    meeting.problems = Array.from({ length: scenario.problemCount }, (_, index) => ({
      id: index + 1,
      sector: scenario.areas.split(",")[0].trim(),
      description: `${scenario.reason} - ponto ${index + 1}.`,
      severity: index === 0 ? "alta" : "media",
      origin: "discussao_atual",
      createdByUserId: 1,
      createdByName: "Diretor",
      approvedByDirector: scenario.currentStage !== "contexto",
      aiSuggestion: "Validar causa raiz, responsável e prazo de execução.",
      createdAt: _daysAgo(Math.max(1, scenario.createdDaysAgo - index)),
      questions: [],
    }));
    meeting.questions = [];
    meeting.comments = [];
    meeting.aiAnalyses = [];
    meeting.decisions = [];
    meeting.actions = scenario.actionStatus ? [{
      id: 1,
      decisionId: null,
      title: `Ação: ${scenario.title}`,
      description: "Acompanhar execução definida na reunião demonstrativa.",
      responsibleUserId: 1,
      responsibleName: "Diretor",
      sector: scenario.areas.split(",")[0].trim(),
      deadlineDays: 7,
      priority: scenario.actionStatus === "atrasada" ? "alta" : "media",
      status: scenario.actionStatus,
      completionEvidence: scenario.actionStatus === "concluida" ? "Evidência registrada no fluxo demonstrativo." : "",
      comments: "",
      createdAt: _daysAgo(scenario.createdDaysAgo),
      completedAt: scenario.actionStatus === "concluida" ? _daysAgo(Math.max(1, scenario.createdDaysAgo - 2)) : null,
      deadlineAt: scenario.actionStatus === "atrasada" ? _daysAgo(1) : _ts(2880),
    }] : [];
    meeting.relatedPendencies = [];
    meeting.history = [
      { id: 1, eventType: "meeting.created", description: "Reunião criada em rascunho.", userId: 1, userName: "Diretor", dataBefore: "{}", dataAfter: "{}", createdAt: meeting.createdAt },
    ];
    _store.set(scenario.id, meeting);
  }
}

function _getMeeting(id: number): MeetingDetailDto {
  _prepareStore();
  const m = _store.get(id);
  if (!m) {
    const template = _store.get(DEMO_MEETING_ID_OFFSET + 1)!;
    const fresh = _clone(template);
    fresh.id = id;
    _store.set(id, fresh);
    return _clone(fresh);
  }
  return _clone(m);
}

function _saveMeeting(m: MeetingDetailDto): void {
  _store.set(m.id, m);
}

function _nextItemId(): number {
  return _itemIdSeq++;
}

const STORE_USERS: UserListItem[] = [
  { id: 1, name: "Diretor", email: "diretor@empresa.com", role: "diretor", sector: "Diretoria" },
  { id: 2, name: "Gestor Produção", email: "producao@empresa.com", role: "producao", sector: "Produção" },
  { id: 3, name: "Gestor Logística", email: "logistica@empresa.com", role: "logistica", sector: "Logística" },
  { id: 4, name: "Analista", email: "analista@empresa.com", role: "participante", sector: "Produção" },
  { id: 5, name: "Vendedor", email: "vendas@empresa.com", role: "vendas", sector: "Vendas" },
  { id: 6, name: "Financeiro", email: "financeiro@empresa.com", role: "financeiro", sector: "Finanças" },
];

function demoUsers(): UserListItem[] {
  return STORE_USERS;
}

function demoMeetings(): MeetingListDto[] {
  _prepareStore();
  return Array.from(_store.values()).map(m => ({
    id: m.id, title: m.title, description: m.description,
    status: m.status, currentStage: m.currentStage,
    createdByName: m.createdByName, createdAt: m.createdAt,
    scheduledAt: m.scheduledAt,
    participantCount: m.participants.length,
    problemCount: m.problems.length,
    questionCount: m.questions.length,
    overdueActionCount: m.actions.filter(a => a.status === "atrasada").length,
  }));
}

function demoMeetingDetail(id: number): MeetingDetailDto {
  return _getMeeting(id);
}

// ----- Store mutation helpers (called from catch blocks) -----

function demoCreateMeeting(data: {
  title: string; description: string; reason: string;
  participantUserIds: number[]; scheduledAt?: string;
  context: string; involvedAreasCsv: string; initialProblems: string[];
}): MeetingDetailDto {
  const id = _nextId++;
  const now = _ts();
  const participants: MeetingParticipantDto[] = data.participantUserIds.map((uid, idx) => {
    const u = STORE_USERS.find(u => u.id === uid);
    return { id: idx + 1, userId: uid, userName: u?.name ?? `Usuário ${uid}`, userEmail: u?.email ?? "", userRole: u?.role ?? "", userSector: u?.sector ?? "", roleInMeeting: uid === 1 ? "diretor" : "participante", participationStatus: "convidado", invitedAt: now };
  });
  const meeting: MeetingDetailDto = {
    id, title: data.title, description: data.description, reason: data.reason,
    status: "rascunho", currentStage: "contexto", createdByUserId: 1, createdByName: "Diretor",
    createdAt: now, scheduledAt: data.scheduledAt ?? null, concludedAt: null,
    context: data.context, involvedAreasCsv: data.involvedAreasCsv,
    aiSummary: "", cancellationReason: "",
    participants, comments: [], problems: [], questions: [],
    aiAnalyses: [], decisions: [], actions: [], relatedPendencies: [],
    history: [{ id: 1, eventType: "meeting.created", description: "Reunião criada em rascunho.", userId: 1, userName: "Diretor", dataBefore: "{}", dataAfter: "{}", createdAt: now }],
  };
  _store.set(id, meeting);
  return _clone(meeting);
}

function demoStartMeeting(id: number): MeetingDetailDto {
  const m = _getMeeting(id);
  m.status = "em_andamento";
  m.currentStage = "contexto";
  m.history.push({ id: _nextItemId(), eventType: "meeting.started", description: "Reunião iniciada pelo Diretor.", userId: 1, userName: "Diretor", dataBefore: "{}", dataAfter: "{}", createdAt: _ts() });
  _saveMeeting(m);
  return _clone(m);
}

function demoUpdateStage(id: number, stage: string, _options: { force?: boolean; justification?: string }): MeetingDetailDto {
  const m = _getMeeting(id);
  const prev = m.currentStage;
  m.currentStage = stage;
  if (m.status === "rascunho" && stage !== "contexto") m.status = "em_andamento";
  if (stage === "acoes") m.status = "aguardando_conclusao";
  if (stage === "acompanhamento") m.status = "concluida";
  m.history.push({ id: _nextItemId(), eventType: "meeting.stage_changed", description: `Etapa alterada de ${prev} para ${stage}.`, userId: 1, userName: "Diretor", dataBefore: "{}", dataAfter: "{}", createdAt: _ts() });
  _saveMeeting(m);
  return _clone(m);
}

function demoSuggestProblems(id: number): MeetingProblemDto[] {
  const m = _getMeeting(id);
  const suggestions = [
    { sector: "Produção", description: "Produção está abaixo do esperado e pode comprometer entregas futuras.", severity: "alta" },
    { sector: "Logística", description: "Risco de entrega parcial se o volume produzido não for confirmado.", severity: "media" },
  ];
  for (const s of suggestions) {
    const exists = m.problems.some(p => p.description === s.description);
    if (!exists) {
      const pid = _nextItemId();
      m.problems.push({ id: pid, sector: s.sector, description: s.description, severity: s.severity, origin: "sugestao_ia", createdByUserId: 1, createdByName: "Diretor", approvedByDirector: false, aiSuggestion: "", createdAt: _ts(), questions: [] });
    }
  }
  _saveMeeting(m);
  return _clone(m).problems;
}

function demoApproveProblem(problemId: number): MeetingProblemDto {
  for (const m of _store.values()) {
    const p = m.problems.find(pr => pr.id === problemId);
    if (p) {
      p.approvedByDirector = true;
      _saveMeeting(m);
      return _clone(p);
    }
  }
  throw new Error("Problema não encontrado.");
}

function demoGenerateAiAnalysis(id: number): MeetingAiAnalysisDto[] {
  const m = _getMeeting(id);
  const approved = m.problems.filter(p => p.approvedByDirector);
  for (const p of approved) {
    const exists = m.aiAnalyses.some(a => a.problemId === p.id);
    if (!exists) {
      m.aiAnalyses.push({ id: _nextItemId(), problemId: p.id, problemDescription: p.description, proposedSolution: "Solução proposta baseada na discussão.", makesSense: true, positivePoints: "Pontos positivos identificados.", negativePoints: "Pontos negativos considerados.", risks: "Riscos avaliados.", expectedImpact: "Impacto esperado positivo.", recommendation: "Recomendação da IA.", alternativeSolution: "Solução alternativa.", suggestedDecision: "Decisão sugerida.", relatedPendencies: "", createdAt: _ts() });
    }
  }
  _saveMeeting(m);
  return _clone(m).aiAnalyses;
}

function demoAddComment(id: number, message: string, stage: string): MeetingCommentDto {
  const m = _getMeeting(id);
  const c: MeetingCommentDto = { id: _nextItemId(), userId: 1, userName: "Você", message, stage, isImportant: false, createdAt: _ts() };
  m.comments.push(c);
  _saveMeeting(m);
  return _clone(c);
}

function demoAddProblem(id: number, data: { sector: string; description: string; severity: string; origin: string }): MeetingProblemDto {
  const m = _getMeeting(id);
  const p: MeetingProblemDto = { id: _nextItemId(), sector: data.sector, description: data.description, severity: data.severity, origin: data.origin, createdByUserId: 1, createdByName: "Diretor", approvedByDirector: true, aiSuggestion: "", createdAt: _ts(), questions: [] };
  m.problems.push(p);
  _saveMeeting(m);
  return _clone(p);
}

function demoAddQuestion(id: number, data: { problemId: number; question: string; responsibleUserId: number; sector: string; isRequired: boolean }): MeetingQuestionDto {
  const m = _getMeeting(id);
  const q: MeetingQuestionDto = { id: _nextItemId(), problemId: data.problemId, question: data.question, responsibleUserId: data.responsibleUserId, responsibleName: "Responsável", sector: data.sector, isRequired: data.isRequired, status: "pendente", answerDeadline: null, createdAt: _ts(), answer: null };
  const problem = m.problems.find(p => p.id === data.problemId);
  if (problem) problem.questions.push(q);
  _saveMeeting(m);
  return _clone(q);
}

function demoAnswerQuestion(questionId: number, answer: string): MeetingAnswerDto {
  const a: MeetingAnswerDto = { id: _nextItemId(), userId: 1, userName: "Você", sector: "Geral", answer, createdAt: _ts() };
  for (const m of _store.values()) {
    for (const p of m.problems) {
      const q = p.questions.find(q => q.id === questionId);
      if (q) {
        q.status = "respondida";
        q.answer = _clone(a);
        _saveMeeting(m);
        return a;
      }
    }
  }
  return a;
}

function demoCreateDecision(id: number, data: {
  problemId: number; chosenSolution: string; solutionOrigin: string; justification: string;
  responsibleUserId: number; deadlineDays: number; priority: string;
  trackingMetric: string; acceptedRisk: string; nextSteps: string; closedPendencies: string;
}): MeetingDecisionDto {
  const m = _getMeeting(id);
  const problem = m.problems.find(p => p.id === data.problemId);
  const d: MeetingDecisionDto = { id: _nextItemId(), problemId: data.problemId, problemDescription: problem?.description ?? "", chosenSolution: data.chosenSolution, solutionOrigin: data.solutionOrigin, justification: data.justification, responsibleUserId: data.responsibleUserId, responsibleName: "Responsável", sector: problem?.sector ?? "", deadlineDays: data.deadlineDays, priority: data.priority, trackingMetric: data.trackingMetric, acceptedRisk: data.acceptedRisk, nextSteps: data.nextSteps, closedPendencies: data.closedPendencies, createdAt: _ts() };
  m.decisions.push(d);
  _saveMeeting(m);
  return _clone(d);
}

function demoCreateAction(id: number, data: { decisionId?: number | null; title: string; description: string; responsibleUserId: number; sector: string; deadlineDays: number; priority: string }): MeetingActionDto {
  const m = _getMeeting(id);
  const a: MeetingActionDto = { id: _nextItemId(), decisionId: data.decisionId ?? null, title: data.title, description: data.description, responsibleUserId: data.responsibleUserId, responsibleName: "Responsável", sector: data.sector, deadlineDays: data.deadlineDays, priority: data.priority, status: "pendente", completionEvidence: "", comments: "", createdAt: _ts(), completedAt: null, deadlineAt: null };
  m.actions.push(a);
  _saveMeeting(m);
  return _clone(a);
}

function demoUpdateActionStatus(actionId: number, data: { status: string; completionEvidence: string; comments: string }): MeetingActionDto {
  for (const m of _store.values()) {
    const a = m.actions.find(ac => ac.id === actionId);
    if (a) {
      a.status = data.status;
      if (data.completionEvidence) a.completionEvidence = data.completionEvidence;
      if (data.comments) a.comments = data.comments;
      if (data.status === "concluida") a.completedAt = _ts();
      _saveMeeting(m);
      return _clone(a);
    }
  }
  return { id: actionId, decisionId: null, title: "Ação", description: "", responsibleUserId: 1, responsibleName: "Você", sector: "Geral", deadlineDays: 0, priority: "media", status: data.status, completionEvidence: data.completionEvidence, comments: data.comments, createdAt: _ts(), completedAt: data.status === "concluida" ? _ts() : null, deadlineAt: null };
}

function demoConcludeMeeting(id: number): void {
  const m = _getMeeting(id);
  m.status = "concluida";
  m.concludedAt = _ts();
  m.currentStage = "acompanhamento";
  m.history.push({ id: _nextItemId(), eventType: "meeting.completed", description: "Reunião encerrada pelo Diretor.", userId: 1, userName: "Diretor", dataBefore: "{}", dataAfter: "{}", createdAt: _ts() });
  _saveMeeting(m);
}

// ----- Static demo helpers (non-meeting data) -----

function demoBriefing(): PreMeetingBriefingDto {
  return {
    totalPendencies: 3,
    pendencies: [
      { id: 1, title: "Ação atrasada: Confirmar matéria-prima", description: "Ação definida em reunião anterior está atrasada há 3 dias.", origin: "acao_atrasada", sector: "Produção", responsibleName: "Gestor Produção", priority: "critica", status: "atrasada", deadlineDays: 5, deadlineAt: _daysAgo(2), createdAt: _daysAgo(5) },
      { id: 2, title: "Decisão sem execução: Estender turno", description: "Decisão tomada na última reunião sem evidência de implementação.", origin: "decisao_nao_executada", sector: "Produção", responsibleName: "Gestor Produção", priority: "alta", status: "em_execucao", deadlineDays: 3, deadlineAt: _daysAgo(1), createdAt: _daysAgo(4) },
      { id: 3, title: "Problema recorrente: Baixa produtividade", description: "Problema de produtividade persiste há 3 semanas.", origin: "problema_recorrente", sector: "Produção", responsibleName: "", priority: "critica", status: "nova", deadlineDays: 0, deadlineAt: null, createdAt: _daysAgo(21) },
    ],
    aiSummary: "Antes de iniciar esta reunião, existem 3 pendências críticas relacionadas à Produção: uma ação atrasada há 3 dias, uma decisão sem evidência de execução e um problema recorrente de baixa produtividade.",
  };
}

function demoPendencies(): CriticalPendingSummaryDto[] {
  return [
    { id: 1, title: "Ação atrasada: Confirmar matéria-prima", description: "Ação definida em reunião anterior para confirmar disponibilidade de matéria-prima junto aos fornecedores.", origin: "acao_atrasada", sector: "Produção", responsibleName: "Gestor Produção", priority: "critica", status: "atrasada", deadlineDays: 5, deadlineAt: _daysAgo(2), createdAt: _daysAgo(5) },
    { id: 2, title: "Decisão não executada: Estender turno", description: "Decisão de estender turno por 2 dias não foi implementada.", origin: "decisao_nao_executada", sector: "Produção", responsibleName: "Gestor Produção", priority: "alta", status: "em_execucao", deadlineDays: 3, deadlineAt: _daysAgo(1), createdAt: _daysAgo(4) },
    { id: 3, title: "Problema recorrente: Baixa produtividade", description: "Produção abaixo da meta por 3 semanas consecutivas.", origin: "problema_recorrente", sector: "Produção", responsibleName: "", priority: "critica", status: "nova", deadlineDays: 0, deadlineAt: null, createdAt: _daysAgo(21) },
    { id: 4, title: "Pergunta sem resposta: Capacidade produtiva", description: "Pergunta sobre capacidade produtiva ficou sem resposta na última reunião.", origin: "pergunta_sem_resposta", sector: "Produção", responsibleName: "Analista", priority: "media", status: "em_analise", deadlineDays: 7, deadlineAt: _daysAgo(4), createdAt: _daysAgo(11) },
    { id: 5, title: "Risco: Dependência de fornecedor único", description: "IA identificou risco crítico de dependência de único fornecedor de matéria-prima.", origin: "risco_ia", sector: "Compras", responsibleName: "", priority: "alta", status: "nova", deadlineDays: 30, deadlineAt: null, createdAt: _daysAgo(2) },
  ];
}

function demoPendingDetail(): CriticalPendingDetailDto {
  return {
    id: 1, title: "Ação atrasada: Confirmar matéria-prima", description: "Ação definida em reunião anterior para confirmar disponibilidade de matéria-prima junto aos fornecedores.",
    origin: "acao_atrasada", sector: "Produção", responsibleUserId: 2, responsibleName: "Gestor Produção",
    priority: "critica", status: "atrasada", deadlineDays: 5, sourceMeetingId: 1, relatedActionId: 1, relatedDecisionId: null,
    notificationHistoryJson: '[{"date":"2026-06-20T10:00:00Z","type":"overdue","recipient":"Gestor Produção"}]',
    escalationHistoryJson: "[]",
    createdAt: _daysAgo(5), resolvedAt: null, deadlineAt: _daysAgo(2),
    aiSuggestion: "Recomenda-se contatar fornecedores alternativos e reportar ao Diretor o impacto no cronograma.",
  };
}

function demoNotifications(): NotificationListDto {
  const notifications: NotificationDto[] = [
    { id: 1, userId: 1, title: "Convite para reunião", message: "Você foi convidado para a reunião: Risco de atraso na produção.", type: "convite_reuniao", priority: "alta", status: "nao_lida", relatedLink: "/reunioes/1", relatedEntity: "Meeting", relatedEntityId: 1, createdAt: _ts(-120), readAt: null },
    { id: 2, userId: 1, title: "Pergunta pendente", message: "Você precisa responder uma pergunta sobre Produção na reunião Risco de atraso na produção.", type: "pergunta_pendente", priority: "alta", status: "nao_lida", relatedLink: "/reunioes/1", relatedEntity: "MeetingQuestion", relatedEntityId: 2, createdAt: _ts(-90), readAt: null },
    { id: 3, userId: 1, title: "Ação atribuída", message: "Uma ação foi atribuída a você: Revisar capacidade produtiva.", type: "acao_atribuida", priority: "media", status: "nao_lida", relatedLink: "/reunioes/1", relatedEntity: "MeetingAction", relatedEntityId: 2, createdAt: _ts(-60), readAt: null },
    { id: 4, userId: 1, title: "Pendência crítica escalada", message: "A pendência 'Confirmar matéria-prima' foi escalada para o Diretor.", type: "pendencia_escalada", priority: "critica", status: "lida", relatedLink: "/pendencias/1", relatedEntity: "CriticalPending", relatedEntityId: 1, createdAt: _daysAgo(1), readAt: _ts(-300) },
    { id: 5, userId: 1, title: "Reunião concluída", message: "A reunião Revisão de metas comerciais foi concluída.", type: "reuniao_concluida", priority: "baixa", status: "lida", relatedLink: "/reunioes/2", relatedEntity: "Meeting", relatedEntityId: 2, createdAt: _daysAgo(3), readAt: _daysAgo(3) },
  ];
  return { total: notifications.length, unreadCount: 3, notifications };
}
