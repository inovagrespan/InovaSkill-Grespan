import { useEffect, useMemo, useRef, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { AlertTriangle, CalendarDays, Check, ChevronDown, ChevronLeft, ChevronRight, DollarSign, Loader2, ReceiptText, Scale, Search } from "lucide-react";
import { Area, AreaChart, CartesianGrid, XAxis, YAxis } from "recharts";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { Command, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { Input } from "@/components/ui/input";
import { KpiCard } from "@/components/ui/kpi-card";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import {
  fetchFinanceCustomers,
  fetchFinanceDashboard,
  type FinanceCustomerOption,
  type FinanceCustomerRevenuePoint,
  type FinanceDashboardItem,
  type FinanceDashboardResponse,
  type FinanceRevenueGranularity,
  type FinanceRevenueTrendPoint,
} from "@/lib/importer-api";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";

export const Route = createFileRoute("/financas")({
  component: FinancasPage,
});

const DEFAULT_DATE_FROM = "2026-01-01";
const DEFAULT_DATE_TO = "2026-06-30";
const DEFAULT_PAGE_SIZE = 20;
const CUSTOMER_SEARCH_DEBOUNCE_MS = 300;
const CUSTOMER_SEARCH_LIMIT = 20;
const MIN_CUSTOMER_SEARCH_LENGTH = 2;
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
  const [customerComboboxOpen, setCustomerComboboxOpen] = useState(false);
  const [customerSearch, setCustomerSearch] = useState("");
  const [customerOptions, setCustomerOptions] = useState<FinanceCustomerOption[]>([]);
  const [customerSearchLoading, setCustomerSearchLoading] = useState(false);
  const [customerSearchMessage, setCustomerSearchMessage] = useState("");
  const customerSearchRequestId = useRef(0);
  const customerSearchValue = useRef(customerSearch);
  const debouncedCustomerSearch = useDebouncedValue(customerSearch, CUSTOMER_SEARCH_DEBOUNCE_MS);

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

  useEffect(() => {
    if (!customerComboboxOpen) return;
    setCustomerSearch(customer);
  }, [customer, customerComboboxOpen]);

  useEffect(() => {
    customerSearchValue.current = customerSearch;
  }, [customerSearch]);

  useEffect(() => {
    if (!customerComboboxOpen) return;

    const normalizedSearch = debouncedCustomerSearch.trim();
    if (normalizedSearch.length > 0 && normalizedSearch.length < MIN_CUSTOMER_SEARCH_LENGTH) {
      setCustomerOptions([]);
      setCustomerSearchMessage(`Digite pelo menos ${MIN_CUSTOMER_SEARCH_LENGTH} caracteres para buscar.`);
      setCustomerSearchLoading(false);
      return;
    }

    const requestId = customerSearchRequestId.current + 1;
    customerSearchRequestId.current = requestId;
    const controller = new AbortController();

    async function loadCustomerOptions() {
      setCustomerSearchLoading(true);
      setCustomerSearchMessage("");
      try {
        const result = await fetchFinanceCustomers({
          search: normalizedSearch,
          limit: CUSTOMER_SEARCH_LIMIT,
          signal: controller.signal,
        });
        if (customerSearchRequestId.current !== requestId || customerSearchValue.current.trim() !== normalizedSearch) return;
        setCustomerOptions(result);
        setCustomerSearchMessage(result.length === 0 ? "Nenhum cliente encontrado." : "");
      } catch (error) {
        if (controller.signal.aborted || customerSearchRequestId.current !== requestId) return;
        setCustomerOptions([]);
        setCustomerSearchMessage((error as Error).message);
      } finally {
        if (!controller.signal.aborted && customerSearchRequestId.current === requestId) {
          setCustomerSearchLoading(false);
        }
      }
    }

    void loadCustomerOptions();
    return () => {
      controller.abort();
    };
  }, [customerComboboxOpen, debouncedCustomerSearch]);

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
  const selectedCustomerLabel = customer.trim() || "Todos os clientes";

  function clearFilters() {
    setCustomer("");
    setCustomerSearch("");
    setDateFrom(DEFAULT_DATE_FROM);
    setDateTo(DEFAULT_DATE_TO);
    setAllTime(false);
  }

  function selectCustomer(value: string) {
    setCustomer(value);
    setCustomerSearch(value);
    setCustomerComboboxOpen(false);
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
            <Popover open={customerComboboxOpen} onOpenChange={setCustomerComboboxOpen}>
              <PopoverTrigger asChild>
                <Button
                  id="finance-customer"
                  type="button"
                  variant="outline"
                  role="combobox"
                  aria-expanded={customerComboboxOpen}
                  className="h-11 w-full justify-between rounded-[10px] border-input bg-background px-3 text-left font-normal"
                >
                  <span className="flex min-w-0 items-center gap-2">
                    <Search className="size-4 shrink-0 text-muted-foreground" />
                    <span className={customer ? "truncate" : "truncate text-muted-foreground"}>{selectedCustomerLabel}</span>
                  </span>
                  <ChevronDown className="size-4 shrink-0 text-muted-foreground" />
                </Button>
              </PopoverTrigger>
              <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0" align="start">
                <Command shouldFilter={false}>
                  <CommandInput
                    value={customerSearch}
                    onValueChange={setCustomerSearch}
                    placeholder="Buscar cliente..."
                  />
                  <CommandList>
                    <CommandGroup>
                      <CommandItem value="__all_customers__" onSelect={() => selectCustomer("")}>
                        <Check className={customer ? "size-4 opacity-0" : "size-4 opacity-100"} />
                        Todos os clientes
                      </CommandItem>
                    </CommandGroup>
                    <CommandGroup>
                      {customerSearchLoading ? (
                        <div className="flex items-center gap-2 px-2 py-3 text-sm text-muted-foreground">
                          <Loader2 className="size-4 animate-spin" />
                          Buscando clientes...
                        </div>
                      ) : null}
                      {!customerSearchLoading && customerSearchMessage ? (
                        <div className="px-2 py-3 text-sm text-muted-foreground">{customerSearchMessage}</div>
                      ) : null}
                      {!customerSearchLoading ? customerOptions.map((option) => (
                        <CommandItem key={`${option.id}-${option.nome}`} value={option.nome} onSelect={() => selectCustomer(option.nome)}>
                          <Check className={customer === option.nome ? "size-4 opacity-100" : "size-4 opacity-0"} />
                          <span className="truncate">{option.nome}</span>
                        </CommandItem>
                      )) : null}
                    </CommandGroup>
                  </CommandList>
                </Command>
              </PopoverContent>
            </Popover>
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
