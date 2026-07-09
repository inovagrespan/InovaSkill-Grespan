import { createFileRoute } from "@tanstack/react-router";
import {
  AlertTriangle,
  ArrowDownRight,
  ArrowUpRight,
  ChevronRight,
  Clock,
  Gauge,
  PackageCheck,
  PackageX,
  RotateCcw,
  Route as RouteIcon,
  ShieldCheck,
  Sparkles,
  Timer,
  TrendingUp,
  Truck,
  WalletCards,
} from "lucide-react";
import { useEffect, useMemo, useState, type ComponentType, type ReactNode } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { DashboardKpiCard } from "@/components/ui/dashboard-kpi-card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import {
  LOGISTICS_PERIOD_OPTIONS,
  LOGISTICS_REFERENCE_DATE,
  buildContextualLogisticsRecommendation,
  buildDemoLogisticsMetricHistory,
  compareLogisticsPeriods,
  demoLogisticsDashboardSource,
  filterLogisticsDashboardSource,
  formatLogisticsDuration,
  selectLatestInventoryBySku,
  summarizeLogisticsRoutes,
  type LogisticsInventoryRecord,
  type LogisticsKpis,
  type LogisticsPeriodDays,
  type LogisticsRouteRecord,
  type LogisticsRouteSummary,
  type LogisticsRecommendationContext,
  type LogisticsRootCause,
} from "@/lib/logistics-dashboard";
import { fetchRouteOccupancySummary, type RouteOccupancySummary } from "@/lib/importer-api";
import { cn } from "@/lib/utils";
import { formatKpiCompactCurrency } from "@/lib/vendas-formatters";
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";

export const Route = createFileRoute("/logistica/")({ component: LogisticsDashboardMetrics });

type ExecutiveMetricId = "returns" | "occupancy" | "loading" | "transit" | "total-cost" | "route-cost" | "inventory-accuracy" | "stockout" | "occurrences" | "fill-rate";
type MetricStatus = "healthy" | "attention" | "critical" | "idle" | "medium" | "good";
type SecondaryMetricId =
  | "occupancy-load"
  | "loading"
  | "route-cost"
  | "inventory-accuracy"
  | "fill-rate"
  | "affected-products"
  | "stockout-history"
  | "route-time"
  | "delayed-deliveries"
  | "critical-routes"
  | "damage"
  | "returns"
  | "reasons"
  | "regions";

type ExecutiveCard = {
  id: ExecutiveMetricId;
  area: string;
  title: string;
  value: string;
  status: MetricStatus;
  change: number | null;
  lowerIsBetter: boolean;
  insight: string;
  icon: ComponentType<{ className?: string }>;
  description: string;
  meaning: string;
  formula: string;
  calculation: string;
  dataUsed: string[];
  factors: string[];
  recommendations: string[];
  detailMetric: SecondaryMetricId;
  rawValue: number;
  occupancySummary?: RouteOccupancySummary;
  unavailableReason?: string;
  showStatus?: boolean;
};

const LOGISTICS_METRIC_UNDER_DEVELOPMENT = "em breve";
const RELEASED_LOGISTICS_METRICS: ReadonlySet<ExecutiveMetricId> = new Set(["occupancy", "stockout"]);
const MEDIUM_OCCUPANCY_LIMIT_PERCENT = 60;
const GOOD_OCCUPANCY_LIMIT_PERCENT = 80;
const CRITICAL_OCCUPANCY_LIMIT_PERCENT = 100;

function isReleasedLogisticsMetric(metricId: ExecutiveMetricId): boolean {
  return RELEASED_LOGISTICS_METRICS.has(metricId);
}

function resolveExecutiveMetricValue(metricId: ExecutiveMetricId, calculatedValue: string): string {
  return isReleasedLogisticsMetric(metricId) ? calculatedValue : LOGISTICS_METRIC_UNDER_DEVELOPMENT;
}

type InvestigationEvidence = {
  id: string;
  title: string;
  subtitle: string;
  fields: Array<{ label: string; value: string }>;
  recommendationContext: LogisticsRecommendationContext;
  occupancyPercent?: number;
  occupancyStatus?: { label: string; tone: "healthy" | "attention" | "critical" | "neutral" };
};

type InvestigationFactor = {
  id: string;
  title: string;
  summary: string;
  cause: LogisticsRootCause;
  evidence: InvestigationEvidence[];
};

const HEALTHY_TRANSIT_MINUTES = 210;
const ATTENTION_TRANSIT_MINUTES = 240;
const HEALTHY_DAMAGE_RATE_PERCENT = 1;
const ATTENTION_DAMAGE_RATE_PERCENT = 2;
const PERCENT_DECIMAL_PLACES = 1;

const numberFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 0 });

function formatLogisticsCurrency(value: number): string {
  return formatKpiCompactCurrency(value);
}

function formatLogisticsWeightKg(value: number): string {
  return `${new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 }).format(value)} kg`;
}

function formatBaseDate(value: string | null): string {
  if (!value) return "Não informado";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("pt-BR");
}

const periodLabels: Record<LogisticsPeriodDays, string> = {
  1: "Hoje",
  7: "7 dias",
  30: "30 dias",
  90: "90 dias",
};

const occurrenceDetails = [
  { type: "Avaria", reason: "Embalagem danificada", product: "Pão Francês Congelado", customer: "Padaria Avenida", region: "Campinas", financialImpact: 1280 },
  { type: "Avaria", reason: "Quebra na movimentação", product: "Croissant Congelado", customer: "Mercado Central", region: "Sorocaba", financialImpact: 940 },
  { type: "Avaria", reason: "Variação de temperatura", product: "Pão de Queijo", customer: "Rede Primavera", region: "Ribeirão Preto", financialImpact: 1760 },
  { type: "Devolução", reason: "Divergência do pedido", product: "Pão Francês Congelado", customer: "Atacado União", region: "ABC Paulista", financialImpact: 2150 },
];

const routeCarriers: Record<string, string> = {
  "ROT-01": "Expresso Grespan",
  "ROT-02": "Transportes Aurora",
  "ROT-03": "Expresso Grespan",
  "ROT-04": "LogSul Cargas",
};

const routeDrivers: Record<string, string> = {
  "ROT-01": "Carlos Mendes",
  "ROT-02": "João Ribeiro",
  "ROT-03": "Marcos Silva",
  "ROT-04": "André Santos",
};

function formatPercent(value: number): string {
  return `${value.toFixed(PERCENT_DECIMAL_PLACES).replace(".", ",")}%`;
}

function formatChange(change: number | null): string {
  if (change == null) return "Sem base anterior";
  if (change === 0) return "Estável vs. período anterior";
  return `${change > 0 ? "+" : ""}${formatPercent(change)} vs. período anterior`;
}

function changePhrase(label: string, change: number | null, lowerIsBetter: boolean): string {
  if (change == null) return `${label} ainda não possui base anterior comparável.`;
  if (change === 0) return `${label} permaneceu estável em relação ao período anterior.`;
  const movement = change > 0 ? "aumentou" : "reduziu";
  const interpretation = (change > 0) === lowerIsBetter ? " exige atenção" : " indica evolução positiva";
  return `${label} ${movement} ${formatPercent(Math.abs(change))} em relação ao período anterior e${interpretation}.`;
}

function occupancyStatus(value: number): MetricStatus {
  if (value < MEDIUM_OCCUPANCY_LIMIT_PERCENT) return "idle";
  if (value > CRITICAL_OCCUPANCY_LIMIT_PERCENT) return "critical";
  if (value >= GOOD_OCCUPANCY_LIMIT_PERCENT) return "good";
  return "medium";
}

function stockoutStatus(value: number): MetricStatus {
  if (value === 0) return "healthy";
  if (value === 1) return "attention";
  return "critical";
}

function transitStatus(value: number): MetricStatus {
  if (value <= HEALTHY_TRANSIT_MINUTES) return "healthy";
  if (value <= ATTENTION_TRANSIT_MINUTES) return "attention";
  return "critical";
}

function occurrenceStatus(value: number): MetricStatus {
  if (value <= HEALTHY_DAMAGE_RATE_PERCENT) return "healthy";
  if (value <= ATTENTION_DAMAGE_RATE_PERCENT) return "attention";
  return "critical";
}

function lowerIsBetterStatus(value: number, healthyLimit: number, attentionLimit: number): MetricStatus {
  if (value <= healthyLimit) return "healthy";
  if (value <= attentionLimit) return "attention";
  return "critical";
}

function higherIsBetterStatus(value: number, healthyLimit: number, attentionLimit: number): MetricStatus {
  if (value >= healthyLimit) return "healthy";
  if (value >= attentionLimit) return "attention";
  return "critical";
}

function costStatus(change: number | null): MetricStatus {
  if (change == null || change <= 0) return "healthy";
  if (change <= 10) return "attention";
  return "critical";
}

function routeOccupancyPresentation(occupancyPercent: number): { label: string; tone: "healthy" | "attention" | "critical" | "neutral" } {
  if (occupancyPercent >= 95) return { label: "Crítico", tone: "critical" };
  if (occupancyPercent >= 90) return { label: "No limite", tone: "attention" };
  if (occupancyPercent >= 80) return { label: "Saudável", tone: "healthy" };
  return { label: "Folga", tone: "neutral" };
}

function occupancyToneClass(tone: "healthy" | "attention" | "critical" | "neutral"): string {
  if (tone === "critical") return "border-red-200 bg-red-600 text-white";
  if (tone === "attention") return "border-amber-200 bg-amber-500 text-white";
  if (tone === "healthy") return "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300";
  return "border-border bg-background text-foreground";
}

function statusPresentation(status: MetricStatus): { label: string; className: string; dotClassName: string } {
  if (status === "healthy") return { label: "Saudável", className: "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300", dotClassName: "bg-emerald-500" };
  if (status === "good") return { label: "Saudável", className: "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300", dotClassName: "bg-emerald-500" };
  if (status === "attention") return { label: "Atenção", className: "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300", dotClassName: "bg-amber-500" };
  if (status === "medium") return { label: "Médio", className: "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300", dotClassName: "bg-amber-500" };
  if (status === "idle") return { label: "Ocioso", className: "border-sky-200 bg-sky-50 text-sky-700 dark:border-sky-900 dark:bg-sky-950/40 dark:text-sky-300", dotClassName: "bg-sky-500" };
  return { label: "Crítico", className: "border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300", dotClassName: "bg-red-500" };
}

function ExecutiveMetricCard({ card, onSelect }: { card: ExecutiveCard; onSelect: () => void }) {
  const released = isReleasedLogisticsMetric(card.id);
  const status = statusPresentation(card.status);
  const favorableTrend = card.change != null && card.change !== 0 && ((card.change < 0) === card.lowerIsBetter);
  const TrendIcon = card.change == null || card.change === 0 ? TrendingUp : card.change > 0 ? ArrowUpRight : ArrowDownRight;

  return (
    <button type="button" onClick={released ? onSelect : undefined} className="h-full text-left">
      <DashboardKpiCard title={card.title} value={card.value} icon={card.icon} interactive={released}>
        {released && card.showStatus !== false && (
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Badge variant="outline" className={status.className}><span className={cn("mr-1.5 size-1.5 rounded-full", status.dotClassName)} />{status.label}</Badge>
            {card.id !== "occupancy" && (
              <span className={cn("inline-flex items-center gap-1 text-xs", card.change == null ? "text-muted-foreground" : favorableTrend ? "text-emerald-600" : "text-amber-600")}>
                <TrendIcon className="size-3.5" />{formatChange(card.change)}
              </span>
            )}
          </div>
        )}
      </DashboardKpiCard>
    </button>
  );
}

function buildExecutiveCards(
  metrics: LogisticsKpis,
  changes: ReturnType<typeof compareLogisticsPeriods>["changes"],
  occupancySummary: RouteOccupancySummary | null,
  occupancyLoading: boolean,
  occupancyError: string | null,
): ExecutiveCard[] {
  const occupancyMetric = occupancySummary?.occupancyRatePercent ?? metrics.occupancyRatePercent;
  const occupancyValue = occupancyLoading ? "Carregando" : occupancyError ? "Indisponível" : formatPercent(occupancyMetric);

  return [
    { id: "returns", area: "Qualidade", title: "Taxa de Devoluções", value: formatPercent(metrics.returnRatePercent), rawValue: metrics.returnRatePercent, status: lowerIsBetterStatus(metrics.returnRatePercent, 2, 4), change: changes.returnRatePercent, lowerIsBetter: true, insight: changePhrase("A taxa de devoluções", changes.returnRatePercent, true), icon: RotateCcw, description: "Percentual expedido que retornou à operação.", meaning: "Mostra quanto do volume expedido foi devolvido por clientes e ajuda a localizar falhas de pedido, produto ou entrega.", formula: "(Unidades devolvidas ÷ unidades expedidas) × 100", calculation: "O sistema somou as unidades devolvidas e dividiu pelo total expedido no período selecionado.", dataUsed: ["Unidades expedidas", "Unidades devolvidas", "Cliente", "Produto", "Rota", "Motivo da devolução"], factors: ["Divergência entre pedido e entrega", "Embalagens danificadas", "Conservação inadequada de congelados"], recommendations: ["Revisar separação nas rotas com devolução", "Validar pedido com clientes recorrentes", "Auditar embalagem e cadeia fria"], detailMetric: "returns" },
    { id: "occupancy", area: "Operação", title: "Taxa de Ocupação", value: occupancyValue, rawValue: occupancyMetric, status: occupancyStatus(occupancyMetric), change: null, lowerIsBetter: false, insight: "A ocupação reflete a base atual de rotas, sem comparação com períodos anteriores.", icon: Gauge, description: "Capacidade dos veículos utilizada pelas cargas.", meaning: "Indica se a frota está sendo bem aproveitada e revela rotas subutilizadas ou acima do limite.", formula: "(Peso carregado ÷ capacidade total dos veículos) × 100", calculation: "O sistema soma o peso das rotas com capacidade configurada na base atual e divide pela soma da capacidade desses veículos.", dataUsed: ["Peso carregado", "Capacidade do veículo", "Veículo", "Rota"], factors: ["Rotas abaixo de 60% estão ociosas", "Rotas de 60% até menos de 80% ficam médias", "Rotas de 80% até 100% ficam saudáveis", "Rotas acima de 100% ficam críticas"], recommendations: ["Consolidar rotas ociosas", "Readequar o tipo de veículo à demanda", "Configurar capacidade dos veículos sem base"], detailMetric: "occupancy-load", occupancySummary, unavailableReason: occupancyError, showStatus: !occupancyLoading && !occupancyError },
    { id: "loading", area: "Operação", title: "Tempo de Carregamento", value: formatLogisticsDuration(metrics.averageLoadingMinutes), rawValue: metrics.averageLoadingMinutes, status: lowerIsBetterStatus(metrics.averageLoadingMinutes, 50, 60), change: changes.averageLoadingMinutes, lowerIsBetter: true, insight: changePhrase("O tempo de carregamento", changes.averageLoadingMinutes, true), icon: Timer, description: "Tempo médio necessário para liberar uma carga.", meaning: "Mede a eficiência do pátio desde o início da carga até a liberação do veículo.", formula: "Horário final do carregamento − horário inicial", calculation: "O sistema calculou a duração de cada carregamento e obteve a média das viagens do período.", dataUsed: ["Início do carregamento", "Fim do carregamento", "Veículo", "Equipe", "Tipo de carga"], factors: ["Fila no pátio", "Separação incompleta", "Carga mista de congelados e equipamentos"], recommendations: ["Pré-separar cargas antes da doca", "Balancear equipes nos horários de pico", "Criar janela específica para maquinários"], detailMetric: "loading" },
    { id: "transit", area: "Transporte", title: "Tempo de Trânsito", value: formatLogisticsDuration(metrics.averageTransitMinutes), rawValue: metrics.averageTransitMinutes, status: transitStatus(metrics.averageTransitMinutes), change: changes.averageTransitMinutes, lowerIsBetter: true, insight: changePhrase("O tempo de trânsito", changes.averageTransitMinutes, true), icon: Clock, description: "Tempo médio entre saída e entrega.", meaning: "Revela a duração real das viagens e o risco de atraso por rota, veículo ou transportadora.", formula: "Horário de entrega − horário de saída", calculation: "A duração das viagens concluídas foi somada e dividida pela quantidade de viagens analisadas.", dataUsed: ["Horário de saída", "Horário de entrega", "Rota", "Veículo", "Transportadora"], factors: ["Congestionamento", "Excesso de paradas", "Janelas restritas de recebimento"], recommendations: ["Replanejar sequenciamento das rotas críticas", "Antecipar saídas em horários de pico", "Negociar janelas com clientes recorrentes"], detailMetric: "route-time" },
    { id: "total-cost", area: "Custos", title: "Custo Logístico Total", value: formatLogisticsCurrency(metrics.totalLogisticsCost), rawValue: metrics.totalLogisticsCost, status: costStatus(changes.totalLogisticsCost), change: changes.totalLogisticsCost, lowerIsBetter: true, insight: changePhrase("O custo logístico", changes.totalLogisticsCost, true), icon: WalletCards, description: "Custo consolidado da operação logística.", meaning: "Consolida o valor gasto para armazenar, preparar e transportar os pedidos no período.", formula: "Transporte + combustível + manutenção + pedágio + armazenagem + operação", calculation: "O sistema somou os custos registrados em todas as viagens do período selecionado.", dataUsed: ["Custo de transporte", "Combustível", "Manutenção", "Pedágio", "Armazenagem", "Operação"], factors: ["Rotas longas ou ociosas", "Aumento do tempo de trânsito", "Devoluções e reentregas"], recommendations: ["Atacar as rotas de maior participação", "Reduzir viagens com baixa ocupação", "Monitorar custo de reentrega"], detailMetric: "route-cost" },
    { id: "route-cost", area: "Custos", title: "Custo Logístico por Rota", value: formatLogisticsCurrency(metrics.costPerRoute), rawValue: metrics.costPerRoute, status: costStatus(changes.costPerRoute), change: changes.costPerRoute, lowerIsBetter: true, insight: changePhrase("O custo por rota", changes.costPerRoute, true), icon: RouteIcon, description: "Custo médio das rotas realizadas.", meaning: "Permite comparar eficiência financeira entre rotas e identificar trajetos que consomem margem.", formula: "Custo logístico total ÷ quantidade de rotas distintas", calculation: `O custo de ${formatLogisticsCurrency(metrics.totalLogisticsCost)} foi dividido por ${metrics.routeCount} rotas distintas.`, dataUsed: ["Custo da viagem", "Rota", "Entregas", "Peso transportado", "Veículo"], factors: ["Baixa ocupação", "Distância e pedágios", "Atrasos e reentregas"], recommendations: ["Consolidar rotas de alto custo", "Comparar custo com faturamento atendido", "Rever veículo e transportadora"], detailMetric: "route-cost" },
    { id: "inventory-accuracy", area: "Estoque", title: "Acuracidade de Estoque", value: formatPercent(metrics.inventoryAccuracyPercent), rawValue: metrics.inventoryAccuracyPercent, status: higherIsBetterStatus(metrics.inventoryAccuracyPercent, 98, 95), change: changes.inventoryAccuracyPercent, lowerIsBetter: false, insight: changePhrase("A acuracidade", changes.inventoryAccuracyPercent, false), icon: ShieldCheck, description: "Aderência entre saldo sistêmico e contagem física.", meaning: "Indica a confiabilidade do estoque usado para prometer pedidos e planejar reposições.", formula: "[1 − (divergência absoluta ÷ estoque sistêmico)] × 100", calculation: "O sistema comparou a última contagem física de cada SKU com o saldo registrado e consolidou as diferenças.", dataUsed: ["Estoque sistêmico", "Estoque contado", "SKU", "Centro de distribuição", "Data da contagem"], factors: ["Movimentações sem baixa", "Erros de contagem", "Perdas de produtos congelados"], recommendations: ["Realizar inventário cíclico dos SKUs divergentes", "Bloquear movimentações sem confirmação", "Auditar perdas e ajustes manuais"], detailMetric: "inventory-accuracy" },
    { id: "stockout", area: "Estoque", title: "Rupturas de Estoque", value: `${metrics.stockoutSkuCount} SKUs`, rawValue: metrics.stockoutSkuCount, status: stockoutStatus(metrics.stockoutSkuCount), change: changes.stockoutSkuCount, lowerIsBetter: true, insight: changePhrase("A ruptura", changes.stockoutSkuCount, true), icon: PackageX, description: "Produtos cuja disponibilidade não cobre a demanda.", meaning: "Mostra quantos produtos podem impedir o atendimento integral dos pedidos.", formula: "Contagem de SKUs com saldo disponível menor que a demanda", calculation: "O sistema usou a posição mais recente de cada SKU e marcou como ruptura quando a demanda superou o saldo disponível.", dataUsed: ["Saldo disponível", "Demanda", "SKU", "Clientes afetados", "Centro de distribuição"], factors: ["Previsão de demanda insuficiente", "Acuracidade baixa", "Reposição atrasada"], recommendations: ["Priorizar compra dos SKUs críticos", "Revisar estoque de segurança", "Realocar saldo entre centros"], detailMetric: "affected-products" },
    { id: "occurrences", area: "Qualidade", title: "Índice de Ocorrências", value: formatPercent(metrics.damageRatePercent), rawValue: metrics.damageRatePercent, status: occurrenceStatus(metrics.damageRatePercent), change: changes.damageRatePercent, lowerIsBetter: true, insight: changePhrase("O índice de ocorrências", changes.damageRatePercent, true), icon: AlertTriangle, description: "Incidência de avarias sobre o volume expedido.", meaning: "Aponta problemas como danos, descongelamento, atraso, divergência e ausência do cliente.", formula: "(Unidades com ocorrência ÷ unidades expedidas) × 100", calculation: "Na base disponível, o sistema consolidou as unidades avariadas e comparou com o volume expedido.", dataUsed: ["Unidades expedidas", "Avarias", "Motivo", "Produto", "Rota", "Cliente"], factors: ["Embalagem inadequada", "Movimentação incorreta", "Variação de temperatura"], recommendations: ["Reforçar padrão de acondicionamento", "Treinar equipes de movimentação", "Monitorar temperatura por rota"], detailMetric: "damage" },
    { id: "fill-rate", area: "Atendimento", title: "Nível de Atendimento / Fill Rate", value: formatPercent(metrics.fillRatePercent), rawValue: metrics.fillRatePercent, status: higherIsBetterStatus(metrics.fillRatePercent, 95, 90), change: changes.fillRatePercent, lowerIsBetter: false, insight: changePhrase("O Fill Rate", changes.fillRatePercent, false), icon: PackageCheck, description: "Percentual da quantidade solicitada que foi entregue.", meaning: "Mede quanto a Grespan conseguiu atender sem falta de produtos ou cortes no pedido.", formula: "(Quantidade entregue ÷ quantidade solicitada) × 100", calculation: "As unidades entregues foram somadas e divididas pelo total solicitado pelos clientes.", dataUsed: ["Quantidade solicitada", "Quantidade entregue", "Cliente", "Produto", "Rota"], factors: ["Ruptura de estoque", "Cortes na separação", "Divergências de inventário"], recommendations: ["Relacionar cortes aos SKUs em ruptura", "Priorizar clientes com recorrência", "Aumentar acuracidade e estoque de segurança"], detailMetric: "fill-rate" },
  ].map((card) => ({ ...card, value: resolveExecutiveMetricValue(card.id, card.value) }));
}

function buildInvestigationFactors(
  metric: ExecutiveMetricId | null,
  routes: LogisticsRouteRecord[],
  inventory: LogisticsInventoryRecord[],
): InvestigationFactor[] {
  if (!metric) return [];
  const costlyRoutes = [...routes].sort((left, right) => right.logisticsCost - left.logisticsCost);
  const slowRoutes = [...routes].sort((left, right) => right.transitMinutes - left.transitMinutes);
  const slowLoads = [...routes].sort((left, right) => right.loadingMinutes - left.loadingMinutes);
  const lowOccupancyRoutes = [...routes].sort((left, right) => (left.loadedKg / left.capacityKg) - (right.loadedKg / right.capacityKg));
  const shortages = inventory
    .map((item) => ({ item, shortage: Math.max(0, item.demandUnits - item.availableUnits) }))
    .filter((entry) => entry.shortage > 0)
    .sort((left, right) => right.shortage - left.shortage);
  const divergences = inventory
    .map((item) => ({ item, difference: Math.abs(item.systemStock - item.countedStock) }))
    .sort((left, right) => right.difference - left.difference);

  const routeEvidence = (route: LogisticsRouteRecord, emphasis: string, showOccupancy = false): InvestigationEvidence => {
    const occupancyPercent = route.capacityKg > 0 ? (route.loadedKg / route.capacityKg) * 100 : 0;
    return {
    id: `${emphasis}-${route.date}-${route.routeId}`,
    title: route.routeName,
    subtitle: `${route.vehicleType} · ${routeCarriers[route.routeId] ?? "Transportadora não informada"}`,
    fields: [
      { label: "Motorista", value: routeDrivers[route.routeId] ?? "Não informado" },
      { label: "Veículo", value: route.vehicleType },
      { label: "Data", value: route.date },
      { label: "Filial", value: route.routeId === "ROT-03" ? "Ribeirão Preto" : "CD Central" },
    ],
    recommendationContext: { subject: emphasis, route: route.routeName, vehicle: `${route.vehicleType} ${route.routeId}` },
    occupancyPercent: showOccupancy ? occupancyPercent : undefined,
    occupancyStatus: showOccupancy ? routeOccupancyPresentation(occupancyPercent) : undefined,
  }; };

  const inventoryEvidence = (entry: typeof shortages[number], index: number): InvestigationEvidence => ({
    id: `stock-${entry.item.sku}`,
    title: entry.item.productName,
    subtitle: `${entry.shortage} unidades sem cobertura · ${entry.item.warehouse}`,
    fields: [
      { label: "Produto", value: entry.item.sku },
      { label: "Cliente", value: index % 2 ? "Rede Primavera" : "Padaria Avenida" },
      { label: "Pedido", value: `PED-${4820 + index}` },
      { label: "Filial", value: entry.item.warehouse },
    ],
    recommendationContext: { subject: "ruptura", product: entry.item.productName, customer: index % 2 ? "Rede Primavera" : "Padaria Avenida" },
  });

  if (metric === "returns") {
    const returns = occurrenceDetails.filter((item) => item.type === "Devolução");
    return [{ id: "return-customer", title: "Cliente com devolução recorrente", summary: `${returns[0]?.customer ?? "Atacado União"} concentra o maior impacto financeiro das devoluções.`, cause: "customer_returns", evidence: returns.map((item, index) => ({ id: `return-${index}`, title: item.customer, subtitle: `${item.product} · ${item.reason}`, fields: [{ label: "Produto", value: item.product }, { label: "Região", value: item.region }, { label: "Impacto", value: formatLogisticsCurrency(item.financialImpact) }, { label: "Data", value: "23/06/2026" }], recommendationContext: { subject: "devoluções", customer: item.customer, product: item.product } })) }];
  }
  if (metric === "occupancy") return [
    { id: "underused-route", title: "Ocupação por rota", summary: `${lowOccupancyRoutes[0]?.routeName ?? "Rota sem dados"} apresenta a menor utilização da capacidade disponível.`, cause: "low_occupancy", evidence: lowOccupancyRoutes.slice(0, 4).map((route) => routeEvidence(route, "baixa ocupação", true)) },
    { id: "loading-impact", title: "Espera no carregamento", summary: "Cargas mistas de pães congelados e fornos alugados aumentam o tempo de preparação e reduzem o giro dos veículos.", cause: "loading_bottleneck", evidence: slowLoads.slice(0, 3).map((route) => routeEvidence(route, "gargalo de carregamento")) },
  ];
  if (metric === "loading") return [{ id: "loading-bottleneck", title: "Gargalo no pátio", summary: `${slowLoads.filter((route) => route.loadingMinutes > 50).length} viagens com pães congelados e equipamentos de panificação ultrapassaram 50 minutos de carregamento.`, cause: "loading_bottleneck", evidence: slowLoads.slice(0, 4).map((route) => routeEvidence(route, "carregamento lento")) }];
  if (metric === "transit") return [{ id: "traffic-route", title: "Rotas acima do tempo planejado", summary: `${slowRoutes.filter((route) => route.transitMinutes > ATTENTION_TRANSIT_MINUTES).length} viagens excederam quatro horas, com concentração nos corredores do interior.`, cause: "congestion", evidence: slowRoutes.slice(0, 4).map((route) => routeEvidence(route, "congestionamento")) }];
  if (metric === "total-cost" || metric === "route-cost") return [
    { id: "expensive-route", title: "Concentração de custo por rota", summary: `${costlyRoutes[0]?.routeName ?? "Rota sem dados"} possui o maior custo do período.`, cause: "route_cost", evidence: costlyRoutes.slice(0, 4).map((route) => routeEvidence(route, "custo elevado")) },
    { id: "rental-equipment", title: "Movimentação de equipamentos alugados", summary: "Entregas e retiradas de fornos nos clientes aumentaram quilometragem, tempo de parada e custo operacional.", cause: "route_cost", evidence: costlyRoutes.slice(0, 3).map((route) => routeEvidence(route, "entrega de forno alugado")) },
  ];
  if (metric === "inventory-accuracy") return [{ id: "inventory-difference", title: "Divergências físicas", summary: `${divergences[0]?.item.productName ?? "Produto sem dados"} apresenta a maior diferença entre saldo sistêmico e contado.`, cause: "inventory_divergence", evidence: divergences.slice(0, 4).map((entry) => ({ id: `divergence-${entry.item.sku}`, title: entry.item.productName, subtitle: `${entry.difference} unidades de divergência · ${entry.item.warehouse}`, fields: [{ label: "Sistema", value: String(entry.item.systemStock) }, { label: "Contado", value: String(entry.item.countedStock) }, { label: "Produto", value: entry.item.sku }, { label: "Filial", value: entry.item.warehouse }], recommendationContext: { subject: "divergência", product: entry.item.productName } })) }];
  if (metric === "stockout") return [
    { id: "forecast-error", title: "Previsão abaixo da demanda", summary: `${shortages.length} produtos não possuem saldo suficiente para cobrir a demanda projetada.`, cause: "demand_forecast", evidence: shortages.slice(0, 3).map(inventoryEvidence) },
    { id: "sales-spike", title: "Aumento inesperado das vendas", summary: "O consumo recente dos pães congelados superou o estoque de segurança configurado.", cause: "sales_spike", evidence: shortages.slice(0, 2).map(inventoryEvidence) },
  ];
  if (metric === "occurrences") {
    const damages = occurrenceDetails.filter((item) => item.type === "Avaria");
    return [{ id: "vehicle-damage", title: "Avarias concentradas por veículo", summary: `${damages.length} ocorrências estão associadas à movimentação ou conservação da carga.`, cause: "vehicle_damage", evidence: damages.map((item, index) => ({ id: `damage-${index}`, title: item.product, subtitle: `${item.reason} · ${item.region}`, fields: [{ label: "Cliente", value: item.customer }, { label: "Veículo", value: `Truck 3/4 - 0${index + 5}` }, { label: "Região", value: item.region }, { label: "Impacto", value: formatLogisticsCurrency(item.financialImpact) }], recommendationContext: { subject: "avaria", vehicle: `Truck 3/4 - 0${index + 5}`, product: item.product } })) }];
  }
  if (metric === "fill-rate") return [{ id: "fill-stockout", title: "Cortes causados por indisponibilidade", summary: `${shortages.length} SKUs reduziram o atendimento integral dos pedidos.`, cause: "demand_forecast", evidence: shortages.slice(0, 3).map(inventoryEvidence) }];
  return [];
}

export function LogisticsDashboardMetrics() {
  const [periodDays, setPeriodDays] = useState<LogisticsPeriodDays>(30);
  const [selectedMetric, setSelectedMetric] = useState<ExecutiveMetricId | null>(null);
  const [selectedFactor, setSelectedFactor] = useState<InvestigationFactor | null>(null);
  const [selectedEvidence, setSelectedEvidence] = useState<InvestigationEvidence | null>(null);
  const [occupancySummary, setOccupancySummary] = useState<RouteOccupancySummary | null>(null);
  const [occupancyLoading, setOccupancyLoading] = useState(true);
  const [occupancyError, setOccupancyError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setOccupancyLoading(true);
    setOccupancyError(null);
    fetchRouteOccupancySummary()
      .then((summary) => {
        if (active) setOccupancySummary(summary);
      })
      .catch((error) => {
        if (!active) return;
        setOccupancySummary(null);
        setOccupancyError((error as Error).message);
      })
      .finally(() => {
        if (active) setOccupancyLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  const comparison = useMemo(() => compareLogisticsPeriods(demoLogisticsDashboardSource, periodDays, LOGISTICS_REFERENCE_DATE), [periodDays]);
  const filteredSource = useMemo(() => filterLogisticsDashboardSource(demoLogisticsDashboardSource, periodDays, LOGISTICS_REFERENCE_DATE), [periodDays]);
  const routeSummaries = useMemo(() => summarizeLogisticsRoutes(filteredSource.routes), [filteredSource.routes]);
  const inventory = useMemo(() => selectLatestInventoryBySku(filteredSource.inventory), [filteredSource.inventory]);
  const metrics = comparison.current;
  const executiveCards = buildExecutiveCards(metrics, comparison.changes, occupancySummary, occupancyLoading, occupancyError);
  const selectedCard = executiveCards.find((card) => card.id === selectedMetric) ?? null;
  const metricHistory = useMemo(() => selectedMetric && selectedMetric !== "occupancy" ? buildMetricHistory(selectedMetric, periodDays) : [], [selectedMetric, periodDays]);
  const investigationFactors = useMemo(
    () => buildInvestigationFactors(selectedMetric, filteredSource.routes, inventory),
    [selectedMetric, filteredSource.routes, inventory],
  );

  function openMetric(metricId: ExecutiveMetricId) {
    setSelectedMetric(metricId);
    setSelectedFactor(null);
    setSelectedEvidence(null);
  }

  function openFactor(factor: InvestigationFactor) {
    setSelectedFactor(factor);
    setSelectedEvidence(null);
  }

  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2"><span className="page-header-kicker">Dashboard</span><Badge variant="outline">Ocupação real</Badge><Badge variant="outline">Demais KPIs demonstrativos</Badge></div>
          <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">Dashboard logístico</h1>
          <p className="mt-1 text-sm text-muted-foreground">Identifique o sinal, encontre a causa raiz e receba uma ação específica sem sair da tela.</p>
        </div>
        <div className="flex flex-wrap gap-2" aria-label="Período da visão executiva">
          {LOGISTICS_PERIOD_OPTIONS.map((days) => (
            <Button key={days} size="sm" variant={periodDays === days ? "default" : "outline"} onClick={() => setPeriodDays(days)}>{periodLabels[days]}</Button>
          ))}
        </div>
      </header>

      <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5" aria-label="Indicadores logísticos">
        {executiveCards.map((card) => <ExecutiveMetricCard key={card.id} card={card} onSelect={() => openMetric(card.id)} />)}
      </section>

      <p className="text-center text-xs text-muted-foreground">A taxa de ocupação usa a base atual de rotas; os demais KPIs continuam demonstrativos até a API expor os eventos logísticos necessários.</p>

      <Dialog open={selectedCard != null && selectedFactor == null} onOpenChange={(open) => !open && selectedFactor == null && setSelectedMetric(null)}>
        <DialogContent className="custom-scrollbar max-h-[90vh] w-[94vw] max-w-4xl overflow-y-auto p-5 sm:p-6">
          {selectedCard && (
            <>
              <DialogHeader className="pr-8 text-left">
                <div className="flex flex-wrap items-center gap-2"><DialogTitle className="text-xl">{selectedCard.title}</DialogTitle><MetricStatusBadge status={selectedCard.status} /></div>
              </DialogHeader>
              <div className="mt-3 space-y-4">
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border bg-muted/20 p-4"><p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">O que aconteceu</p><p className="mt-2 text-lg font-semibold">{selectedCard.value}</p><p className="mt-1 text-sm text-muted-foreground">{selectedCard.id === "occupancy" ? "Base atual de rotas" : formatChange(selectedCard.change)}</p></div>
                  <div className="rounded-lg border bg-muted/20 p-4"><p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Por que está neste status</p><p className="mt-2 text-sm leading-relaxed text-muted-foreground">{selectedCard.insight}</p></div>
                </div>
                {selectedCard.id === "occupancy" ? <OccupancySummaryDetails card={selectedCard} /> : <MetricHistoryChart history={metricHistory} periodDays={periodDays} />}
                {selectedCard.id !== "occupancy" && <div><h3 className="text-sm font-semibold">Principais fatores que impactaram o resultado</h3><div className="mt-3 space-y-2">{investigationFactors.map((factor) => <button type="button" key={factor.id} onClick={() => openFactor(factor)} className="flex w-full items-center justify-between gap-3 rounded-lg border p-4 text-left transition-colors hover:border-primary/40 hover:bg-primary/[0.03]"><div><p className="text-sm font-semibold">{factor.title}</p><p className="mt-1 text-xs leading-relaxed text-muted-foreground">{factor.summary}</p></div><ChevronRight className="size-4 shrink-0 text-primary" /></button>)}</div></div>}
              </div>
            </>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={selectedFactor != null} onOpenChange={(open) => { if (!open) { setSelectedFactor(null); setSelectedEvidence(null); } }}>
        <DialogContent className="custom-scrollbar max-h-[90vh] w-[94vw] max-w-4xl overflow-y-auto p-5 sm:p-6">
          {selectedFactor && selectedCard && (
            <>
              <DialogHeader className="pr-8 text-left"><div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground"><span>{selectedCard.title}</span><ChevronRight className="size-3" /><span>{selectedFactor.title}</span></div><DialogTitle>Nível 3 · Investigação</DialogTitle><DialogDescription>{selectedFactor.summary}</DialogDescription></DialogHeader>
              <div className="mt-6 space-y-3"><h3 className="text-sm font-semibold">Evidências relacionadas ao problema</h3>{selectedFactor.evidence.map((evidence) => <button type="button" key={evidence.id} onClick={() => setSelectedEvidence(evidence)} className={cn("w-full rounded-lg border p-4 text-left transition-colors hover:border-primary/40", selectedEvidence?.id === evidence.id && "border-primary bg-primary/[0.04] ring-2 ring-primary/10")}><div className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold">{evidence.title}</p><p className="mt-1 text-xs text-muted-foreground">{evidence.subtitle}</p></div><div className="flex shrink-0 items-center gap-2">{evidence.occupancyStatus && <Badge variant="outline" className={occupancyToneClass(evidence.occupancyStatus.tone)}>{evidence.occupancyStatus.label}</Badge>}<ChevronRight className="size-4 text-primary" /></div></div>{evidence.occupancyPercent != null && <div className="mt-4"><div className="h-2 overflow-hidden rounded-full bg-muted"><div className="h-full rounded-full bg-primary transition-all" style={{ width: `${Math.min(100, Math.max(0, evidence.occupancyPercent))}%` }} /></div><p className="mt-2 text-xs text-muted-foreground">{formatPercent(evidence.occupancyPercent)} de ocupação</p></div>}<div className="mt-3 grid grid-cols-2 gap-2">{evidence.fields.map((field) => <div key={`${evidence.id}-${field.label}`}><p className="text-[10px] uppercase tracking-wide text-muted-foreground">{field.label}</p><p className="text-xs font-medium">{field.value}</p></div>)}</div></button>)}</div>
              {selectedEvidence ? <div className="mt-6 rounded-xl border border-primary/20 bg-primary/5 p-5"><div className="flex gap-3"><Sparkles className="mt-0.5 size-5 shrink-0 text-primary" /><div><p className="text-xs font-semibold uppercase tracking-wider text-primary">Nível 4 · Tomada de decisão</p><p className="mt-2 text-sm font-semibold">Ação recomendada para {selectedEvidence.title}</p><p className="mt-2 text-sm leading-relaxed text-muted-foreground">{buildContextualLogisticsRecommendation(selectedFactor.cause, selectedEvidence.recommendationContext)}</p></div></div></div> : <div className="mt-6 rounded-lg border border-dashed p-4 text-center text-sm text-muted-foreground">Selecione uma evidência para chegar à ação recomendada.</div>}
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function OccupancySummaryDetails({ card }: { card: ExecutiveCard }) {
  const summary = card.occupancySummary;

  if (card.unavailableReason) {
    return <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">{card.unavailableReason}</div>;
  }

  if (!summary) {
    return <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">Carregando dados atuais de rotas.</div>;
  }

  const sourceLabel = summary.snapshot
    ? `v${summary.snapshot.version} · ${summary.snapshot.fileName}`
    : "Sem base publicada";

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-3">
        <ForecastItem title="Peso total" value={formatLogisticsWeightKg(summary.totalWeightKg)} detail="Rotas com capacidade configurada" />
        <ForecastItem title="Capacidade total" value={formatLogisticsWeightKg(summary.totalCapacityKg)} detail="Soma dos veículos considerados" />
        <ForecastItem title="Rotas analisadas" value={String(summary.routeCount)} detail="Base atual de rotas" />
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <ForecastItem title="Com capacidade" value={String(summary.routesWithCapacity)} detail="Entram na taxa" />
        <ForecastItem title="Sem capacidade" value={String(summary.routesWithoutCapacity)} detail="Ficam fora do cálculo" />
        <ForecastItem title="Importação de rotas" value={sourceLabel} detail={summary.snapshot ? `Atualizado em ${formatBaseDate(summary.snapshot.finishedAt)}` : "Publique uma importação de rotas"} />
      </div>
    </div>
  );
}

function ForecastItem({ title, value, detail }: { title: string; value: string; detail: string }) {
  return <div className="rounded-lg border bg-muted/20 p-3"><p className="text-xs text-muted-foreground">{title}</p><p className="mt-1 text-lg font-semibold">{value}</p><p className="mt-1 text-[11px] text-muted-foreground">{detail}</p></div>;
}

type MetricHistoryPoint = { label: string; value: number; formattedValue: string };

function formatMetricHistoryValue(metric: ExecutiveMetricId, value: number): string {
  if (metric === "loading" || metric === "transit") return formatLogisticsDuration(value);
  if (metric === "total-cost" || metric === "route-cost") return formatLogisticsCurrency(value);
  if (metric === "stockout") return `${value} SKUs`;
  return formatPercent(value);
}

function buildMetricHistory(metric: ExecutiveMetricId, periodDays: LogisticsPeriodDays): MetricHistoryPoint[] {
  return buildDemoLogisticsMetricHistory(metric, periodDays).map((point) => ({
    ...point,
    formattedValue: formatMetricHistoryValue(metric, point.value),
  }));
}

function MetricStatusBadge({ status }: { status: MetricStatus }) {
  const presentation = statusPresentation(status);
  return <Badge variant="outline" className={presentation.className}><span className={cn("mr-1.5 size-1.5 rounded-full", presentation.dotClassName)} />{presentation.label}</Badge>;
}

function MetricHistoryChart({ history, periodDays }: { history: MetricHistoryPoint[]; periodDays: LogisticsPeriodDays }) {
  const timelineLabel = periodDays === 1 ? "Hoje, por horário" : `Últimos ${periodDays} dias`;
  return (
    <div className="logistics-modal-chart rounded-xl border p-4">
      <div className="flex items-center justify-between gap-3"><div><h3 className="text-sm font-semibold text-[var(--logistics-modal-chart-title)]">Evolução do indicador</h3><p className="mt-0.5 text-xs text-[var(--logistics-modal-chart-muted)]">Linha temporal: {timelineLabel}</p></div><Badge variant="outline">{periodLabels[periodDays]}</Badge></div>
      <div className="mt-3 h-44 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={history} margin={{ left: 0, right: 8, top: 10, bottom: 0 }}>
            <defs><linearGradient id="logisticsMetricTrendGradient" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="var(--logistics-modal-chart-line)" stopOpacity={0.42} /><stop offset="100%" stopColor="var(--logistics-modal-chart-line)" stopOpacity={0.02} /></linearGradient></defs>
            <CartesianGrid vertical={false} stroke="var(--logistics-modal-chart-grid)" strokeDasharray="3 3" />
            <XAxis dataKey="label" axisLine={false} tickLine={false} tick={{ fill: "var(--logistics-modal-chart-axis)", fontSize: 10 }} />
            <YAxis axisLine={false} tickLine={false} width={42} tick={{ fill: "var(--logistics-modal-chart-axis)", fontSize: 10 }} />
            <Tooltip formatter={(_, __, item) => [item.payload.formattedValue, "Valor"]} contentStyle={{ background: "var(--logistics-modal-chart-tooltip-bg)", borderColor: "var(--logistics-modal-chart-tooltip-border)", borderRadius: 8, color: "var(--logistics-modal-chart-title)" }} />
            <Area type="monotone" dataKey="value" stroke="var(--logistics-modal-chart-line)" strokeWidth={2.3} fill="url(#logisticsMetricTrendGradient)" dot={{ r: 2.5, fill: "var(--logistics-modal-chart-line)", strokeWidth: 0 }} activeDot={{ r: 4.5 }} />
          </AreaChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

type InvestigationContentProps = {
  metric: SecondaryMetricId;
  metrics: LogisticsKpis;
  routes: LogisticsRouteRecord[];
  routeSummaries: LogisticsRouteSummary[];
  inventory: LogisticsInventoryRecord[];
  inventoryHistory: LogisticsInventoryRecord[];
};

function InvestigationContent(props: InvestigationContentProps) {
  const { metric, metrics, routes, routeSummaries, inventory, inventoryHistory } = props;

  if (metric === "occupancy-load") {
    return <InvestigationLayout summaries={[
      { title: "Ocupação consolidada", value: formatPercent(metrics.occupancyRatePercent), detail: "Peso total sobre capacidade" },
      { title: "Rotas analisadas", value: String(metrics.routeCount), detail: "Rotas distintas no período" },
    ]} title="Capacidade utilizada por rota">
      {routeSummaries.map((route) => <InvestigationRow key={route.routeId} title={route.routeName} subtitle={`${numberFormatter.format(route.loadedKg)} kg de ${numberFormatter.format(route.capacityKg)} kg`} value={formatPercent(route.occupancyPercent)} />)}
    </InvestigationLayout>;
  }

  if (metric === "loading") {
    const slowLoads = routes.filter((route) => route.loadingMinutes > 50).length;
    return <InvestigationLayout summaries={[
      { title: "Tempo médio", value: formatLogisticsDuration(metrics.averageLoadingMinutes), detail: "Média por viagem" },
      { title: "Carregamentos lentos", value: String(slowLoads), detail: "Acima de 50 minutos" },
    ]} title="Tempo por viagem">
      {routes.map((route) => <InvestigationRow key={`${route.date}-${route.routeId}`} title={route.routeName} subtitle={`${route.date} · ${route.vehicleType}`} value={formatLogisticsDuration(route.loadingMinutes)} critical={route.loadingMinutes > 50} />)}
    </InvestigationLayout>;
  }

  if (metric === "route-cost") {
    const mostExpensiveRoute = routeSummaries[0]?.routeName ?? "Sem rota";
    return <InvestigationLayout summaries={[
      { title: "Custo total", value: formatLogisticsCurrency(metrics.totalLogisticsCost), detail: "Soma do período" },
      { title: "Custo médio", value: formatLogisticsCurrency(metrics.costPerRoute), detail: `Maior impacto: ${mostExpensiveRoute}` },
    ]} title="Participação no custo total">
      {[...routeSummaries].sort((left, right) => right.logisticsCost - left.logisticsCost).map((route) => {
        const share = metrics.totalLogisticsCost > 0 ? (route.logisticsCost / metrics.totalLogisticsCost) * 100 : 0;
        return <InvestigationRow key={route.routeId} title={route.routeName} subtitle={`${formatPercent(share)} do custo · ${route.vehicleType}`} value={formatLogisticsCurrency(route.logisticsCost)} />;
      })}
    </InvestigationLayout>;
  }

  if (metric === "inventory-accuracy") {
    const totalDifference = inventory.reduce((total, item) => total + Math.abs(item.systemStock - item.countedStock), 0);
    return <InvestigationLayout summaries={[
      { title: "Acuracidade", value: formatPercent(metrics.inventoryAccuracyPercent), detail: "Posição mais recente por SKU" },
      { title: "Divergência", value: `${totalDifference} un`, detail: "Diferença física absoluta" },
    ]} title="Sistema versus contagem física">
      {inventory.map((item) => { const difference = item.countedStock - item.systemStock; return <InvestigationRow key={item.sku} title={item.productName} subtitle={`${item.sku} · Sistema ${item.systemStock} · Contado ${item.countedStock}`} value={`${difference > 0 ? "+" : ""}${difference} un`} critical={difference !== 0} />; })}
    </InvestigationLayout>;
  }

  if (metric === "fill-rate") {
    const requested = routes.reduce((total, route) => total + route.requestedUnits, 0);
    const delivered = routes.reduce((total, route) => total + route.deliveredUnits, 0);
    return <InvestigationLayout summaries={[
      { title: "Fill Rate", value: formatPercent(metrics.fillRatePercent), detail: "Entregue sobre solicitado" },
      { title: "Não atendido", value: `${Math.max(0, requested - delivered)} un`, detail: `${delivered} de ${requested} unidades entregues` },
    ]} title="Atendimento por rota">
      {routes.map((route) => { const fillRate = route.requestedUnits > 0 ? (route.deliveredUnits / route.requestedUnits) * 100 : 0; return <InvestigationRow key={`${route.date}-${route.routeId}`} title={route.routeName} subtitle={`${route.deliveredUnits} de ${route.requestedUnits} unidades`} value={formatPercent(fillRate)} critical={fillRate < 95} />; })}
    </InvestigationLayout>;
  }

  if (metric === "affected-products") {
    const affected = inventory.filter((item) => item.availableUnits < item.demandUnits);
    const totalShortage = affected.reduce((total, item) => total + item.demandUnits - item.availableUnits, 0);
    return <InvestigationLayout summaries={[
      { title: "Produtos afetados", value: `${affected.length} SKUs`, detail: "Saldo menor que a demanda" },
      { title: "Déficit estimado", value: `${totalShortage} un`, detail: "Demanda ainda não atendida" },
    ]} title="Produto → cliente → valor perdido">
      {affected.map((item, index) => { const shortage = item.demandUnits - item.availableUnits; const lostValue = shortage * (index + 1) * 18; return <InvestigationRow key={item.sku} title={item.productName} subtitle={`${index % 2 ? "Rede Primavera" : "Padaria Avenida"} · ${shortage} un faltantes`} value={formatLogisticsCurrency(lostValue)} critical />; })}
    </InvestigationLayout>;
  }

  if (metric === "stockout-history") {
    const history = [...inventoryHistory].sort((left, right) => right.date.localeCompare(left.date));
    const ruptureEvents = history.filter((item) => item.availableUnits < item.demandUnits).length;
    return <InvestigationLayout summaries={[
      { title: "Eventos de ruptura", value: String(ruptureEvents), detail: "Ocorrências no histórico" },
      { title: "SKUs monitorados", value: String(new Set(history.map((item) => item.sku)).size), detail: "Produtos com posição registrada" },
    ]} title="Linha do tempo de disponibilidade">
      {history.map((item) => { const rupture = item.availableUnits < item.demandUnits; return <InvestigationRow key={`${item.date}-${item.sku}`} title={`${item.date} · ${item.productName}`} subtitle={`Disponível ${item.availableUnits} · Demanda ${item.demandUnits}`} value={rupture ? "Ruptura" : "Atendido"} critical={rupture} />; })}
    </InvestigationLayout>;
  }

  if (metric === "route-time") {
    return <InvestigationLayout summaries={[
      { title: "Tempo médio", value: formatLogisticsDuration(metrics.averageTransitMinutes), detail: "Todas as viagens" },
      { title: "Transportadoras", value: String(new Set(routes.map((route) => routeCarriers[route.routeId])).size), detail: "Operadores no período" },
    ]} title="Rota → veículo → transportadora → histórico">
      {routes.map((route) => <InvestigationRow key={`${route.date}-${route.routeId}`} title={route.routeName} subtitle={`${route.vehicleType} · ${routeCarriers[route.routeId] ?? "Não informada"} · ${route.date}`} value={formatLogisticsDuration(route.transitMinutes)} critical={route.transitMinutes > ATTENTION_TRANSIT_MINUTES} />)}
    </InvestigationLayout>;
  }

  if (metric === "delayed-deliveries") {
    const delayed = routes.filter((route) => route.transitMinutes > ATTENTION_TRANSIT_MINUTES);
    const excessMinutes = delayed.reduce((total, route) => total + route.transitMinutes - ATTENTION_TRANSIT_MINUTES, 0);
    return <InvestigationLayout summaries={[
      { title: "Viagens atrasadas", value: String(delayed.length), detail: "Acima de quatro horas" },
      { title: "Excesso acumulado", value: formatLogisticsDuration(excessMinutes), detail: "Tempo além da referência" },
    ]} title="Atrasos que exigem ação">
      {delayed.map((route) => <InvestigationRow key={`${route.date}-${route.routeId}`} title={route.routeName} subtitle={`${route.vehicleType} · ${routeCarriers[route.routeId] ?? "Não informada"}`} value={`+${formatLogisticsDuration(route.transitMinutes - ATTENTION_TRANSIT_MINUTES)}`} critical />)}
    </InvestigationLayout>;
  }

  if (metric === "critical-routes") {
    const criticalRoutes = routes.filter((route) => route.transitMinutes > ATTENTION_TRANSIT_MINUTES || route.loadedKg / route.capacityKg < 0.7);
    return <InvestigationLayout summaries={[
      { title: "Rotas críticas", value: String(criticalRoutes.length), detail: "Atraso ou baixa ocupação" },
      { title: "Custo exposto", value: formatLogisticsCurrency(criticalRoutes.reduce((total, route) => total + route.logisticsCost, 0)), detail: "Custo das viagens críticas" },
    ]} title="Priorização de rotas">
      {criticalRoutes.map((route) => <InvestigationRow key={`${route.date}-${route.routeId}`} title={route.routeName} subtitle={`${formatLogisticsDuration(route.transitMinutes)} · ${formatPercent((route.loadedKg / route.capacityKg) * 100)} ocupado`} value={formatLogisticsCurrency(route.logisticsCost)} critical />)}
    </InvestigationLayout>;
  }

  if (metric === "damage" || metric === "returns") {
    const type = metric === "damage" ? "Avaria" : "Devolução";
    const records = occurrenceDetails.filter((item) => item.type === type);
    return <InvestigationLayout summaries={[
      { title: type === "Avaria" ? "Damage Rate" : "Taxa de devoluções", value: type === "Avaria" ? formatPercent(metrics.damageRatePercent) : formatPercent(metrics.returnRatePercent), detail: "Sobre unidades expedidas" },
      { title: "Impacto financeiro", value: formatLogisticsCurrency(records.reduce((total, item) => total + item.financialImpact, 0)), detail: `${records.length} registros analisados` },
    ]} title="Produto → cliente → região → impacto">
      {records.map((item) => <InvestigationRow key={`${item.customer}-${item.reason}`} title={item.product} subtitle={`${item.customer} · ${item.region} · ${item.reason}`} value={formatLogisticsCurrency(item.financialImpact)} critical />)}
    </InvestigationLayout>;
  }

  const groupingKey = metric === "reasons" ? "reason" : "region";
  const groupedOccurrences = groupOccurrences(groupingKey);
  return <InvestigationLayout summaries={[
    { title: metric === "reasons" ? "Motivos distintos" : "Regiões afetadas", value: String(groupedOccurrences.length), detail: "Concentração das ocorrências" },
    { title: "Impacto total", value: formatLogisticsCurrency(occurrenceDetails.reduce((total, item) => total + item.financialImpact, 0)), detail: "Avarias e devoluções" },
  ]} title={metric === "reasons" ? "Ocorrências por motivo" : "Ocorrências por região"}>
    {groupedOccurrences.map((item) => <InvestigationRow key={item.label} title={item.label} subtitle={`${item.count} ocorrências · ${formatPercent(item.sharePercent)} do total`} value={formatLogisticsCurrency(item.financialImpact)} />)}
  </InvestigationLayout>;
}

function groupOccurrences(key: "reason" | "region") {
  const grouped = new Map<string, { count: number; financialImpact: number }>();
  for (const occurrence of occurrenceDetails) {
    const label = occurrence[key];
    const current = grouped.get(label) ?? { count: 0, financialImpact: 0 };
    current.count += 1;
    current.financialImpact += occurrence.financialImpact;
    grouped.set(label, current);
  }
  return [...grouped.entries()].map(([label, value]) => ({
    label,
    ...value,
    sharePercent: occurrenceDetails.length ? (value.count / occurrenceDetails.length) * 100 : 0,
  })).sort((left, right) => right.count - left.count || right.financialImpact - left.financialImpact);
}

function InvestigationLayout({ summaries, title, children }: { summaries: Array<{ title: string; value: string; detail: string }>; title: string; children: ReactNode }) {
  return <div className="mt-6 space-y-5"><div className="grid grid-cols-2 gap-3">{summaries.map((summary) => <ForecastItem key={summary.title} {...summary} />)}</div><div className="space-y-3"><h3 className="text-sm font-semibold">{title}</h3>{children}</div></div>;
}

function InvestigationRow({ title, subtitle, value, critical = false }: { title: string; subtitle: string; value: string; critical?: boolean }) {
  return <div className="rounded-lg border p-4"><div className="flex items-start justify-between gap-3"><div><p className="text-sm font-semibold">{title}</p><p className="mt-1 text-xs text-muted-foreground">{subtitle}</p></div><Badge variant={critical ? "destructive" : "outline"}>{value}</Badge></div></div>;
}
