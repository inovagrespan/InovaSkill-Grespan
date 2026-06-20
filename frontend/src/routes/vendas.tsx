import { useEffect, useMemo, useRef, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { KpiCard } from "@/components/ui/kpi-card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SkeletonMetricCard, SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  buildSalesRankingData,
  buildSalesTrendData,
  describeSalesRankingMetric,
  describeSalesTimelineGranularity,
  describeSalesTrendMetric,
  type SalesRankingMetric,
  type SalesTrendMetric,
} from "@/lib/sales-dashboard";
import {
  type CommercialInvoiceAnalyticsGranularity,
  type CommercialInvoiceAnalyticsResponse,
  type CommercialInvoiceDetails,
  type CommercialInvoiceSummaryResponse,
  type CommercialTransaction,
  fetchCommercialInvoiceAnalytics,
  fetchCommercialInvoiceDetails,
  fetchCommercialInvoices,
  fetchCommercialTransactions,
} from "@/lib/importer-api";
import { resolveSalesTimelineGranularity } from "@/lib/sales-timeline";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";
import {
  ChevronDown,
  PackageSearch,
  ReceiptText,
  Search,
  SlidersHorizontal,
  Users,
  Weight,
  type LucideIcon,
} from "lucide-react";
import { Area, AreaChart, Bar, BarChart, CartesianGrid, XAxis, YAxis } from "recharts";

export const Route = createFileRoute("/vendas")({
  component: VendasPage,
});

type PeriodPreset = "today" | "week" | "month" | "quarter" | "year" | "custom";
type ViewMode = "invoices" | "items";
type SalesCachedData = {
  items: Awaited<ReturnType<typeof fetchCommercialTransactions>>;
  invoices: CommercialInvoiceSummaryResponse;
  analytics: CommercialInvoiceAnalyticsResponse;
  storedAt: number;
};
type PersistedSalesState = {
  periodPreset: PeriodPreset;
  dateFrom: string;
  dateTo: string;
  customerName: string;
  productCodeInput: string;
  documentNumberInput: string;
  city: string;
  companyName: string;
  productGroup: string;
  transactionType: string;
  advancedOpen: boolean;
  viewMode: ViewMode;
  trendMetric: SalesTrendMetric;
  rankingMetric: SalesRankingMetric;
  invoicePage: number;
  page: number;
};

const FILTER_DEBOUNCE_MS = 300;
const SEARCH_MIN_LENGTH = 2;
const DEFAULT_PAGE_SIZE = 20;
const DEFAULT_SUMMARY_PAGE_SIZE = 20;
const SALES_STATE_STORAGE_KEY = "inovaskill:vendas:state:v2";
const SALES_CACHE_TTL_MS = 60_000;
const SALES_CHART_HEIGHT_CLASS_NAME = "h-[var(--dashboard-chart-height)] min-h-[var(--dashboard-chart-height)]";
const SALES_CHART_CARD_CLASS_NAME = "sales-chart-card overflow-hidden border-border/80 bg-card/95 shadow-sm hover:translate-y-0 hover:border-border/80 hover:shadow-sm";
const SALES_CHART_SELECT_CLASS_NAME = "h-9 rounded-[var(--dashboard-control-radius)] border border-[var(--sales-chart-select-border)] bg-[var(--sales-chart-select-bg)] px-3 text-sm text-[var(--sales-chart-title)] shadow-xs outline-none transition-colors focus:border-primary/40 focus:ring-2 focus:ring-ring/40";
const SALES_ANALYTICS_PANEL_CLASS_NAME = "rounded-[var(--dashboard-panel-radius)] border border-border/70 bg-[var(--surface-soft)]/70 p-4";
const SALES_CHART_EMPTY_STATE_CLASS_NAME = "flex h-[var(--dashboard-chart-height)] items-center justify-center rounded-[var(--dashboard-panel-radius)] border border-dashed border-border bg-muted/30 p-6 text-center text-sm text-muted-foreground";
const SALES_GRID_STROKE = "var(--sales-chart-grid)";
const SALES_AXIS_COLOR = "var(--sales-chart-axis)";
const SALES_CURSOR_STROKE = "var(--sales-chart-cursor)";
const SALES_REVENUE_COLOR = "var(--sales-chart-line)";
const SALES_REVENUE_FILL = "var(--sales-chart-fill)";
const SALES_REVENUE_FILL_END = "var(--sales-chart-fill-end)";
const SALES_RANKING_COLOR = "rgba(180,35,47,0.82)";
const SALES_RANKING_COLOR_SOFT = "rgba(180,35,47,0.58)";

const periodOptions: Array<{ value: PeriodPreset; label: string }> = [
  { value: "today", label: "Hoje" },
  { value: "week", label: "Semana" },
  { value: "month", label: "Mês" },
  { value: "quarter", label: "Trimestre" },
  { value: "year", label: "Ano" },
  { value: "custom", label: "Personalizado" },
];

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value ?? 0);
}

function formatDecimal(value: number): string {
  return new Intl.NumberFormat("pt-BR", { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(value ?? 0);
}

function formatDate(value: string): string {
  if (!value) return "-";
  return new Date(`${value}T00:00:00`).toLocaleDateString("pt-BR");
}

function formatRevenueAxisTick(value: number): string {
  const numericValue = Number(value);

  if (numericValue === 0) {
    return "0";
  }

  if (Math.abs(numericValue) >= 1_000_000_000) {
    return `${new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 }).format(numericValue / 1_000_000_000)}B`;
  }

  if (Math.abs(numericValue) >= 1_000_000) {
    return `${new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 }).format(numericValue / 1_000_000)}M`;
  }

  if (Math.abs(numericValue) >= 1_000) {
    return `${new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 0 }).format(numericValue / 1_000)}k`;
  }

  return new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 0 }).format(numericValue);
}

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

function resolveLastThreeMonthsPeriod(): { dateFrom: string; dateTo: string } {
  const now = new Date();
  return {
    dateFrom: toInputDate(new Date(now.getFullYear(), now.getMonth() - 2, 1)),
    dateTo: toInputDate(now),
  };
}

function makeEmptyAnalytics(granularity: CommercialInvoiceAnalyticsGranularity): CommercialInvoiceAnalyticsResponse {
  return {
    granularity,
    summary: {
      totalInvoices: 0,
      totalAmount: 0,
      totalWeightKg: 0,
      totalCustomers: 0,
      totalItems: 0,
      totalQuantity: 0,
    },
    trend: [],
    ranking: [],
  };
}

export function normalizeSalesSearchFilter(value: string): string {
  const trimmed = value.trim();
  return trimmed.length >= SEARCH_MIN_LENGTH ? trimmed : "";
}

export function buildSalesTimelineSubtitle(granularity: CommercialInvoiceAnalyticsGranularity, metric: SalesTrendMetric): string {
  return `${describeSalesTrendMetric(metric)} por ${describeSalesTimelineGranularity(granularity)}.`;
}

function buildSalesRankingSubtitle(metric: SalesRankingMetric): string {
  return `${describeSalesRankingMetric(metric)} nos filtros atuais.`;
}

function resolveInvoiceAnalyticsGranularity(input: {
  periodPreset: PeriodPreset;
  dateFrom: string;
  dateTo: string;
}): CommercialInvoiceAnalyticsGranularity {
  const timelineGranularity = resolveSalesTimelineGranularity(input);
  switch (timelineGranularity) {
    case "hour":
    case "day":
      return "day";
    case "week":
      return "week";
    default:
      return "month";
  }
}

function readPersistedSalesState(defaultPeriod: { dateFrom: string; dateTo: string }): PersistedSalesState {
  if (typeof window === "undefined") {
    return buildDefaultSalesState(defaultPeriod);
  }

  try {
    const raw = window.sessionStorage.getItem(SALES_STATE_STORAGE_KEY);
    if (!raw) return buildDefaultSalesState(defaultPeriod);
    const parsed = JSON.parse(raw) as Partial<PersistedSalesState> & { summaryPage?: number; viewMode?: "summary" | ViewMode };
    return {
      ...buildDefaultSalesState(defaultPeriod),
      ...parsed,
      viewMode: parsed.viewMode === "summary" ? "invoices" : (parsed.viewMode ?? "invoices"),
      trendMetric: parsed.trendMetric ?? "invoiceCount",
      rankingMetric: parsed.rankingMetric ?? "amount",
      invoicePage: parsed.invoicePage ?? parsed.summaryPage ?? 1,
    };
  } catch {
    return buildDefaultSalesState(defaultPeriod);
  }
}

function buildDefaultSalesState(defaultPeriod: { dateFrom: string; dateTo: string }): PersistedSalesState {
  return {
    periodPreset: "quarter",
    dateFrom: defaultPeriod.dateFrom,
    dateTo: defaultPeriod.dateTo,
    customerName: "",
    productCodeInput: "",
    documentNumberInput: "",
    city: "",
    companyName: "",
    productGroup: "",
    transactionType: "",
    advancedOpen: false,
    viewMode: "invoices",
    trendMetric: "invoiceCount",
    rankingMetric: "amount",
    invoicePage: 1,
    page: 1,
  };
}

function buildFriendlyError(error: unknown): string {
  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return "Não foi possível carregar os dados de vendas.";
}

function VendasPage() {
  const defaultPeriod = resolveLastThreeMonthsPeriod();
  const persistedState = useMemo(() => readPersistedSalesState(defaultPeriod), [defaultPeriod.dateFrom, defaultPeriod.dateTo]);
  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>(persistedState.periodPreset);
  const [dateFrom, setDateFrom] = useState(persistedState.dateFrom);
  const [dateTo, setDateTo] = useState(persistedState.dateTo);
  const [customerName, setCustomerName] = useState(persistedState.customerName);
  const [productCodeInput, setProductCodeInput] = useState(persistedState.productCodeInput);
  const [documentNumberInput, setDocumentNumberInput] = useState(persistedState.documentNumberInput);
  const [city, setCity] = useState(persistedState.city);
  const [companyName, setCompanyName] = useState(persistedState.companyName);
  const [productGroup, setProductGroup] = useState(persistedState.productGroup);
  const [transactionType, setTransactionType] = useState(persistedState.transactionType);
  const [advancedOpen, setAdvancedOpen] = useState(persistedState.advancedOpen);
  const [viewMode, setViewMode] = useState<ViewMode>(persistedState.viewMode);
  const [trendMetric, setTrendMetric] = useState<SalesTrendMetric>(persistedState.trendMetric);
  const [rankingMetric, setRankingMetric] = useState<SalesRankingMetric>(persistedState.rankingMetric);
  const [invoicePage, setInvoicePage] = useState(persistedState.invoicePage);
  const [page, setPage] = useState(persistedState.page);
  const [items, setItems] = useState<CommercialTransaction[]>([]);
  const [total, setTotal] = useState(0);
  const [invoices, setInvoices] = useState<CommercialInvoiceSummaryResponse | null>(null);
  const [analytics, setAnalytics] = useState<CommercialInvoiceAnalyticsResponse | null>(null);
  const [selectedInvoiceNumber, setSelectedInvoiceNumber] = useState("");
  const [invoiceDetailsOpen, setInvoiceDetailsOpen] = useState(false);
  const [invoiceDetailsByNumber, setInvoiceDetailsByNumber] = useState<Record<string, CommercialInvoiceDetails>>({});
  const [invoiceDetailsLoadingNumber, setInvoiceDetailsLoadingNumber] = useState("");
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const requestIdRef = useRef(0);
  const cacheRef = useRef(new Map<string, SalesCachedData>());

  const debouncedDocumentNumber = useDebouncedValue(documentNumberInput, FILTER_DEBOUNCE_MS);
  const debouncedProductCode = useDebouncedValue(productCodeInput, FILTER_DEBOUNCE_MS);
  const documentNumber = normalizeSalesSearchFilter(debouncedDocumentNumber);
  const productCode = normalizeSalesSearchFilter(debouncedProductCode);

  const pageSize = DEFAULT_PAGE_SIZE;
  const summaryPageSize = DEFAULT_SUMMARY_PAGE_SIZE;
  const effectiveCustomerName = customerName.trim() || companyName.trim();
  const hasAdvancedFilters = Boolean(customerName || city || companyName || productGroup || transactionType || periodPreset === "custom");
  const totalPages = useMemo(() => Math.max(1, Math.ceil(total / pageSize)), [total, pageSize]);
  const invoiceTotalPages = useMemo(() => Math.max(1, Math.ceil((invoices?.totalItems ?? 0) / summaryPageSize)), [invoices?.totalItems, summaryPageSize]);
  const analyticsGranularity = useMemo(
    () => resolveInvoiceAnalyticsGranularity({ periodPreset, dateFrom, dateTo }),
    [dateFrom, dateTo, periodPreset],
  );
  const hasResults = (analytics?.summary.totalInvoices ?? 0) > 0;
  const trendData = useMemo(() => buildSalesTrendData(analytics, trendMetric), [analytics, trendMetric]);
  const chartData = useMemo(() => buildSalesRankingData(analytics, rankingMetric), [analytics, rankingMetric]);

  async function loadInvoiceDetails(documentNumberValue: string) {
    setInvoiceDetailsLoadingNumber(documentNumberValue);
    try {
      const details = await fetchCommercialInvoiceDetails(documentNumberValue);
      setInvoiceDetailsByNumber((current) => ({ ...current, [documentNumberValue]: details }));
    } catch (error) {
      setMessage(buildFriendlyError(error));
    } finally {
      setInvoiceDetailsLoadingNumber((current) => (current === documentNumberValue ? "" : current));
    }
  }

  async function openInvoiceDetails(documentNumberValue: string) {
    setSelectedInvoiceNumber(documentNumberValue);
    setInvoiceDetailsOpen(true);
    if (invoiceDetailsByNumber[documentNumberValue]) {
      return;
    }

    await loadInvoiceDetails(documentNumberValue);
  }

  async function loadData(targetPage = page, targetInvoicePage = invoicePage, signal?: AbortSignal) {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setMessage("");

    const queryKey = JSON.stringify({
      targetPage,
      targetInvoicePage,
      pageSize,
      summaryPageSize,
      documentNumber,
      customerName: effectiveCustomerName,
      productCode,
      city,
      productGroup,
      transactionType,
      dateFrom,
      dateTo,
      analyticsGranularity,
    });
    const cached = cacheRef.current.get(queryKey);
    if (cached && Date.now() - cached.storedAt < SALES_CACHE_TTL_MS) {
      setItems(cached.items.items);
      setPage(cached.items.page);
      setTotal(cached.items.total);
      setInvoices(cached.invoices);
      setInvoicePage(cached.invoices.page);
      setAnalytics(cached.analytics);
      setLoading(false);
      return;
    }

    try {
      const [itemsData, invoicesData, analyticsData] = await Promise.all([
        fetchCommercialTransactions({
          page: targetPage,
          pageSize,
          documentNumber,
          customerName: effectiveCustomerName,
          productCode,
          city,
          productGroup,
          transactionType,
          dateFrom,
          dateTo,
          signal,
        }),
        fetchCommercialInvoices({
          page: targetInvoicePage,
          pageSize: summaryPageSize,
          documentNumber,
          customerName: effectiveCustomerName,
          productCode,
          city,
          productGroup,
          transactionType,
          dateFrom,
          dateTo,
          signal,
        }),
        fetchCommercialInvoiceAnalytics({
          granularity: analyticsGranularity,
          documentNumber,
          customerName: effectiveCustomerName,
          productCode,
          city,
          productGroup,
          transactionType,
          dateFrom,
          dateTo,
          signal,
        }),
      ]);

      if (requestId !== requestIdRef.current || signal?.aborted) {
        return;
      }

      cacheRef.current.set(queryKey, {
        items: itemsData,
        invoices: invoicesData,
        analytics: analyticsData,
        storedAt: Date.now(),
      });
      setItems(itemsData.items);
      setPage(itemsData.page);
      setTotal(itemsData.total);
      setInvoices(invoicesData);
      setInvoicePage(invoicesData.page);
      setAnalytics(analyticsData);
    } catch (error) {
      if ((error instanceof DOMException && error.name === "AbortError") || signal?.aborted || requestId !== requestIdRef.current) {
        return;
      }

      setItems([]);
      setPage(targetPage);
      setTotal(0);
      setInvoices({ page: targetInvoicePage, pageSize: summaryPageSize, totalItems: 0, totalAmount: 0, totalQuantity: 0, totalWeightKg: 0, items: [] });
      setInvoicePage(targetInvoicePage);
      setAnalytics(makeEmptyAnalytics(analyticsGranularity));
      setMessage(buildFriendlyError(error));
    } finally {
      if (requestId === requestIdRef.current && !signal?.aborted) {
        setLoading(false);
      }
    }
  }

  function applyPeriod(preset: PeriodPreset) {
    setPeriodPreset(preset);
    if (preset === "custom") {
      setAdvancedOpen(true);
      return;
    }

    const next = resolvePeriod(preset);
    setDateFrom(next.dateFrom);
    setDateTo(next.dateTo);
  }

  function clearFilters() {
    const next = resolveLastThreeMonthsPeriod();
    setPeriodPreset("quarter");
    setDateFrom(next.dateFrom);
    setDateTo(next.dateTo);
    setCustomerName("");
    setProductCodeInput("");
    setDocumentNumberInput("");
    setCity("");
    setCompanyName("");
    setProductGroup("");
    setTransactionType("");
    setInvoicePage(1);
    setPage(1);
    setSelectedInvoiceNumber("");
    setInvoiceDetailsOpen(false);
  }

  useEffect(() => {
    setInvoicePage(1);
    setPage(1);
    setSelectedInvoiceNumber("");
    setInvoiceDetailsOpen(false);
  }, [dateFrom, dateTo, customerName, productCode, documentNumber, city, companyName, productGroup, transactionType]);

  useEffect(() => {
    const controller = new AbortController();
    void loadData(1, 1, controller.signal);
    return () => controller.abort();
  }, [dateFrom, dateTo, customerName, productCode, documentNumber, city, companyName, productGroup, transactionType, analyticsGranularity]);

  useEffect(() => {
    const controller = new AbortController();
    void loadData(page, invoicePage, controller.signal);
    return () => controller.abort();
  }, [page, invoicePage]);

  useEffect(() => {
    if (typeof window === "undefined") return;

    const state: PersistedSalesState = {
      periodPreset,
      dateFrom,
      dateTo,
      customerName,
      productCodeInput,
      documentNumberInput,
      city,
      companyName,
      productGroup,
      transactionType,
      advancedOpen,
      viewMode,
      trendMetric,
      rankingMetric,
      invoicePage,
      page,
    };
    window.sessionStorage.setItem(SALES_STATE_STORAGE_KEY, JSON.stringify(state));
  }, [periodPreset, dateFrom, dateTo, customerName, productCodeInput, documentNumberInput, city, companyName, productGroup, transactionType, advancedOpen, viewMode, trendMetric, rankingMetric, invoicePage, page]);

  return (
    <div className="page-shell space-y-8">
      <header className="animate-soft-enter space-y-5">
        <div className="max-w-3xl">
          <span className="page-header-kicker">Smart Core / Vendas</span>
          <h1 className="mt-2 text-3xl font-display tracking-tight text-foreground md:text-4xl">Vendas</h1>
          <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
            Dashboard analítico para acompanhar emissão, composição e impacto das notas fiscais filtradas.
          </p>
        </div>

        <section className="space-y-4 rounded-xl border border-border/80 bg-surface/95 p-4 shadow-xs">
          <div className="flex flex-wrap gap-2">
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
          </div>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_1fr_auto] lg:items-end">
            <div className="space-y-1.5">
              <Label htmlFor="sales-document">Nota fiscal</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input id="sales-document" className="pl-9 pr-16" value={documentNumberInput} onChange={(event) => setDocumentNumberInput(event.target.value)} placeholder="Buscar por nota fiscal" />
                {documentNumberInput && (
                  <Button type="button" variant="ghost" size="sm" className="absolute right-1 top-1/2 h-8 -translate-y-1/2 px-2 text-xs" onClick={() => setDocumentNumberInput("")}>
                    Limpar
                  </Button>
                )}
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="sales-product">Produto</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input id="sales-product" className="pl-9 pr-16" value={productCodeInput} onChange={(event) => setProductCodeInput(event.target.value)} placeholder="Buscar código ou descrição" />
                {productCodeInput && (
                  <Button type="button" variant="ghost" size="sm" className="absolute right-1 top-1/2 h-8 -translate-y-1/2 px-2 text-xs" onClick={() => setProductCodeInput("")}>
                    Limpar
                  </Button>
                )}
              </div>
            </div>

            <Button type="button" variant="outline" className="h-10" onClick={() => setAdvancedOpen((value) => !value)}>
              <SlidersHorizontal className="mr-2 size-4" />
              Filtros avançados
              {hasAdvancedFilters && <Badge className="ml-2" variant="secondary">ativo</Badge>}
              <ChevronDown className={`ml-2 size-4 transition-transform ${advancedOpen ? "rotate-180" : ""}`} />
            </Button>
          </div>

          {advancedOpen && (
            <div className="grid grid-cols-1 gap-4 border-t border-border pt-4 md:grid-cols-2 xl:grid-cols-4">
              <Field label="Cliente" value={customerName} onChange={setCustomerName} placeholder="Nome do cliente" />
              <Field label="Cidade" value={city} onChange={setCity} placeholder="Cidade" />
              <Field label="Empresa" value={companyName} onChange={setCompanyName} placeholder="Nome da empresa" />
              <Field label="Grupo" value={productGroup} onChange={setProductGroup} placeholder="Grupo do produto" />
              <Field label="Operação" value={transactionType} onChange={setTransactionType} placeholder="Venda, devolução..." />
              <DateField label="Data inicial" value={dateFrom} onChange={(value) => { setPeriodPreset("custom"); setDateFrom(value); }} />
              <DateField label="Data final" value={dateTo} onChange={(value) => { setPeriodPreset("custom"); setDateTo(value); }} />
              <div className="flex items-end">
                <Button type="button" variant="outline" className="w-full" onClick={clearFilters}>
                  Limpar filtros
                </Button>
              </div>
            </div>
          )}
        </section>
      </header>

      {message && (
        <Alert variant="destructive" className="animate-soft-enter">
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <section className="metric-row">
        <MetricCard
          loading={loading}
          error={Boolean(message)}
          empty={!hasResults}
          title="Notas fiscais"
          value={formatKpiCompactNumber(analytics?.summary.totalInvoices ?? 0)}
          tooltip={String(analytics?.summary.totalInvoices ?? 0)}
          periodLabel="Total de notas fiscais encontradas nos filtros"
          icon={ReceiptText}
        />
        <MetricCard
          loading={loading}
          error={Boolean(message)}
          empty={!hasResults}
          title="Valor das notas"
          value={formatKpiCompactCurrency(analytics?.summary.totalAmount ?? 0)}
          tooltip={formatCurrency(analytics?.summary.totalAmount ?? 0)}
          periodLabel="Valor total acumulado nas notas fiscais filtradas"
          icon={Users}
        />
        <MetricCard
          loading={loading}
          error={Boolean(message)}
          empty={!hasResults}
          title="Peso movimentado"
          value={formatKpiCompactNumber(analytics?.summary.totalWeightKg ?? 0)}
          tooltip={`${formatDecimal(analytics?.summary.totalWeightKg ?? 0)} kg`}
          periodLabel="Peso total movimentado nas notas filtradas"
          icon={PackageSearch}
        />
        <MetricCard
          loading={loading}
          error={Boolean(message)}
          empty={!hasResults}
          title="Clientes impactados"
          value={formatKpiCompactNumber(analytics?.summary.totalCustomers ?? 0)}
          tooltip={String(analytics?.summary.totalCustomers ?? 0)}
          periodLabel="Clientes com participação nas notas do período"
          icon={Weight}
        />
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card className={SALES_CHART_CARD_CLASS_NAME}>
          <CardHeader className="pb-3">
            <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
              <div>
                <CardTitle className="text-base text-[var(--sales-chart-title)]">Evolução das notas fiscais</CardTitle>
                <p className="mt-1 text-xs text-[var(--sales-chart-muted)]">{buildSalesTimelineSubtitle(analyticsGranularity, trendMetric)}</p>
              </div>
              <select className={SALES_CHART_SELECT_CLASS_NAME} value={trendMetric} onChange={(event) => setTrendMetric(event.target.value as SalesTrendMetric)}>
                <option value="invoiceCount">Quantidade de notas</option>
                <option value="totalAmount">Valor total das notas</option>
                <option value="totalWeightKg">Peso total movimentado</option>
              </select>
            </div>
          </CardHeader>
          <CardContent className="pt-0">
            {loading ? (
              <SkeletonTable rows={4} columns={2} />
            ) : !hasResults || trendData.length === 0 ? (
              <EmptyState text="Sem resultado para gerar a evolução das notas fiscais." />
            ) : (
              <div className={SALES_ANALYTICS_PANEL_CLASS_NAME}>
                <ChartContainer
                  config={{ value: { label: describeSalesTrendMetric(trendMetric), color: SALES_REVENUE_COLOR } }}
                  className={`${SALES_CHART_HEIGHT_CLASS_NAME} w-full [&_.recharts-cartesian-axis-tick_text]:fill-[var(--sales-chart-axis)]`}
                >
                  <AreaChart data={trendData} margin={{ left: 0, right: 8, top: 12, bottom: 4 }}>
                    <defs>
                      <linearGradient id="sales-revenue-fill" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor={SALES_REVENUE_FILL} stopOpacity={1} />
                        <stop offset="100%" stopColor={SALES_REVENUE_FILL_END} stopOpacity={1} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid vertical={false} stroke={SALES_GRID_STROKE} />
                    <XAxis
                      dataKey="label"
                      tickLine={false}
                      axisLine={false}
                      tickMargin={10}
                      tick={{ fill: SALES_AXIS_COLOR, fontSize: 11 }}
                    />
                    <YAxis
                      width={58}
                      tickCount={4}
                      tickLine={false}
                      axisLine={false}
                      tickMargin={10}
                      tick={{ fill: SALES_AXIS_COLOR, fontSize: 11 }}
                      tickFormatter={formatRevenueAxisTick}
                    />
                    <ChartTooltip
                      cursor={{ stroke: SALES_CURSOR_STROKE, strokeWidth: 2 }}
                      content={<SalesRevenueTooltip metric={trendMetric} />}
                    />
                    <Area
                      dataKey="value"
                      name={describeSalesTrendMetric(trendMetric)}
                      type="monotone"
                      fill="url(#sales-revenue-fill)"
                      stroke={SALES_REVENUE_COLOR}
                      strokeWidth={2.8}
                      dot={{ r: 3, fill: SALES_REVENUE_COLOR, strokeWidth: 0 }}
                      activeDot={{ r: 5, fill: SALES_REVENUE_COLOR }}
                      isAnimationActive={false}
                    />
                  </AreaChart>
                </ChartContainer>
              </div>
            )}
          </CardContent>
        </Card>

        <Card className={SALES_CHART_CARD_CLASS_NAME}>
          <CardHeader className="pb-3">
            <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
              <div>
                <CardTitle className="text-base text-foreground">Ranking por empresa</CardTitle>
                <p className="mt-1 text-xs text-muted-foreground">{buildSalesRankingSubtitle(rankingMetric)}</p>
              </div>
              <div className="flex flex-wrap gap-2">
                <select className={SALES_CHART_SELECT_CLASS_NAME} value={rankingMetric} onChange={(event) => setRankingMetric(event.target.value as SalesRankingMetric)}>
                  <option value="amount">Maior faturamento</option>
                  <option value="invoiceCount">Maior quantidade de notas</option>
                  <option value="items">Maior quantidade de itens</option>
                  <option value="weight">Maior peso movimentado</option>
                </select>
              </div>
            </div>
          </CardHeader>
          <CardContent className="pt-0">
            {loading ? (
              <SkeletonTable rows={4} columns={3} />
            ) : !hasResults ? (
              <EmptyState text="Sem empresas para o ranking atual." />
            ) : (
              <div className={SALES_ANALYTICS_PANEL_CLASS_NAME}>
                <ChartContainer config={{ valor: { label: describeSalesRankingMetric(rankingMetric), color: SALES_RANKING_COLOR } }} className={`${SALES_CHART_HEIGHT_CLASS_NAME} w-full`}>
                  <BarChart data={chartData} margin={{ left: 0, right: 8, top: 12, bottom: 18 }}>
                    <defs>
                      <linearGradient id="sales-ranking-bar" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor={SALES_RANKING_COLOR} stopOpacity={0.92} />
                        <stop offset="100%" stopColor={SALES_RANKING_COLOR_SOFT} stopOpacity={0.72} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid vertical={false} stroke={SALES_GRID_STROKE} strokeDasharray="3 6" />
                    <XAxis
                      dataKey="companyName"
                      interval={0}
                      tick={{ fill: SALES_AXIS_COLOR, fontSize: 10 }}
                      tickFormatter={(value) => String(value).length > 12 ? `${String(value).slice(0, 12)}...` : String(value)}
                      tickLine={false}
                      axisLine={false}
                      height={38}
                    />
                    <YAxis
                      width={76}
                      tick={{ fill: SALES_AXIS_COLOR, fontSize: 11 }}
                      tickLine={false}
                      axisLine={false}
                      tickFormatter={(value) => rankingMetric === "amount" ? formatKpiCompactCurrency(Number(value)) : formatKpiCompactNumber(Number(value))}
                    />
                    <ChartTooltip content={<ChartTooltipContent formatter={(value) => rankingMetric === "amount" ? formatCurrency(Number(value)) : formatDecimal(Number(value))} />} />
                    <Bar dataKey="value" name={describeSalesRankingMetric(rankingMetric)} fill="url(#sales-ranking-bar)" radius={[6, 6, 0, 0]} />
                  </BarChart>
                </ChartContainer>
              </div>
            )}
          </CardContent>
        </Card>
      </section>

      <Card className="animate-soft-enter border-border/80 bg-card/95 hover:translate-y-0 hover:border-border/80 hover:shadow-sm">
        <CardHeader className="pb-3">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <CardTitle>{viewMode === "invoices" ? "Notas fiscais" : "Itens de venda"}</CardTitle>
              <p className="mt-1 text-xs text-muted-foreground">
                {loading ? "Carregando..." : `${viewMode === "invoices" ? invoices?.totalItems ?? 0 : total} registro(s) nesta visualização`}
              </p>
            </div>
            <div className="inline-flex w-fit rounded-md border border-border p-1">
              <Button type="button" size="sm" variant={viewMode === "invoices" ? "default" : "ghost"} onClick={() => setViewMode("invoices")}>
                Notas
              </Button>
              <Button type="button" size="sm" variant={viewMode === "items" ? "default" : "ghost"} onClick={() => setViewMode("items")}>
                Itens
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          {viewMode === "invoices" ? (
            <InvoiceTable
              loading={loading}
              invoices={invoices ?? { page: 1, pageSize: summaryPageSize, totalItems: 0, totalAmount: 0, totalQuantity: 0, totalWeightKg: 0, items: [] }}
              invoicePage={invoicePage}
              invoiceTotalPages={invoiceTotalPages}
              onOpenInvoice={openInvoiceDetails}
              onPrevious={() => setInvoicePage((value) => Math.max(1, value - 1))}
              onNext={() => setInvoicePage((value) => value + 1)}
            />
          ) : (
            <ItemsTable
              loading={loading}
              items={items}
              page={page}
              totalPages={totalPages}
              onPrevious={() => setPage((value) => Math.max(1, value - 1))}
              onNext={() => setPage((value) => value + 1)}
            />
          )}
        </CardContent>
      </Card>

      <InvoiceDetailsDialog
        open={invoiceDetailsOpen}
        onOpenChange={setInvoiceDetailsOpen}
        documentNumber={selectedInvoiceNumber}
        details={selectedInvoiceNumber ? invoiceDetailsByNumber[selectedInvoiceNumber] : undefined}
        loading={selectedInvoiceNumber !== "" && invoiceDetailsLoadingNumber === selectedInvoiceNumber}
      />
    </div>
  );
}

function Field({ label, value, onChange, placeholder }: { label: string; value: string; onChange: (value: string) => void; placeholder: string }) {
  return (
    <div className="space-y-1">
      <Label>{label}</Label>
      <Input value={value} onChange={(event) => onChange(event.target.value)} placeholder={placeholder} />
    </div>
  );
}

function DateField({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <div className="space-y-1">
      <Label>{label}</Label>
      <Input type="date" value={value} onChange={(event) => onChange(event.target.value)} />
    </div>
  );
}

function EmptyState({ text }: { text: string }) {
  return <div className={SALES_CHART_EMPTY_STATE_CLASS_NAME}>{text}</div>;
}

function SalesRevenueTooltip({
  active,
  payload,
  metric,
}: {
  active?: boolean;
  payload?: Array<{ value?: number; payload?: { tooltipLabel?: string } }>;
  metric: SalesTrendMetric;
}) {
  if (!active || !payload?.length) {
    return null;
  }

  const point = payload[0];
  const value = typeof point?.value === "number" ? point.value : Number(point?.value ?? 0);
  const tooltipLabel = point?.payload?.tooltipLabel ?? "";
  const formattedValue = metric === "totalAmount" ? formatCurrency(value) : metric === "totalWeightKg" ? `${formatDecimal(value)} kg` : formatDecimal(value);

  return (
    <div className="min-w-[196px] rounded-lg border border-border/70 bg-surface px-4 py-3 text-left shadow-md">
      <p className="text-xs font-medium text-muted-foreground">{tooltipLabel}</p>
      <p className="mt-1 text-sm font-semibold text-foreground">{formattedValue}</p>
    </div>
  );
}

function MetricCard({
  loading,
  error,
  empty,
  title,
  value,
  tooltip,
  percentageChange = null,
  trendData,
  periodLabel,
  icon,
}: {
  loading: boolean;
  error: boolean;
  empty: boolean;
  title: string;
  value: string;
  tooltip: string;
  percentageChange?: number | null;
  trendData?: number[];
  periodLabel: string;
  icon: LucideIcon;
}) {
  if (loading) return <SkeletonMetricCard />;

  if (error) {
    return <KpiCard title={title} value="Erro" valueTooltip="Falha ao carregar indicador" showPercentageChange={false} icon={icon} periodLabel="Não foi possível carregar" />;
  }

  if (empty) {
    return <KpiCard title={title} value="Sem resultado" valueTooltip="Nenhum dado para os filtros atuais" showPercentageChange={false} icon={icon} periodLabel="Ajuste período ou filtros" />;
  }

  return (
    <KpiCard
      title={title}
      value={value}
      valueTooltip={tooltip}
      percentageChange={percentageChange}
      showPercentageChange={percentageChange != null}
      trendData={trendData ?? []}
      periodLabel={periodLabel}
      icon={icon}
    />
  );
}

function InvoiceTable({
  loading,
  invoices,
  invoicePage,
  invoiceTotalPages,
  onOpenInvoice,
  onPrevious,
  onNext,
}: {
  loading: boolean;
  invoices: CommercialInvoiceSummaryResponse;
  invoicePage: number;
  invoiceTotalPages: number;
  onOpenInvoice: (documentNumber: string) => void | Promise<void>;
  onPrevious: () => void;
  onNext: () => void;
}) {
  if (loading) return <SkeletonTable rows={6} columns={7} />;

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Nota fiscal</TableHead>
            <TableHead>Cliente</TableHead>
            <TableHead>Data</TableHead>
            <TableHead>Total da nota</TableHead>
            <TableHead>Itens</TableHead>
            <TableHead>Quantidade</TableHead>
            <TableHead>Peso (kg)</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {invoices.items.length === 0 && (
            <TableRow>
              <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                Nenhuma nota fiscal encontrada.
              </TableCell>
            </TableRow>
          )}
          {invoices.items.map((invoice) => (
            <TableRow
              key={invoice.documentNumber}
              className="animate-soft-enter cursor-pointer"
              onClick={() => void onOpenInvoice(invoice.documentNumber)}
            >
              <TableCell>
                <Button
                  type="button"
                  variant="ghost"
                  className="h-auto px-0 text-left font-semibold"
                  onClick={(event) => {
                    event.stopPropagation();
                    void onOpenInvoice(invoice.documentNumber);
                  }}
                >
                  {invoice.documentNumber}
                </Button>
              </TableCell>
              <TableCell>{invoice.customerName}</TableCell>
              <TableCell>{formatDate(invoice.transactionDate)}</TableCell>
              <TableCell>{formatCurrency(invoice.totalAmount)}</TableCell>
              <TableCell>{formatDecimal(invoice.totalItems)}</TableCell>
              <TableCell>{formatDecimal(invoice.totalQuantity)}</TableCell>
              <TableCell>{formatDecimal(invoice.totalWeightKg)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <Pager page={invoicePage} totalPages={invoiceTotalPages} disabled={loading} onPrevious={onPrevious} onNext={onNext} />
    </>
  );
}

function InvoiceDetailsDialog({
  open,
  onOpenChange,
  documentNumber,
  details,
  loading,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  documentNumber: string;
  details?: CommercialInvoiceDetails;
  loading: boolean;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="custom-scrollbar max-h-[90vh] w-[95vw] max-w-5xl overflow-y-auto border-border/80 bg-surface p-4 pt-6 sm:p-6 sm:pt-7">
        <DialogHeader>
          <DialogTitle>Detalhes da Nota Fiscal</DialogTitle>
          <DialogDescription>
            {documentNumber ? `Informações da nota ${documentNumber}.` : "Informações detalhadas da nota fiscal selecionada."}
          </DialogDescription>
        </DialogHeader>

        {loading ? (
          <div className="py-8 text-sm text-muted-foreground">Carregando detalhes da nota fiscal...</div>
        ) : details ? (
          <div className="space-y-6">
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
              <InvoiceInfo label="Nota fiscal" value={details.documentNumber} />
              <InvoiceInfo label="Cliente" value={details.customerName} />
              <InvoiceInfo label="Data da compra" value={formatDate(details.transactionDate)} />
              <InvoiceInfo label="Total da nota" value={formatCurrency(details.totalAmount)} />
              <InvoiceInfo label="Quantidade total" value={formatDecimal(details.totalQuantity)} />
              <InvoiceInfo label="Peso total" value={`${formatDecimal(details.totalWeightKg)} kg`} />
              <InvoiceInfo label="Itens na nota" value={String(details.totalItems)} />
              <InvoiceInfo label="Operação" value={details.transactionType || "-"} />
            </div>

            <div className="space-y-3">
              <h3 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Itens da nota</h3>
              <div className="overflow-x-auto rounded-lg border border-border/70">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>#</TableHead>
                      <TableHead>Produto</TableHead>
                      <TableHead>Quantidade</TableHead>
                      <TableHead>Peso (kg)</TableHead>
                      <TableHead>Total</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {details.items.map((item, index) => (
                      <TableRow key={item.id}>
                        <TableCell>{index + 1}</TableCell>
                        <TableCell>{item.productCode} - {item.productDescription}</TableCell>
                        <TableCell>{formatDecimal(item.quantity)}</TableCell>
                        <TableCell>{formatDecimal(item.grossWeightKg)}</TableCell>
                        <TableCell>{formatCurrency(item.totalAmount)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </div>
          </div>
        ) : (
          <div className="py-8 text-sm text-muted-foreground">Não foi possível carregar os detalhes desta nota fiscal.</div>
        )}
      </DialogContent>
    </Dialog>
  );
}

function InvoiceInfo({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border/70 bg-muted/20 p-3">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="mt-1 font-medium text-foreground">{value}</p>
    </div>
  );
}

function ItemsTable({ loading, items, page, totalPages, onPrevious, onNext }: { loading: boolean; items: CommercialTransaction[]; page: number; totalPages: number; onPrevious: () => void; onNext: () => void }) {
  if (loading) return <SkeletonTable rows={8} columns={5} />;

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Documento</TableHead>
            <TableHead>Produto</TableHead>
            <TableHead>Quantidade</TableHead>
            <TableHead>Peso (kg)</TableHead>
            <TableHead>Faturamento</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.length === 0 && (
            <TableRow>
              <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                Nenhuma venda encontrada.
              </TableCell>
            </TableRow>
          )}
          {items.map((item) => (
            <TableRow key={item.id} className="animate-soft-enter">
              <TableCell>{item.documentNumber}</TableCell>
              <TableCell>{item.productCode} - {item.productDescription}</TableCell>
              <TableCell>{formatDecimal(item.quantity)}</TableCell>
              <TableCell>{formatDecimal(item.grossWeightKg)}</TableCell>
              <TableCell>{formatCurrency(item.totalAmount)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <Pager page={page} totalPages={totalPages} disabled={loading} onPrevious={onPrevious} onNext={onNext} />
    </>
  );
}

function Pager({ page, totalPages, disabled, onPrevious, onNext }: { page: number; totalPages: number; disabled: boolean; onPrevious: () => void; onNext: () => void }) {
  return (
    <div className="flex items-center justify-end gap-2">
      <Button variant="outline" size="sm" disabled={page <= 1 || disabled} onClick={onPrevious}>
        Anterior
      </Button>
      <span className="text-xs text-muted-foreground">Página {page} de {totalPages}</span>
      <Button variant="outline" size="sm" disabled={page >= totalPages || disabled} onClick={onNext}>
        Próxima
      </Button>
    </div>
  );
}
