import { FormEvent, useEffect, useMemo, useState } from "react";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { cn } from "@/lib/utils";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { FeedbackMessage } from "@/components/ui/feedback-message";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { KpiCard } from "@/components/ui/kpi-card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { SkeletonChart, SkeletonMetricCard, SkeletonModalContent, SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { InsightCard } from "@/components/ui/insight-card";
import { ArrowDownRight, ArrowUpRight, Building2, CalendarClock, DollarSign, MapPin, Receipt, TrendingUp, UserRound, Users, AlertTriangle, Target, BarChart3 } from "lucide-react";
import { Area, AreaChart, Bar, BarChart, CartesianGrid, Line, LineChart, XAxis, YAxis } from "recharts";
import {
  fetchCustomerAnalyticsSummary,
  fetchFinanceDashboard,
  fetchCustomerIndividualAnalysis,
  fetchCustomerNewCustomersMonthly,
  fetchCustomerPurchaseHistory,
  fetchCustomerRanking,
  fetchCustomerInsights,
  type CustomerIndividualAnalysisScope,
  type CustomerAnalyticsSummary,
  type CustomerDetailSummary,
  type FinanceDashboardResponse,
  type CustomerNewCustomersMonthlyResponse,
  type CustomerPurchaseHistoryResponse,
  type CustomerRankingItem,
  type CustomerInsightsResponse,
  type CustomerTimelineResponse,
} from "@/lib/importer-api";
import {
  formatNullableCurrency,
  formatNullableCurrencyTooltip,
  formatPurchaseFrequency,
  formatVariationPercent,
  resolveRankingTrend,
  resolveCustomerStatusVariant,
} from "@/lib/customer-details";
import { computeNewCustomersInsights } from "@/lib/customer-new-customers";
import { buildCustomerPeriodTrend } from "@/lib/customer-period-insights";
import {
  fetchCustomerFinanceImpact,
  formatImpactActionPercent,
  getImpactCustomerName,
  sortImpactListByAttention,
} from "@/lib/customer-finance-impact";
import { buildSupplierRouteDashboard } from "@/lib/customer-supplier-routing";
import { calculateProjectedMarginPercent, calculateSimulatedProjectedCost, fetchCustomerFinanceProjections } from "@/lib/customer-finance-projections";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";
import { authFetch } from "@/lib/auth";
import { buildServiceUrl } from "@/lib/api-url";
import { VendasPage } from "@/routes/vendas";

export const Route = createFileRoute("/clientes")({
  validateSearch: (search: Record<string, unknown>) => ({
    cliente: typeof search.cliente === "string" ? search.cliente : undefined,
    aba: isClientesTab(search.aba) ? search.aba : undefined,
  }),
  component: ClientesPage,
});

type ClientesTab = "impacto" | "projecoes" | "fornecedores" | "clientes" | "nota-fiscal";

function isClientesTab(value: unknown): value is ClientesTab {
  return value === "impacto" || value === "projecoes" || value === "fornecedores" || value === "clientes" || value === "nota-fiscal";
}

const CLIENT_REVENUE_CHART_LIMIT = 8;
const DEMO_PREVIOUS_REVENUE_FACTOR = 0.92;
const REVENUE_CHART_STROKE = "#f43f5e";
const REVENUE_CHART_GRID = "rgba(148, 163, 184, 0.12)";
const FINANCE_PAGE_SIZE = 20;
const IMPACT_KPI_CARD_CLASS_NAME = "p-3";
const SUPPLIER_SKELETON_CARD_KEYS = ["notified", "routes", "escalated"] as const;
const ALL_SUPPLIERS_FILTER_VALUE = "todos";
const HISTORY_ANALYSIS_SCOPE: CustomerIndividualAnalysisScope = "historical";
const CURRENT_FILTERS_ANALYSIS_SCOPE: CustomerIndividualAnalysisScope = "current";
type CustomerDetailsPeriod = "all" | "1m" | "3m" | "12m";

const DETAILS_PERIOD_OPTIONS: Array<{ value: CustomerDetailsPeriod; label: string }> = [
  { value: "all", label: "Período inteiro" },
  { value: "12m", label: "Último ano" },
  { value: "3m", label: "Últimos 3 meses" },
  { value: "1m", label: "Último mês" },
];

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value ?? 0);
}

function formatDate(value: string | null): string {
  if (!value) return "N/A";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleDateString("pt-BR");
}

function formatMonthYear(value: string): string {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleDateString("pt-BR", { month: "short", year: "numeric" }).replace(".", "");
}

function formatDecimal(value: number): string {
  return new Intl.NumberFormat("pt-BR", { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(value ?? 0);
}

function formatTimelineMetricValue(value: number, metric: CustomerTimelineResponse["metric"]): string {
  if (metric === "revenue") return formatKpiCompactCurrency(value);
  return formatKpiCompactNumber(value);
}

function describeTimelineGranularity(granularity: CustomerTimelineResponse["granularity"] | "weekly" | "monthly"): string {
  if (granularity === "weekly") return "semana";
  return "mês";
}

function describeTimelineGranularityPlural(granularity: CustomerTimelineResponse["granularity"] | "weekly" | "monthly"): string {
  if (granularity === "weekly") return "semanas";
  return "meses";
}

function buildAnalysisNarrative(
  trendLabel: ReturnType<typeof buildCustomerPeriodTrend>["trendLabel"],
  granularity: "weekly" | "monthly",
  scope: CustomerIndividualAnalysisScope,
): string {
  const periodLabel = scope === HISTORY_ANALYSIS_SCOPE ? "Nos últimos 12 meses" : "No período filtrado";
  if (trendLabel === "Crescendo") {
    return `${periodLabel}, o cliente apresentou tendência de crescimento, com avanço consistente por ${describeTimelineGranularityPlural(granularity)}.`;
  }

  if (trendLabel === "Caindo") {
    return `${periodLabel}, o cliente apresentou retração e pede acompanhamento mais próximo da frequência e do faturamento.`;
  }

  if (trendLabel === "Estável") {
    return `${periodLabel}, o cliente manteve comportamento estável, sem oscilações relevantes entre os ${describeTimelineGranularityPlural(granularity)} analisados.`;
  }

  return `${periodLabel}, ainda não há base comparável suficiente para interpretar a evolução do cliente.`;
}

const DEMO_CUSTOMER_SUMMARY: CustomerAnalyticsSummary = {
  activeCustomers: 84,
  totalRevenue: 187_590,
  totalOrders: 128,
  averageTicket: 1_465.55,
  averageRevenuePerCustomer: 2_232.02,
  newCustomers: 9,
  inactiveCustomers: 14,
  currentPeriodStart: "2026-06-01",
  currentPeriodEnd: "2026-06-07",
  previousPeriodStart: "2026-05-01",
  previousPeriodEnd: "2026-05-31",
};

const DEMO_CUSTOMER_RANKING: CustomerRankingItem[] = [
  { customerCode: "CLI-001", customerName: "Padaria São Bento", revenue: 64_850, quantity: 2_420, weight: 12_800, orders: 28, averageTicket: 2_316.07, variationPercent: 12.6 },
  { customerCode: "CLI-002", customerName: "Supermercado Primavera", revenue: 52_300, quantity: 3_180, weight: 9_750, orders: 21, averageTicket: 2_490.48, variationPercent: -6.4 },
  { customerCode: "CLI-003", customerName: "Cafeteria Grão & Massa", revenue: 38_940, quantity: 1_760, weight: 4_980, orders: 18, averageTicket: 2_163.33, variationPercent: 24.8 },
  { customerCode: "CLI-004", customerName: "Rede Conveniência Rota 12", revenue: 31_500, quantity: 980, weight: 2_400, orders: 12, averageTicket: 2_625, variationPercent: 5.7 },
];

function makeDemoFinanceDashboard(): FinanceDashboardResponse {
  const totalAmount = DEMO_CUSTOMER_RANKING.reduce((total, item) => total + item.revenue, 0);

  return {
    customers: DEMO_CUSTOMER_RANKING.map((item) => item.customerName),
    summary: {
      totalRevenue: totalAmount,
      totalOrders: DEMO_CUSTOMER_SUMMARY.totalOrders,
      totalQuantity: DEMO_CUSTOMER_RANKING.reduce((total, item) => total + item.quantity, 0),
      averageTicket: DEMO_CUSTOMER_SUMMARY.averageTicket,
    },
    customerRanking: DEMO_CUSTOMER_RANKING.map((item) => ({
      customer: item.customerName,
      revenue: item.revenue,
    })),
    revenueTrend: [
      { period: "2026-01", label: "jan", revenue: 38_200 },
      { period: "2026-02", label: "fev", revenue: 42_700 },
      { period: "2026-03", label: "mar", revenue: 39_900 },
      { period: "2026-04", label: "abr", revenue: 51_600 },
      { period: "2026-05", label: "mai", revenue: 58_300 },
      { period: "2026-06", label: "jun", revenue: totalAmount },
    ],
    items: DEMO_CUSTOMER_RANKING.map((item, index) => ({
      customer: item.customerName,
      date: `2026-06-0${Math.min(index + 3, 7)}`,
      revenue: item.revenue,
      orders: item.orders,
      quantity: item.quantity,
    })),
    page: 1,
    pageSize: FINANCE_PAGE_SIZE,
    totalItems: DEMO_CUSTOMER_RANKING.length,
    totalPages: 1,
  };
}

type PeriodPreset = "today" | "week" | "month" | "quarter" | "year" | "custom";

const periodOptions: Array<{ value: PeriodPreset; label: string }> = [
  { value: "today", label: "Hoje" },
  { value: "week", label: "Semana" },
  { value: "month", label: "Mês" },
  { value: "quarter", label: "Trimestre" },
  { value: "year", label: "Ano" },
  { value: "custom", label: "Personalizado" },
];

function toInputDate(value: Date): string {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, "0");
  const day = String(value.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function startOfWeek(date: Date): Date {
  const copy = new Date(date);
  const diff = (copy.getDay() + 6) % 7;
  copy.setDate(copy.getDate() - diff);
  return copy;
}

function resolvePeriod(preset: PeriodPreset): { dateFrom: string; dateTo: string } {
  const now = new Date();
  if (preset === "today") return { dateFrom: toInputDate(now), dateTo: toInputDate(now) };
  if (preset === "week") return { dateFrom: toInputDate(startOfWeek(now)), dateTo: toInputDate(now) };
  if (preset === "quarter") {
    const quarterStartMonth = Math.floor(now.getMonth() / 3) * 3;
    return { dateFrom: toInputDate(new Date(now.getFullYear(), quarterStartMonth, 1)), dateTo: toInputDate(now) };
  }
  if (preset === "year") return { dateFrom: toInputDate(new Date(now.getFullYear(), 0, 1)), dateTo: toInputDate(now) };
  return { dateFrom: toInputDate(new Date(now.getFullYear(), now.getMonth(), 1)), dateTo: toInputDate(now) };
}

function filterDemoCustomers(customerFilter: string): CustomerRankingItem[] {
  const normalized = customerFilter.trim().toLowerCase();
  if (!normalized) return DEMO_CUSTOMER_RANKING;

  return DEMO_CUSTOMER_RANKING.filter((item) => (
    item.customerCode.toLowerCase().includes(normalized) ||
    item.customerName.toLowerCase().includes(normalized)
  ));
}

function sortDemoCustomers(
  items: CustomerRankingItem[],
  sortBy: "revenue" | "growth" | "drop" | "quantity" | "weight" | "ticket",
): CustomerRankingItem[] {
  const sorted = [...items];
  if (sortBy === "growth") return sorted.sort((a, b) => (b.variationPercent ?? -Infinity) - (a.variationPercent ?? -Infinity));
  if (sortBy === "drop") return sorted.sort((a, b) => (a.variationPercent ?? Infinity) - (b.variationPercent ?? Infinity));
  if (sortBy === "quantity") return sorted.sort((a, b) => b.quantity - a.quantity);
  if (sortBy === "weight") return sorted.sort((a, b) => b.weight - a.weight);
  if (sortBy === "ticket") return sorted.sort((a, b) => b.averageTicket - a.averageTicket);
  return sorted.sort((a, b) => b.revenue - a.revenue);
}

function buildRiskCustomerActionSuggestions(customer: any): string[] {
  const customerName = getImpactCustomerName(customer);
  const riskLevel = customer.nivelRisco ?? customer.NivelRisco ?? "risco";
  const declinePeriod = customer.mesesQueda ?? customer.MesesQueda ?? "período recente";
  const monthlyImpact = formatCurrency(customer.impactoFinanceiro ?? customer.ImpactoFinanceiro ?? 0);

  return [
    `Priorizar contato comercial com ${customerName} em até 24 horas para entender a causa da queda.`,
    `Revisar pedidos, frequência e mix dos últimos meses, pois o cliente está em nível ${riskLevel} e acumula ${declinePeriod}.`,
    `Montar uma oferta de recuperação com condição comercial controlada, limitada ao impacto estimado de ${monthlyImpact}/mês.`,
    "Agendar acompanhamento semanal até estabilizar faturamento, frequência de compra e margem.",
  ];
}

function getSupplierRiskBadgeClassName(riskLevel: string): string {
  if (riskLevel === "critical") return "bg-red-900 text-white";
  if (riskLevel === "high") return "bg-red-600 text-white";
  return "bg-amber-500 text-white";
}

function getSupplierStatusLabel(status: string): string {
  if (status === "escalated") return "Escalado à gerência";
  if (status === "notified") return "Aguardando ação";
  return "Em atenção";
}

function formatSupplierDateTime(value: string | null): string {
  if (!value) return "Sem notificação";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "short" });
}

function RevenueAreaChart({
  data,
  gradientId,
}: {
  data: Array<{ label: string; value: number }>;
  gradientId: string;
}) {
  return (
    <ChartContainer config={{ value: { label: "Faturamento", color: REVENUE_CHART_STROKE } }} className="h-[260px] min-h-[260px] w-full text-[11px]">
      <AreaChart data={data} margin={{ left: 6, right: 12, top: 14, bottom: 8 }}>
        <defs>
          <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor={REVENUE_CHART_STROKE} stopOpacity={0.55} />
            <stop offset="92%" stopColor={REVENUE_CHART_STROKE} stopOpacity={0.04} />
          </linearGradient>
        </defs>
        <CartesianGrid vertical={false} stroke={REVENUE_CHART_GRID} />
        <XAxis dataKey="label" axisLine={false} tickLine={false} tick={{ fill: "#8b95a7", fontSize: 11 }} minTickGap={16} />
        <YAxis width={72} axisLine={false} tickLine={false} tick={{ fill: "#8b95a7", fontSize: 11 }} tickFormatter={(value) => formatKpiCompactCurrency(Number(value))} />
        <ChartTooltip content={<ChartTooltipContent className="border-slate-700 bg-slate-950 text-slate-100" formatter={(value) => formatCurrency(Number(value))} />} />
        <Area dataKey="value" name="Faturamento" type="monotone" stroke="var(--color-value)" strokeWidth={2.6} fill={`url(#${gradientId})`} dot={{ r: 3, fill: REVENUE_CHART_STROKE, strokeWidth: 0 }} activeDot={{ r: 5, fill: REVENUE_CHART_STROKE, stroke: "#fee2e2", strokeWidth: 2 }} />
      </AreaChart>
    </ChartContainer>
  );
}

function ReceitaVsCustoChart({ data }: { data: Array<{ label: string; receita: number; custo: number }> }) {
  return (
    <ChartContainer config={{ receita: { label: "Receita", color: "#059669" }, custo: { label: "Custo", color: "#B91C1C" } }} className="h-[260px] min-h-[260px] w-full text-[11px]">
      <AreaChart data={data} margin={{ left: 6, right: 12, top: 14, bottom: 8 }}>
        <defs>
          <linearGradient id="receita-gradient" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="#059669" stopOpacity={0.45} />
            <stop offset="92%" stopColor="#059669" stopOpacity={0.04} />
          </linearGradient>
          <linearGradient id="custo-gradient" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="#B91C1C" stopOpacity={0.35} />
            <stop offset="92%" stopColor="#B91C1C" stopOpacity={0.03} />
          </linearGradient>
        </defs>
        <CartesianGrid vertical={false} stroke={REVENUE_CHART_GRID} />
        <XAxis dataKey="label" axisLine={false} tickLine={false} tick={{ fill: "#8b95a7", fontSize: 11 }} minTickGap={16} />
        <YAxis width={72} axisLine={false} tickLine={false} tick={{ fill: "#8b95a7", fontSize: 11 }} tickFormatter={(value) => formatKpiCompactCurrency(Number(value))} />
        <ChartTooltip content={<ChartTooltipContent className="border-slate-700 bg-slate-950 text-slate-100" formatter={(value, name) => [`${formatCurrency(Number(value))}`, name === "custo" ? "Custo" : "Receita"]} />} />
        <Area dataKey="receita" name="receita" type="monotone" stroke="#059669" strokeWidth={2.6} fill="url(#receita-gradient)" dot={{ r: 3, fill: "#059669", strokeWidth: 0 }} activeDot={{ r: 5, fill: "#059669", stroke: "#fee2e2", strokeWidth: 2 }} />
        <Area dataKey="custo" name="custo" type="monotone" stroke="#B91C1C" strokeWidth={2} fill="url(#custo-gradient)" strokeDasharray="6 4" dot={false} />
      </AreaChart>
    </ChartContainer>
  );
}

function RankingBarChart({ data }: { data: Array<{ label: string; value: number }> }) {
  return (
    <ChartContainer config={{ value: { label: "Faturamento", color: REVENUE_CHART_STROKE } }} className="h-[260px] min-h-[260px] w-full text-[11px]">
      <BarChart data={data} margin={{ left: 6, right: 12, top: 14, bottom: 18 }}>
        <defs>
          <linearGradient id="ranking-bar-fill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={REVENUE_CHART_STROKE} stopOpacity={0.92} />
            <stop offset="100%" stopColor={REVENUE_CHART_STROKE} stopOpacity={0.72} />
          </linearGradient>
        </defs>
        <CartesianGrid vertical={false} stroke={REVENUE_CHART_GRID} strokeDasharray="3 6" />
        <XAxis
          dataKey="label"
          axisLine={false}
          tickLine={false}
          tick={{ fill: "#8b95a7", fontSize: 10 }}
          interval={0}
          height={50}
          angle={-20}
          textAnchor="end"
          tickFormatter={(value) => String(value).length > 14 ? `${String(value).slice(0, 14)}...` : String(value)}
        />
        <YAxis
          width={72}
          axisLine={false}
          tickLine={false}
          tick={{ fill: "#8b95a7", fontSize: 11 }}
          tickFormatter={(value) => formatKpiCompactCurrency(Number(value))}
        />
        <ChartTooltip content={<ChartTooltipContent className="border-slate-700 bg-slate-950 text-slate-100" formatter={(value) => formatCurrency(Number(value))} />} />
        <Bar dataKey="value" name="Faturamento" fill="url(#ranking-bar-fill)" radius={[6, 6, 0, 0]} maxBarSize={48} />
      </BarChart>
    </ChartContainer>
  );
}

function formatProjectionConfidence(value: number | null | undefined): string {
  if (value == null) return "—";
  const normalized = value > 1 ? value : value * 100;
  return `${normalized.toFixed(0)}%`;
}

function ClientesPage() {
  const navigate = useNavigate({ from: "/clientes" });
  const { cliente, aba } = Route.useSearch();
  const [summary, setSummary] = useState<CustomerAnalyticsSummary | null>(null);
  const [items, setItems] = useState<CustomerRankingItem[]>([]);
  const [financeDashboard, setFinanceDashboard] = useState<FinanceDashboardResponse | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalItems, setTotalItems] = useState(0);
  const [sortBy, setSortBy] = useState<"revenue" | "growth" | "drop" | "quantity" | "weight" | "ticket">("revenue");
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const [activeTab, setActiveTab] = useState<ClientesTab>(aba ?? "impacto");
  const [impactoData, setImpactoData] = useState<any>(null);
  const [impactoLoading, setImpactoLoading] = useState(false);
  const [riskActionCustomer, setRiskActionCustomer] = useState<any | null>(null);
  const [selectedSupplier, setSelectedSupplier] = useState(ALL_SUPPLIERS_FILTER_VALUE);
  const [supplierCaseCustomer, setSupplierCaseCustomer] = useState<any | null>(null);
  const [projecoesData, setProjecoesData] = useState<any>(null);
  const [projecoesLoading, setProjecoesLoading] = useState(false);
  const [historicoData, setHistoricoData] = useState<any>(null);
  const [historicoLoading, setHistoricoLoading] = useState(false);
  const [historicoSortBy, setHistoricoSortBy] = useState("revenue");

  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>("quarter");
  const [dateFrom, setDateFrom] = useState(() => resolvePeriod("quarter").dateFrom);
  const [dateTo, setDateTo] = useState(() => toInputDate(new Date()));
  const [customer, setCustomer] = useState("");
  const [city, setCity] = useState("");
  const [productGroup, setProductGroup] = useState("");
  const [productCode, setProductCode] = useState("");
  const [transactionType, setTransactionType] = useState("");

  const [selectedCustomerId, setSelectedCustomerId] = useState<string | null>(null);
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [detailsMessage, setDetailsMessage] = useState("");
  const [detailsPeriod, setDetailsPeriod] = useState<CustomerDetailsPeriod>("12m");
  const [details, setDetails] = useState<CustomerDetailSummary | null>(null);
  const [timeline, setTimeline] = useState<CustomerTimelineResponse | null>(null);
  const [insights, setInsights] = useState<CustomerInsightsResponse | null>(null);
  const [history, setHistory] = useState<CustomerPurchaseHistoryResponse | null>(null);
  const [historyPage, setHistoryPage] = useState(1);
  const [analysisScope, setAnalysisScope] = useState<CustomerIndividualAnalysisScope>(HISTORY_ANALYSIS_SCOPE);
  const [resolvedAnalysisPeriod, setResolvedAnalysisPeriod] = useState<{ periodStart: string; periodEnd: string } | null>(null);
  const [timelineMetric, setTimelineMetric] = useState<"revenue" | "quantity" | "weight" | "orders" | "averageTicket">("revenue");
  const [newCustomersOpen, setNewCustomersOpen] = useState(false);
  const [newCustomersLoading, setNewCustomersLoading] = useState(false);
  const [newCustomersMessage, setNewCustomersMessage] = useState("");
  const [newCustomersMonthly, setNewCustomersMonthly] = useState<CustomerNewCustomersMonthlyResponse | null>(null);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalItems / pageSize)), [totalItems, pageSize]);
  const historyTotalPages = useMemo(
    () => Math.max(1, Math.ceil((history?.totalItems ?? 0) / (history?.pageSize ?? 10))),
    [history?.totalItems, history?.pageSize],
  );
  const financeMetrics = financeDashboard?.summary ?? makeDemoFinanceDashboard().summary;
  const financeRevenueTrendData = useMemo(
    () => (financeDashboard?.revenueTrend ?? []).map((item) => ({
      label: item.label,
      value: item.revenue,
    })),
    [financeDashboard?.revenueTrend],
  );
  const financeCustomerRankingData = useMemo(
    () => (financeDashboard?.customerRanking ?? []).slice(0, CLIENT_REVENUE_CHART_LIMIT).map((item) => ({
      label: item.customer,
      value: item.revenue,
    })),
    [financeDashboard?.customerRanking],
  );
  const projecoesReceitaCustoData = useMemo(
    () => (financeDashboard?.revenueTrend ?? []).map((item, index) => ({
      label: item.label,
      receita: item.revenue,
      custo: calculateSimulatedProjectedCost(item.revenue, index),
    })),
    [financeDashboard?.revenueTrend],
  );
  const supplierRouteDashboard = useMemo(
    () => buildSupplierRouteDashboard(impactoData?.risco ?? [], selectedSupplier),
    [impactoData?.risco, selectedSupplier],
  );
  const timelineChartData = useMemo(
    () => {
      const base = (timeline?.points ?? []).map((point) => ({
        ...point,
        chartValue: Number(point.value ?? point[timelineMetric] ?? 0),
        predictedValue: null as number | null,
        isForecast: false,
      }));

      if (!timeline || base.length === 0 || timeline.granularity !== "monthly" || timelineMetric !== "revenue" || insights?.predictedRevenue == null) {
        return base;
      }

      const lastDate = new Date(base[base.length - 1].periodStart);
      if (Number.isNaN(lastDate.getTime())) return base;
      const predictedDate = new Date(lastDate);
      predictedDate.setMonth(predictedDate.getMonth() + 1);

      return [
        ...base,
        {
          periodStart: predictedDate.toISOString(),
          value: insights.predictedRevenue,
          revenue: insights.predictedRevenue,
          quantity: 0,
          weight: 0,
          orders: 0,
          chartValue: null,
          predictedValue: insights.predictedRevenue,
          isForecast: true,
        },
      ];
    },
    [timeline, timelineMetric, insights?.predictedRevenue],
  );
  const periodTrendSummary = useMemo(
    () => buildCustomerPeriodTrend(timeline?.points ?? []),
    [timeline],
  );
  const analysisNarrative = useMemo(
    () => buildAnalysisNarrative(periodTrendSummary.trendLabel, timeline?.granularity ?? "monthly", analysisScope),
    [analysisScope, periodTrendSummary.trendLabel, timeline?.granularity],
  );

  async function load(targetPage: number) {
    setLoading(true);
    setMessage("");
    const errors: string[] = [];

    const results = await Promise.allSettled([
      fetchCustomerAnalyticsSummary({ dateFrom, dateTo, customer, city, productGroup, productCode, transactionType }),
      fetchCustomerRanking({ page: targetPage, pageSize, sortBy, dateFrom, dateTo, customer, city, productGroup, productCode, transactionType }),
      fetchFinanceDashboard({
        customer,
        dateFrom,
        dateTo,
        allTime: false,
        revenueGranularity: "monthly",
        page: 1,
        pageSize: FINANCE_PAGE_SIZE,
      }),
    ]);

    if (results[0].status === "fulfilled") {
      setSummary(results[0].value);
    } else {
      errors.push(`Resumo: ${(results[0].reason as Error)?.message ?? "erro desconhecido"}`);
    }

    if (results[1].status === "fulfilled") {
      const ranking = results[1].value;
      setItems(ranking.items);
      setPage(ranking.page);
      setTotalItems(ranking.totalItems);
    } else {
      errors.push(`Ranking: ${(results[1].reason as Error)?.message ?? "erro desconhecido"}`);
    }

    if (results[2].status === "fulfilled") {
      setFinanceDashboard(results[2].value);
    } else {
      errors.push(`Dashboard Financeiro: ${(results[2].reason as Error)?.message ?? "erro desconhecido"}`);
    }

    if (errors.length > 0) {
      setMessage("Alguns blocos não carregaram: " + errors.join("; "));
    }

    setLoading(false);
  }

  async function loadImpacto() {
    setImpactoLoading(true);
    try {
      setImpactoData(await fetchCustomerFinanceImpact());
    } catch {
      setImpactoData(null);
    } finally {
      setImpactoLoading(false);
    }
  }

  async function loadProjecoes() {
    setProjecoesLoading(true);
    try {
      setProjecoesData(await fetchCustomerFinanceProjections());
    } catch {
      setProjecoesData(null);
    } finally {
      setProjecoesLoading(false);
    }
  }

  async function loadHistorico(pagina = 1, sort = "revenue") {
    setHistoricoLoading(true);
    try {
      const base = buildServiceUrl("api/analytics-financeiro");
      const r = await authFetch(`${base}/historico?pagina=${pagina}&tamanho=20&sortBy=${sort}`);
      if (r.ok) setHistoricoData(await r.json());
    } catch { /* */ } finally { setHistoricoLoading(false); }
  }

  function resolveDetailsDateRange(period: CustomerDetailsPeriod): { dateFrom?: string; dateTo?: string } {
    const hoje = new Date();
    const dataFim = toInputDate(hoje);
    if (period === "all") return { dateTo: dataFim };
    const dias = period === "1m" ? 30 : period === "3m" ? 90 : 365;
    const inicio = new Date(hoje);
    inicio.setDate(inicio.getDate() - dias);
    return { dateFrom: toInputDate(inicio), dateTo: dataFim };
  }

  async function loadCustomerDetails(customerId: string, targetHistoryPage = 1, scope = analysisScope, metric = timelineMetric, period = detailsPeriod) {
    setDetailsLoading(true);
    setDetailsMessage("");
    const dateRange = resolveDetailsDateRange(period);
    try {
      const [analysisData, insightsData] = await Promise.allSettled([
        fetchCustomerIndividualAnalysis({
          customerId,
          scope,
          metric,
          dateFrom: dateRange.dateFrom,
          dateTo: dateRange.dateTo,
        }),
        fetchCustomerInsights({ customerId, movingAverageWindowMonths: 3 }),
      ]);

      if (analysisData.status === "rejected") {
        throw analysisData.reason;
      }

      const analysis = analysisData.value;
      setDetails(analysis.summary);
      setTimeline({
        granularity: analysis.granularity,
        metric: analysis.metric,
        points: analysis.points,
      });
      setResolvedAnalysisPeriod({ periodStart: analysis.periodStart, periodEnd: analysis.periodEnd });

      const historyData = await fetchCustomerPurchaseHistory({
        customerId,
        dateFrom: analysis.periodStart,
        dateTo: analysis.periodEnd,
        page: targetHistoryPage,
        pageSize: 10,
      });

      setInsights(insightsData.status === "fulfilled" ? insightsData.value : null);
      setHistory(historyData);
      setHistoryPage(targetHistoryPage);

      const partialErrors: string[] = [];
      if (insightsData.status === "rejected") partialErrors.push("insights");
      if (partialErrors.length > 0) {
        setDetailsMessage(`Alguns blocos não carregaram (${partialErrors.join(", ")}), mas o resumo do cliente foi exibido.`);
      }
    } catch (error) {
      setDetailsMessage((error as Error).message);
    } finally {
      setDetailsLoading(false);
    }
  }

  useEffect(() => {
    void load(1);
    if (activeTab === "impacto" && !impactoData && !impactoLoading) void loadImpacto();
    if (activeTab === "fornecedores" && !impactoData && !impactoLoading) void loadImpacto();
    if (activeTab === "projecoes" && !projecoesData && !projecoesLoading) void loadProjecoes();
  }, [sortBy, dateFrom, dateTo]);

  useEffect(() => {
    if (!aba || aba === activeTab) return;
    setActiveTab(aba);
    if (aba === "impacto" && !impactoData && !impactoLoading) void loadImpacto();
    if (aba === "fornecedores" && !impactoData && !impactoLoading) void loadImpacto();
    if (aba === "projecoes" && !projecoesData && !projecoesLoading) void loadProjecoes();
    if (aba === "clientes" && !historicoData && !historicoLoading) void loadHistorico(1, historicoSortBy);
  }, [aba]);

  useEffect(() => {
    if (!selectedCustomerId || !detailsOpen) return;
    void loadCustomerDetails(selectedCustomerId, 1, analysisScope, timelineMetric, detailsPeriod);
  }, [analysisScope, timelineMetric, detailsPeriod]);

  useEffect(() => {
    if (!detailsOpen && !newCustomersOpen) return;
    const timers = [
      window.setTimeout(() => window.dispatchEvent(new Event("resize")), 40),
      window.setTimeout(() => window.dispatchEvent(new Event("resize")), 180),
    ];
    return () => timers.forEach((timerId) => window.clearTimeout(timerId));
  }, [detailsOpen, newCustomersOpen, analysisScope, timelineMetric, newCustomersMonthly?.points.length]);

  useEffect(() => {
    if (!cliente) {
      setDetailsOpen(false);
      setSelectedCustomerId(null);
      return;
    }

    if (cliente === selectedCustomerId && detailsOpen) return;
    setSelectedCustomerId(cliente);
    setDetailsOpen(true);
    void loadCustomerDetails(cliente, 1, analysisScope);
  }, [cliente]);

  function applyPeriod(preset: PeriodPreset) {
    setPeriodPreset(preset);
    if (preset === "custom") return;
    const next = resolvePeriod(preset);
    setDateFrom(next.dateFrom);
    setDateTo(next.dateTo);
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    void load(1);
  }

  function openDetails(customerId: string) {
    void navigate({
      to: "/clientes",
      search: (prev) => ({ ...prev, cliente: customerId }),
      replace: false,
    });
  }

  function handleDetailsOpenChange(open: boolean) {
    setDetailsOpen(open);
    if (open) return;
    void navigate({
      to: "/clientes",
      search: (prev) => ({ ...prev, cliente: undefined }),
      replace: false,
    });
  }

  async function loadNewCustomersMonthly() {
    setNewCustomersLoading(true);
    setNewCustomersMessage("");
    try {
      const response = await fetchCustomerNewCustomersMonthly({
        dateFrom,
        dateTo,
        customer,
        city,
        productGroup,
        productCode,
        transactionType,
      });
      setNewCustomersMonthly(response);
    } catch (error) {
      setNewCustomersMessage((error as Error).message);
      setNewCustomersMonthly(null);
    } finally {
      setNewCustomersLoading(false);
    }
  }

  function openNewCustomersModal() {
    setNewCustomersOpen(true);
    void loadNewCustomersMonthly();
  }

  return (
    <div className="page-shell">
      <header className="animate-soft-enter">
        <span className="page-header-kicker">Smart Core / Clientes</span>
        <h1 className="mt-2 text-3xl font-display font-semibold tracking-tight">Análise Financeira de Clientes</h1>
        <p className="mt-1 text-sm text-muted-foreground">Painel executivo com impacto, projeções e histórico detalhado.</p>
      </header>

      <FeedbackMessage message={message} type="error" onDismiss={() => setMessage("")} />

      <div className="flex flex-wrap gap-1 rounded-lg bg-muted p-1 animate-soft-enter">
        {(["impacto", "projecoes", "fornecedores", "clientes", "nota-fiscal"] as const).map(tab => (
          <button
            key={tab}
            onClick={() => {
              setActiveTab(tab);
              void navigate({
                to: "/clientes",
                search: (prev) => ({ ...prev, aba: tab }),
                replace: true,
              });
              if (tab === "impacto" && !impactoData && !impactoLoading) void loadImpacto();
              if (tab === "fornecedores" && !impactoData && !impactoLoading) void loadImpacto();
              if (tab === "projecoes" && !projecoesData && !projecoesLoading) void loadProjecoes();
              if (tab === "clientes" && !historicoData && !historicoLoading) void loadHistorico(1, historicoSortBy);
            }}
            className={cn(
              "px-4 py-2 text-sm font-medium rounded-md transition-all",
              activeTab === tab ? "bg-surface text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
            )}
          >
            {tab === "impacto" && "Impacto"}
            {tab === "projecoes" && "Projeções"}
            {tab === "fornecedores" && "Fornecedores"}
            {tab === "clientes" && "Clientes"}
            {tab === "nota-fiscal" && "Nota Fiscal"}
          </button>
        ))}
      </div>

      {activeTab === "impacto" && (
        <div className="space-y-4 animate-soft-enter">
          {impactoLoading ? (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {[1,2,3,4].map(i => <SkeletonMetricCard key={i} />)}
            </div>
          ) : impactoData ? (
            <>
              {/* Resumo Executivo */}
              <section className="metric-row">
                <KpiCard className={IMPACT_KPI_CARD_CLASS_NAME} title="Maior cliente" value={impactoData.resumo?.maiorClienteNome ?? impactoData.resumo?.maiorCliente ?? "—"} icon={DollarSign} periodLabel={impactoData.resumo?.maiorFaturamento ? formatCurrency(impactoData.resumo.maiorFaturamento) : ""} showPercentageChange={false} loading={false} />
                <KpiCard className={IMPACT_KPI_CARD_CLASS_NAME} title="Maior crescimento" value={impactoData.resumo?.maiorCrescimentoNome ?? "—"} icon={TrendingUp} periodLabel={impactoData.resumo?.maiorCrescimentoPct ? `+${impactoData.resumo.maiorCrescimentoPct.toFixed(1)}%` : ""} showPercentageChange={false} loading={false} />
                <KpiCard className={IMPACT_KPI_CARD_CLASS_NAME} title="Maior queda" value={impactoData.resumo?.maiorQuedaNome ?? "—"} icon={ArrowDownRight} periodLabel={impactoData.resumo?.maiorQuedaPct ? `${impactoData.resumo.maiorQuedaPct.toFixed(1)}%` : ""} showPercentageChange={false} loading={false} />
                <KpiCard className={IMPACT_KPI_CARD_CLASS_NAME} title="Mais consistente" value={impactoData.resumo?.consistenteNome ?? "—"} icon={Target} periodLabel="" showPercentageChange={false} loading={false} />
                <KpiCard className={IMPACT_KPI_CARD_CLASS_NAME} title="Maior potencial" value={impactoData.resumo?.maiorPotencialNome ?? "—"} icon={Target} periodLabel={impactoData.resumo?.maiorPotencialScore ? `Score ${impactoData.resumo.maiorPotencialScore}` : ""} showPercentageChange={false} loading={false} />
              </section>

              {/* Alertas */}
              {impactoData.alertas?.length > 0 && (
                <section className="space-y-2">
                  <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">Alertas</h2>
                  <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
                    {impactoData.alertas.map((a: any, i: number) => (
                      <InsightCard key={i} type={a.severidade === "Crítico" ? "alert" : a.severidade === "Alto" ? "alert" : "info"}>
                        <span className="font-semibold">{a.severidade}</span>: {a.mensagem}
                      </InsightCard>
                    ))}
                  </div>
                </section>
              )}

              {/* Risco e Crescimento lado a lado */}
              <section className="grid grid-cols-1 gap-4 lg:grid-cols-2">
                <Card>
                  <CardHeader className="flex flex-row items-center justify-between gap-3">
                    <CardTitle className="text-sm font-semibold text-[#B91C1C]">Clientes em Risco</CardTitle>
                    {impactoData.risco?.length > 0 && (
                      <Button type="button" variant="outline" size="sm" asChild>
                        <Link to="/clientes-impacto" search={{ tipo: "risco" }}>Ver todos</Link>
                      </Button>
                    )}
                  </CardHeader>
                  <CardContent className="space-y-3">
                    {impactoData.risco?.length > 0 ? sortImpactListByAttention(impactoData.risco, "risco").slice(0, 6).map((c: any) => (
                      <div key={c.clienteId ?? c.ClienteId ?? getImpactCustomerName(c)} className="flex items-center justify-between gap-3 text-sm py-1.5 border-b border-border/30 last:border-0">
                        <div className="min-w-0 flex-1">
                          <p className="font-medium truncate">{getImpactCustomerName(c)}</p>
                          <p className="text-xs text-muted-foreground">{c.nivelRisco} · {c.mesesQueda}</p>
                        </div>
                        <div className="text-right ml-3 shrink-0">
                          <p className="text-[#B91C1C] font-medium">{formatImpactActionPercent(c, "risco")}</p>
                          <p className="text-xs text-muted-foreground">{formatCurrency(c.impactoFinanceiro)}/mês</p>
                        </div>
                        <Button type="button" variant="outline" size="sm" className="shrink-0" onClick={() => setRiskActionCustomer(c)}>
                          <AlertTriangle className="size-4 text-red-600" />
                          Ações
                        </Button>
                      </div>
                    )) : <p className="text-sm text-muted-foreground">Nenhum cliente em risco identificado.</p>}
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader className="flex flex-row items-center justify-between gap-3">
                    <CardTitle className="text-sm font-semibold text-[#059669]">Maiores Crescimentos</CardTitle>
                    {impactoData.crescimento?.length > 0 && (
                      <Button type="button" variant="outline" size="sm" asChild>
                        <Link to="/clientes-impacto" search={{ tipo: "crescimento" }}>Ver todos</Link>
                      </Button>
                    )}
                  </CardHeader>
                  <CardContent className="space-y-3">
                    {impactoData.crescimento?.length > 0 ? sortImpactListByAttention(impactoData.crescimento, "crescimento").slice(0, 6).map((c: any) => (
                      <div key={c.clienteId ?? c.ClienteId ?? getImpactCustomerName(c)} className="flex items-center justify-between text-sm py-1.5 border-b border-border/30 last:border-0">
                        <div className="min-w-0 flex-1">
                          <p className="font-medium truncate">{getImpactCustomerName(c)}</p>
                          <p className="text-xs text-muted-foreground">{c.potencialFuturo}</p>
                        </div>
                        <div className="text-right ml-3 shrink-0">
                          <p className="text-[#059669] font-medium">{formatImpactActionPercent(c, "crescimento", true)}</p>
                          <p className="text-xs text-muted-foreground">{formatCurrency(c.valorGerado)} gerado</p>
                        </div>
                      </div>
                    )) : <p className="text-sm text-muted-foreground">Nenhum cliente com crescimento expressivo.</p>}
                  </CardContent>
                </Card>
              </section>

              {/* Oportunidades */}
              {impactoData.oportunidades?.length > 0 && (
                <Card>
                  <CardHeader className="flex flex-row items-center justify-between gap-3">
                    <CardTitle className="text-sm font-semibold text-[#059669]">Oportunidades</CardTitle>
                    <Button type="button" variant="outline" size="sm" asChild>
                      <Link to="/clientes-impacto" search={{ tipo: "oportunidades" }}>Ver todos</Link>
                    </Button>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                      {sortImpactListByAttention(impactoData.oportunidades, "oportunidades").slice(0, 6).map((c: any) => (
                        <div key={c.clienteId ?? c.ClienteId ?? getImpactCustomerName(c)} className="rounded-lg border p-3 space-y-1">
                          <p className="font-medium text-sm">{getImpactCustomerName(c)}</p>
                          <p className="text-xs text-muted-foreground">{c.potencial} · Score {c.scorePotencial}</p>
                          <div className="flex justify-between text-xs">
                            <span className="text-[#059669]">{formatImpactActionPercent(c, "oportunidades", true)}</span>
                            <span>{formatCurrency(c.faturamento12M)}</span>
                          </div>
                        </div>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              )}
            </>
          ) : (
            <p className="text-sm text-muted-foreground py-8 text-center">Carregue os dados para ver o painel de impacto.</p>
          )}
        </div>
      )}

      {activeTab === "fornecedores" && (
        <div className="space-y-4 animate-soft-enter">
          {impactoLoading ? (
            <div className="metric-row">{SUPPLIER_SKELETON_CARD_KEYS.map((key) => <SkeletonMetricCard key={key} />)}</div>
          ) : impactoData ? (
            <>
              <Card>
                <CardHeader className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                  <div>
                    <CardTitle className="text-sm font-semibold">Central de Fornecedores</CardTitle>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Dados consumidos da integração Grespan/TOTVS, sem cadastro manual de fornecedores.
                    </p>
                  </div>
                  <div className="flex flex-col gap-1 sm:min-w-[260px]">
                    <Label htmlFor="supplier-filter" className="text-xs text-muted-foreground">Filtrar por fornecedor</Label>
                    <select
                      id="supplier-filter"
                      value={selectedSupplier}
                      onChange={(event) => setSelectedSupplier(event.target.value)}
                      className="h-10 rounded-md border border-input bg-background px-3 text-sm outline-none transition-colors focus:border-ring focus:ring-2 focus:ring-ring/20"
                    >
                      <option value={ALL_SUPPLIERS_FILTER_VALUE}>Todos os fornecedores</option>
                      {supplierRouteDashboard.suppliers.map((supplier) => (
                        <option key={supplier.supplierId} value={supplier.supplierId}>{supplier.supplierName}</option>
                      ))}
                    </select>
                  </div>
                </CardHeader>
              </Card>

              <section className="metric-row">
                <KpiCard
                  title="Clientes por fornecedor"
                  value={formatKpiCompactNumber(supplierRouteDashboard.summary.totalCustomers)}
                  showPercentageChange={false}
                  icon={Users}
                  periodLabel="Relacionamento recebido da integração"
                  loading={false}
                />
                <KpiCard
                  title="Clientes em risco"
                  value={formatKpiCompactNumber(supplierRouteDashboard.summary.riskCustomers)}
                  showPercentageChange={false}
                  icon={AlertTriangle}
                  periodLabel="Atenção, Alto e Crítico"
                  loading={false}
                />
                <KpiCard
                  title="Casos aguardando ação"
                  value={formatKpiCompactNumber(supplierRouteDashboard.summary.awaitingAction)}
                  showPercentageChange={false}
                  icon={CalendarClock}
                  periodLabel={`${supplierRouteDashboard.summary.notifiedCustomers} fornecedor(es) notificado(s)`}
                  loading={false}
                />
                <KpiCard
                  title="Escalados à gerência"
                  value={formatKpiCompactNumber(supplierRouteDashboard.summary.escalatedCustomers)}
                  showPercentageChange={false}
                  icon={UserRound}
                  periodLabel="Sem ação registrada no prazo"
                  loading={false}
                />
                <KpiCard
                  title="Tempo médio de resposta"
                  value={supplierRouteDashboard.summary.averageSupplierResponseHours == null ? "—" : `${supplierRouteDashboard.summary.averageSupplierResponseHours}h`}
                  showPercentageChange={false}
                  icon={BarChart3}
                  periodLabel="Baseado nas notificações registradas"
                  loading={false}
                />
              </section>

              {supplierRouteDashboard.routes.length === 0 ? (
                <p className="text-sm text-muted-foreground py-8 text-center">Nenhum cliente em risco para notificar fornecedores.</p>
              ) : (
                <>
                  <section className="grid grid-cols-1 gap-4 lg:grid-cols-3">
                    {([
                      { key: "attention", label: "Atenção", value: supplierRouteDashboard.riskBuckets.attention, className: "border-amber-300 bg-amber-50 text-amber-700" },
                      { key: "high", label: "Alto", value: supplierRouteDashboard.riskBuckets.high, className: "border-red-300 bg-red-50 text-red-700" },
                      { key: "critical", label: "Crítico", value: supplierRouteDashboard.riskBuckets.critical, className: "border-red-900 bg-red-950 text-white" },
                    ] as const).map((bucket) => (
                      <Card key={bucket.key} className={cn("border", bucket.className)}>
                        <CardHeader className="pb-2">
                          <CardTitle className="text-sm font-semibold">{bucket.label}</CardTitle>
                        </CardHeader>
                        <CardContent>
                          <p className="text-2xl font-semibold tabular-nums">{formatKpiCompactNumber(bucket.value)}</p>
                        </CardContent>
                      </Card>
                    ))}
                  </section>

                  <Card>
                    <CardHeader>
                      <CardTitle className="text-sm font-semibold">Dashboard Gerencial por Fornecedor</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="overflow-x-auto custom-scrollbar">
                        <Table className="min-w-[840px]">
                          <TableHeader>
                            <TableRow>
                              <TableHead>Fornecedor</TableHead>
                              <TableHead className="text-right">Clientes</TableHead>
                              <TableHead className="text-right">Em risco</TableHead>
                              <TableHead className="text-right">Críticos</TableHead>
                              <TableHead className="text-right">Aguardando ação</TableHead>
                              <TableHead className="text-right">Escalados</TableHead>
                              <TableHead className="text-right">Tempo médio</TableHead>
                            </TableRow>
                          </TableHeader>
                          <TableBody>
                            {supplierRouteDashboard.suppliers.map((supplier) => (
                              <TableRow key={supplier.supplierId}>
                                <TableCell className="font-medium">{supplier.supplierName}</TableCell>
                                <TableCell className="text-right tabular-nums">{supplier.totalCustomers}</TableCell>
                                <TableCell className="text-right tabular-nums">{supplier.riskCustomers}</TableCell>
                                <TableCell className="text-right tabular-nums">{supplier.criticalCustomers}</TableCell>
                                <TableCell className="text-right tabular-nums">{supplier.awaitingAction}</TableCell>
                                <TableCell className="text-right tabular-nums">{supplier.escalatedCustomers}</TableCell>
                                <TableCell className="text-right tabular-nums">{supplier.averageResponseHours == null ? "—" : `${supplier.averageResponseHours}h`}</TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </div>
                    </CardContent>
                  </Card>

                  {supplierRouteDashboard.managementQueue.length > 0 && (
                    <Card>
                      <CardHeader>
                        <CardTitle className="text-sm font-semibold text-red-700">Fila da Gerência</CardTitle>
                      </CardHeader>
                      <CardContent className="space-y-2">
                        {supplierRouteDashboard.managementQueue.map((situation) => (
                          <button
                            key={situation.customerId}
                            type="button"
                            onClick={() => setSupplierCaseCustomer(situation)}
                            className="flex w-full items-center justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-left text-sm transition-colors hover:bg-red-100"
                          >
                            <span>
                              <span className="font-medium text-red-900">{situation.customerName}</span>
                              <span className="block text-xs text-red-700">{situation.supplierName} · {situation.riskReason}</span>
                            </span>
                            <Badge variant="outline" className="border-0 bg-red-700 text-white">Escalado</Badge>
                          </button>
                        ))}
                      </CardContent>
                    </Card>
                  )}

                  <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
                    {supplierRouteDashboard.routes.map((route) => (
                      <Card key={route.routeName}>
                        <CardHeader>
                          <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
                            <CardTitle className="flex items-center gap-2 text-sm font-semibold">
                              <MapPin className="size-4 text-primary" />
                              {route.routeName}
                            </CardTitle>
                            <span className="text-xs text-muted-foreground">Origem: integração Grespan/TOTVS</span>
                          </div>
                        </CardHeader>
                        <CardContent>
                          <div className="overflow-x-auto custom-scrollbar">
                            <Table className="min-w-[860px]">
                              <TableHeader>
                                <TableRow>
                                  <TableHead>Cliente</TableHead>
                                  <TableHead>Fornecedor</TableHead>
                                  <TableHead>Risco</TableHead>
                                  <TableHead>Status</TableHead>
                                  <TableHead>Notificação</TableHead>
                                  <TableHead className="text-right">Impacto/mês</TableHead>
                                </TableRow>
                              </TableHeader>
                              <TableBody>
                                {route.customers.map((situation) => (
                                  <TableRow
                                    key={situation.customerId}
                                    className="cursor-pointer"
                                    onClick={() => setSupplierCaseCustomer(situation)}
                                  >
                                    <TableCell className="font-medium">{situation.customerName}</TableCell>
                                    <TableCell>{situation.supplierName}</TableCell>
                                    <TableCell>
                                      <Badge variant="outline" className={cn("w-fit border-0 font-semibold", getSupplierRiskBadgeClassName(situation.riskLevel))}>
                                        {situation.riskLabel}
                                      </Badge>
                                    </TableCell>
                                    <TableCell>
                                      <div className="space-y-1">
                                        <span className="text-sm">{getSupplierStatusLabel(situation.status)}</span>
                                        <p className="text-xs text-muted-foreground">
                                          {situation.status === "escalated"
                                            ? `Passou de ${situation.responseDeadlineHours}h sem ação`
                                            : situation.status === "notified"
                                              ? `${situation.remainingHours}h para evitar escalonamento`
                                              : "Monitorar evolução"}
                                        </p>
                                      </div>
                                    </TableCell>
                                    <TableCell>{formatSupplierDateTime(situation.notificationSentAt)}</TableCell>
                                    <TableCell className="text-right tabular-nums">{formatCurrency(situation.monthlyImpact)}</TableCell>
                                  </TableRow>
                                ))}
                              </TableBody>
                            </Table>
                          </div>
                        </CardContent>
                      </Card>
                    ))}
                  </section>
                </>
              )}
            </>
          ) : (
            <p className="text-sm text-muted-foreground py-8 text-center">Carregue o impacto para ver fornecedores por rota.</p>
          )}
        </div>
      )}

      {activeTab === "projecoes" && (
        <div className="space-y-4 animate-soft-enter">
          {projecoesLoading ? (
            <div className="metric-row">{[1,2,3,4].map(i => <SkeletonMetricCard key={i} />)}</div>
          ) : projecoesData ? (
            <>
              <section className="metric-row">
                <KpiCard
                  title="Faturamento mensal atual"
                  value={formatKpiCompactCurrency(projecoesData.projecoes?.faturamentoMensalAtual ?? 0)}
                  valueTooltip={formatCurrency(projecoesData.projecoes?.faturamentoMensalAtual ?? 0)}
                  showPercentageChange={false}
                  icon={DollarSign}
                  periodLabel="Média dos últimos 12 meses"
                  loading={false}
                />
                <KpiCard
                  title="Próximo mês"
                  value={formatKpiCompactCurrency(projecoesData.projecoes?.proximoMes ?? 0)}
                  valueTooltip={formatCurrency(projecoesData.projecoes?.proximoMes ?? 0)}
                  showPercentageChange={false}
                  icon={TrendingUp}
                  periodLabel="Projeção para os próximos 30 dias"
                  loading={false}
                />
                <KpiCard
                  title="Próximos 3 meses"
                  value={formatKpiCompactCurrency(projecoesData.projecoes?.proximos3Meses ?? 0)}
                  valueTooltip={formatCurrency(projecoesData.projecoes?.proximos3Meses ?? 0)}
                  showPercentageChange={false}
                  icon={BarChart3}
                  periodLabel="Projeção acumulada para 90 dias"
                  loading={false}
                />
                <KpiCard
                  title="Próximos 12 meses"
                  value={formatKpiCompactCurrency(projecoesData.projecoes?.proximos12Meses ?? 0)}
                  valueTooltip={formatCurrency(projecoesData.projecoes?.proximos12Meses ?? 0)}
                  showPercentageChange={false}
                  icon={Target}
                  periodLabel="Projeção acumulada para 360 dias"
                  loading={false}
                />
              </section>

              <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
                <Card>
                  <CardHeader><CardTitle className="text-sm font-semibold">Cenários</CardTitle></CardHeader>
                  <CardContent className="space-y-3">
                    {[
                      { label: "Conservador", margem: projecoesData.cenarioConservador?.margem ?? -15, cor: "#D97706" },
                      { label: "Realista", margem: projecoesData.cenarioRealista?.margem ?? 0, cor: "#2563EB" },
                      { label: "Otimista", margem: projecoesData.cenarioOtimista?.margem ?? 15, cor: "#059669" },
                    ].map(cenario => {
                      const base = projecoesData.projecoes?.proximos3Meses ?? 0;
                      const valor = base * (1 + cenario.margem / 100);
                      return (
                        <div key={cenario.label} className="flex items-center justify-between text-sm py-2 border-b border-border/30 last:border-0">
                          <span className="flex items-center gap-2"><span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: cenario.cor }} />{cenario.label}</span>
                          <span className="font-medium tabular-nums">{formatCurrency(valor)}</span>
                        </div>
                      );
                    })}
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader><CardTitle className="text-sm font-semibold">Tendências</CardTitle></CardHeader>
                  <CardContent className="space-y-3">
                    {projecoesData.tendencias?.map((t: any) => (
                      <div key={t.label} className="flex items-center justify-between text-sm py-1.5 border-b border-border/30 last:border-0">
                        <span>{t.label}</span>
                        <div className="text-right">
                          <p className="font-medium">{t.total} cliente(s)</p>
                          {t.faturamento > 0 && <p className="text-xs text-muted-foreground">{formatCurrency(t.faturamento)}</p>}
                        </div>
                      </div>
                    ))}
                  </CardContent>
                </Card>
              </section>

              <Card>
                <CardHeader>
                  <div className="flex items-center justify-between">
                    <CardTitle className="text-sm font-semibold">Receita vs Custo</CardTitle>
                    <span className="text-xs text-muted-foreground">Custo simulado até integração</span>
                  </div>
                </CardHeader>
                <CardContent>
                  {projecoesReceitaCustoData.length === 0 ? (
                    <div className="flex h-[260px] items-center justify-center rounded-md border border-dashed border-slate-800 text-sm text-slate-400">Sem dados de faturamento para o período.</div>
                  ) : (
                    <ReceitaVsCustoChart data={projecoesReceitaCustoData} />
                  )}
                </CardContent>
              </Card>

              {projecoesData.evolucaoClientes?.length > 0 && (
                <Card>
                  <CardHeader>
                    <div className="flex items-center justify-between">
                      <CardTitle className="text-sm font-semibold">Evolução por cliente</CardTitle>
                      <span className="text-xs text-muted-foreground">{projecoesData.evolucaoClientes.length} cliente(s) com previsão</span>
                    </div>
                  </CardHeader>
                  <CardContent>
                    <div className="overflow-x-auto custom-scrollbar">
                      <Table className="min-w-[860px]">
                        <TableHeader>
                          <TableRow>
                            <TableHead>Cliente</TableHead>
                            <TableHead className="text-right">Valor atual (3M)</TableHead>
                            <TableHead className="text-right">Projetado (30d)</TableHead>
                            <TableHead className="text-right">Custo simulado</TableHead>
                            <TableHead className="text-right">Margem proj.</TableHead>
                            <TableHead className="text-right">Diferença</TableHead>
                            <TableHead>Tendência</TableHead>
                            <TableHead className="text-right">Confiança</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {(projecoesData.evolucaoClientes ?? []).map((evo: any, index: number) => {
                            const projectedRevenue = evo.valorProjetado ?? 0;
                            const projectedCost = evo.custoProjetado ?? calculateSimulatedProjectedCost(projectedRevenue, index);
                            const projectedMargin = evo.margemProjetadaPercentual ?? calculateProjectedMarginPercent(projectedRevenue, projectedCost);

                            return (
                              <TableRow key={evo.ClienteId ?? evo.clienteId}>
                                <TableCell className="font-medium">{evo.clienteNome ?? evo.ClienteId ?? evo.clienteId}</TableCell>
                                <TableCell className="text-right tabular-nums">{formatCurrency(evo.valorAtual ?? 0)}</TableCell>
                                <TableCell className="text-right tabular-nums">{formatCurrency(projectedRevenue)}</TableCell>
                                <TableCell className="text-right tabular-nums">{formatCurrency(projectedCost)}</TableCell>
                                <TableCell className="text-right tabular-nums">{formatDecimal(projectedMargin)}%</TableCell>
                                <TableCell className="text-right tabular-nums">
                                  {(evo.diferenca ?? 0) >= 0 ? (
                                    <span className="text-green-600 font-semibold">+{formatCurrency(evo.diferenca)}</span>
                                  ) : (
                                    <span className="text-red-600 font-semibold">{formatCurrency(evo.diferenca)}</span>
                                  )}
                                </TableCell>
                                <TableCell>
                                  <Badge variant="outline" className={cn("w-fit whitespace-nowrap border-0 font-semibold", (evo.tendenciaPrevista ?? evo.TendenciaPrevista ?? "") === "Crescimento" ? "bg-green-600 text-white" : (evo.tendenciaPrevista ?? evo.TendenciaPrevista ?? "") === "Queda" ? "bg-red-600 text-white" : "bg-blue-600 text-white")}>
                                    {evo.tendenciaPrevista ?? evo.TendenciaPrevista ?? "—"}
                                  </Badge>
                                </TableCell>
                                <TableCell className="text-right tabular-nums">{formatProjectionConfidence(evo.confiancaModelo ?? evo.ConfiancaModelo)}</TableCell>
                              </TableRow>
                            );
                          })}
                        </TableBody>
                      </Table>
                    </div>
                  </CardContent>
                </Card>
              )}
            </>
          ) : (
            <p className="text-sm text-muted-foreground py-8 text-center">Carregue os dados para ver as projeções.</p>
          )}
        </div>
      )}

      {activeTab === "clientes" && (
      <div className="space-y-4 animate-soft-enter">
        <section className="flex flex-wrap gap-2 rounded-xl border border-border/80 bg-surface/95 p-3 shadow-xs">
          {periodOptions.map((option) => (
            <Button
              key={option.value}
              type="button"
              size="sm"
              variant={periodPreset === option.value ? "default" : "outline"}
              onClick={() => applyPeriod(option.value)}
            >
              {option.label}
            </Button>
          ))}
        </section>

        {!historicoLoading && financeDashboard && (
          <section className="metric-row">
            <KpiCard
              title="Faturamento total"
              value={formatKpiCompactCurrency(financeMetrics.totalRevenue)}
              valueTooltip={formatCurrency(financeMetrics.totalRevenue)}
              showPercentageChange={false}
              icon={DollarSign}
              periodLabel="Métrica financeira consolidada pelos filtros"
              loading={false}
            />
            <KpiCard
              title="Ticket médio"
              value={formatKpiCompactCurrency(financeMetrics.averageTicket)}
              valueTooltip={formatCurrency(financeMetrics.averageTicket)}
              showPercentageChange={false}
              icon={Receipt}
              periodLabel="Faturamento dividido pelos pedidos"
              loading={false}
            />
            <KpiCard
              title="Pedidos"
              value={formatKpiCompactNumber(financeMetrics.totalOrders)}
              valueTooltip={String(financeMetrics.totalOrders)}
              showPercentageChange={false}
              icon={Users}
              periodLabel="Documentos financeiros do período"
              loading={false}
            />
            <KpiCard
              title="Quantidade"
              value={formatKpiCompactNumber(financeMetrics.totalQuantity)}
              valueTooltip={formatDecimal(financeMetrics.totalQuantity)}
              showPercentageChange={false}
              icon={TrendingUp}
              periodLabel="Quantidade comprada na base filtrada"
              loading={false}
            />
          </section>
        )}
        {!historicoLoading && financeDashboard && (
          <section className="grid grid-cols-1 gap-3 xl:grid-cols-2">
            <Card className="overflow-hidden border-border bg-surface text-foreground shadow-sm dark:border-[#182033] dark:bg-[#070b14] dark:text-slate-100 dark:shadow-lg">
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-foreground dark:text-slate-100">Evolução da Receita</CardTitle>
                <p className="text-xs text-muted-foreground">Faturamento financeiro consolidado pelos filtros.</p>
              </CardHeader>
              <CardContent className="pt-0">
                {financeRevenueTrendData.length === 0 ? (
                  <div className="flex h-[260px] items-center justify-center rounded-md border border-dashed border-slate-800 text-sm text-slate-400">Sem resultado para gerar gráfico de faturamento.</div>
                ) : (
                  <RevenueAreaChart data={financeRevenueTrendData} gradientId="clientes-finance-revenue-gradient" />
                )}
              </CardContent>
            </Card>

            <Card className="overflow-hidden border-border bg-surface text-foreground shadow-sm dark:border-[#182033] dark:bg-[#070b14] dark:text-slate-100 dark:shadow-lg">
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-foreground dark:text-slate-100">Ranking por empresa</CardTitle>
                <p className="text-xs text-muted-foreground">Clientes com maior faturamento financeiro no período.</p>
              </CardHeader>
              <CardContent className="pt-0">
                {financeCustomerRankingData.length === 0 ? (
                  <div className="flex h-[260px] items-center justify-center rounded-md border border-dashed border-slate-800 text-sm text-slate-400">Sem empresas para o ranking atual.</div>
                ) : (
                  <RankingBarChart data={financeCustomerRankingData} />
                )}
              </CardContent>
            </Card>
          </section>
        )}

        {historicoLoading ? (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {[1,2,3,4].map(i => <SkeletonMetricCard key={i} />)}
          </div>
        ) : historicoData ? (
          <Card>
              <CardHeader>
                <div className="flex items-center justify-between">
                  <CardTitle>Clientes</CardTitle>
                  <select
                    className="h-8 rounded-md border border-border bg-background px-2 text-xs"
                    value={historicoSortBy}
                    onChange={(e) => {
                      const newSort = e.target.value;
                      setHistoricoSortBy(newSort);
                      void loadHistorico(1, newSort);
                    }}
                  >
                    <option value="revenue">Maior faturamento</option>
                    <option value="growth">Melhor crescimento</option>
                    <option value="drop">Pior crescimento</option>
                    <option value="ticket">Maior ticket médio</option>
                    <option value="quantity">Maior volume</option>
                    <option value="score">Maior score potencial</option>
                  </select>
                </div>
              </CardHeader>
              <CardContent>
                <div className="overflow-x-auto custom-scrollbar">
                  <Table className="min-w-[1000px]">
                    <TableHeader>
                      <TableRow>
                        <TableHead>Código</TableHead>
                        <TableHead>Cliente</TableHead>
                        <TableHead className="text-right">Fat. 12M</TableHead>
                        <TableHead className="text-right">Fat. 6M</TableHead>
                        <TableHead className="text-right">Fat. 3M</TableHead>
                        <TableHead className="text-right">Cresc. 12M</TableHead>
                        <TableHead className="text-right">Ticket médio</TableHead>
                        <TableHead className="text-right">Freq. compra</TableHead>
                        <TableHead className="text-right">Score pot.</TableHead>
                        <TableHead>Tendência</TableHead>
                        <TableHead>Classif.</TableHead>
                        <TableHead>Risco</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {(historicoData.items ?? []).length === 0 && (
                        <TableRow>
                          <TableCell colSpan={12} className="py-8 text-center text-muted-foreground">Nenhum indicador encontrado.</TableCell>
                        </TableRow>
                      )}
                      {(historicoData.items ?? []).map((item: any) => (
                        <TableRow
                          key={item.ClienteId ?? item.clienteId}
                          className="cursor-pointer"
                          onClick={() => openDetails(item.ClienteId ?? item.customerCode ?? item.clienteId)}
                        >
                          <TableCell className="text-muted-foreground font-mono text-xs">{item.ClienteId ?? item.clienteId}</TableCell>
                          <TableCell className="font-medium">{item.ClienteNome ?? item.clienteNome ?? ""}</TableCell>
                          <TableCell className="text-right tabular-nums" title={formatCurrency(item.Faturamento12M ?? item.faturamento12M ?? 0)}>{formatKpiCompactCurrency(item.Faturamento12M ?? item.faturamento12M ?? 0)}</TableCell>
                          <TableCell className="text-right tabular-nums" title={formatCurrency(item.Faturamento6M ?? item.faturamento6M ?? 0)}>{formatKpiCompactCurrency(item.Faturamento6M ?? item.faturamento6M ?? 0)}</TableCell>
                          <TableCell className="text-right tabular-nums" title={formatCurrency(item.Faturamento3M ?? item.faturamento3M ?? 0)}>{formatKpiCompactCurrency(item.Faturamento3M ?? item.faturamento3M ?? 0)}</TableCell>
                          <TableCell className="text-right tabular-nums" title={(item.Crescimento12M ?? item.crescimento12M) != null ? formatVariationPercent(item.Crescimento12M ?? item.crescimento12M ?? 0) : "—"}>
                            {(item.Crescimento12M ?? item.crescimento12M) != null ? (
                              <span className={cn("font-semibold", (item.Crescimento12M ?? item.crescimento12M ?? 0) >= 0 ? "text-green-600" : "text-red-600")}>
                            {formatVariationPercent(item.Crescimento12M ?? item.crescimento12M ?? 0)}
                          </span>
                            ) : "—"}
                          </TableCell>
                          <TableCell className="text-right tabular-nums" title={formatCurrency(item.TicketMedioGeral ?? item.ticketMedioGeral ?? 0)}>{formatKpiCompactCurrency(item.TicketMedioGeral ?? item.ticketMedioGeral ?? 0)}</TableCell>
                          <TableCell className="text-right tabular-nums" title={`${formatDecimal(item.FrequenciaCompra ?? item.frequenciaCompra ?? 0)} compras/mês`}>{formatDecimal(item.FrequenciaCompra ?? item.frequenciaCompra ?? 0)}</TableCell>
                          <TableCell className="text-right tabular-nums" title={`Score ${item.ScorePotencial ?? item.scorePotencial ?? 0}`}>{item.ScorePotencial ?? item.scorePotencial ?? 0}</TableCell>
                          <TableCell>
                            <Badge variant="outline" className={cn("w-fit whitespace-nowrap border-0 font-semibold", (item.Tendencia ?? item.tendencia ?? "") === "Crescimento" ? "bg-green-600 text-white" : (item.Tendencia ?? item.tendencia ?? "") === "Queda" ? "bg-red-600 text-white" : "bg-blue-600 text-white")}>
                              {item.Tendencia ?? item.tendencia ?? "—"}
                            </Badge>
                          </TableCell>
                          <TableCell><span className="text-sm">{item.Classificacao ?? item.classificacao ?? "—"}</span></TableCell>
                          <TableCell>
                            <span className={cn(
                              "text-sm font-medium",
                              item.nivelRisco === "Crítico" ? "text-red-600" :
                              item.nivelRisco === "Alto" ? "text-orange-500" :
                              item.nivelRisco === "Médio" ? "text-yellow-600" :
                              "text-green-600"
                            )}>
                              {item.nivelRisco ?? "—"}
                            </span>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>

                <div className="flex items-center justify-end gap-2 mt-4">
                  <Button
                    variant="outline" size="sm"
                    disabled={historicoData.pagina <= 1}
                    onClick={() => void loadHistorico((historicoData.pagina ?? 1) - 1, historicoSortBy)}
                  >Anterior</Button>
                  <span className="text-xs text-muted-foreground">
                    Página {historicoData.pagina ?? 1} de {Math.max(1, Math.ceil((historicoData.total ?? 0) / (historicoData.tamanho ?? 20)))}
                  </span>
                  <Button
                    variant="outline" size="sm"
                    disabled={(historicoData.pagina ?? 1) >= Math.ceil((historicoData.total ?? 0) / (historicoData.tamanho ?? 20))}
                    onClick={() => void loadHistorico((historicoData.pagina ?? 1) + 1, historicoSortBy)}
                  >Próxima</Button>
                </div>
              </CardContent>
            </Card>
        ) : (
          <p className="text-sm text-muted-foreground py-8 text-center">Carregue os dados para ver o histórico de indicadores.</p>
        )}
      </div>
      )}

      {activeTab === "nota-fiscal" && (
        <div className="animate-soft-enter space-y-4">
          <VendasPage embedded />
        </div>
      )}

      <Dialog open={Boolean(riskActionCustomer)} onOpenChange={(open) => !open && setRiskActionCustomer(null)}>
        <DialogContent className="custom-scrollbar max-h-[85vh] w-[95vw] max-w-2xl overflow-y-auto p-5 pt-8 pr-10 sm:p-6 sm:pt-9 sm:pr-12">
          <DialogHeader>
            <DialogTitle>Sugestões de ações</DialogTitle>
            <DialogDescription>
              Plano recomendado para sanar o risco de {riskActionCustomer ? getImpactCustomerName(riskActionCustomer) : "cliente"}.
            </DialogDescription>
          </DialogHeader>

          {riskActionCustomer ? (
            <div className="space-y-4">
              <div className="rounded-lg border border-border/70 bg-muted/25 p-3">
                <p className="text-sm font-semibold">{getImpactCustomerName(riskActionCustomer)}</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  {riskActionCustomer.nivelRisco ?? riskActionCustomer.NivelRisco ?? "Risco"} · {riskActionCustomer.mesesQueda ?? riskActionCustomer.MesesQueda ?? "período recente"} · {formatCurrency(riskActionCustomer.impactoFinanceiro ?? riskActionCustomer.ImpactoFinanceiro ?? 0)}/mês
                </p>
              </div>

              <div className="space-y-2">
                {buildRiskCustomerActionSuggestions(riskActionCustomer).map((suggestion, index) => (
                  <div key={suggestion} className="flex gap-3 rounded-lg border border-border/70 bg-surface p-3 text-sm">
                    <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
                      {index + 1}
                    </span>
                    <p>{suggestion}</p>
                  </div>
                ))}
              </div>

              <div className="flex justify-end">
                <Button type="button" onClick={() => setRiskActionCustomer(null)}>Entendi</Button>
              </div>
            </div>
          ) : null}
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(supplierCaseCustomer)} onOpenChange={(open) => !open && setSupplierCaseCustomer(null)}>
        <DialogContent className="custom-scrollbar max-h-[88vh] w-[95vw] max-w-3xl overflow-y-auto p-5 pt-8 pr-10 sm:p-6 sm:pt-9 sm:pr-12">
          <DialogHeader>
            <DialogTitle>Central de acompanhamento</DialogTitle>
            <DialogDescription>
              Situação importada da integração e monitorada até ação do fornecedor ou escalonamento à gerência.
            </DialogDescription>
          </DialogHeader>

          {supplierCaseCustomer ? (
            <div className="space-y-4">
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="rounded-lg border border-border/70 bg-muted/25 p-3">
                  <p className="text-xs text-muted-foreground">Cliente</p>
                  <p className="mt-1 text-sm font-semibold">{supplierCaseCustomer.customerName}</p>
                </div>
                <div className="rounded-lg border border-border/70 bg-muted/25 p-3">
                  <p className="text-xs text-muted-foreground">Fornecedor responsável</p>
                  <p className="mt-1 text-sm font-semibold">{supplierCaseCustomer.supplierName}</p>
                </div>
                <div className="rounded-lg border border-border/70 bg-muted/25 p-3">
                  <p className="text-xs text-muted-foreground">Nível de risco</p>
                  <Badge variant="outline" className={cn("mt-1 w-fit border-0 font-semibold", getSupplierRiskBadgeClassName(supplierCaseCustomer.riskLevel))}>
                    {supplierCaseCustomer.riskLabel}
                  </Badge>
                </div>
                <div className="rounded-lg border border-border/70 bg-muted/25 p-3">
                  <p className="text-xs text-muted-foreground">Prazo para resolução</p>
                  <p className="mt-1 text-sm font-semibold">{formatSupplierDateTime(supplierCaseCustomer.responseDeadlineAt)}</p>
                  <p className="mt-1 text-xs text-muted-foreground">{getSupplierStatusLabel(supplierCaseCustomer.status)}</p>
                </div>
              </div>

              <Card>
                <CardHeader>
                  <CardTitle className="text-sm font-semibold">Motivo do risco</CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-sm text-muted-foreground">{supplierCaseCustomer.riskReason}</p>
                </CardContent>
              </Card>

              <div className="grid gap-4 lg:grid-cols-2">
                <Card>
                  <CardHeader>
                    <CardTitle className="text-sm font-semibold">Histórico de ocorrências</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-2">
                    {supplierCaseCustomer.occurrenceHistory.map((item: string) => (
                      <div key={item} className="rounded-md border border-border/70 bg-surface p-2 text-sm">{item}</div>
                    ))}
                  </CardContent>
                </Card>

                <Card>
                  <CardHeader>
                    <CardTitle className="text-sm font-semibold">Checklist de ações</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-2">
                    {supplierCaseCustomer.actionChecklist.map((item: string) => (
                      <label key={item} className="flex items-start gap-2 rounded-md border border-border/70 bg-surface p-2 text-sm">
                        <input type="checkbox" className="mt-1" readOnly />
                        <span>{item}</span>
                      </label>
                    ))}
                  </CardContent>
                </Card>
              </div>

              <div className="rounded-lg border border-border/70 bg-muted/20 p-3 text-xs text-muted-foreground">
                Notificação ao fornecedor: {formatSupplierDateTime(supplierCaseCustomer.notificationSentAt)}.
              </div>
            </div>
          ) : null}
        </DialogContent>
      </Dialog>

      <Dialog open={detailsOpen} onOpenChange={handleDetailsOpenChange}>
        <DialogContent className="custom-scrollbar max-h-[90vh] w-[95vw] max-w-6xl overflow-y-auto border-border/80 bg-surface p-4 pt-6 sm:p-6 sm:pt-7 [&>button]:right-3 [&>button]:top-3 [&>button]:inline-flex [&>button]:h-9 [&>button]:w-9 [&>button]:items-center [&>button]:justify-center [&>button]:rounded-md [&>button]:border [&>button]:border-border/70 [&>button]:bg-surface [&>button]:opacity-100 [&>button_svg]:h-4.5 [&>button_svg]:w-4.5 sm:[&>button]:right-4 sm:[&>button]:top-4">
          <DialogHeader className="pr-10 sm:pr-12">
            <DialogTitle>Detalhes do Cliente</DialogTitle>
            <DialogDescription>Análise individual de comportamento de compra</DialogDescription>
          </DialogHeader>

          {detailsMessage && (
            <Alert variant="destructive" className="mt-4">
              <AlertDescription>{detailsMessage}</AlertDescription>
            </Alert>
          )}

          {details && (
            <div className="mt-2 space-y-5 px-0.5 pb-8 sm:mt-3 sm:px-1">
              <Card className="overflow-hidden border-border/80 bg-gradient-to-br from-background via-background to-muted/35 shadow-md">
                <div className="h-0.5 w-full bg-primary/70" />
                <CardContent className="space-y-4 p-4 pt-7 sm:p-5 sm:pt-8">
                  <div className="mt-1 flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                    <div className="min-w-0 space-y-2 lg:max-w-[78%]">
                      <div className="inline-flex items-center gap-2 rounded-full border border-border/70 bg-surface px-3 py-1 text-xs text-muted-foreground">
                        <UserRound className="h-3.5 w-3.5" />
                        Código: {details.customerCode}
                      </div>
                      <h3 className="truncate text-2xl font-semibold tracking-tight text-foreground sm:text-3xl" title={details.customerName}>
                        {details.customerName}
                      </h3>
                    </div>
                    <Badge variant={resolveCustomerStatusVariant(details.status)} className="w-fit px-3 py-1 text-sm">
                      {details.status}
                    </Badge>
                  </div>

                  <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 xl:grid-cols-4">
                    <div className="rounded-lg border border-border/70 bg-surface p-3">
                      <div className="mb-1 flex items-center gap-2 text-xs text-muted-foreground">
                        <UserRound className="h-3.5 w-3.5" />
                        Nome do cliente
                      </div>
                      <p className="truncate font-medium" title={details.customerName}>{details.customerName}</p>
                    </div>
                    <div className="rounded-lg border border-border/70 bg-surface p-3">
                      <div className="mb-1 flex items-center gap-2 text-xs text-muted-foreground">
                        <MapPin className="h-3.5 w-3.5" />
                        Cidade
                      </div>
                      <p className="truncate font-medium" title={details.city || "N/A"}>{details.city || "N/A"}</p>
                    </div>
                    <div className="rounded-lg border border-border/70 bg-surface p-3">
                      <div className="mb-1 flex items-center gap-2 text-xs text-muted-foreground">
                        <Building2 className="h-3.5 w-3.5" />
                        Empresa vinculada
                      </div>
                      <p className="truncate font-medium" title={details.linkedCompany || "N/A"}>{details.linkedCompany || "N/A"}</p>
                    </div>
                    <div className="rounded-lg border border-border/70 bg-surface p-3">
                      <div className="mb-1 flex items-center gap-2 text-xs text-muted-foreground">
                        <CalendarClock className="h-3.5 w-3.5" />
                        Última compra
                      </div>
                      <p className="font-medium">{formatDate(details.lastPurchaseDate)}</p>
                    </div>
                  </div>
                </CardContent>
              </Card>

              <section className="space-y-3">
                <div className="space-y-3 rounded-xl border border-border/70 bg-muted/20 p-4">
                  <div className="space-y-1">
                    <h4 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Período da análise</h4>
                    <p className="text-sm text-muted-foreground">
                      {detailsPeriod === "all" ? "Todo o histórico disponível do cliente." :
                       detailsPeriod === "12m" ? "Últimos 12 meses de compras." :
                       detailsPeriod === "3m" ? "Últimos 3 meses de compras." :
                       "Últimos 30 dias de compras."}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {DETAILS_PERIOD_OPTIONS.map(opt => (
                      <Button
                        key={opt.value}
                        type="button"
                        size="sm"
                        variant={detailsPeriod === opt.value ? "default" : "outline"}
                        onClick={() => setDetailsPeriod(opt.value)}
                      >
                        {opt.label}
                      </Button>
                    ))}
                  </div>
                </div>

                <div className="flex items-center gap-2 px-0.5">
                  <DollarSign className="h-4 w-4 text-primary" />
                  <h4 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Indicadores financeiros</h4>
                </div>
                <div className="metric-row">
                  <KpiCard className="border-primary/25 bg-gradient-to-b from-surface to-muted/35" title="Faturamento total" value={formatKpiCompactCurrency(details.totalRevenue)} valueTooltip={formatCurrency(details.totalRevenue)} showPercentageChange={false} icon={DollarSign} periodLabel={analysisScope === HISTORY_ANALYSIS_SCOPE ? "Faturamento acumulado nos últimos 12 meses" : "Faturamento acumulado no período selecionado"} />
                  <KpiCard className="border-primary/20 bg-gradient-to-b from-surface to-muted/30" title="Ticket médio" value={formatNullableCurrency(details.averageTicket, formatKpiCompactCurrency)} valueTooltip={formatNullableCurrencyTooltip(details.averageTicket, formatCurrency)} showPercentageChange={false} icon={Receipt} periodLabel="Faturamento total dividido pelo total de pedidos" />
                  <KpiCard className="border-border/80 bg-gradient-to-b from-surface to-muted/25" title="Média mensal" value={formatNullableCurrency(details.averageRevenueMonthly, formatKpiCompactCurrency)} valueTooltip={formatNullableCurrencyTooltip(details.averageRevenueMonthly, formatCurrency)} showPercentageChange={false} icon={CalendarClock} periodLabel="Média das receitas por meses com compra no período" />
                </div>
              </section>

              <section className="space-y-3">
                <div className="flex items-center gap-2 px-0.5">
                  <TrendingUp className="h-4 w-4 text-primary" />
                  <h4 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Indicadores operacionais</h4>
                </div>
                <div className="metric-row">
                  <KpiCard className="border-border/80 bg-gradient-to-b from-surface to-muted/25" title="Quantidade total" value={`${formatKpiCompactNumber(details.totalQuantity)}`} valueTooltip={`${formatDecimal(details.totalQuantity)} unidades`} showPercentageChange={false} icon={TrendingUp} periodLabel="Soma das quantidades no período filtrado" />
                  <KpiCard className="border-border/80 bg-gradient-to-b from-surface to-muted/25" title="Peso total" value={`${formatKpiCompactNumber(details.totalWeight)} kg`} valueTooltip={`${formatDecimal(details.totalWeight)} kg`} showPercentageChange={false} icon={TrendingUp} periodLabel="Peso acumulado das compras no período" />
                  <KpiCard className="border-border/80 bg-gradient-to-b from-surface to-muted/25" title="Total de pedidos" value={formatKpiCompactNumber(details.totalOrders)} valueTooltip={`${String(details.totalOrders)} pedidos`} showPercentageChange={false} icon={UserRound} periodLabel="Pedidos distintos por documento no período" />
                  <KpiCard className="border-primary/20 bg-gradient-to-b from-surface to-muted/30" title="Frequência média" value={formatPurchaseFrequency(details.averageDaysBetweenPurchases).value} valueTooltip={formatPurchaseFrequency(details.averageDaysBetweenPurchases).tooltip} showPercentageChange={false} icon={CalendarClock} periodLabel={`Compra em média a cada ${formatPurchaseFrequency(details.averageDaysBetweenPurchases).value}`} />
                </div>
              </section>

              <Card className="border-border/80 bg-card/95">
                <CardHeader className="pb-2">
                  <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
                    <CardTitle>Evolução temporal</CardTitle>
                    <select className="h-9 rounded-md border border-border bg-background px-2 text-sm" value={timelineMetric} onChange={(e) => setTimelineMetric(e.target.value as any)}>
                      <option value="revenue">Faturamento</option>
                      <option value="orders">Quantidade de pedidos</option>
                      <option value="quantity">Quantidade comprada</option>
                      <option value="averageTicket">Ticket médio</option>
                    </select>
                  </div>
                </CardHeader>
                <CardContent className="pb-5">
                  {resolvedAnalysisPeriod && (
                    <p className="mb-3 text-xs text-muted-foreground">
                      {analysisScope === HISTORY_ANALYSIS_SCOPE ? "Últimos 12 meses" : "Período filtrado"}: {formatDate(resolvedAnalysisPeriod.periodStart)} até {formatDate(resolvedAnalysisPeriod.periodEnd)}.
                      {" "}Agrupamento automático por {describeTimelineGranularity(timeline?.granularity ?? "monthly")}.
                    </p>
                  )}
                  {!timeline && <p className="text-sm text-muted-foreground">Dados insuficientes para evolução temporal neste período.</p>}
                  {timeline && timeline.points.length === 0 && <p className="text-sm text-muted-foreground">Sem pontos para o período selecionado.</p>}
                  {timeline && timeline.points.length > 0 && (
                    <div className="mb-4 space-y-3">
                      <div className="metric-row">
                        <KpiCard
                          className="border-border/80 bg-gradient-to-b from-surface to-muted/25"
                          title={`Média por ${describeTimelineGranularity(timeline.granularity)}`}
                          value={
                            periodTrendSummary.averageValue == null
                              ? "Sem base"
                              : formatTimelineMetricValue(periodTrendSummary.averageValue, timeline.metric)
                          }
                          valueTooltip={
                            periodTrendSummary.averageValue == null
                              ? "Dados insuficientes no período filtrado"
                              : formatTimelineMetricValue(periodTrendSummary.averageValue, timeline.metric)
                          }
                          showPercentageChange={false}
                          icon={CalendarClock}
                          periodLabel={`Média dos pontos exibidos no gráfico para este ${describeTimelineGranularity(timeline.granularity)}.`}
                        />
                        <KpiCard
                          className="border-border/80 bg-gradient-to-b from-surface to-muted/25"
                          title="Tendência do período"
                          value={periodTrendSummary.trendLabel}
                          valueTooltip={periodTrendSummary.trendLabel}
                          showPercentageChange={false}
                          icon={TrendingUp}
                          periodLabel={
                            periodTrendSummary.comparableIntervals > 0
                              ? `${periodTrendSummary.comparableIntervals} comparações entre ${describeTimelineGranularityPlural(timeline.granularity)} do gráfico.`
                              : "Ainda não há base comparável suficiente entre os pontos do gráfico."
                          }
                          allowWrapValue
                        />
                        <KpiCard
                          className="border-border/80 bg-gradient-to-b from-surface to-muted/25"
                          title={`Variação média por ${describeTimelineGranularity(timeline.granularity)}`}
                          value={formatVariationPercent(periodTrendSummary.averageChangePercent)}
                          valueTooltip={
                            periodTrendSummary.averageChangePercent == null
                              ? "Dados insuficientes para comparar pontos consecutivos"
                              : formatVariationPercent(periodTrendSummary.averageChangePercent)
                          }
                          showPercentageChange={false}
                          icon={ArrowUpRight}
                          periodLabel={`Média das oscilações entre um ${describeTimelineGranularity(timeline.granularity)} e o seguinte.`}
                        />
                      </div>
                      <div className="rounded-lg border border-border bg-muted/20 p-3 text-sm text-muted-foreground">
                        {analysisNarrative}
                      </div>
                    </div>
                  )}
                  <div className="overflow-x-auto custom-scrollbar -mx-1 px-1">
                    <div className="min-w-[500px]">
                    <ChartContainer config={{ value: { label: "Histórico", color: "var(--primary)" }, forecast: { label: "Previsão", color: "hsl(var(--chart-4))" } }} className="h-[320px] min-h-[320px] w-full pb-1 sm:h-[340px] sm:min-h-[340px]">
                      <LineChart data={timelineChartData} margin={{ left: 8, right: 12, top: 10, bottom: 20 }}>
                        <CartesianGrid vertical={false} />
                        <XAxis dataKey="periodStart" tickFormatter={(value) => formatMonthYear(String(value))} minTickGap={24} />
                        <YAxis width={86} tickFormatter={(value) => formatTimelineMetricValue(Number(value), timelineMetric)} />
                        <ChartTooltip content={<ChartTooltipContent labelFormatter={(value) => new Date(String(value)).toLocaleDateString("pt-BR")} formatter={(value) => formatTimelineMetricValue(Number(value), timelineMetric)} />} />
                        <Line dataKey="chartValue" name="Histórico" type="monotone" stroke="var(--color-value)" strokeWidth={2.4} dot={{ r: 3 }} activeDot={{ r: 5 }} connectNulls />
                        <Line dataKey="predictedValue" name="Previsão" type="monotone" stroke="var(--color-forecast)" strokeWidth={2.4} dot={{ r: 3 }} strokeDasharray="6 6" connectNulls />
                      </LineChart>
                    </ChartContainer>
                    </div>
                  </div>
                </CardContent>
              </Card>

              <Card className="border-border/80 bg-card/95">
                <CardHeader>
                  <CardTitle>Histórico de compras</CardTitle>
                </CardHeader>
                <CardContent className="space-y-3">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Data</TableHead>
                        <TableHead>Documento</TableHead>
                        <TableHead>Produto</TableHead>
                        <TableHead>Quantidade</TableHead>
                        <TableHead>Valor unitário</TableHead>
                        <TableHead>Total</TableHead>
                        <TableHead>Peso</TableHead>
                        <TableHead>Tipo</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {(history?.items ?? []).length === 0 && (
                        <TableRow>
                          <TableCell colSpan={8} className="py-6 text-center text-muted-foreground">Sem compras para o escopo atual da análise.</TableCell>
                        </TableRow>
                      )}
                      {(history?.items ?? []).map((item) => (
                        <TableRow key={`${item.date}-${item.document}-${item.product}`}>
                          <TableCell>{formatDate(item.date)}</TableCell>
                          <TableCell>{item.document}</TableCell>
                          <TableCell>{item.product}</TableCell>
                          <TableCell>{formatDecimal(item.quantity)} un</TableCell>
                          <TableCell>{formatCurrency(item.unitPrice)}</TableCell>
                          <TableCell>{formatCurrency(item.total)}</TableCell>
                          <TableCell>{formatDecimal(item.weight)} kg</TableCell>
                          <TableCell>{item.operationType}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                  <div className="flex items-center justify-end gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={historyPage <= 1 || detailsLoading || !selectedCustomerId}
                      onClick={() => selectedCustomerId && void loadCustomerDetails(selectedCustomerId, historyPage - 1)}
                    >
                      Anterior
                    </Button>
                    <span className="text-xs text-muted-foreground">Página {historyPage} de {historyTotalPages}</span>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={historyPage >= historyTotalPages || detailsLoading || !selectedCustomerId}
                      onClick={() => selectedCustomerId && void loadCustomerDetails(selectedCustomerId, historyPage + 1)}
                    >
                      Próxima
                    </Button>
                  </div>
                </CardContent>
              </Card>
            </div>
          )}

          {!details && detailsLoading && (
            <div className="mt-4 space-y-3">
              <SkeletonModalContent />
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={newCustomersOpen} onOpenChange={setNewCustomersOpen}>
        <DialogContent className="custom-scrollbar max-h-[85vh] w-[95vw] max-w-4xl overflow-y-auto p-5 pt-8 pr-10 sm:p-6 sm:pt-9 sm:pr-12">
          <DialogHeader>
            <DialogTitle>Novos clientes por mês</DialogTitle>
            <DialogDescription>
              Evolução mensal de entrada de novos clientes no período filtrado.
            </DialogDescription>
          </DialogHeader>

          {newCustomersMessage && (
            <Alert variant="destructive" className="mt-2">
              <AlertDescription>{newCustomersMessage}</AlertDescription>
            </Alert>
          )}

          {newCustomersLoading && (
            <div className="mt-4 space-y-3">
              <SkeletonMetricCard />
              <SkeletonChart className="h-72" />
            </div>
          )}

          {!newCustomersLoading && newCustomersMonthly && (
            <div className="mt-4 space-y-4">
              <div className="metric-row">
                <KpiCard
                  title="Total no período"
                  value={formatKpiCompactNumber(newCustomersMonthly.totalNewCustomers)}
                  valueTooltip={String(newCustomersMonthly.totalNewCustomers)}
                  showPercentageChange={false}
                  icon={Users}
                  periodLabel={`${formatDate(newCustomersMonthly.periodStart)} a ${formatDate(newCustomersMonthly.periodEnd)}`}
                />
                <KpiCard
                  title="Média por mês"
                  value={formatKpiCompactNumber(computeNewCustomersInsights(newCustomersMonthly.points).averagePerMonth)}
                  valueTooltip={formatDecimal(computeNewCustomersInsights(newCustomersMonthly.points).averagePerMonth)}
                  showPercentageChange={false}
                  icon={TrendingUp}
                  periodLabel="Novos clientes por mês (média do intervalo)"
                />
                <KpiCard
                  title="Mês com pico"
                  value={formatKpiCompactNumber(computeNewCustomersInsights(newCustomersMonthly.points).peakMonthValue)}
                  valueTooltip={String(computeNewCustomersInsights(newCustomersMonthly.points).peakMonthValue)}
                  showPercentageChange={false}
                  icon={CalendarClock}
                  periodLabel={computeNewCustomersInsights(newCustomersMonthly.points).peakMonthLabel}
                />
              </div>

              <Card>
                <CardHeader>
                  <CardTitle>Linha mensal de novos clientes</CardTitle>
                </CardHeader>
                <CardContent>
                  <ChartContainer config={{ newCustomers: { label: "Novos clientes", color: "hsl(var(--chart-2))" } }} className="h-[300px] w-full">
                    <LineChart data={newCustomersMonthly.points} margin={{ left: 8, right: 8, top: 10, bottom: 4 }}>
                      <CartesianGrid vertical={false} />
                      <XAxis dataKey="monthStart" tickFormatter={(value) => new Date(value).toLocaleDateString("pt-BR", { month: "short", year: "2-digit" })} minTickGap={20} />
                      <YAxis allowDecimals={false} />
                      <ChartTooltip
                        content={
                          <ChartTooltipContent
                            labelFormatter={(value) => new Date(String(value)).toLocaleDateString("pt-BR", { month: "long", year: "numeric" })}
                            formatter={(value) => formatDecimal(Number(value))}
                          />
                        }
                      />
                      <Line dataKey="newCustomers" type="monotone" stroke="var(--color-newCustomers)" strokeWidth={2.5} dot />
                    </LineChart>
                  </ChartContainer>
                </CardContent>
              </Card>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
