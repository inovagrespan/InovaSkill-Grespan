import { FormEvent, useEffect, useMemo, useState } from "react";
import { Link, createFileRoute, useNavigate } from "@tanstack/react-router";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { KpiCard } from "@/components/ui/kpi-card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { SkeletonChart, SkeletonMetricCard, SkeletonModalContent, SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { AlertTriangle, ArrowDownRight, ArrowUpRight, Building2, CalendarClock, DollarSign, MapPin, Minus, Receipt, TrendingUp, UserRound, Users, type LucideIcon } from "lucide-react";
import { Line, LineChart, CartesianGrid, XAxis, YAxis } from "recharts";
import {
  fetchCustomerAnalyticsSummary,
  fetchCustomerComparison,
  fetchCustomerDetailsSummary,
  fetchCustomerNewCustomersMonthly,
  fetchCustomerPurchaseHistory,
  fetchCustomerRanking,
  fetchCustomerInsights,
  fetchCustomerTimeline,
  fetchCustomerTopProducts,
  type CustomerAnalyticsSummary,
  type CustomerComparisonItem,
  type CustomerDetailSummary,
  type CustomerNewCustomersMonthlyResponse,
  type CustomerPurchaseHistoryResponse,
  type CustomerRankingItem,
  type CustomerInsightsResponse,
  type CustomerTimelineResponse,
  type CustomerTopProductItem,
} from "@/lib/importer-api";
import {
  formatNullableCurrency,
  formatNullableCurrencyTooltip,
  formatPurchaseFrequency,
  formatVariationPercent,
  resolveCustomerStatusVariant,
} from "@/lib/customer-details";
import { buildCustomerCommercialIntelligence, type CommercialIntelligenceCard, type CommercialIntelligenceTone, type CustomerCommercialIntelligence } from "@/lib/customer-commercial-intelligence";
import { computeNewCustomersInsights } from "@/lib/customer-new-customers";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";

export const Route = createFileRoute("/clientes")({
  validateSearch: (search: Record<string, unknown>) => ({
    cliente: typeof search.cliente === "string" ? search.cliente : undefined,
  }),
  component: ClientesPage,
});

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

const intelligenceToneStyles: Record<CommercialIntelligenceTone, {
  className: string;
  iconClassName: string;
  badgeVariant: "default" | "destructive" | "secondary" | "outline";
}> = {
  success: {
    className: "border-[var(--success)]/30 bg-[var(--success)]/10",
    iconClassName: "text-[var(--success)]",
    badgeVariant: "default",
  },
  warning: {
    className: "border-[var(--warning)]/35 bg-[var(--warning)]/10",
    iconClassName: "text-[var(--warning)]",
    badgeVariant: "outline",
  },
  danger: {
    className: "border-[var(--danger)]/35 bg-[var(--danger)]/10",
    iconClassName: "text-[var(--danger)]",
    badgeVariant: "destructive",
  },
  neutral: {
    className: "border-border/80 bg-gradient-to-b from-surface to-muted/25",
    iconClassName: "text-primary",
    badgeVariant: "secondary",
  },
};

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
  { customerCode: "CLI-001", customerName: "Mercado São Bento", revenue: 64_850, quantity: 2_420, weight: 12_800, orders: 28, averageTicket: 2_316.07, variationPercent: 12.6 },
  { customerCode: "CLI-002", customerName: "Atacado Primavera", revenue: 52_300, quantity: 3_180, weight: 9_750, orders: 21, averageTicket: 2_490.48, variationPercent: -6.4 },
  { customerCode: "CLI-003", customerName: "Super Lopes", revenue: 38_940, quantity: 1_760, weight: 4_980, orders: 18, averageTicket: 2_163.33, variationPercent: 24.8 },
  { customerCode: "CLI-004", customerName: "Distribuidora Central", revenue: 31_500, quantity: 980, weight: 2_400, orders: 12, averageTicket: 2_625, variationPercent: 5.7 },
];

function IntelligenceDecisionCard({
  title,
  card,
  icon: Icon,
  metric,
  className = "",
}: {
  title: string;
  card: CommercialIntelligenceCard;
  icon: LucideIcon;
  metric?: string;
  className?: string;
}) {
  const tone = intelligenceToneStyles[card.tone];

  return (
    <div className={`rounded-xl border p-4 ${tone.className} ${className}`}>
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 items-center gap-2">
          <Icon className={`h-4 w-4 shrink-0 ${tone.iconClassName}`} />
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{title}</p>
        </div>
        <Badge variant={tone.badgeVariant} className="shrink-0">{card.status}</Badge>
      </div>
      {metric && <p className="mt-3 text-2xl font-display tracking-tight text-foreground">{metric}</p>}
      <p className="mt-3 text-sm font-semibold text-foreground">{card.summary}</p>
      <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{card.detail}</p>
    </div>
  );
}

function ProductsIntelligenceCard({ intelligence }: { intelligence: CustomerCommercialIntelligence }) {
  const tone = intelligenceToneStyles[intelligence.productsTone];

  return (
    <div className={`rounded-xl border p-4 ${tone.className}`}>
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 items-center gap-2">
          <Receipt className={`h-4 w-4 shrink-0 ${tone.iconClassName}`} />
          <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Produtos Mais Relevantes</p>
        </div>
        <Badge variant={tone.badgeVariant} className="shrink-0">{intelligence.relevantProducts.length > 0 ? "Mix principal" : "Sem base"}</Badge>
      </div>
      <p className="mt-3 text-sm font-semibold text-foreground">{intelligence.productsSummary}</p>
      <div className="mt-3 space-y-2">
        {intelligence.relevantProducts.length === 0 && (
          <p className="rounded-md bg-muted/40 p-3 text-sm text-muted-foreground">Amplie o período ou aguarde novas compras para identificar produtos sustentadores.</p>
        )}
        {intelligence.relevantProducts.map((product) => (
          <div key={`${product.code}-${product.name}`} className="rounded-md bg-background/70 p-3">
            <div className="flex items-start justify-between gap-3">
              <p className="min-w-0 truncate text-sm font-medium" title={product.name}>{product.name}</p>
              <span className="shrink-0 text-sm font-semibold">{product.sharePercent.toFixed(1)}%</span>
            </div>
            <p className="mt-1 text-xs text-muted-foreground">{product.summary}</p>
          </div>
        ))}
      </div>
    </div>
  );
}

function canComparePeriod(item: CustomerComparisonItem): boolean {
  return item.previousValue > 0 && item.variationPercent != null && !Number.isNaN(item.variationPercent);
}

function resolveComparisonTone(value: number): {
  label: string;
  className: string;
  icon: typeof ArrowUpRight;
} {
  if (value > 0.05) {
    return {
      label: "Crescimento",
      className: "border-[var(--success)]/30 bg-[var(--success)]/10 text-[var(--success)]",
      icon: ArrowUpRight,
    };
  }

  if (value < -0.05) {
    return {
      label: "Queda",
      className: "border-[var(--danger)]/30 bg-[var(--danger)]/10 text-[var(--danger)]",
      icon: ArrowDownRight,
    };
  }

  return {
    label: "Estável",
    className: "border-border bg-muted/50 text-muted-foreground",
    icon: Minus,
  };
}

function toInputDate(value: Date): string {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, "0");
  const day = String(value.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
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

function ClientesPage() {
  const navigate = useNavigate({ from: "/clientes" });
  const { cliente } = Route.useSearch();
  const [summary, setSummary] = useState<CustomerAnalyticsSummary | null>(null);
  const [items, setItems] = useState<CustomerRankingItem[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalItems, setTotalItems] = useState(0);
  const [sortBy, setSortBy] = useState<"revenue" | "growth" | "drop" | "quantity" | "weight" | "ticket">("revenue");
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");

  const [dateFrom, setDateFrom] = useState(() => toInputDate(new Date(Date.now() - 1000 * 60 * 60 * 24 * 30)));
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
  const [details, setDetails] = useState<CustomerDetailSummary | null>(null);
  const [timeline, setTimeline] = useState<CustomerTimelineResponse | null>(null);
  const [topProducts, setTopProducts] = useState<CustomerTopProductItem[]>([]);
  const [comparison, setComparison] = useState<CustomerComparisonItem[]>([]);
  const [insights, setInsights] = useState<CustomerInsightsResponse | null>(null);
  const [history, setHistory] = useState<CustomerPurchaseHistoryResponse | null>(null);
  const [historyPage, setHistoryPage] = useState(1);
  const [timelineGranularity, setTimelineGranularity] = useState<"daily" | "weekly" | "monthly">("monthly");
  const [timelineMetric, setTimelineMetric] = useState<"revenue" | "quantity" | "weight" | "orders">("revenue");
  const [movingAverageWindow, setMovingAverageWindow] = useState<3 | 6 | 12>(3);
  const [newCustomersOpen, setNewCustomersOpen] = useState(false);
  const [newCustomersLoading, setNewCustomersLoading] = useState(false);
  const [newCustomersMessage, setNewCustomersMessage] = useState("");
  const [newCustomersMonthly, setNewCustomersMonthly] = useState<CustomerNewCustomersMonthlyResponse | null>(null);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalItems / pageSize)), [totalItems, pageSize]);
  const historyTotalPages = useMemo(
    () => Math.max(1, Math.ceil((history?.totalItems ?? 0) / (history?.pageSize ?? 10))),
    [history?.totalItems, history?.pageSize],
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
  const comparablePeriods = useMemo(() => comparison.filter(canComparePeriod), [comparison]);
  const commercialIntelligence = useMemo(
    () => buildCustomerCommercialIntelligence({ details, insights, comparison, topProducts }),
    [details, insights, comparison, topProducts],
  );

  async function load(targetPage: number) {
    setLoading(true);
    setMessage("");
    const demoRanking = sortDemoCustomers(filterDemoCustomers(customer), sortBy);
    try {
      const [summaryData, rankingData] = await Promise.all([
        fetchCustomerAnalyticsSummary({ dateFrom, dateTo, customer, city, productGroup, productCode, transactionType }),
        fetchCustomerRanking({ page: targetPage, pageSize, sortBy, dateFrom, dateTo, customer, city, productGroup, productCode, transactionType }),
      ]);
      if (summaryData.activeCustomers === 0 || rankingData.items.length === 0) {
        setSummary(DEMO_CUSTOMER_SUMMARY);
        setItems(demoRanking);
        setPage(targetPage);
        setTotalItems(demoRanking.length);
        return;
      }
      setSummary(summaryData);
      setItems(rankingData.items);
      setPage(rankingData.page);
      setTotalItems(rankingData.totalItems);
    } catch (error) {
      setSummary(DEMO_CUSTOMER_SUMMARY);
      setItems(demoRanking);
      setPage(targetPage);
      setTotalItems(demoRanking.length);
      setMessage("");
    } finally {
      setLoading(false);
    }
  }

  async function loadCustomerDetails(customerId: string, targetHistoryPage = 1, granularity = timelineGranularity, metric = timelineMetric) {
    setDetailsLoading(true);
    setDetailsMessage("");
    try {
      const [summaryData, timelineData, topProductsData, comparisonData, historyData, insightsData] = await Promise.allSettled([
        fetchCustomerDetailsSummary({ customerId, dateFrom, dateTo }),
        fetchCustomerTimeline({ customerId, dateFrom, dateTo, granularity, metric }),
        fetchCustomerTopProducts({ customerId, dateFrom, dateTo }),
        fetchCustomerComparison({ customerId, referenceDate: dateTo }),
        fetchCustomerPurchaseHistory({ customerId, dateFrom, dateTo, page: targetHistoryPage, pageSize: 10 }),
        fetchCustomerInsights({ customerId, movingAverageWindowMonths: movingAverageWindow }),
      ]);

      if (summaryData.status === "rejected") {
        throw summaryData.reason;
      }

      setDetails(summaryData.value);
      setTimeline(timelineData.status === "fulfilled" ? timelineData.value : null);
      setTopProducts(topProductsData.status === "fulfilled" ? topProductsData.value : []);
      setComparison(comparisonData.status === "fulfilled" ? comparisonData.value.items : []);
      setInsights(insightsData.status === "fulfilled" ? insightsData.value : null);
      setHistory(historyData.status === "fulfilled" ? historyData.value : { page: targetHistoryPage, pageSize: 10, totalItems: 0, items: [] });
      setHistoryPage(targetHistoryPage);

      const partialErrors: string[] = [];
      if (timelineData.status === "rejected") partialErrors.push("evolução temporal");
      if (topProductsData.status === "rejected") partialErrors.push("produtos");
      if (comparisonData.status === "rejected") partialErrors.push("comparativo");
      if (historyData.status === "rejected") partialErrors.push("histórico");
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
  }, [sortBy]);

  useEffect(() => {
    if (!selectedCustomerId || !detailsOpen) return;
    void loadCustomerDetails(selectedCustomerId, 1, timelineGranularity, timelineMetric);
  }, [timelineGranularity, timelineMetric, movingAverageWindow]);

  useEffect(() => {
    if (!detailsOpen && !newCustomersOpen) return;
    const timers = [
      window.setTimeout(() => window.dispatchEvent(new Event("resize")), 40),
      window.setTimeout(() => window.dispatchEvent(new Event("resize")), 180),
    ];
    return () => timers.forEach((timerId) => window.clearTimeout(timerId));
  }, [detailsOpen, newCustomersOpen, timelineGranularity, timelineMetric, newCustomersMonthly?.points.length]);

  useEffect(() => {
    if (!cliente) {
      setDetailsOpen(false);
      setSelectedCustomerId(null);
      return;
    }

    if (cliente === selectedCustomerId && detailsOpen) return;
    setSelectedCustomerId(cliente);
    setDetailsOpen(true);
    void loadCustomerDetails(cliente, 1);
  }, [cliente]);

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
        <h1 className="mt-2 text-4xl font-display tracking-tight">Análise de Clientes</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">Visão detalhada do comportamento de compra por cliente.</p>
      </header>

      {message && (
        <Alert variant="destructive" className="animate-soft-enter">
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <Card className="animate-soft-enter">
        <CardHeader>
          <CardTitle>Filtros</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="grid grid-cols-1 gap-3 md:grid-cols-4">
            <div className="space-y-1">
              <Label>Data inicial</Label>
              <Input type="date" value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>Data final</Label>
              <Input type="date" value={dateTo} onChange={(e) => setDateTo(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>Cliente</Label>
              <Input value={customer} onChange={(e) => setCustomer(e.target.value)} placeholder="Código ou nome" />
            </div>
            <div className="space-y-1">
              <Label>Cidade</Label>
              <Input value={city} onChange={(e) => setCity(e.target.value)} placeholder="Cidade" />
            </div>
            <div className="space-y-1">
              <Label>Grupo</Label>
              <Input value={productGroup} onChange={(e) => setProductGroup(e.target.value)} placeholder="Grupo do produto" />
            </div>
            <div className="space-y-1">
              <Label>Produto</Label>
              <Input value={productCode} onChange={(e) => setProductCode(e.target.value)} placeholder="Código do produto" />
            </div>
            <div className="space-y-1">
              <Label>Tipo de operação</Label>
              <Input value={transactionType} onChange={(e) => setTransactionType(e.target.value)} placeholder="Tipo" />
            </div>
            <div className="flex items-end justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => { setCustomer(""); setCity(""); setProductGroup(""); setProductCode(""); setTransactionType(""); }}>
                Limpar
              </Button>
              <Button type="submit">Filtrar</Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card className="animate-soft-enter">
        <CardHeader>
          <CardTitle>Resumo</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
            {loading ? (
              <>
                <SkeletonMetricCard />
                <SkeletonMetricCard />
                <SkeletonMetricCard />
                <SkeletonMetricCard />
              </>
            ) : (
            <>
            <KpiCard title="Clientes ativos" value={formatKpiCompactNumber(summary?.activeCustomers ?? 0)} valueTooltip={String(summary?.activeCustomers ?? 0)} showPercentageChange={false} icon={Users} periodLabel="Clientes com compras no período" loading={loading} />
            <KpiCard title="Faturamento total" value={formatKpiCompactCurrency(summary?.totalRevenue ?? 0)} valueTooltip={formatCurrency(summary?.totalRevenue ?? 0)} showPercentageChange={false} icon={DollarSign} periodLabel={summary ? `${formatDate(summary.currentPeriodStart)} a ${formatDate(summary.currentPeriodEnd)}` : "Período atual"} loading={loading} />
            <KpiCard title="Ticket médio" value={formatKpiCompactCurrency(summary?.averageTicket ?? 0)} valueTooltip={formatCurrency(summary?.averageTicket ?? 0)} showPercentageChange={false} icon={Receipt} periodLabel="Faturamento dividido por pedidos" loading={loading} />
            <button
              type="button"
              className="text-left"
              onClick={openNewCustomersModal}
              aria-label="Abrir detalhamento de novos clientes por mês"
            >
              <KpiCard title="Novos clientes" value={formatKpiCompactNumber(summary?.newCustomers ?? 0)} valueTooltip={String(summary?.newCustomers ?? 0)} showPercentageChange={false} icon={TrendingUp} periodLabel={summary ? `Inativos no período: ${summary.inactiveCustomers}` : ""} loading={loading} />
            </button>
            </>
            )}
          </div>

          <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
            <p className="text-sm font-medium">Ranking de clientes (clique na linha para abrir detalhes)</p>
            <select className="h-9 rounded-md border border-border bg-background px-2 text-sm" value={sortBy} onChange={(e) => setSortBy(e.target.value as any)}>
              <option value="revenue">Maior faturamento</option>
              <option value="growth">Maior crescimento</option>
              <option value="drop">Maior queda</option>
              <option value="quantity">Maior volume</option>
              <option value="ticket">Maior ticket médio</option>
            </select>
          </div>

          {loading ? (
            <SkeletonTable rows={8} columns={7} />
          ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Cliente</TableHead>
                <TableHead>Faturamento</TableHead>
                <TableHead>Quantidade</TableHead>
                <TableHead>Peso</TableHead>
                <TableHead>Pedidos</TableHead>
                <TableHead>Ticket médio</TableHead>
                <TableHead>Variação</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {!loading && items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">Sem dados para os filtros selecionados.</TableCell>
                </TableRow>
              )}
              {items.map((item) => (
                <TableRow key={`${item.customerCode}-${item.customerName}`} className="cursor-pointer" onClick={() => openDetails(item.customerCode)}>
                  <TableCell>{item.customerName}</TableCell>
                  <TableCell>{formatCurrency(item.revenue)}</TableCell>
                  <TableCell>{formatDecimal(item.quantity)}</TableCell>
                  <TableCell>{formatDecimal(item.weight)}</TableCell>
                  <TableCell>{item.orders}</TableCell>
                  <TableCell>{formatCurrency(item.averageTicket)}</TableCell>
                  <TableCell className={item.variationPercent == null ? "text-muted-foreground" : item.variationPercent >= 0 ? "text-[var(--success)]" : "text-[var(--danger)]"}>{formatVariationPercent(item.variationPercent)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          )}

          <div className="flex items-center justify-end gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => void load(page - 1)}>Anterior</Button>
            <span className="text-xs text-muted-foreground">Página {page} de {totalPages}</span>
            <Button variant="outline" size="sm" disabled={page >= totalPages || loading} onClick={() => void load(page + 1)}>Próxima</Button>
          </div>
        </CardContent>
      </Card>

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
                <div className="flex items-center gap-2 px-0.5">
                  <DollarSign className="h-4 w-4 text-primary" />
                  <h4 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Indicadores financeiros</h4>
                </div>
                <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
                  <KpiCard className="border-primary/25 bg-gradient-to-b from-surface to-muted/35" title="Faturamento total" value={formatKpiCompactCurrency(details.totalRevenue)} valueTooltip={formatCurrency(details.totalRevenue)} showPercentageChange={false} icon={DollarSign} periodLabel="Faturamento acumulado no período selecionado" />
                  <KpiCard className="border-primary/20 bg-gradient-to-b from-surface to-muted/30" title="Ticket médio" value={formatNullableCurrency(details.averageTicket, formatKpiCompactCurrency)} valueTooltip={formatNullableCurrencyTooltip(details.averageTicket, formatCurrency)} showPercentageChange={false} icon={Receipt} periodLabel="Faturamento total dividido pelo total de pedidos" />
                  <KpiCard className="border-border/80 bg-gradient-to-b from-surface to-muted/25" title="Média mensal" value={formatNullableCurrency(details.averageRevenueMonthly, formatKpiCompactCurrency)} valueTooltip={formatNullableCurrencyTooltip(details.averageRevenueMonthly, formatCurrency)} showPercentageChange={false} icon={CalendarClock} periodLabel="Média das receitas por meses com compra no período" />
                  <KpiCard className="border-border/80 bg-gradient-to-b from-surface to-muted/25" title="Média semanal" value={formatNullableCurrency(details.averageRevenueWeekly, formatKpiCompactCurrency)} valueTooltip={formatNullableCurrencyTooltip(details.averageRevenueWeekly, formatCurrency)} showPercentageChange={false} icon={CalendarClock} periodLabel="Média das receitas por semanas com compra no período" />
                </div>
              </section>

              <section className="space-y-3">
                <div className="flex items-center gap-2 px-0.5">
                  <TrendingUp className="h-4 w-4 text-primary" />
                  <h4 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Indicadores operacionais</h4>
                </div>
                <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
                  <KpiCard className="border-border/80 bg-gradient-to-b from-surface to-muted/25" title="Quantidade total" value={formatKpiCompactNumber(details.totalQuantity)} valueTooltip={formatDecimal(details.totalQuantity)} showPercentageChange={false} icon={TrendingUp} periodLabel="Soma das quantidades no período filtrado" />
                  <KpiCard className="border-border/80 bg-gradient-to-b from-surface to-muted/25" title="Peso total" value={formatKpiCompactNumber(details.totalWeight)} valueTooltip={formatDecimal(details.totalWeight)} showPercentageChange={false} icon={TrendingUp} periodLabel="Peso acumulado das compras no período" />
                  <KpiCard className="border-border/80 bg-gradient-to-b from-surface to-muted/25" title="Total de pedidos" value={formatKpiCompactNumber(details.totalOrders)} valueTooltip={String(details.totalOrders)} showPercentageChange={false} icon={UserRound} periodLabel="Pedidos distintos por documento no período" />
                  <KpiCard className="border-primary/20 bg-gradient-to-b from-surface to-muted/30" title="Frequência média" value={formatPurchaseFrequency(details.averageDaysBetweenPurchases).value} valueTooltip={formatPurchaseFrequency(details.averageDaysBetweenPurchases).tooltip} showPercentageChange={false} icon={CalendarClock} periodLabel={`Compra em média a cada ${formatPurchaseFrequency(details.averageDaysBetweenPurchases).value}`} />
                </div>
              </section>

              <section className="space-y-3">
                <div className="flex items-center justify-between gap-3 px-0.5">
                  <div className="flex items-center gap-2">
                    <AlertTriangle className="h-4 w-4 text-primary" />
                    <h4 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Inteligência Comercial</h4>
                  </div>
                  <div className="flex flex-wrap items-center justify-end gap-2">
                    <Button size="sm" variant="outline" asChild>
                      <Link to="/clientes/analise-comercial" search={{ cliente: selectedCustomerId ?? details.customerCode }}>
                        Ver análise completa
                      </Link>
                    </Button>
                    <select className="h-9 rounded-md border border-border bg-background px-2 text-sm" aria-label="Base histórica para inteligência comercial" value={movingAverageWindow} onChange={(e) => setMovingAverageWindow(Number(e.target.value) as 3 | 6 | 12)}>
                      <option value={3}>Base: últimos 3 meses</option>
                      <option value={6}>Base: últimos 6 meses</option>
                      <option value={12}>Base: últimos 12 meses</option>
                    </select>
                  </div>
                </div>
                <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
                  <IntelligenceDecisionCard title="Saúde do Cliente" card={commercialIntelligence.health} icon={AlertTriangle} />
                  <IntelligenceDecisionCard title="Tendência de Consumo" card={commercialIntelligence.trend} icon={TrendingUp} />
                  <IntelligenceDecisionCard
                    title="Potencial Esperado"
                    card={commercialIntelligence.potential}
                    icon={DollarSign}
                    metric={commercialIntelligence.potential.metricValue == null ? undefined : formatKpiCompactCurrency(commercialIntelligence.potential.metricValue)}
                  />
                  <IntelligenceDecisionCard title="Recomendação Comercial" card={commercialIntelligence.recommendation} icon={UserRound} />
                  <IntelligenceDecisionCard
                    title="Estabilidade de Consumo"
                    card={commercialIntelligence.stability}
                    icon={CalendarClock}
                    metric={commercialIntelligence.stability.metricValue == null ? undefined : formatVariationPercent(commercialIntelligence.stability.metricValue)}
                  />
                  <ProductsIntelligenceCard intelligence={commercialIntelligence} />
                </div>
              </section>

              <Card className="border-border/80 bg-card/95">
                <CardHeader className="pb-2">
                  <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
                    <CardTitle>Evolução temporal</CardTitle>
                    <div className="flex flex-wrap gap-2">
                      <select className="h-9 rounded-md border border-border bg-background px-2 text-sm" value={timelineGranularity} onChange={(e) => setTimelineGranularity(e.target.value as any)}>
                        <option value="daily">Diário</option>
                        <option value="weekly">Semanal</option>
                        <option value="monthly">Mensal</option>
                      </select>
                      <select className="h-9 rounded-md border border-border bg-background px-2 text-sm" value={timelineMetric} onChange={(e) => setTimelineMetric(e.target.value as any)}>
                        <option value="revenue">Faturamento</option>
                        <option value="quantity">Quantidade</option>
                        <option value="weight">Peso</option>
                        <option value="orders">Pedidos</option>
                      </select>
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="pb-5">
                  {!timeline && <p className="text-sm text-muted-foreground">Dados insuficientes para evolução temporal neste período.</p>}
                  {timeline && timeline.points.length === 0 && <p className="text-sm text-muted-foreground">Sem pontos para o período selecionado.</p>}
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
                </CardContent>
              </Card>

              <Card className="border-border/80 bg-card/95">
                <CardHeader>
                  <div className="flex flex-col gap-1">
                    <CardTitle>Comparativo de períodos</CardTitle>
                    <p className="text-sm text-muted-foreground">Faturamento atual comparado com a base anterior equivalente.</p>
                  </div>
                </CardHeader>
                <CardContent>
                  {comparablePeriods.length === 0 ? (
                    <div className="rounded-lg border border-dashed border-border bg-muted/30 p-5 text-sm text-muted-foreground">
                      Dados insuficientes para comparação
                    </div>
                  ) : (
                    <div className="grid grid-cols-1 gap-3 lg:grid-cols-3">
                      {comparison.map((item) => {
                        const isComparable = canComparePeriod(item);
                        const tone = isComparable ? resolveComparisonTone(item.variationPercent ?? 0) : null;
                        const Icon = tone?.icon ?? Minus;

                        return (
                          <div key={item.label} className="rounded-lg border border-border bg-surface p-4">
                            <div className="flex items-start justify-between gap-3">
                              <div>
                                <p className="text-sm font-semibold text-foreground">{item.label}</p>
                                <p className="mt-1 text-xs text-muted-foreground">
                                  {isComparable ? "Comparação com período anterior" : "Base anterior indisponível"}
                                </p>
                              </div>
                              <span className={`inline-flex items-center gap-1 rounded-full border px-2 py-1 text-xs font-semibold ${tone?.className ?? "border-border bg-muted/50 text-muted-foreground"}`}>
                                <Icon className="h-3.5 w-3.5" />
                                {isComparable ? tone?.label : "Sem dados"}
                              </span>
                            </div>

                            {isComparable ? (
                              <div className="mt-4 space-y-4">
                                <p className={`text-3xl font-display tracking-tight ${tone?.className.includes("danger") ? "text-[var(--danger)]" : tone?.className.includes("success") ? "text-[var(--success)]" : "text-foreground"}`}>
                                  {formatVariationPercent(item.variationPercent)}
                                </p>
                                <div className="grid grid-cols-2 gap-2 text-sm">
                                  <div className="rounded-md bg-muted/40 p-3">
                                    <p className="text-xs text-muted-foreground">Atual</p>
                                    <p className="mt-1 font-semibold">{formatCurrency(item.currentValue)}</p>
                                  </div>
                                  <div className="rounded-md bg-muted/40 p-3">
                                    <p className="text-xs text-muted-foreground">Anterior</p>
                                    <p className="mt-1 font-semibold">{formatCurrency(item.previousValue)}</p>
                                  </div>
                                </div>
                              </div>
                            ) : (
                              <div className="mt-4 rounded-md bg-muted/40 p-3 text-sm text-muted-foreground">
                                Dados insuficientes para comparação
                              </div>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  )}
                </CardContent>
              </Card>

              <Card className="border-border/80 bg-card/95">
                <CardHeader>
                  <CardTitle>Produtos mais comprados</CardTitle>
                </CardHeader>
                <CardContent>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Produto</TableHead>
                        <TableHead>Quantidade</TableHead>
                        <TableHead>Faturamento</TableHead>
                        <TableHead>Participação</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {topProducts.length === 0 && (
                        <TableRow>
                          <TableCell colSpan={4} className="py-6 text-center text-muted-foreground">Sem produtos para o período selecionado.</TableCell>
                        </TableRow>
                      )}
                      {topProducts.map((item) => (
                        <TableRow key={`${item.productCode}-${item.productDescription}`}>
                          <TableCell>{item.productDescription}</TableCell>
                          <TableCell>{formatDecimal(item.quantity)}</TableCell>
                          <TableCell>{formatCurrency(item.revenue)}</TableCell>
                          <TableCell>{item.sharePercent.toFixed(1)}%</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
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
                          <TableCell colSpan={8} className="py-6 text-center text-muted-foreground">Sem compras para o período selecionado.</TableCell>
                        </TableRow>
                      )}
                      {(history?.items ?? []).map((item) => (
                        <TableRow key={`${item.date}-${item.document}-${item.product}`}>
                          <TableCell>{formatDate(item.date)}</TableCell>
                          <TableCell>{item.document}</TableCell>
                          <TableCell>{item.product}</TableCell>
                          <TableCell>{formatDecimal(item.quantity)}</TableCell>
                          <TableCell>{formatCurrency(item.unitPrice)}</TableCell>
                          <TableCell>{formatCurrency(item.total)}</TableCell>
                          <TableCell>{formatDecimal(item.weight)}</TableCell>
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
              <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
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
