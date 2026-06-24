import { createFileRoute, redirect } from "@tanstack/react-router";
import {
  AlertTriangle,
  ArrowDownRight,
  ArrowUpRight,
  Banknote,
  BarChart3,
  Calculator,
  CheckCircle2,
  ChevronRight,
  Clock,
  FileCheck2,
  PieChart,
  ShieldCheck,
  Users,
} from "lucide-react";
import { useMemo, useState, type ComponentType } from "react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { canCurrentUserAccessAdministrativeArea } from "@/lib/auth";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/administrativo")({
  beforeLoad: () => {
    if (!canCurrentUserAccessAdministrativeArea()) {
      throw redirect({ to: "/" });
    }
  },
  component: AdministrativoPage,
});

type MetricStatus = "healthy" | "attention" | "critical";
type AdminMetricId = "cash-flow" | "receivables" | "payables" | "documents" | "headcount" | "compliance" | "budget" | "cycle-time";

type AdminMetric = {
  id: AdminMetricId;
  area: string;
  title: string;
  value: string;
  status: MetricStatus;
  change: number | null;
  lowerIsBetter: boolean;
  icon: ComponentType<{ className?: string }>;
  description: string;
  meaning: string;
  formula: string;
  calculation: string;
  dataUsed: string[];
  factors: string[];
  recommendations: string[];
  history: Array<{ label: string; value: number; formattedValue: string }>;
  investigation: Array<{ title: string; detail: string; value: string; critical?: boolean }>;
};

const percentFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 });
const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL", maximumFractionDigits: 0 });

function formatPercent(value: number): string {
  return `${percentFormatter.format(value)}%`;
}

function formatChange(change: number | null): string {
  if (change == null) return "Sem base anterior";
  if (change === 0) return "Estável vs. período anterior";
  return `${change > 0 ? "+" : ""}${formatPercent(change)} vs. período anterior`;
}

function statusPresentation(status: MetricStatus): { label: string; className: string; dotClassName: string } {
  if (status === "healthy") return { label: "Saudável", className: "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300", dotClassName: "bg-emerald-500" };
  if (status === "attention") return { label: "Atenção", className: "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300", dotClassName: "bg-amber-500" };
  return { label: "Crítico", className: "border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300", dotClassName: "bg-red-500" };
}

const adminMetrics: AdminMetric[] = [
  {
    id: "cash-flow",
    area: "Financeiro",
    title: "Saldo de Caixa Projetado",
    value: currencyFormatter.format(428000),
    status: "healthy",
    change: 6.8,
    lowerIsBetter: false,
    icon: Banknote,
    description: "Projeção demonstrativa de caixa para os próximos 30 dias.",
    meaning: "Mostra se a empresa teria folga financeira para cumprir compromissos previstos sem depender de entradas extraordinárias.",
    formula: "Saldo inicial + recebimentos previstos − pagamentos previstos",
    calculation: "Base falsa: R$ 310 mil de saldo inicial + R$ 742 mil em recebimentos simulados − R$ 624 mil em pagamentos simulados.",
    dataUsed: ["Saldo bancário simulado", "Contas a receber simuladas", "Contas a pagar simuladas", "Calendário financeiro fictício"],
    factors: ["Concentração de recebimentos no fim do mês", "Pagamentos de fornecedores congelados", "Folha concentrada na primeira semana"],
    recommendations: ["Antecipar recebíveis de maior valor", "Separar caixa mínimo operacional", "Negociar vencimentos críticos com fornecedores"],
    history: [
      { label: "Jan", value: 315000, formattedValue: currencyFormatter.format(315000) },
      { label: "Fev", value: 340000, formattedValue: currencyFormatter.format(340000) },
      { label: "Mar", value: 366000, formattedValue: currencyFormatter.format(366000) },
      { label: "Abr", value: 401000, formattedValue: currencyFormatter.format(401000) },
      { label: "Mai", value: 398000, formattedValue: currencyFormatter.format(398000) },
      { label: "Jun", value: 428000, formattedValue: currencyFormatter.format(428000) },
    ],
    investigation: [
      { title: "Recebimentos concentrados", detail: "43% das entradas fictícias vencem nos últimos cinco dias úteis.", value: "43%", critical: false },
      { title: "Folha e encargos", detail: "Compromissos simulados de RH pressionam a primeira semana.", value: currencyFormatter.format(188000) },
      { title: "Fornecedores críticos", detail: "Três fornecedores concentram obrigações de curto prazo.", value: currencyFormatter.format(231000) },
    ],
  },
  {
    id: "receivables",
    area: "Cobrança",
    title: "Inadimplência Simulada",
    value: formatPercent(4.7),
    status: "attention",
    change: 1.2,
    lowerIsBetter: true,
    icon: AlertTriangle,
    description: "Percentual fictício de títulos vencidos sobre o contas a receber.",
    meaning: "Ajuda a estimar risco de caixa caso clientes relevantes atrasem pagamentos.",
    formula: "(Valor vencido ÷ contas a receber total) × 100",
    calculation: "Base falsa: R$ 51 mil vencidos divididos por R$ 1,08 milhão em recebíveis simulados.",
    dataUsed: ["Títulos fictícios", "Clientes simulados", "Data de vencimento", "Valor original"],
    factors: ["Cliente recorrente com atraso acima de 15 dias", "Boletos em contestação", "Prazo médio alongado no canal atacado"],
    recommendations: ["Priorizar cobrança dos maiores saldos", "Criar régua de contato automática", "Bloquear novo prazo para reincidentes"],
    history: [
      { label: "Jan", value: 2.8, formattedValue: formatPercent(2.8) },
      { label: "Fev", value: 3.1, formattedValue: formatPercent(3.1) },
      { label: "Mar", value: 3.5, formattedValue: formatPercent(3.5) },
      { label: "Abr", value: 4.0, formattedValue: formatPercent(4.0) },
      { label: "Mai", value: 3.5, formattedValue: formatPercent(3.5) },
      { label: "Jun", value: 4.7, formattedValue: formatPercent(4.7) },
    ],
    investigation: [
      { title: "Atacado interior", detail: "Maior concentração fictícia de títulos vencidos.", value: currencyFormatter.format(22000), critical: true },
      { title: "Títulos em contestação", detail: "Notas com divergência comercial ainda sem tratativa.", value: "8 títulos" },
      { title: "Atraso médio", detail: "Tempo médio simulado após vencimento.", value: "11 dias" },
    ],
  },
  {
    id: "payables",
    area: "Contas a pagar",
    title: "Pagamentos no Prazo",
    value: formatPercent(96.2),
    status: "healthy",
    change: -0.4,
    lowerIsBetter: false,
    icon: CheckCircle2,
    description: "Percentual demonstrativo de obrigações pagas até o vencimento.",
    meaning: "Indica disciplina financeira e reduz risco de multas ou bloqueios de fornecimento.",
    formula: "(Pagamentos no prazo ÷ pagamentos totais) × 100",
    calculation: "Base falsa: 151 pagamentos no prazo de um total de 157 obrigações simuladas.",
    dataUsed: ["Contas a pagar simuladas", "Data de vencimento", "Data de baixa", "Fornecedor"],
    factors: ["Aprovação manual de despesas", "Notas fiscais recebidas com atraso", "Pagamentos dependentes de conferência"],
    recommendations: ["Antecipar conferência fiscal", "Criar alerta de aprovação pendente", "Separar fornecedores estratégicos"],
    history: [
      { label: "Jan", value: 93.8, formattedValue: formatPercent(93.8) },
      { label: "Fev", value: 95.1, formattedValue: formatPercent(95.1) },
      { label: "Mar", value: 95.8, formattedValue: formatPercent(95.8) },
      { label: "Abr", value: 96.5, formattedValue: formatPercent(96.5) },
      { label: "Mai", value: 96.6, formattedValue: formatPercent(96.6) },
      { label: "Jun", value: 96.2, formattedValue: formatPercent(96.2) },
    ],
    investigation: [
      { title: "Aprovações atrasadas", detail: "Pedidos internos fictícios ficaram sem aprovação final.", value: "6" },
      { title: "Notas sem conferência", detail: "Documentos aguardando validação administrativa.", value: "4" },
      { title: "Multas evitadas", detail: "Valor estimado preservado por pagamentos no prazo.", value: currencyFormatter.format(12800) },
    ],
  },
  {
    id: "documents",
    area: "Fiscal",
    title: "Documentos Pendentes",
    value: "18",
    status: "attention",
    change: -10.0,
    lowerIsBetter: true,
    icon: FileCheck2,
    description: "Quantidade fictícia de documentos aguardando conferência.",
    meaning: "Mostra o volume represado que pode atrasar pagamentos, fechamento fiscal ou auditoria.",
    formula: "Contagem de documentos com status pendente",
    calculation: "Base falsa: foram somados documentos sem conferência, sem anexo ou com divergência de valor.",
    dataUsed: ["Notas simuladas", "Status de conferência", "Centro de custo", "Responsável"],
    factors: ["Documentos sem pedido vinculado", "Divergência entre valor e contrato", "Anexos ausentes"],
    recommendations: ["Priorizar documentos acima de R$ 10 mil", "Cobrar anexos por responsável", "Criar bloqueio para recorrência sem pedido"],
    history: [
      { label: "Jan", value: 32, formattedValue: "32" },
      { label: "Fev", value: 29, formattedValue: "29" },
      { label: "Mar", value: 26, formattedValue: "26" },
      { label: "Abr", value: 22, formattedValue: "22" },
      { label: "Mai", value: 20, formattedValue: "20" },
      { label: "Jun", value: 18, formattedValue: "18" },
    ],
    investigation: [
      { title: "Sem pedido", detail: "Documentos fictícios sem vínculo de compra.", value: "7", critical: true },
      { title: "Divergência de valor", detail: "Diferença entre valor informado e contrato simulado.", value: "5" },
      { title: "Anexo ausente", detail: "Evidências obrigatórias não anexadas.", value: "6" },
    ],
  },
  {
    id: "headcount",
    area: "RH",
    title: "Absenteísmo Administrativo",
    value: formatPercent(2.4),
    status: "healthy",
    change: -0.8,
    lowerIsBetter: true,
    icon: Users,
    description: "Taxa demonstrativa de ausências na equipe administrativa.",
    meaning: "Ajuda a avaliar risco operacional de atrasos por falta de capacidade na rotina administrativa.",
    formula: "(Horas ausentes ÷ horas planejadas) × 100",
    calculation: "Base falsa: 42 horas ausentes divididas por 1.760 horas planejadas.",
    dataUsed: ["Escala simulada", "Apontamentos fictícios", "Centro administrativo", "Motivo da ausência"],
    factors: ["Ausência pontual", "Treinamentos internos", "Revezamento em fechamento mensal"],
    recommendations: ["Planejar cobertura no fechamento", "Registrar substitutos por atividade", "Monitorar áreas com concentração de ausência"],
    history: [
      { label: "Jan", value: 3.8, formattedValue: formatPercent(3.8) },
      { label: "Fev", value: 3.2, formattedValue: formatPercent(3.2) },
      { label: "Mar", value: 3.0, formattedValue: formatPercent(3.0) },
      { label: "Abr", value: 2.9, formattedValue: formatPercent(2.9) },
      { label: "Mai", value: 3.2, formattedValue: formatPercent(3.2) },
      { label: "Jun", value: 2.4, formattedValue: formatPercent(2.4) },
    ],
    investigation: [
      { title: "Fechamento mensal", detail: "Cobertura simulada no período crítico.", value: "92%" },
      { title: "Treinamentos", detail: "Horas administrativas alocadas em capacitação.", value: "18h" },
      { title: "Backlog associado", detail: "Pendências administrativas geradas no período.", value: "3" },
    ],
  },
  {
    id: "compliance",
    area: "Governança",
    title: "Conformidade de Processos",
    value: formatPercent(91.5),
    status: "attention",
    change: 2.1,
    lowerIsBetter: false,
    icon: ShieldCheck,
    description: "Percentual fictício de processos com checklist completo.",
    meaning: "Mostra aderência a controles internos, aprovações e documentação mínima.",
    formula: "(Processos conformes ÷ processos auditados) × 100",
    calculation: "Base falsa: 86 processos conformes em 94 processos auditados.",
    dataUsed: ["Checklists simulados", "Aprovações", "Evidências", "Responsável pelo processo"],
    factors: ["Evidência anexada fora do prazo", "Aprovação em alçada incorreta", "Checklist incompleto"],
    recommendations: ["Revisar processos abaixo de 90%", "Automatizar cobrança de evidências", "Treinar responsáveis por alçada"],
    history: [
      { label: "Jan", value: 87.2, formattedValue: formatPercent(87.2) },
      { label: "Fev", value: 88.1, formattedValue: formatPercent(88.1) },
      { label: "Mar", value: 89.4, formattedValue: formatPercent(89.4) },
      { label: "Abr", value: 90.2, formattedValue: formatPercent(90.2) },
      { label: "Mai", value: 89.4, formattedValue: formatPercent(89.4) },
      { label: "Jun", value: 91.5, formattedValue: formatPercent(91.5) },
    ],
    investigation: [
      { title: "Alçada incorreta", detail: "Aprovações fictícias fora da regra definida.", value: "3" },
      { title: "Evidência tardia", detail: "Documentos anexados após a data limite.", value: "5" },
      { title: "Processos críticos", detail: "Processos simulados com impacto financeiro alto.", value: "2", critical: true },
    ],
  },
  {
    id: "budget",
    area: "Orçamento",
    title: "Desvio Orçamentário",
    value: formatPercent(3.6),
    status: "attention",
    change: 0.9,
    lowerIsBetter: true,
    icon: PieChart,
    description: "Variação fictícia entre realizado e orçamento planejado.",
    meaning: "Indica se os gastos administrativos estão respeitando o orçamento de referência.",
    formula: "[(Realizado − Orçado) ÷ Orçado] × 100",
    calculation: "Base falsa: R$ 518 mil realizados contra R$ 500 mil orçados.",
    dataUsed: ["Orçamento simulado", "Despesas realizadas fictícias", "Centro de custo", "Conta contábil"],
    factors: ["Serviços terceirizados acima do plano", "Manutenção corretiva", "Compras emergenciais"],
    recommendations: ["Separar despesas recorrentes e emergenciais", "Revisar contratos com maior desvio", "Criar trava para compras sem orçamento"],
    history: [
      { label: "Jan", value: 1.8, formattedValue: formatPercent(1.8) },
      { label: "Fev", value: 2.4, formattedValue: formatPercent(2.4) },
      { label: "Mar", value: 2.7, formattedValue: formatPercent(2.7) },
      { label: "Abr", value: 3.1, formattedValue: formatPercent(3.1) },
      { label: "Mai", value: 2.7, formattedValue: formatPercent(2.7) },
      { label: "Jun", value: 3.6, formattedValue: formatPercent(3.6) },
    ],
    investigation: [
      { title: "Terceiros", detail: "Conta fictícia com maior desvio.", value: formatPercent(7.9), critical: true },
      { title: "Manutenção", detail: "Gastos corretivos sem previsão inicial.", value: currencyFormatter.format(14000) },
      { title: "Compras emergenciais", detail: "Pedidos fora do ciclo orçamentário.", value: "11" },
    ],
  },
  {
    id: "cycle-time",
    area: "Operação interna",
    title: "Tempo de Aprovação",
    value: "18h",
    status: "healthy",
    change: -12.5,
    lowerIsBetter: true,
    icon: Clock,
    description: "Tempo médio fictício para concluir aprovações administrativas.",
    meaning: "Mede a velocidade da rotina administrativa e o risco de travar compras, pagamentos ou decisões.",
    formula: "Média de data/hora de aprovação − data/hora de solicitação",
    calculation: "Base falsa: 214 solicitações com média de 18 horas entre abertura e aprovação final.",
    dataUsed: ["Solicitações simuladas", "Data de abertura", "Data de aprovação", "Responsável", "Alçada"],
    factors: ["Aprovação concentrada em poucos gestores", "Solicitações sem documentação", "Alçadas acima do valor padrão"],
    recommendations: ["Delegar aprovações de baixo risco", "Bloquear solicitação sem documentação", "Criar alerta de aprovação acima de 24h"],
    history: [
      { label: "Jan", value: 31, formattedValue: "31h" },
      { label: "Fev", value: 28, formattedValue: "28h" },
      { label: "Mar", value: 24, formattedValue: "24h" },
      { label: "Abr", value: 22, formattedValue: "22h" },
      { label: "Mai", value: 21, formattedValue: "21h" },
      { label: "Jun", value: 18, formattedValue: "18h" },
    ],
    investigation: [
      { title: "Aprovações acima de 24h", detail: "Solicitações fictícias fora do alvo operacional.", value: "17" },
      { title: "Documentação incompleta", detail: "Principal causa simulada de retorno.", value: "9" },
      { title: "Alçada diretoria", detail: "Casos que exigiram validação superior.", value: "5" },
    ],
  },
];

function AdministrativoPage() {
  const [selectedMetric, setSelectedMetric] = useState<AdminMetricId | null>(null);
  const selectedCard = useMemo(
    () => adminMetrics.find((metric) => metric.id === selectedMetric) ?? null,
    [selectedMetric],
  );

  return (
    <div className="page-shell">
      <header className="animate-soft-enter mb-8 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <span className="page-header-kicker">Smart Core / Administrativo</span>
          <h1 className="mt-2 mb-2 text-4xl font-display tracking-tight text-balance">Administrativo da Empresa</h1>
          <p className="max-w-[70ch] text-muted-foreground text-pretty">
            Painel executivo demonstrativo para rotina administrativa, caixa, conformidade e aprovações.
          </p>
        </div>
        <Badge variant="outline" className="w-fit border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300">
          Dados demonstrativos
        </Badge>
      </header>

      <section className="mb-5 rounded-xl border border-border bg-surface p-5 animate-soft-enter">
        <div className="flex items-center gap-3">
          <span className="inline-flex size-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <Calculator className="size-5" />
          </span>
          <div>
            <p className="text-[10px] font-mono uppercase tracking-widest text-primary">Base simulada</p>
            <h2 className="mt-1 text-xl font-display">Indicadores administrativos</h2>
            <p className="mt-1 max-w-[75ch] text-sm text-muted-foreground">
              Todos os KPIs abaixo são falsos e servem para validar layout, narrativa, histórico e explicação de cálculo.
            </p>
          </div>
        </div>
      </section>

      <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5" aria-label="Indicadores administrativos">
        {adminMetrics.map((card) => (
          <ExecutiveMetricCard key={card.id} card={card} onSelect={() => setSelectedMetric(card.id)} />
        ))}
      </section>

      <MetricDetailsDialog metric={selectedCard} onOpenChange={(open) => !open && setSelectedMetric(null)} />
    </div>
  );
}

function ExecutiveMetricCard({ card, onSelect }: { card: AdminMetric; onSelect: () => void }) {
  const status = statusPresentation(card.status);
  const favorableTrend = card.change != null && card.change !== 0 && ((card.change < 0) === card.lowerIsBetter);
  const TrendIcon = card.change == null || card.change === 0 ? BarChart3 : card.change > 0 ? ArrowUpRight : ArrowDownRight;

  return (
    <button type="button" onClick={onSelect} className="h-full text-left">
      <Card className="h-full cursor-pointer hover:border-primary/40">
        <CardContent className="flex h-full flex-col p-5">
          <div className="grid min-h-11 grid-cols-[minmax(0,1fr)_auto] items-center gap-4">
            <h2 className="flex min-h-10 min-w-0 items-center text-balance text-sm font-semibold leading-snug">{card.title}</h2>
            <span className="inline-flex size-9 shrink-0 translate-y-1.5 items-center justify-center self-center rounded-full border border-primary/10 bg-primary/10 text-primary"><card.icon className="size-4" /></span>
          </div>
          <p className="mt-4 text-3xl font-display tracking-tight">{card.value}</p>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Badge variant="outline" className={status.className}><span className={cn("mr-1.5 size-1.5 rounded-full", status.dotClassName)} />{status.label}</Badge>
            <span className={cn("inline-flex items-center gap-1 text-xs", card.change == null ? "text-muted-foreground" : favorableTrend ? "text-emerald-600" : "text-amber-600")}>
              <TrendIcon className="size-3.5" />{formatChange(card.change)}
            </span>
          </div>
        </CardContent>
      </Card>
    </button>
  );
}

function MetricDetailsDialog({ metric, onOpenChange }: { metric: AdminMetric | null; onOpenChange: (open: boolean) => void }) {
  return (
    <Dialog open={metric != null} onOpenChange={onOpenChange}>
      <DialogContent className="custom-scrollbar max-h-[90vh] w-[94vw] max-w-2xl overflow-y-auto p-5 sm:p-6">
        {metric && (
          <>
            <DialogHeader className="pr-8 text-left">
              <div className="flex flex-wrap items-center gap-2">
                <DialogTitle className="text-xl">{metric.title}</DialogTitle>
                <MetricStatusBadge status={metric.status} />
              </div>
              <DialogDescription>Nível 2 · Entender o indicador demonstrativo</DialogDescription>
            </DialogHeader>

            <div className="mt-6 space-y-6">
              <div className="rounded-xl border bg-muted/20 p-4">
                <p className="text-sm font-semibold">O que significa</p>
                <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{metric.meaning}</p>
              </div>

              <div className="grid gap-3 md:grid-cols-2">
                <InfoBlock title="Fórmula" value={metric.formula} />
                <InfoBlock title="Como foi calculado" value={metric.calculation} />
              </div>

              <MetricHistoryChart history={metric.history} />

              <div>
                <h3 className="text-sm font-semibold">Dados usados</h3>
                <div className="mt-3 flex flex-wrap gap-2">
                  {metric.dataUsed.map((item) => <Badge key={item} variant="outline">{item}</Badge>)}
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <ListBlock title="Fatores que influenciam" items={metric.factors} />
                <ListBlock title="Ações recomendadas" items={metric.recommendations} />
              </div>

              <div>
                <h3 className="text-sm font-semibold">Histórico e investigação</h3>
                <div className="mt-3 space-y-3">
                  {metric.investigation.map((item) => (
                    <div key={item.title} className="rounded-lg border p-4">
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <p className="text-sm font-semibold">{item.title}</p>
                          <p className="mt-1 text-xs text-muted-foreground">{item.detail}</p>
                        </div>
                        <Badge variant={item.critical ? "destructive" : "outline"}>{item.value}</Badge>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

function MetricStatusBadge({ status }: { status: MetricStatus }) {
  const presentation = statusPresentation(status);
  return <Badge variant="outline" className={presentation.className}><span className={cn("mr-1.5 size-1.5 rounded-full", presentation.dotClassName)} />{presentation.label}</Badge>;
}

function InfoBlock({ title, value }: { title: string; value: string }) {
  return <div className="rounded-lg border bg-surface p-4"><p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{title}</p><p className="mt-2 text-sm leading-relaxed">{value}</p></div>;
}

function ListBlock({ title, items }: { title: string; items: string[] }) {
  return <div className="rounded-lg border bg-surface p-4"><p className="text-sm font-semibold">{title}</p><ul className="mt-3 space-y-2">{items.map((item) => <li key={item} className="flex gap-2 text-sm text-muted-foreground"><ChevronRight className="mt-0.5 size-4 shrink-0 text-primary" /><span>{item}</span></li>)}</ul></div>;
}

function MetricHistoryChart({ history }: { history: AdminMetric["history"] }) {
  const maximum = Math.max(1, ...history.map((point) => point.value));
  return (
    <div>
      <div className="flex items-center justify-between"><h3 className="text-sm font-semibold">Histórico do indicador</h3><span className="text-xs text-muted-foreground">{history.length} períodos</span></div>
      <div className="mt-3 flex h-48 items-end gap-2 rounded-lg border bg-muted/15 p-4">
        {history.map((point) => (
          <div key={point.label} className="flex h-full min-w-0 flex-1 flex-col items-center justify-end gap-2" title={`${point.label}: ${point.formattedValue}`}>
            <span className="text-[10px] text-muted-foreground">{point.formattedValue}</span>
            <div className="w-full rounded-t bg-primary/75 transition-all" style={{ height: `${Math.max(4, (point.value / maximum) * 100)}%` }} />
            <span className="text-[10px] text-muted-foreground">{point.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
