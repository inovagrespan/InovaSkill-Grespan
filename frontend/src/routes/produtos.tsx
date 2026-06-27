import { createFileRoute } from "@tanstack/react-router";
import {
  AlertTriangle,
  ArrowDownRight,
  ArrowUpRight,
  ChevronRight,
  Factory,
  Gauge,
  PackageCheck,
  PackageX,
  Recycle,
  TimerReset,
  TrendingUp,
  Wrench,
} from "lucide-react";
import { useMemo, useState, type ComponentType } from "react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/produtos")({
  component: ProducaoPage,
});

type MetricStatus = "healthy" | "attention" | "critical";
type ProductionMetricId = "efficiency" | "plan" | "scrap" | "setup" | "oee" | "late-orders" | "material" | "capacity";

type ProductionMetric = {
  id: ProductionMetricId;
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
const integerFormatter = new Intl.NumberFormat("pt-BR");

function formatPercent(value: number): string {
  return `${percentFormatter.format(value)}%`;
}

function formatChange(change: number | null): string {
  if (change == null) return "Sem base anterior";
  if (change === 0) return "Estável vs. período anterior";
  return `${change > 0 ? "+" : ""}${formatPercent(change)} vs. período anterior`;
}

function statusPresentation(status: MetricStatus): { label: string; className: string; dotClassName: string } {
  if (status === "healthy") {
    return {
      label: "Saudável",
      className: "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300",
      dotClassName: "bg-emerald-500",
    };
  }

  if (status === "attention") {
    return {
      label: "Atenção",
      className: "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300",
      dotClassName: "bg-amber-500",
    };
  }

  return {
    label: "Crítico",
    className: "border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300",
    dotClassName: "bg-red-500",
  };
}

const productionMetrics: ProductionMetric[] = [
  {
    id: "efficiency",
    area: "Linha",
    title: "Eficiência de Produção",
    value: formatPercent(87.4),
    status: "healthy",
    change: 4.1,
    lowerIsBetter: false,
    icon: Factory,
    description: "Percentual demonstrativo entre produção realizada e capacidade planejada.",
    meaning: "Indica quanto da capacidade planejada foi convertida em produção útil no período simulado.",
    formula: "(Unidades produzidas boas ÷ unidades planejadas) × 100",
    calculation: "Base falsa: 43.720 unidades boas divididas por 50.000 unidades planejadas.",
    dataUsed: ["Ordens de produção fictícias", "Quantidade planejada", "Quantidade boa", "Turno e linha simulados"],
    factors: ["Paradas curtas na linha 2", "Troca de operadores no turno B", "Ritmo acima do plano na linha 1"],
    recommendations: ["Replicar setup da linha 1", "Mapear microparadas por turno", "Revisar metas das ordens de baixa complexidade"],
    history: [
      { label: "Jan", value: 80.2, formattedValue: formatPercent(80.2) },
      { label: "Fev", value: 82.6, formattedValue: formatPercent(82.6) },
      { label: "Mar", value: 84.9, formattedValue: formatPercent(84.9) },
      { label: "Abr", value: 85.8, formattedValue: formatPercent(85.8) },
      { label: "Mai", value: 83.3, formattedValue: formatPercent(83.3) },
      { label: "Jun", value: 87.4, formattedValue: formatPercent(87.4) },
    ],
    investigation: [
      { title: "Linha 1 acima do plano", detail: "Produção fictícia superou a meta em produtos de baixo setup.", value: "+6,2%" },
      { title: "Microparadas", detail: "Ocorrências simuladas com menos de 10 minutos concentradas no turno B.", value: "31", critical: true },
      { title: "Ordens reprogramadas", detail: "Ordens deslocadas para evitar gargalo de embalagem.", value: "8" },
    ],
  },
  {
    id: "plan",
    area: "PCP",
    title: "Cumprimento do Plano",
    value: formatPercent(92.1),
    status: "healthy",
    change: 2.7,
    lowerIsBetter: false,
    icon: PackageCheck,
    description: "Percentual fictício de ordens concluídas dentro do plano de produção.",
    meaning: "Mostra a aderência entre o que foi planejado pelo PCP e o que a fábrica simulada entregou.",
    formula: "(Ordens concluídas no plano ÷ ordens planejadas) × 100",
    calculation: "Base falsa: 82 ordens concluídas dentro do plano de um total de 89 ordens planejadas.",
    dataUsed: ["Plano mestre fictício", "Ordens simuladas", "Data prometida", "Data de conclusão"],
    factors: ["Disponibilidade de matéria-prima", "Sequenciamento de linhas", "Repriorização comercial"],
    recommendations: ["Congelar janela de programação", "Separar ordens urgentes do plano base", "Revisar promessas de entrega antes do aceite"],
    history: [
      { label: "Jan", value: 86.8, formattedValue: formatPercent(86.8) },
      { label: "Fev", value: 88.4, formattedValue: formatPercent(88.4) },
      { label: "Mar", value: 89.1, formattedValue: formatPercent(89.1) },
      { label: "Abr", value: 91.6, formattedValue: formatPercent(91.6) },
      { label: "Mai", value: 89.4, formattedValue: formatPercent(89.4) },
      { label: "Jun", value: 92.1, formattedValue: formatPercent(92.1) },
    ],
    investigation: [
      { title: "Ordens no prazo", detail: "Ordens fictícias encerradas dentro da janela planejada.", value: "82" },
      { title: "Reprogramações", detail: "Mudanças simuladas solicitadas após congelamento do plano.", value: "7", critical: true },
      { title: "Atraso médio", detail: "Média fictícia das ordens que saíram do plano.", value: "1,8 dia" },
    ],
  },
  {
    id: "scrap",
    area: "Qualidade",
    title: "Índice de Refugo",
    value: formatPercent(2.9),
    status: "attention",
    change: 0.6,
    lowerIsBetter: true,
    icon: PackageX,
    description: "Percentual demonstrativo de unidades refugadas sobre produção total.",
    meaning: "Aponta perdas de processo e risco de retrabalho, desperdício ou atraso de entrega.",
    formula: "(Unidades refugadas ÷ unidades produzidas totais) × 100",
    calculation: "Base falsa: 1.306 unidades refugadas divididas por 45.026 unidades produzidas.",
    dataUsed: ["Apontamentos fictícios", "Motivo de refugo", "Produto", "Linha", "Turno"],
    factors: ["Ajuste de máquina após setup", "Matéria-prima fora do padrão", "Inspeção final mais rigorosa"],
    recommendations: ["Separar refugos por causa raiz", "Auditar primeira peça após setup", "Bloquear lote de matéria-prima reincidente"],
    history: [
      { label: "Jan", value: 2.1, formattedValue: formatPercent(2.1) },
      { label: "Fev", value: 2.3, formattedValue: formatPercent(2.3) },
      { label: "Mar", value: 2.4, formattedValue: formatPercent(2.4) },
      { label: "Abr", value: 2.7, formattedValue: formatPercent(2.7) },
      { label: "Mai", value: 2.3, formattedValue: formatPercent(2.3) },
      { label: "Jun", value: 2.9, formattedValue: formatPercent(2.9) },
    ],
    investigation: [
      { title: "Refugo pós-setup", detail: "Maior causa fictícia de perda nas primeiras peças.", value: "41%", critical: true },
      { title: "Material suspeito", detail: "Lotes simulados com variação de qualidade.", value: "3 lotes" },
      { title: "Custo estimado", detail: "Perda demonstrativa convertida em custo industrial.", value: "R$ 18 mil" },
    ],
  },
  {
    id: "setup",
    area: "Setup",
    title: "Setup Médio",
    value: "42 min",
    status: "attention",
    change: -5.3,
    lowerIsBetter: true,
    icon: TimerReset,
    description: "Tempo médio fictício de troca entre ordens ou produtos.",
    meaning: "Mede quanto tempo produtivo é consumido em preparação de linha.",
    formula: "Soma dos tempos de setup ÷ quantidade de setups",
    calculation: "Base falsa: 2.394 minutos de setup divididos por 57 trocas registradas.",
    dataUsed: ["Eventos de setup simulados", "Hora de início", "Hora de liberação", "Linha", "Família de produto"],
    factors: ["Troca de ferramental", "Limpeza entre famílias", "Aguardando liberação de qualidade"],
    recommendations: ["Agrupar famílias semelhantes", "Preparar ferramental antes da parada", "Medir setup interno e externo separadamente"],
    history: [
      { label: "Jan", value: 55, formattedValue: "55 min" },
      { label: "Fev", value: 51, formattedValue: "51 min" },
      { label: "Mar", value: 49, formattedValue: "49 min" },
      { label: "Abr", value: 45, formattedValue: "45 min" },
      { label: "Mai", value: 47, formattedValue: "47 min" },
      { label: "Jun", value: 42, formattedValue: "42 min" },
    ],
    investigation: [
      { title: "Maior setup", detail: "Troca fictícia de família crítica consumiu mais tempo.", value: "96 min", critical: true },
      { title: "Setup externo", detail: "Preparações realizadas antes da parada.", value: "63%" },
      { title: "Aguardando qualidade", detail: "Tempo simulado parado esperando liberação.", value: "5h" },
    ],
  },
  {
    id: "oee",
    area: "Ativos",
    title: "OEE Simulado",
    value: formatPercent(78.6),
    status: "attention",
    change: 3.2,
    lowerIsBetter: false,
    icon: Gauge,
    description: "Indicador demonstrativo de disponibilidade, performance e qualidade.",
    meaning: "Resume o aproveitamento industrial combinando tempo disponível, ritmo de produção e qualidade.",
    formula: "Disponibilidade × Performance × Qualidade",
    calculation: "Base falsa: 88% de disponibilidade × 92% de performance × 97% de qualidade.",
    dataUsed: ["Tempo disponível fictício", "Paradas", "Ritmo planejado", "Produção boa", "Refugo"],
    factors: ["Paradas não planejadas", "Velocidade real abaixo do padrão", "Refugo no início das ordens"],
    recommendations: ["Atacar paradas de maior duração", "Atualizar tempos padrão por família", "Usar primeira peça aprovada como gatilho de produção"],
    history: [
      { label: "Jan", value: 72.4, formattedValue: formatPercent(72.4) },
      { label: "Fev", value: 73.8, formattedValue: formatPercent(73.8) },
      { label: "Mar", value: 75.0, formattedValue: formatPercent(75) },
      { label: "Abr", value: 76.9, formattedValue: formatPercent(76.9) },
      { label: "Mai", value: 75.4, formattedValue: formatPercent(75.4) },
      { label: "Jun", value: 78.6, formattedValue: formatPercent(78.6) },
    ],
    investigation: [
      { title: "Disponibilidade", detail: "Componente fictício mais afetado por paradas.", value: formatPercent(88) },
      { title: "Performance", detail: "Velocidade média simulada contra padrão técnico.", value: formatPercent(92) },
      { title: "Qualidade", detail: "Unidades boas sobre produção total.", value: formatPercent(97) },
    ],
  },
  {
    id: "late-orders",
    area: "Ordens",
    title: "Ordens em Atraso",
    value: "12",
    status: "critical",
    change: 20.0,
    lowerIsBetter: true,
    icon: AlertTriangle,
    description: "Quantidade fictícia de ordens produtivas fora da data prometida.",
    meaning: "Mostra pressão operacional com potencial impacto em entrega, faturamento e atendimento ao cliente.",
    formula: "Contagem de ordens com data prometida vencida e status aberto",
    calculation: "Base falsa: foram encontradas 12 ordens abertas com promessa anterior à data de referência.",
    dataUsed: ["Ordens simuladas", "Data prometida", "Status de produção", "Cliente interno", "Linha"],
    factors: ["Falta de matéria-prima", "Repriorização comercial", "Capacidade tomada por ordens urgentes"],
    recommendations: ["Criar fila de recuperação diária", "Prometer nova data com base em capacidade real", "Escalar ordens críticas para PCP e vendas"],
    history: [
      { label: "Jan", value: 9, formattedValue: "9" },
      { label: "Fev", value: 8, formattedValue: "8" },
      { label: "Mar", value: 10, formattedValue: "10" },
      { label: "Abr", value: 11, formattedValue: "11" },
      { label: "Mai", value: 10, formattedValue: "10" },
      { label: "Jun", value: 12, formattedValue: "12" },
    ],
    investigation: [
      { title: "Cliente prioritário", detail: "Ordens fictícias de maior impacto comercial.", value: "4", critical: true },
      { title: "Atraso acima de 3 dias", detail: "Ordens com maior risco de ruptura.", value: "5", critical: true },
      { title: "Linha gargalo", detail: "Maior concentração simulada de atraso.", value: "Linha 3" },
    ],
  },
  {
    id: "material",
    area: "Materiais",
    title: "Consumo de Matéria-Prima",
    value: formatPercent(96.8),
    status: "healthy",
    change: -1.1,
    lowerIsBetter: true,
    icon: Recycle,
    description: "Uso fictício de matéria-prima em relação ao consumo padrão esperado.",
    meaning: "Compara o consumo real simulado com a ficha técnica para apontar desperdício ou economia.",
    formula: "(Consumo realizado ÷ consumo padrão) × 100",
    calculation: "Base falsa: 96,8 toneladas consumidas para uma necessidade padrão de 100 toneladas.",
    dataUsed: ["Baixas simuladas de estoque", "Ficha técnica fictícia", "Ordem de produção", "Lote de matéria-prima"],
    factors: ["Melhor aproveitamento de corte", "Mix de produto menos intensivo", "Ajustes de apontamento em estoque"],
    recommendations: ["Validar apontamentos abaixo do padrão", "Preservar boas práticas de corte", "Comparar consumo por família de produto"],
    history: [
      { label: "Jan", value: 101.7, formattedValue: formatPercent(101.7) },
      { label: "Fev", value: 100.8, formattedValue: formatPercent(100.8) },
      { label: "Mar", value: 99.6, formattedValue: formatPercent(99.6) },
      { label: "Abr", value: 98.4, formattedValue: formatPercent(98.4) },
      { label: "Mai", value: 97.9, formattedValue: formatPercent(97.9) },
      { label: "Jun", value: 96.8, formattedValue: formatPercent(96.8) },
    ],
    investigation: [
      { title: "Economia simulada", detail: "Diferença fictícia contra consumo padrão.", value: "3,2 t" },
      { title: "Família com ganho", detail: "Produto demonstrativo com melhor aproveitamento.", value: "Linha leve" },
      { title: "Apontamentos zerados", detail: "Ordens que precisam validação antes de virar referência.", value: "2" },
    ],
  },
  {
    id: "capacity",
    area: "Capacidade",
    title: "Capacidade Disponível",
    value: formatPercent(14.5),
    status: "attention",
    change: -3.8,
    lowerIsBetter: false,
    icon: Wrench,
    description: "Percentual demonstrativo de capacidade livre para absorver novas ordens.",
    meaning: "Ajuda a entender se a fábrica simulada tem espaço para aceitar demanda adicional sem atrasar o plano.",
    formula: "[(Horas disponíveis − horas carregadas) ÷ horas disponíveis] × 100",
    calculation: "Base falsa: 1.200 horas disponíveis e 1.026 horas já carregadas no plano.",
    dataUsed: ["Calendário fabril fictício", "Carga por ordem", "Turnos", "Paradas planejadas", "Horas disponíveis"],
    factors: ["Manutenção preventiva", "Ordens urgentes", "Limite de operadores no turno C"],
    recommendations: ["Reservar colchão para urgências", "Simular hora extra antes de aceitar novo pedido", "Deslocar ordens de baixa prioridade"],
    history: [
      { label: "Jan", value: 22.0, formattedValue: formatPercent(22) },
      { label: "Fev", value: 20.4, formattedValue: formatPercent(20.4) },
      { label: "Mar", value: 18.7, formattedValue: formatPercent(18.7) },
      { label: "Abr", value: 17.2, formattedValue: formatPercent(17.2) },
      { label: "Mai", value: 18.3, formattedValue: formatPercent(18.3) },
      { label: "Jun", value: 14.5, formattedValue: formatPercent(14.5) },
    ],
    investigation: [
      { title: "Horas livres", detail: "Capacidade fictícia ainda não carregada.", value: "174h" },
      { title: "Turno gargalo", detail: "Menor folga simulada de operação.", value: "Turno C", critical: true },
      { title: "Paradas planejadas", detail: "Horas reservadas para manutenção preventiva.", value: "46h" },
    ],
  },
];

function ProducaoPage() {
  const [selectedMetric, setSelectedMetric] = useState<ProductionMetricId | null>(null);
  const selectedCard = useMemo(
    () => productionMetrics.find((metric) => metric.id === selectedMetric) ?? null,
    [selectedMetric],
  );

  return (
    <div className="page-shell">
      <header className="animate-soft-enter mb-8 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <span className="page-header-kicker">Produção</span>
          <h1 className="mt-2 mb-2 text-4xl font-display tracking-tight text-balance">Produção</h1>
          <p className="max-w-[70ch] text-muted-foreground text-pretty">
            Painel executivo demonstrativo para acompanhar eficiência, plano, qualidade e capacidade fabril.
          </p>
        </div>
      </header>

      <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5" aria-label="Indicadores de produção">
        {productionMetrics.map((card) => (
          <ExecutiveMetricCard key={card.id} card={card} onSelect={() => setSelectedMetric(card.id)} />
        ))}
      </section>

      <MetricDetailsDialog metric={selectedCard} onOpenChange={(open) => !open && setSelectedMetric(null)} />
    </div>
  );
}

function ExecutiveMetricCard({ card, onSelect }: { card: ProductionMetric; onSelect: () => void }) {
  const status = statusPresentation(card.status);
  const favorableTrend = card.change != null && card.change !== 0 && ((card.change < 0) === card.lowerIsBetter);
  const TrendIcon = card.change == null || card.change === 0 ? TrendingUp : card.change > 0 ? ArrowUpRight : ArrowDownRight;

  return (
    <button type="button" onClick={onSelect} className="h-full text-left">
      <Card className="h-full cursor-pointer hover:border-primary/40">
        <CardContent className="relative flex h-full flex-col p-5">
          <div className="min-h-11 pr-12">
            <h2 className="flex min-h-10 min-w-0 items-center text-balance text-sm font-semibold leading-snug">{card.title}</h2>
            <span className="absolute right-5 top-5 inline-flex size-9 shrink-0 items-center justify-center rounded-full border border-primary/10 bg-primary/10 text-primary">
              <card.icon className="size-4" />
            </span>
          </div>
          <p className="mt-4 text-3xl font-display tracking-tight">{card.value}</p>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Badge variant="outline" className={status.className}>
              <span className={cn("mr-1.5 size-1.5 rounded-full", status.dotClassName)} />
              {status.label}
            </Badge>
            <span className={cn("inline-flex items-center gap-1 text-xs", card.change == null ? "text-muted-foreground" : favorableTrend ? "text-emerald-600" : "text-amber-600")}>
              <TrendIcon className="size-3.5" />
              {formatChange(card.change)}
            </span>
          </div>
        </CardContent>
      </Card>
    </button>
  );
}

function MetricDetailsDialog({ metric, onOpenChange }: { metric: ProductionMetric | null; onOpenChange: (open: boolean) => void }) {
  return (
    <Dialog open={metric != null} onOpenChange={onOpenChange}>
      <DialogContent className="custom-scrollbar max-h-[90vh] w-[94vw] max-w-4xl overflow-y-auto p-5 sm:p-6">
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
  return (
    <Badge variant="outline" className={presentation.className}>
      <span className={cn("mr-1.5 size-1.5 rounded-full", presentation.dotClassName)} />
      {presentation.label}
    </Badge>
  );
}

function InfoBlock({ title, value }: { title: string; value: string }) {
  return (
    <div className="rounded-lg border bg-surface p-4">
      <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{title}</p>
      <p className="mt-2 text-sm leading-relaxed">{value}</p>
    </div>
  );
}

function ListBlock({ title, items }: { title: string; items: string[] }) {
  return (
    <div className="rounded-lg border bg-surface p-4">
      <p className="text-sm font-semibold">{title}</p>
      <ul className="mt-3 space-y-2">
        {items.map((item) => (
          <li key={item} className="flex gap-2 text-sm text-muted-foreground">
            <ChevronRight className="mt-0.5 size-4 shrink-0 text-primary" />
            <span>{item}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function MetricHistoryChart({ history }: { history: ProductionMetric["history"] }) {
  const maximum = Math.max(1, ...history.map((point) => point.value));

  return (
    <div>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold">Histórico do indicador</h3>
        <span className="text-xs text-muted-foreground">{integerFormatter.format(history.length)} períodos</span>
      </div>
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
