import { useMemo, useState, type ComponentType } from "react";
import {
  BadgeDollarSign,
  CalendarRange,
  ChevronRight,
  Clock,
  Gem,
  Gift,
  Package,
  ReceiptText,
  Sparkles,
  Target,
  TrendingUp,
  UserCheck,
  Users,
  Weight,
} from "lucide-react";
import {
  Area,
  AreaChart,
  CartesianGrid,
  ComposedChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { Badge } from "./ui/badge";
import { Button } from "./ui/button";
import { Card, CardContent } from "./ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "./ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "./ui/select";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";
import { cn } from "@/lib/utils";
import {
  buildContextualSalesRecommendation,
  buildSalesKpiHistory,
  calculateSalesControlSnapshot,
  type SalesControlPeriodDays,
  type SalesHistoryPoint,
  type SalesKpiId,
  type SalesMetricStatus,
  type SalesRootCause,
} from "@/lib/sales-control-tower";
import {
  buildSalesRevenueExpenseSeries,
  DEMO_SALES_FINANCIAL_RECORDS,
  SALES_FINANCIAL_REFERENCE_DATE,
  type SalesFinancialPeriod,
} from "@/lib/sales-revenue-expense-chart";

type SalesCard = {
  id: SalesKpiId;
  title: string;
  value: string;
  status: SalesMetricStatus;
  change: number;
  lowerIsBetter: boolean;
  icon: ComponentType<{ className?: string }>;
  insight: string;
};
type SalesEvidence = {
  id: string;
  title: string;
  subtitle: string;
  fields: Array<{ label: string; value: string }>;
  subject: string;
};
type SalesFactor = {
  id: string;
  title: string;
  summary: string;
  cause: SalesRootCause;
  evidence: SalesEvidence[];
};

const periods: SalesControlPeriodDays[] = [1, 7, 30, 90];
const periodLabels: Record<SalesControlPeriodDays, string> = {
  1: "Hoje",
  7: "7 dias",
  30: "30 dias",
  90: "90 dias",
};
const financialPeriodLabels: Record<SalesFinancialPeriod, string> = {
  daily: "Diário",
  monthly: "Mensal",
  yearly: "Anual",
};
const currency = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

function percentage(value: number): string {
  return `${value.toFixed(1).replace(".", ",")}%`;
}
function changeLabel(value: number): string {
  return `${value > 0 ? "+" : ""}${percentage(value)} vs. período anterior`;
}
function statusClass(status: SalesMetricStatus): string {
  if (status === "Normal")
    return "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300";
  if (status === "Atenção")
    return "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300";
  return "border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300";
}

function buildCards(periodDays: SalesControlPeriodDays): SalesCard[] {
  const metric = calculateSalesControlSnapshot(periodDays);
  return [
    {
      id: "sales-volume",
      title: "Volume de Vendas",
      value: `${formatKpiCompactCurrency(metric.revenue)} · ${formatKpiCompactNumber(metric.weightKg)} kg`,
      status: "Normal",
      change: 8.4,
      lowerIsBetter: false,
      icon: Weight,
      insight:
        "Faturamento e peso vendidos avançaram com maior demanda de pães congelados em Marília e Bauru.",
    },
    {
      id: "bonus-volume",
      title: "Volume de Bonificação",
      value: `${formatKpiCompactCurrency(metric.bonusAmount)} · ${percentage(metric.bonusRatePercent)}`,
      status: metric.bonusRatePercent > 3 ? "Atenção" : "Normal",
      change: 3.1,
      lowerIsBetter: true,
      icon: Gift,
      insight:
        "As bonificações cresceram acima das vendas e estão concentradas em ações de ativação de novos clientes.",
    },
    {
      id: "active-consumption",
      title: "Consumo de Clientes Ativos",
      value: `+${percentage(metric.activeCustomerConsumptionChangePercent)}`,
      status: "Normal",
      change: 2.5,
      lowerIsBetter: false,
      icon: Users,
      insight:
        "Clientes ativos ampliaram o mix de pães congelados e a frequência média de reposição.",
    },
    {
      id: "product-consumption",
      title: "Consumo por Produto",
      value: `+${percentage(metric.productConsumptionChangePercent)}`,
      status: "Normal",
      change: 4.2,
      lowerIsBetter: false,
      icon: Package,
      insight:
        "Pão Francês Congelado 60g lidera o crescimento, enquanto duas linhas perderam consumo.",
    },
    {
      id: "conversion",
      title: "Prospecção × Fechamento",
      value: `${metric.closedDeals}/${metric.prospects} · ${percentage(metric.conversionRatePercent)}`,
      status: metric.conversionRatePercent >= 30 ? "Normal" : "Atenção",
      change: 1.8,
      lowerIsBetter: false,
      icon: Target,
      insight:
        "A conversão melhorou, mas propostas de locação de equipamentos ainda alongam o fechamento.",
    },
    {
      id: "conversion-time",
      title: "Tempo de Conversão",
      value: `${metric.conversionDays.toFixed(1).replace(".", ",")} dias`,
      status: metric.conversionDays <= 15 ? "Normal" : "Atenção",
      change: -6.6,
      lowerIsBetter: true,
      icon: Clock,
      insight:
        "Testes de produto e aprovação de fornos alugados concentram a maior espera do pipeline.",
    },
    {
      id: "seasonality",
      title: "Sazonalidade de Consumo",
      value: `Índice ${metric.seasonalityIndex.toFixed(2).replace(".", ",")}`,
      status: "Normal",
      change: 3.5,
      lowerIsBetter: false,
      icon: CalendarRange,
      insight: "Festas juninas elevaram a procura por pão de queijo e itens de conveniência.",
    },
    {
      id: "retention",
      title: "Taxa de Retenção",
      value: percentage(metric.retentionRatePercent),
      status: metric.retentionRatePercent >= 90 ? "Normal" : "Atenção",
      change: 0.7,
      lowerIsBetter: false,
      icon: UserCheck,
      insight:
        "A retenção evoluiu, mas clientes sem equipamento adequado apresentam risco maior de redução.",
    },
    {
      id: "mape",
      title: "Erro de Previsão (MAPE)",
      value: percentage(metric.mapePercent),
      status:
        metric.mapePercent <= 10 ? "Normal" : metric.mapePercent <= 15 ? "Atenção" : "Crítico",
      change: -10.3,
      lowerIsBetter: true,
      icon: TrendingUp,
      insight:
        "A previsão ficou mais precisa após separar consumo recorrente de ações promocionais.",
    },
    {
      id: "average-price",
      title: "Preço Médio por Vendedor/Região",
      value: `${formatKpiCompactCurrency(metric.averagePricePerKg)}/kg`,
      status: "Normal",
      change: 1.2,
      lowerIsBetter: false,
      icon: BadgeDollarSign,
      insight: "Bauru apresenta preço médio inferior devido ao mix e à maior distância logística.",
    },
    {
      id: "average-ticket",
      title: "Ticket Médio",
      value: formatKpiCompactCurrency(metric.averageTicket),
      status: "Normal",
      change: 4.8,
      lowerIsBetter: false,
      icon: ReceiptText,
      insight:
        "O ticket aumentou com venda combinada de congelados, treinamento e locação de fornos.",
    },
    {
      id: "ltv",
      title: "Lifetime Value (LTV)",
      value: formatKpiCompactCurrency(metric.lifetimeValue),
      status: "Normal",
      change: 5.6,
      lowerIsBetter: false,
      icon: Gem,
      insight:
        "Clientes com forno alugado e mix recorrente apresentam maior retenção e valor potencial.",
    },
  ];
}

const customerEvidence: SalesEvidence[] = [
  {
    id: "customer-1",
    title: "Padaria Santa Clara",
    subtitle: "Marília · Vendedor Carlos Lima",
    subject: "Padaria Santa Clara",
    fields: [
      { label: "Consumo", value: "1.860 kg" },
      { label: "Variação", value: "+18,4%" },
      { label: "Equipamento", value: "Forno Turbo alugado" },
      { label: "Última compra", value: "23/06/2026" },
    ],
  },
  {
    id: "customer-2",
    title: "Supermercado Vitória",
    subtitle: "Bauru · Vendedora Ana Paula",
    subject: "Supermercado Vitória",
    fields: [
      { label: "Consumo", value: "1.420 kg" },
      { label: "Variação", value: "-9,2%" },
      { label: "Mix", value: "4 produtos" },
      { label: "Última compra", value: "21/06/2026" },
    ],
  },
];
const productEvidence: SalesEvidence[] = [
  {
    id: "product-1",
    title: "Pão Francês Congelado 60g",
    subtitle: "Linha de maior crescimento",
    subject: "Pão Francês Congelado 60g",
    fields: [
      { label: "Volume", value: "68.400 kg" },
      { label: "Variação", value: "+12,4%" },
      { label: "Clientes", value: "148" },
      { label: "Preço médio", value: "R$ 18,72/kg" },
    ],
  },
  {
    id: "product-2",
    title: "Pão de Queijo 1kg",
    subtitle: "Pico sazonal de junho",
    subject: "Pão de Queijo 1kg",
    fields: [
      { label: "Volume", value: "31.800 kg" },
      { label: "Variação", value: "+16,8%" },
      { label: "Região", value: "Marília" },
      { label: "Sazonalidade", value: "Alta" },
    ],
  },
];
const decliningProductEvidence: SalesEvidence[] = [
  {
    id: "product-decline-1",
    title: "Croissant Congelado 80g",
    subtitle: "Queda concentrada em Bauru",
    subject: "Croissant Congelado 80g",
    fields: [
      { label: "Volume", value: "8.940 kg" },
      { label: "Variação", value: "-6,4%" },
      { label: "Vendedora", value: "Ana Paula" },
      { label: "Região", value: "Bauru" },
    ],
  },
];
const pipelineEvidence: SalesEvidence[] = [
  {
    id: "pipeline-1",
    title: "Rede Primavera",
    subtitle: "Proposta com forno em avaliação",
    subject: "proposta da Rede Primavera",
    fields: [
      { label: "Vendedor", value: "Rafael Souza" },
      { label: "Região", value: "Tupã" },
      { label: "Etapa", value: "Teste de produto" },
      { label: "Tempo aberto", value: "26 dias" },
    ],
  },
  {
    id: "pipeline-2",
    title: "Padaria Bela Vista",
    subtitle: "Negociação de mix congelado",
    subject: "proposta da Padaria Bela Vista",
    fields: [
      { label: "Vendedora", value: "Ana Paula" },
      { label: "Região", value: "Bauru" },
      { label: "Etapa", value: "Proposta" },
      { label: "Tempo aberto", value: "19 dias" },
    ],
  },
];

function factorsFor(id: SalesKpiId): SalesFactor[] {
  if (id === "sales-volume")
    return [
      {
        id: "sales",
        title: "Linhas que sustentam o crescimento",
        summary:
          "Pães congelados de maior recorrência concentram o avanço do faturamento e do peso vendido.",
        cause: "sales_growth",
        evidence: productEvidence,
      },
    ];
  if (id === "bonus-volume")
    return [
      {
        id: "bonus",
        title: "Bonificação acima do crescimento",
        summary: "Três campanhas concentram 62% do valor bonificado.",
        cause: "bonus_efficiency",
        evidence: customerEvidence,
      },
    ];
  if (id === "active-consumption")
    return [
      {
        id: "customers",
        title: "Clientes que elevaram o consumo",
        summary:
          "O crescimento está concentrado nos clientes com maior frequência e forno alugado.",
        cause: "active_consumption_growth",
        evidence: customerEvidence.slice(0, 1),
      },
    ];
  if (id === "product-consumption")
    return [
      {
        id: "products-growth",
        title: "Produtos em crescimento",
        summary: "Pão Francês e Pão de Queijo respondem pela maior parte do avanço.",
        cause: "product_growth",
        evidence: productEvidence,
      },
      {
        id: "products-drop",
        title: "Produtos com perda de consumo",
        summary: "O Croissant Congelado perdeu consumo principalmente na região de Bauru.",
        cause: "product_drop",
        evidence: decliningProductEvidence,
      },
    ];
  if (id === "conversion")
    return [
      {
        id: "pipeline",
        title: "Oportunidades sem fechamento",
        summary: "Aprovação de equipamentos e teste de produto são os principais bloqueios.",
        cause: "conversion_gap",
        evidence: pipelineEvidence,
      },
    ];
  if (id === "conversion-time")
    return [
      {
        id: "time",
        title: "Negociações acima do prazo",
        summary: "Propostas com locação de forno levam oito dias a mais para fechar.",
        cause: "slow_pipeline",
        evidence: pipelineEvidence,
      },
    ];
  if (id === "seasonality")
    return [
      {
        id: "season",
        title: "Produtos com efeito sazonal",
        summary: "Pão de Queijo e linhas de conveniência aceleraram no período junino.",
        cause: "seasonality",
        evidence: productEvidence,
      },
    ];
  if (id === "retention")
    return [
      {
        id: "retention",
        title: "Clientes com risco de redução",
        summary: "Clientes com baixa frequência e mix estreito concentram o risco.",
        cause: "retention_risk",
        evidence: customerEvidence.slice(1),
      },
    ];
  if (id === "mape")
    return [
      {
        id: "forecast",
        title: "Desvios da previsão comercial",
        summary: "Promoções e instalações de equipamentos distorceram a demanda prevista.",
        cause: "forecast_error",
        evidence: productEvidence,
      },
    ];
  if (id === "average-price")
    return [
      {
        id: "price",
        title: "Diferença de preço por região",
        summary: "Bauru opera abaixo da média por frete e composição do mix.",
        cause: "price_gap",
        evidence: customerEvidence.slice(1),
      },
    ];
  if (id === "average-ticket")
    return [
      {
        id: "ticket",
        title: "Composição do ticket",
        summary: "Mix mais amplo e locação de forno explicam o aumento do valor por pedido.",
        cause: "ticket_growth",
        evidence: customerEvidence.slice(0, 1),
      },
    ];
  if (id === "ltv")
    return [
      {
        id: "ltv",
        title: "Clientes de maior valor potencial",
        summary: "Recorrência, mix e permanência da locação sustentam o valor do relacionamento.",
        cause: "ltv_growth",
        evidence: customerEvidence.slice(0, 1),
      },
    ];
  return [];
}

function historyValue(id: SalesKpiId, value: number): string {
  if (["sales-volume", "bonus-volume", "average-ticket", "ltv"].includes(id))
    return currency.format(value);
  if (id === "conversion-time") return `${value.toFixed(1)} dias`;
  if (id === "average-price") return `${currency.format(value)}/kg`;
  if (id === "seasonality") return value.toFixed(2);
  return percentage(value);
}

function compactCurrency(value: number): string {
  return formatKpiCompactCurrency(value);
}

function SalesRevenueExpenseChart() {
  const [period, setPeriod] = useState<SalesFinancialPeriod>("monthly");
  const data = useMemo(
    () =>
      buildSalesRevenueExpenseSeries(
        DEMO_SALES_FINANCIAL_RECORDS,
        period,
        SALES_FINANCIAL_REFERENCE_DATE,
      ),
    [period],
  );

  return (
    <Card className="sales-financial-chart overflow-hidden border-[var(--sales-financial-border)] shadow-sm">
      <CardContent className="p-4 sm:p-6">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0 pt-1">
            <h2 className="text-sm font-display font-semibold leading-tight tracking-tight text-[var(--sales-financial-title)]">
              Gastos × Faturamento
            </h2>
          </div>
          <Select
            value={period}
            onValueChange={(value) => setPeriod(value as SalesFinancialPeriod)}
          >
            <SelectTrigger
              className="h-8 w-full border-[var(--sales-financial-border)] bg-[var(--sales-financial-control)] text-xs text-[var(--sales-financial-title)] sm:w-28"
              aria-label="Período do gráfico financeiro"
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {(Object.entries(financialPeriodLabels) as Array<[SalesFinancialPeriod, string]>).map(
                ([value, label]) => (
                  <SelectItem key={value} value={value}>
                    {label}
                  </SelectItem>
                ),
              )}
            </SelectContent>
          </Select>
        </div>
        <div className="mt-3 flex flex-wrap gap-4 text-xs text-[var(--sales-financial-muted)]">
          <span className="inline-flex items-center gap-2">
            <i className="size-2.5 rounded-full bg-[var(--sales-financial-revenue)]" />
            Faturamento
          </span>
          <span className="inline-flex items-center gap-2">
            <i className="size-2.5 rounded-full bg-[var(--sales-financial-expenses)]" />
            Gastos
          </span>
        </div>
        <div className="mt-2 h-64 w-full sm:h-72">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={data} margin={{ left: 0, right: 12, top: 12, bottom: 0 }}>
              <defs>
                <linearGradient id="sales-financial-revenue-fill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--sales-financial-revenue-fill)" stopOpacity={0.34} />
                  <stop offset="55%" stopColor="var(--sales-financial-revenue-fill)" stopOpacity={0.14} />
                  <stop offset="100%" stopColor="var(--sales-financial-revenue-fill)" stopOpacity={0.02} />
                </linearGradient>
                <linearGradient id="sales-financial-expenses-fill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--sales-financial-expenses-fill)" stopOpacity={0.28} />
                  <stop offset="70%" stopColor="var(--sales-financial-expenses-fill)" stopOpacity={0.08} />
                  <stop offset="100%" stopColor="var(--sales-financial-expenses-fill)" stopOpacity={0.01} />
                </linearGradient>
              </defs>
              <CartesianGrid vertical={false} stroke="var(--sales-financial-grid)" strokeDasharray="3 8" />
              <XAxis
                dataKey="label"
                axisLine={false}
                tickLine={false}
                tick={{ fill: "var(--sales-financial-axis)", fontSize: 11 }}
              />
              <YAxis
                width={52}
                axisLine={false}
                tickLine={false}
                tickFormatter={compactCurrency}
                tick={{ fill: "var(--sales-financial-axis)", fontSize: 11 }}
              />
              <Tooltip
                formatter={(value, name) => [
                  currency.format(Number(value)),
                  name === "revenue" ? "Faturamento" : "Gastos",
                ]}
                labelFormatter={(label) => `${financialPeriodLabels[period]}: ${label}`}
                contentStyle={{
                  background: "var(--sales-financial-tooltip)",
                  border: "1px solid var(--sales-financial-tooltip-border)",
                  borderRadius: "0.75rem",
                  color: "var(--sales-financial-title)",
                }}
                cursor={{ stroke: "var(--sales-financial-cursor)", strokeWidth: 1.5 }}
              />
              <Area
                type="linear"
                dataKey="expenses"
                stroke="none"
                fill="url(#sales-financial-expenses-fill)"
                isAnimationActive={false}
              />
              <Area
                type="linear"
                dataKey="revenue"
                stroke="none"
                fill="url(#sales-financial-revenue-fill)"
                isAnimationActive={false}
              />
              <Line
                type="linear"
                dataKey="expenses"
                stroke="var(--sales-financial-expenses)"
                strokeWidth={2.2}
                dot={{ r: 2.5, fill: "var(--sales-financial-expenses)", strokeWidth: 0 }}
                activeDot={{ r: 4 }}
              />
              <Line
                type="linear"
                dataKey="revenue"
                stroke="var(--sales-financial-revenue)"
                strokeWidth={2.4}
                dot={{ r: 2.5, fill: "var(--sales-financial-revenue)", strokeWidth: 0 }}
                activeDot={{ r: 4 }}
              />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      </CardContent>
    </Card>
  );
}

function SalesTrendChart({
  id,
  history,
  period,
}: {
  id: SalesKpiId;
  history: SalesHistoryPoint[];
  period: SalesControlPeriodDays;
}) {
  return (
    <div className="sales-chart-card rounded-xl border p-4">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-semibold text-[var(--sales-chart-title)]">
            Evolução do indicador
          </p>
          <p className="text-xs text-[var(--sales-chart-muted)]">
            {period === 1 ? "Hoje, por horário" : `Últimos ${period} dias`}
          </p>
        </div>
        <Badge variant="outline">{periodLabels[period]}</Badge>
      </div>
      <div className="mt-3 h-44">
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={history} margin={{ left: 0, right: 8, top: 8 }}>
            <defs>
              <linearGradient id="salesControlTrend" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor="var(--sales-chart-line)" stopOpacity={0.4} />
                <stop offset="100%" stopColor="var(--sales-chart-line)" stopOpacity={0.02} />
              </linearGradient>
            </defs>
            <CartesianGrid vertical={false} stroke="var(--sales-chart-grid)" />
            <XAxis
              dataKey="label"
              axisLine={false}
              tickLine={false}
              tick={{ fill: "var(--sales-chart-axis)", fontSize: 10 }}
            />
            <YAxis
              width={48}
              axisLine={false}
              tickLine={false}
              tick={{ fill: "var(--sales-chart-axis)", fontSize: 10 }}
            />
            <Tooltip formatter={(value) => [historyValue(id, Number(value)), "Valor"]} />
            <Area
              type="monotone"
              dataKey="value"
              stroke="var(--sales-chart-line)"
              strokeWidth={2.3}
              fill="url(#salesControlTrend)"
              dot={{ r: 2.5, fill: "var(--sales-chart-line)", strokeWidth: 0 }}
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

export function SalesControlTower() {
  const [period, setPeriod] = useState<SalesControlPeriodDays>(30);
  const [selectedId, setSelectedId] = useState<SalesKpiId | null>(null);
  const [factor, setFactor] = useState<SalesFactor | null>(null);
  const [evidence, setEvidence] = useState<SalesEvidence | null>(null);
  const cards = useMemo(() => buildCards(period), [period]);
  const selected = cards.find((card) => card.id === selectedId) ?? null;
  const factors = useMemo(() => (selectedId ? factorsFor(selectedId) : []), [selectedId]);
  const history = useMemo(
    () => (selectedId ? buildSalesKpiHistory(selectedId, period) : []),
    [selectedId, period],
  );

  return (
    <div className="page-shell app-background space-y-6">
      <header className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <span className="page-header-kicker">Vendas</span>
          <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">
            Torre Comercial
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Identifique variações, encontre causas e transforme sinais comerciais em ações.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {periods.map((days) => (
            <Button
              key={days}
              size="sm"
              variant={period === days ? "default" : "outline"}
              onClick={() => setPeriod(days)}
            >
              {periodLabels[days]}
            </Button>
          ))}
        </div>
      </header>
      <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
        {cards.map((card) => {
          const Icon = card.icon;
          return (
            <button
              type="button"
              key={card.id}
              onClick={() => {
                setSelectedId(card.id);
                setFactor(null);
                setEvidence(null);
              }}
              className="h-full text-left"
            >
              <Card className="h-full">
                <CardContent className="relative flex h-full flex-col p-6 sm:p-7">
                  <div className="min-h-11 pr-14">
                    <h2 className="flex min-h-10 items-center text-balance text-sm font-semibold leading-snug">
                      {card.title}
                    </h2>
                    <span className="absolute right-6 top-6 inline-flex size-9 items-center justify-center rounded-full border border-primary/10 bg-primary/10 text-primary sm:right-7 sm:top-7">
                      <Icon className="size-4" />
                    </span>
                  </div>
                  <p className="mt-4 text-2xl font-display tracking-tight">{card.value}</p>
                  <div className="mt-3 flex flex-wrap items-center gap-2">
                    <Badge variant="outline" className={statusClass(card.status)}>
                      {card.status}
                    </Badge>
                    <span
                      className={cn(
                        "text-xs",
                        card.change < 0 === card.lowerIsBetter
                          ? "text-emerald-600"
                          : "text-amber-600",
                      )}
                    >
                      {changeLabel(card.change)}
                    </span>
                  </div>
                </CardContent>
              </Card>
            </button>
          );
        })}
      </section>
      <SalesRevenueExpenseChart />
      <Dialog
        open={selected != null && factor == null}
        onOpenChange={(open) => !open && factor == null && setSelectedId(null)}
      >
        <DialogContent className="custom-scrollbar max-h-[90vh] w-[94vw] max-w-4xl overflow-y-auto">
          <>
            {selected && (
              <>
                <DialogHeader>
                  <div className="flex items-center gap-2">
                    <DialogTitle>{selected.title}</DialogTitle>
                    <Badge className={statusClass(selected.status)} variant="outline">
                      {selected.status}
                    </Badge>
                  </div>
                  <DialogDescription>Nível 2 · Entender o resultado</DialogDescription>
                </DialogHeader>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border bg-muted/20 p-4">
                    <p className="text-xs uppercase text-muted-foreground">O que aconteceu</p>
                    <p className="mt-2 text-xl font-semibold">{selected.value}</p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {changeLabel(selected.change)}
                    </p>
                  </div>
                  <div className="rounded-lg border bg-muted/20 p-4">
                    <p className="text-xs uppercase text-muted-foreground">
                      Por que está neste status
                    </p>
                    <p className="mt-2 text-sm text-muted-foreground">{selected.insight}</p>
                  </div>
                </div>
                <SalesTrendChart id={selected.id} history={history} period={period} />
                <div>
                  <p className="text-sm font-semibold">Principais fatores</p>
                  <div className="mt-3 space-y-2">
                    {factors.map((item) => (
                      <button
                        key={item.id}
                        type="button"
                        onClick={() => setFactor(item)}
                        className="flex w-full items-center justify-between rounded-lg border p-4 text-left hover:border-primary/40"
                      >
                        <div>
                          <p className="text-sm font-semibold">{item.title}</p>
                          <p className="mt-1 text-xs text-muted-foreground">{item.summary}</p>
                        </div>
                        <ChevronRight className="size-4 text-primary" />
                      </button>
                    ))}
                  </div>
                </div>
              </>
            )}
          </>
        </DialogContent>
      </Dialog>
      <Dialog
        open={factor != null}
        onOpenChange={(open) => {
          if (!open) {
            setFactor(null);
            setEvidence(null);
          }
        }}
      >
        <DialogContent className="custom-scrollbar max-h-[90vh] w-[94vw] max-w-4xl overflow-y-auto">
          {factor && selected && (
            <>
              <DialogHeader>
                <div className="flex items-center gap-1 text-xs text-muted-foreground">
                  <span>{selected.title}</span>
                  <ChevronRight className="size-3" />
                  <span>{factor.title}</span>
                </div>
                <DialogTitle>Nível 3 · Investigação</DialogTitle>
                <DialogDescription>{factor.summary}</DialogDescription>
              </DialogHeader>
              <div className="space-y-2">
                {factor.evidence.map((item) => (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => setEvidence(item)}
                    className={cn(
                      "w-full rounded-lg border p-4 text-left",
                      evidence?.id === item.id && "border-primary bg-primary/[0.04]",
                    )}
                  >
                    <p className="text-sm font-semibold">{item.title}</p>
                    <p className="text-xs text-muted-foreground">{item.subtitle}</p>
                    <div className="mt-3 grid grid-cols-2 gap-2">
                      {item.fields.map((field) => (
                        <div key={field.label}>
                          <p className="text-[10px] uppercase text-muted-foreground">
                            {field.label}
                          </p>
                          <p className="text-xs font-medium">{field.value}</p>
                        </div>
                      ))}
                    </div>
                  </button>
                ))}
              </div>
              {evidence ? (
                <div className="rounded-xl border border-primary/20 bg-primary/5 p-5">
                  <div className="flex gap-3">
                    <Sparkles className="size-5 shrink-0 text-primary" />
                    <div>
                      <p className="text-xs font-semibold uppercase text-primary">
                        Nível 4 · Tomada de decisão
                      </p>
                      <p className="mt-2 text-sm font-semibold">Ação para {evidence.title}</p>
                      <p className="mt-2 text-sm text-muted-foreground">
                        {buildContextualSalesRecommendation(factor.cause, evidence.subject)}
                      </p>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="rounded-lg border border-dashed p-4 text-center text-sm text-muted-foreground">
                  Selecione uma evidência para gerar a ação recomendada.
                </div>
              )}
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
