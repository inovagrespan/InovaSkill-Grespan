import { useEffect, useMemo, useState, type ComponentType } from "react";
import { createFileRoute } from "@tanstack/react-router";
import {
  AlertTriangle,
  BellRing,
  CheckCircle2,
  ChevronRight,
  Clock,
  Eye,
  Flag,
  ListChecks,
  RefreshCw,
  Search,
  Sparkles,
  Users,
} from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import {
  evaluateAiAlertEscalation,
  fetchAiAlertsDashboard,
  updateAiAlertStatus,
  type AiAlertDashboard,
  type AiAlertItem,
  type AiAlertStatus,
} from "@/lib/importer-api";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/alertas")({
  component: AlertasPage,
});

type AlertMetric = {
  title: string;
  value: number;
  detail: string;
  icon: ComponentType<{ className?: string }>;
  tone: "neutral" | "healthy" | "attention" | "critical";
};

const areas = ["Todas", "Vendas", "Logística", "Produção", "Administrativo", "Diretoria"];
const statuses: Array<AiAlertStatus | "Todos"> = ["Todos", "Novo", "Em análise", "Atrasado", "Escalado para diretoria", "Resolvido"];
const severities = ["Todas", "Baixo", "Médio", "Alto", "Crítico"];
const emptyText = "Não informado";

function AlertasPage() {
  const [dashboard, setDashboard] = useState<AiAlertDashboard | null>(null);
  const [selectedAlert, setSelectedAlert] = useState<AiAlertItem | null>(null);
  const [area, setArea] = useState("Todas");
  const [status, setStatus] = useState<AiAlertStatus | "Todos">("Todos");
  const [severity, setSeverity] = useState("Todas");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(true);

  async function loadDashboard() {
    try {
      setLoading(true);
      const data = await fetchAiAlertsDashboard({
        area: area === "Todas" ? undefined : area,
        status: status === "Todos" ? undefined : status,
        severity: severity === "Todas" ? undefined : severity,
      });
      setDashboard(data);
      setSelectedAlert((current) => data.alerts.find((alert) => alert.id === current?.id) ?? null);
      setMessage("");
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadDashboard();
  }, [area, status, severity]);

  const metrics = useMemo<AlertMetric[]>(() => {
    const summary = dashboard?.summary;
    return [
      { title: "Alertas visíveis", value: summary?.total ?? 0, detail: "Alertas liberados para o perfil atual", icon: BellRing, tone: "neutral" },
      { title: "Críticos", value: summary?.critical ?? 0, detail: "Demandam prioridade de resposta", icon: AlertTriangle, tone: "critical" },
      { title: "Atrasados", value: summary?.late ?? 0, detail: "Prazo de resposta vencido", icon: Clock, tone: "attention" },
      { title: "Escalados", value: summary?.escalated ?? 0, detail: "Encaminhados para diretoria", icon: Flag, tone: "critical" },
      { title: "Reuniões necessárias", value: summary?.requiresMeeting ?? 0, detail: "Exigem alinhamento entre áreas", icon: Users, tone: "attention" },
    ];
  }, [dashboard]);

  async function runAlertAction(action: () => Promise<void>) {
    try {
      await action();
      await loadDashboard();
    } catch (error) {
      setMessage((error as Error).message);
    }
  }

  const alerts = dashboard?.alerts ?? [];
  return (
    <div className="page-shell space-y-5">
      <header className="animate-soft-enter grid gap-4 xl:grid-cols-[minmax(0,1fr)_auto] xl:items-end">
        <div className="max-w-4xl">
          <span className="page-header-kicker">Alertas</span>
          <h1 className="mt-2 text-3xl font-display tracking-tight text-balance md:text-4xl">Alertas gerados por IA</h1>
          <p className="mt-2 max-w-[78ch] text-sm text-muted-foreground text-pretty">
            Central executiva para acompanhar pendências geradas pela IA, entender causa provável e encaminhar a ação correta por área.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2 xl:justify-end">
          <Button variant="outline" onClick={() => void loadDashboard()} disabled={loading}>
            <RefreshCw className="size-4" />
            Atualizar
          </Button>
        </div>
      </header>

      {message && (
        <Alert variant="destructive">
          <AlertTriangle className="size-4" />
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5" aria-label="Resumo de alertas">
        {metrics.map((metric) => (
          <AlertMetricCard key={metric.title} metric={metric} />
        ))}
      </section>

      <section className="rounded-xl border border-border bg-surface p-4">
        <div className="mb-3 flex items-center justify-between gap-3">
          <div>
            <h2 className="text-sm font-semibold">Filtros da fila</h2>
            <p className="text-xs text-muted-foreground">Ajuste área, status e gravidade sem perder o contexto da lista.</p>
          </div>
          <span className="hidden text-xs text-muted-foreground sm:inline">{formatInteger(alerts.length)} registros</span>
        </div>
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1fr)_auto] lg:items-end">
          <FilterSelect label="Área" value={area} values={areas} onChange={setArea} />
          <FilterSelect label="Status" value={status} values={statuses} onChange={(value) => setStatus(value as AiAlertStatus | "Todos")} />
          <FilterSelect label="Gravidade" value={severity} values={severities} onChange={setSeverity} />
          <Button className="h-10 lg:w-auto" variant="secondary" onClick={() => {
            setArea("Todas");
            setStatus("Todos");
            setSeverity("Todas");
          }}>
            <Search className="size-4" />
            Limpar filtros
          </Button>
        </div>
      </section>

      <section className="rounded-xl border border-border bg-surface p-4">
        <div className="space-y-3">
          <div className="flex items-center justify-between gap-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Fila de alertas</h2>
            <span className="text-xs text-muted-foreground">{formatInteger(alerts.length)} registros</span>
          </div>

          {loading && <p className="rounded-lg border border-border bg-muted/20 p-4 text-sm text-muted-foreground">Carregando alertas...</p>}
          {!loading && alerts.length === 0 && (
            <p className="rounded-lg border border-border bg-muted/20 p-4 text-sm text-muted-foreground">Nenhum alerta encontrado para os filtros atuais.</p>
          )}
          {alerts.map((alert) => (
            <AlertListItem
              key={alert.id}
              alert={alert}
              onSelect={() => setSelectedAlert(alert)}
            />
          ))}
        </div>

        <Dialog open={selectedAlert != null} onOpenChange={(open) => !open && setSelectedAlert(null)}>
          <DialogContent className="custom-scrollbar max-h-[90vh] w-[94vw] max-w-5xl overflow-y-auto p-5 sm:p-6">
            {selectedAlert && (
              <>
                <DialogHeader className="pr-8 text-left">
                  <DialogTitle>Detalhe do alerta</DialogTitle>
                  <DialogDescription>Entenda o alerta, revise evidências e execute a próxima ação.</DialogDescription>
                </DialogHeader>
                <AlertDetails
                  alert={selectedAlert}
                  onMarkViewed={() => runAlertAction(() => updateAiAlertStatus({ id: selectedAlert.id, status: "Visualizado", justification: "Visualizado pelo gestor." }).then(() => undefined))}
                  onStartAnalysis={() => runAlertAction(() => updateAiAlertStatus({ id: selectedAlert.id, status: "Em análise", justification: "Análise iniciada." }).then(() => undefined))}
                  onResolve={() => runAlertAction(() => updateAiAlertStatus({ id: selectedAlert.id, status: "Resolvido", justification: "Tratativa concluída." }).then(() => undefined))}
                  onEscalate={() => runAlertAction(() => evaluateAiAlertEscalation(selectedAlert.id))}
                />
              </>
            )}
          </DialogContent>
        </Dialog>
      </section>
    </div>
  );
}

function AlertMetricCard({ metric }: { metric: AlertMetric }) {
  const Icon = metric.icon;
  return (
    <Card className="h-full border-border bg-surface">
      <CardContent className="relative flex h-full flex-col p-5">
        <div className="min-h-11 pr-12">
          <h2 className="flex min-h-10 min-w-0 items-center text-balance text-sm font-semibold leading-snug">{metric.title}</h2>
          <span className="absolute right-5 top-5 inline-flex size-9 shrink-0 items-center justify-center rounded-full border border-primary/10 bg-primary/10 text-primary">
            <Icon className="size-4" />
          </span>
        </div>
        <p className="mt-4 text-3xl font-display tracking-tight">{formatInteger(metric.value)}</p>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <Badge variant="outline" className={toneClass(metric.tone)}>
            <span className={cn("mr-1.5 size-1.5 rounded-full", toneDotClass(metric.tone))} />
            {toneLabel(metric.tone)}
          </Badge>
          <span className="text-xs text-muted-foreground">{metric.detail}</span>
        </div>
      </CardContent>
    </Card>
  );
}

function AlertListItem({ alert, onSelect }: { alert: AiAlertItem; onSelect: () => void }) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className="group grid w-full grid-cols-1 gap-4 rounded-lg border bg-background p-4 text-left transition-colors hover:border-primary/40 hover:bg-primary/[0.03] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/70 lg:grid-cols-[minmax(0,1fr)_minmax(150px,180px)_minmax(150px,180px)_32px] lg:items-center"
    >
      <div className="min-w-0 border-l-2 border-primary/30 pl-3">
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant={severityVariant(alert.severity)}>{alert.severity}</Badge>
          <Badge variant={alert.isLate ? "destructive" : "outline"}>{alert.status}</Badge>
          {alert.requiresMeeting && <Badge variant="outline">Reunião necessária</Badge>}
        </div>
        <h3 className="mt-2 truncate text-sm font-semibold leading-snug">{textOrFallback(alert.title, "Alerta gerado pela IA")}</h3>
        <p className="mt-1 line-clamp-1 text-xs leading-relaxed text-muted-foreground">{textOrFallback(alert.description, "A IA gerou este alerta, mas a descrição detalhada ainda não foi sincronizada.")}</p>
      </div>
      <div className="rounded-lg border border-border/70 bg-muted/15 p-3 text-xs">
        <p className="text-muted-foreground">Área</p>
        <p className="mt-1 truncate font-medium text-foreground">{textOrFallback(alert.responsibleArea)}</p>
      </div>
      <Deadline label="Resposta" value={alert.responseDeadlineAt} late={alert.isLate} />
      <ChevronRight className="size-4 text-primary transition-transform group-hover:translate-x-0.5 lg:justify-self-end" />
    </button>
  );
}

function AlertDetails({
  alert,
  onMarkViewed,
  onStartAnalysis,
  onResolve,
  onEscalate,
}: {
  alert: AiAlertItem;
  onMarkViewed: () => void;
  onStartAnalysis: () => void;
  onResolve: () => void;
  onEscalate: () => void;
}) {
  const statusHistory = buildStatusHistory(alert);
  const notificationHistory = buildNotificationHistory(alert);
  const escalationHistory = buildEscalationHistory(alert);
  const evidence = buildEvidenceItems(alert);
  const relatedTasks = listOrFallback(alert.relatedTasks, ["Validar dados do alerta", "Definir responsável pela tratativa"]);

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <div className="flex flex-wrap gap-2">
            <Badge variant={severityVariant(alert.severity)}>{alert.severity}</Badge>
            <Badge variant={alert.status === "Resolvido" ? "default" : alert.isLate ? "destructive" : "outline"}>{alert.status}</Badge>
            {alert.requiresMeeting && <Badge variant="outline">Reunião necessária</Badge>}
          </div>
          <h2 className="mt-3 text-2xl font-display tracking-tight">{textOrFallback(alert.title, "Alerta gerado pela IA")}</h2>
          <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{textOrFallback(alert.description, "A IA gerou este alerta, mas a descrição detalhada ainda não foi sincronizada.")}</p>
        </div>
        <span className="inline-flex size-11 shrink-0 items-center justify-center rounded-full border border-primary/10 bg-primary/10 text-primary">
          <Eye className="size-5" />
        </span>
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <Info label="Área responsável" value={textOrFallback(alert.responsibleArea)} />
        <Info label="Gestor responsável" value={textOrFallback(alert.responsibleManager, `Gestor de ${textOrFallback(alert.responsibleArea, "área")}`)} />
        <Info label="Áreas envolvidas" value={listOrFallback(alert.involvedAreas, [textOrFallback(alert.responsibleArea, "Área responsável")]).join(", ")} />
        <Info label="Notificações" value={formatInteger(alert.notificationCount)} />
        <Info label="Criado em" value={formatDate(alert.createdAt, "Data de criação não informada")} />
        <Info label="Origem" value={textOrFallback(alert.origin, "IA")} />
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <InsightBlock title="Sugestão da IA" icon={Sparkles} value={textOrFallback(alert.aiSuggestion, "Revisar o alerta com o gestor responsável, validar os dados operacionais e registrar a decisão tomada.")} />
        <InsightBlock title="Impacto esperado" icon={AlertTriangle} value={textOrFallback(alert.expectedImpact, "Impacto ainda não calculado pela IA. Use a gravidade, o prazo e a área responsável como base inicial de priorização.")} />
      </div>

      <section className="rounded-xl border border-border p-4">
        <div className="flex items-center gap-2">
          <ListChecks className="size-4 text-primary" />
          <h3 className="text-sm font-semibold">Evidências usadas pela IA</h3>
        </div>
        <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
          {evidence.map((item) => (
            <Info key={item.label} label={item.label} value={item.value} />
          ))}
        </div>
      </section>

      <section className="rounded-xl border border-border p-4">
        <h3 className="text-sm font-semibold">Tarefas recomendadas</h3>
        <ul className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
          {relatedTasks.map((task) => (
            <li key={task} className="flex gap-2 rounded-lg border bg-muted/20 p-3 text-sm text-muted-foreground">
              <ChevronRight className="mt-0.5 size-4 shrink-0 text-primary" />
              <span>{task}</span>
            </li>
          ))}
        </ul>
      </section>

      <div className="flex flex-wrap gap-2">
        <Button size="sm" variant="outline" onClick={onMarkViewed}>
          <Eye className="size-4" />
          Visualizado
        </Button>
        <Button size="sm" variant="secondary" onClick={onStartAnalysis}>
          <Clock className="size-4" />
          Em análise
        </Button>
        <Button size="sm" onClick={onResolve}>
          <CheckCircle2 className="size-4" />
          Resolver
        </Button>
        <Button size="sm" variant="destructive" onClick={onEscalate}>
          <Flag className="size-4" />
          Avaliar escalonamento
        </Button>
      </div>

      <div className="grid grid-cols-1 gap-3 xl:grid-cols-3">
        <HistoryBlock title="Histórico de status" items={statusHistory.map((item, index) => ({
          key: `${item.changedAt}-${index}`,
          title: `${item.previousStatus || "Criação"} → ${item.newStatus}`,
          subtitle: `${formatDate(item.changedAt, "Data não informada")} · ${item.changedBy || "Sistema"}${item.justification ? ` · ${item.justification}` : ""}`,
        }))} />
        <HistoryBlock title="Notificações enviadas" items={notificationHistory.map((item, index) => ({
          key: `${item.sentAt}-${index}`,
          title: `${textOrFallback(item.channel, "Sistema")} · ${textOrFallback(item.recipient, "destinatário não informado")}`,
          subtitle: `${formatDate(item.sentAt, "Data não informada")} · ${textOrFallback(item.reason, "Motivo não informado")}`,
        }))} />
        <HistoryBlock title="Escalonamentos" items={escalationHistory.map((item, index) => ({
          key: `${item.escalatedAt}-${index}`,
          title: `${textOrFallback(item.fromRecipient, "Sistema")} → ${textOrFallback(item.toRecipient, "Diretoria")}`,
          subtitle: `${formatDate(item.escalatedAt, "Data não informada")} · ${textOrFallback(item.reason, "Motivo não informado")}`,
        }))} />
      </div>
    </div>
  );
}

function InsightBlock({ title, value, icon: Icon }: { title: string; value: string; icon: ComponentType<{ className?: string }> }) {
  return (
    <div className="rounded-xl border border-border p-4">
      <div className="flex items-center gap-2">
        <Icon className="size-4 text-primary" />
        <p className="text-sm font-semibold">{title}</p>
      </div>
      <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{value}</p>
    </div>
  );
}

function HistoryBlock({ title, items }: { title: string; items: Array<{ key: string; title: string; subtitle: string }> }) {
  return (
    <div className="rounded-xl border border-border p-4">
      <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{title}</p>
      <div className="space-y-2">
        {items.map((item) => (
          <div key={item.key} className="rounded-lg bg-muted/30 p-3 text-xs">
            <p className="font-medium">{item.title}</p>
            <p className="mt-1 leading-relaxed text-muted-foreground">{item.subtitle}</p>
          </div>
        ))}
      </div>
    </div>
  );
}

function FilterSelect({ label, value, values, onChange }: { label: string; value: string; values: readonly string[]; onChange: (value: string) => void }) {
  return (
    <label className="space-y-1 text-sm">
      <span className="text-xs text-muted-foreground">{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)} className="h-9 w-full rounded-md border border-input bg-background px-3 text-sm outline-none focus:ring-2 focus:ring-ring/70">
        {values.map((item) => <option key={item} value={item}>{item}</option>)}
      </select>
    </label>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border bg-muted/15 p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 break-words text-sm font-medium">{value}</p>
    </div>
  );
}

function Deadline({ label, value, late }: { label: string; value?: string | null; late: boolean }) {
  return (
    <div className={cn("rounded-lg border border-border/70 bg-muted/15 p-3 text-xs", late && "border-destructive/30 bg-destructive/5")}>
      <p className="text-muted-foreground">{label}</p>
      <p className={cn("mt-1 truncate font-medium", late ? "text-destructive" : "text-foreground")}>{formatDate(value, "Sem prazo")}</p>
    </div>
  );
}

function severityVariant(severity: string): "default" | "secondary" | "destructive" | "outline" {
  if (severity === "Crítico") return "destructive";
  if (severity === "Alto") return "secondary";
  if (severity === "Médio") return "default";
  return "outline";
}

function toneClass(tone: AlertMetric["tone"]): string {
  if (tone === "healthy") return "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300";
  if (tone === "attention") return "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300";
  if (tone === "critical") return "border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300";
  return "border-border bg-background text-foreground";
}

function toneDotClass(tone: AlertMetric["tone"]): string {
  if (tone === "healthy") return "bg-emerald-500";
  if (tone === "attention") return "bg-amber-500";
  if (tone === "critical") return "bg-red-500";
  return "bg-primary";
}

function toneTextClass(tone: AlertMetric["tone"]): string {
  if (tone === "attention") return "text-amber-600";
  if (tone === "critical") return "text-red-600";
  if (tone === "healthy") return "text-emerald-600";
  return "text-foreground";
}

function toneLabel(tone: AlertMetric["tone"]): string {
  if (tone === "healthy") return "Saudável";
  if (tone === "attention") return "Atenção";
  if (tone === "critical") return "Crítico";
  return "Monitorado";
}

function formatDate(value?: string | null, fallback = "-"): string {
  if (!value) return fallback;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("pt-BR");
}

function formatInteger(value: number): string {
  return new Intl.NumberFormat("pt-BR").format(value);
}

function textOrFallback(value: string | null | undefined, fallback = emptyText): string {
  const text = value?.trim();
  return text ? text : fallback;
}

function listOrFallback(values: string[] | null | undefined, fallback: string[]): string[] {
  const cleaned = (values ?? []).map((value) => value.trim()).filter(Boolean);
  return cleaned.length > 0 ? cleaned : fallback;
}

function buildEvidenceItems(alert: AiAlertItem): Array<{ label: string; value: string }> {
  try {
    const parsed = JSON.parse(alert.evidenceJson || "{}");
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      const entries = Object.entries(parsed)
        .filter(([, value]) => value !== null && value !== undefined && String(value).trim() !== "")
        .map(([label, value]) => ({ label, value: String(value) }));
      if (entries.length > 0) return entries;
    }
  } catch {
    const evidenceText = alert.evidenceJson.trim();
    if (evidenceText) return [{ label: "Evidência", value: evidenceText }];
  }

  return [
    { label: "Base de análise", value: "Sem evidências estruturadas sincronizadas." },
    { label: "Próximo passo", value: "Validar a origem dos dados com a área responsável." },
  ];
}

function buildStatusHistory(alert: AiAlertItem): AiAlertItem["statusHistory"] {
  if (alert.statusHistory.length > 0) return alert.statusHistory;
  return [{
    previousStatus: "",
    newStatus: textOrFallback(alert.status, "Novo"),
    changedBy: "Sistema",
    justification: "Registro inicial do alerta gerado pela IA.",
    changedAt: alert.createdAt,
  }];
}

function buildNotificationHistory(alert: AiAlertItem): AiAlertItem["notificationHistory"] {
  if (alert.notificationHistory.length > 0) return alert.notificationHistory;
  return [{
    recipient: textOrFallback(alert.responsibleManager, "Gestor responsável"),
    channel: "Painel",
    reason: alert.notificationCount > 0 ? `${formatInteger(alert.notificationCount)} notificação(ões) registrada(s).` : "Notificação disponível no painel de alertas.",
    sentAt: alert.lastNotificationAt || alert.createdAt,
  }];
}

function buildEscalationHistory(alert: AiAlertItem): AiAlertItem["escalationHistory"] {
  if (alert.escalationHistory.length > 0) return alert.escalationHistory;
  if (alert.escalatedAt || alert.status === "Escalado para diretoria") {
    return [{
      fromRecipient: textOrFallback(alert.responsibleManager, "Gestor responsável"),
      toRecipient: "Diretoria",
      reason: "Alerta crítico ou fora do prazo encaminhado para decisão.",
      escalatedAt: alert.escalatedAt || alert.createdAt,
    }];
  }

  return [{
    fromRecipient: "Sistema",
    toRecipient: "Diretoria",
    reason: "Sem escalonamento registrado até o momento.",
    escalatedAt: "",
  }];
}
