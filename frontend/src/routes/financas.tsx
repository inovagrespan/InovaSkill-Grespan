import { useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { AlertTriangle, CalendarDays, ChevronLeft, ChevronRight, DollarSign, ReceiptText, Scale, Search } from "lucide-react";
import { Area, AreaChart, CartesianGrid, XAxis, YAxis } from "recharts";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { Input } from "@/components/ui/input";
import { KpiCard } from "@/components/ui/kpi-card";
import { Label } from "@/components/ui/label";
import {
  fetchFinanceDashboard,
  type FinanceCustomerRevenuePoint,
  type FinanceDashboardItem,
  type FinanceDashboardResponse,
  type FinanceRevenueGranularity,
  type FinanceRevenueTrendPoint,
} from "@/lib/importer-api";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";

export const Route = createFileRoute("/financas")({
  component: FinancasPage,
});

const DEFAULT_DATE_FROM = "2026-01-01";
const DEFAULT_DATE_TO = "2026-06-30";
const DEFAULT_PAGE_SIZE = 20;
const FINANCE_CHART_HEIGHT_CLASS_NAME = "h-[var(--dashboard-chart-height)] min-h-[var(--dashboard-chart-height)]";
const FINANCE_CHART_CARD_CLASS_NAME = "finance-chart-card overflow-hidden rounded-xl border bg-surface text-foreground shadow-sm dark:bg-[#111821] dark:text-slate-100 dark:shadow-lg";
const FINANCE_CHART_SELECT_CLASS_NAME = "h-8 rounded-[10px] border border-[var(--finance-chart-select-border)] bg-[var(--finance-chart-select-bg)] px-3 text-xs font-medium text-[var(--finance-chart-title)] outline-none transition-colors focus:border-primary/50 focus:ring-2 focus:ring-primary/20";
const FINANCE_CHART_GRID_STROKE = "var(--finance-chart-grid)";
const FINANCE_AXIS_COLOR = "var(--finance-chart-axis)";
const FINANCE_CURSOR_STROKE = "var(--finance-chart-cursor)";
const FINANCE_REVENUE_COLOR = "var(--finance-chart-line)";
const FINANCE_REVENUE_FILL = "var(--finance-chart-fill)";
const FINANCE_REVENUE_FILL_END = "var(--finance-chart-fill-end)";

const revenueGranularityOptions: Array<{ value: FinanceRevenueGranularity; label: string }> = [
  { value: "weekly", label: "Semanal" },
  { value: "monthly", label: "Mensal" },
  { value: "yearly", label: "Anual" },
];

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value);
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 2 }).format(value);
}

function formatDate(value: string): string {
  return new Date(`${value}T00:00:00`).toLocaleDateString("pt-BR");
}

function FinancasPage() {
  const [customer, setCustomer] = useState("");
  const [dateFrom, setDateFrom] = useState(DEFAULT_DATE_FROM);
  const [dateTo, setDateTo] = useState(DEFAULT_DATE_TO);
  const [allTime, setAllTime] = useState(false);
  const [revenueGranularity, setRevenueGranularity] = useState<FinanceRevenueGranularity>("monthly");
  const [page, setPage] = useState(1);
  const [dashboard, setDashboard] = useState<FinanceDashboardResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");

  useEffect(() => {
    setPage(1);
  }, [customer, dateFrom, dateTo, allTime, revenueGranularity]);

  useEffect(() => {
    let cancelled = false;

    async function loadFinanceDashboard() {
      setLoading(true);
      try {
        const result = await fetchFinanceDashboard({
          customer,
          dateFrom,
          dateTo,
          allTime,
          revenueGranularity,
          page,
          pageSize: DEFAULT_PAGE_SIZE,
        });
        if (cancelled) return;
        setDashboard(result);
        setMessage("");
      } catch (error) {
        if (cancelled) return;
        setDashboard(null);
        setMessage((error as Error).message);
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void loadFinanceDashboard();
    return () => {
      cancelled = true;
    };
  }, [allTime, customer, dateFrom, dateTo, page, revenueGranularity]);

  const customers = useMemo(() => dashboard?.customers ?? [], [dashboard?.customers]);
  const metrics = useMemo(
    () => dashboard?.summary ?? { totalRevenue: 0, totalOrders: 0, totalQuantity: 0, averageTicket: 0 },
    [dashboard],
  );
  const revenueTrend = useMemo<FinanceRevenueTrendPoint[]>(() => dashboard?.revenueTrend ?? [], [dashboard]);
  const customerRanking = useMemo<FinanceCustomerRevenuePoint[]>(() => dashboard?.customerRanking ?? [], [dashboard]);
  const financeItems = useMemo<FinanceDashboardItem[]>(() => dashboard?.items ?? [], [dashboard]);
  const totalItems = dashboard?.totalItems ?? 0;
  const totalPages = dashboard?.totalPages ?? 1;
  const currentPage = dashboard?.page ?? page;
  const pageSize = dashboard?.pageSize ?? DEFAULT_PAGE_SIZE;
  const pageStart = totalItems === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const pageEnd = totalItems === 0 ? 0 : Math.min(currentPage * pageSize, totalItems);
  const periodLabel = allTime ? "Tempo total" : `${formatDate(dateFrom)} até ${formatDate(dateTo)}`;

  function clearFilters() {
    setCustomer("");
    setDateFrom(DEFAULT_DATE_FROM);
    setDateTo(DEFAULT_DATE_TO);
    setAllTime(false);
  }

  return (
    <div className="page-shell space-y-6">
      <header className="animate-soft-enter">
        <span className="page-header-kicker">Smart Core / Finanças</span>
        <h1 className="mt-2 text-4xl font-display tracking-tight">Finanças</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Painel financeiro com filtros de cliente, período e métricas consolidadas a partir da base importada.
        </p>
      </header>

      {message && (
        <Alert variant="destructive">
          <AlertTriangle className="size-4" />
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <section className="rounded-lg border border-border bg-surface p-4">
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-[1.2fr_0.9fr_0.9fr_auto_auto] lg:items-end">
          <div className="space-y-1">
            <Label htmlFor="finance-customer">Cliente</Label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="finance-customer"
                list="finance-customer-options"
                className="pl-9"
                value={customer}
                onChange={(event) => setCustomer(event.target.value)}
                placeholder="Todos os clientes"
              />
            </div>
            <datalist id="finance-customer-options">
              {customers.map((item) => <option key={item} value={item} />)}
            </datalist>
          </div>

          <div className="space-y-1">
            <Label htmlFor="finance-date-from">Data inicial</Label>
            <Input id="finance-date-from" type="date" value={dateFrom} disabled={allTime} onChange={(event) => setDateFrom(event.target.value)} />
          </div>

          <div className="space-y-1">
            <Label htmlFor="finance-date-to">Data final</Label>
            <Input id="finance-date-to" type="date" value={dateTo} disabled={allTime} onChange={(event) => setDateTo(event.target.value)} />
          </div>

          <Button type="button" variant={allTime ? "default" : "outline"} onClick={() => setAllTime((value) => !value)}>
            <CalendarDays className="size-4" />
            Tempo total
          </Button>

          <Button type="button" variant="outline" onClick={clearFilters}>
            Limpar
          </Button>
        </div>
      </section>

      <section className="metric-row">
        <KpiCard
          title="Faturamento total"
          value={formatKpiCompactCurrency(metrics.totalRevenue)}
          valueTooltip={formatCurrency(metrics.totalRevenue)}
          showPercentageChange={false}
          periodLabel={periodLabel}
          icon={DollarSign}
          loading={loading}
        />
        <KpiCard
          title="Ticket médio"
          value={formatKpiCompactCurrency(metrics.averageTicket)}
          valueTooltip={formatCurrency(metrics.averageTicket)}
          showPercentageChange={false}
          periodLabel="Faturamento dividido pelos pedidos"
          icon={ReceiptText}
          loading={loading}
        />
        <KpiCard
          title="Peso / quantidade"
          value={formatKpiCompactNumber(metrics.totalQuantity)}
          valueTooltip={`${formatNumber(metrics.totalQuantity)} unidades compradas`}
          showPercentageChange={false}
          periodLabel="Quantidade comprada pelo cliente"
          icon={Scale}
          loading={loading}
        />
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card className={FINANCE_CHART_CARD_CLASS_NAME}>
          <CardHeader className="pb-3">
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle className="text-base text-[var(--finance-chart-title)]">Evolução da Receita</CardTitle>
                <p className="mt-1 text-xs text-[var(--finance-chart-muted)]">{periodLabel}</p>
              </div>
              <select
                className={FINANCE_CHART_SELECT_CLASS_NAME}
                value={revenueGranularity}
                onChange={(event) => setRevenueGranularity(event.target.value as FinanceRevenueGranularity)}
                aria-label="Granularidade do faturamento em finanças"
              >
                {revenueGranularityOptions.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </div>
          </CardHeader>
          <CardContent className="pt-0">
            {loading ? (
              <div className={`${FINANCE_CHART_HEIGHT_CLASS_NAME} flex items-center justify-center text-sm text-muted-foreground`}>
                Carregando receita...
              </div>
            ) : (
              <ChartContainer
                config={{ revenue: { label: "Faturamento", color: FINANCE_REVENUE_COLOR } }}
                className={`${FINANCE_CHART_HEIGHT_CLASS_NAME} w-full [&_.recharts-cartesian-axis-tick_text]:fill-[var(--finance-chart-axis)]`}
              >
                <AreaChart data={revenueTrend} margin={{ left: 0, right: 8, top: 12, bottom: 4 }}>
                  <defs>
                    <linearGradient id="finance-revenue-fill" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor={FINANCE_REVENUE_FILL} stopOpacity={1} />
                      <stop offset="100%" stopColor={FINANCE_REVENUE_FILL_END} stopOpacity={1} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid vertical={false} stroke={FINANCE_CHART_GRID_STROKE} />
                  <XAxis dataKey="label" tickLine={false} axisLine={false} tickMargin={10} minTickGap={18} stroke={FINANCE_AXIS_COLOR} />
                  <YAxis
                    width={64}
                    tickLine={false}
                    axisLine={false}
                    tickMargin={8}
                    tickFormatter={(value) => formatKpiCompactCurrency(Number(value))}
                    stroke={FINANCE_AXIS_COLOR}
                  />
                  <ChartTooltip
                    cursor={{ stroke: FINANCE_CURSOR_STROKE, strokeWidth: 2 }}
                    content={<ChartTooltipContent formatter={(value) => formatCurrency(Number(value))} />}
                  />
                  <Area
                    type="monotone"
                    dataKey="revenue"
                    name="Faturamento"
                    stroke={FINANCE_REVENUE_COLOR}
                    strokeWidth={2.8}
                    fill="url(#finance-revenue-fill)"
                    dot={{ r: 3, fill: FINANCE_REVENUE_COLOR, strokeWidth: 0 }}
                    activeDot={{ r: 5, fill: FINANCE_REVENUE_COLOR }}
                    isAnimationActive={false}
                  />
                </AreaChart>
              </ChartContainer>
            )}
          </CardContent>
        </Card>

        <Card className={FINANCE_CHART_CARD_CLASS_NAME}>
          <CardHeader className="pb-3">
            <CardTitle className="text-base text-[var(--finance-chart-title)]">Ranking por empresa</CardTitle>
          </CardHeader>
          <CardContent className="pt-0">
            {loading ? (
              <div className={`${FINANCE_CHART_HEIGHT_CLASS_NAME} flex items-center justify-center text-sm text-muted-foreground`}>
                Carregando ranking...
              </div>
            ) : (
              <ChartContainer
                config={{ revenue: { label: "Faturamento", color: FINANCE_REVENUE_COLOR } }}
                className={`${FINANCE_CHART_HEIGHT_CLASS_NAME} w-full [&_.recharts-cartesian-axis-tick_text]:fill-[var(--finance-chart-axis)]`}
              >
                <AreaChart data={customerRanking} margin={{ left: 0, right: 8, top: 12, bottom: 4 }}>
                  <defs>
                    <linearGradient id="finance-ranking-fill" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor={FINANCE_REVENUE_FILL} stopOpacity={1} />
                      <stop offset="100%" stopColor={FINANCE_REVENUE_FILL_END} stopOpacity={1} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid vertical={false} stroke={FINANCE_CHART_GRID_STROKE} />
                  <XAxis
                    dataKey="customer"
                    tickLine={false}
                    axisLine={false}
                    tickMargin={10}
                    interval={0}
                    minTickGap={10}
                    tickFormatter={(value) => String(value).split(" ")[0] ?? String(value)}
                    stroke={FINANCE_AXIS_COLOR}
                  />
                  <YAxis
                    width={64}
                    tickLine={false}
                    axisLine={false}
                    tickMargin={8}
                    tickFormatter={(value) => formatKpiCompactCurrency(Number(value))}
                    stroke={FINANCE_AXIS_COLOR}
                  />
                  <ChartTooltip
                    cursor={{ stroke: FINANCE_CURSOR_STROKE, strokeWidth: 2 }}
                    content={<ChartTooltipContent formatter={(value) => formatCurrency(Number(value))} />}
                  />
                  <Area
                    type="monotone"
                    dataKey="revenue"
                    name="Faturamento"
                    stroke={FINANCE_REVENUE_COLOR}
                    strokeWidth={2.8}
                    fill="url(#finance-ranking-fill)"
                    dot={{ r: 3, fill: FINANCE_REVENUE_COLOR, strokeWidth: 0 }}
                    activeDot={{ r: 5, fill: FINANCE_REVENUE_COLOR }}
                    isAnimationActive={false}
                  />
                </AreaChart>
              </ChartContainer>
            )}
          </CardContent>
        </Card>
      </section>

      <section className="rounded-xl border border-border bg-surface p-5">
        <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h2 className="font-display text-xl">Base financeira filtrada</h2>
            <p className="text-sm text-muted-foreground">
              {totalItems === 0
                ? "Nenhum lançamento financeiro encontrado."
                : `Exibindo ${pageStart}-${pageEnd} de ${totalItems} lançamento(s) financeiro(s).`}
            </p>
          </div>
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <span>Página {currentPage} de {totalPages}</span>
            <Button type="button" variant="outline" size="sm" onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={loading || currentPage <= 1}>
              <ChevronLeft className="size-4" />
              Anterior
            </Button>
            <Button type="button" variant="outline" size="sm" onClick={() => setPage((value) => Math.min(totalPages, value + 1))} disabled={loading || currentPage >= totalPages}>
              Próxima
              <ChevronRight className="size-4" />
            </Button>
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-sm">
            <thead>
              <tr className="border-b border-border text-left text-xs uppercase tracking-wide text-muted-foreground">
                <th className="py-3 pr-4 font-medium">Cliente</th>
                <th className="py-3 pr-4 font-medium">Data</th>
                <th className="py-3 pr-4 font-medium">Faturamento</th>
                <th className="py-3 pr-4 font-medium">Pedidos</th>
                <th className="py-3 font-medium">Peso / quantidade</th>
              </tr>
            </thead>
            <tbody>
              {financeItems.map((item) => (
                <tr key={`${item.customer}-${item.date}-${item.revenue}`} className="border-b border-border/70 last:border-0">
                  <td className="py-3 pr-4 font-medium">{item.customer}</td>
                  <td className="py-3 pr-4">{formatDate(item.date)}</td>
                  <td className="py-3 pr-4">{formatCurrency(item.revenue)}</td>
                  <td className="py-3 pr-4">{formatNumber(item.orders)}</td>
                  <td className="py-3">{formatNumber(item.quantity)}</td>
                </tr>
              ))}
              {!loading && financeItems.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-8 text-center text-muted-foreground">Nenhuma métrica encontrada para os filtros atuais.</td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
