import { useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { KpiCard } from "@/components/ui/kpi-card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { SkeletonMetricCard, SkeletonTable } from "@/components/ui/skeleton";
import { Boxes, ChevronDown, DollarSign, LineChart as LineChartIcon, Scale, Search, SlidersHorizontal, Weight } from "lucide-react";
import { Bar, BarChart, CartesianGrid, Line, LineChart, XAxis, YAxis } from "recharts";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";
import {
  fetchCommercialTransactionsSummary,
  fetchCommercialTransactions,
  type CommercialTransactionSummaryResponse,
  type SummaryGranularity,
  type SummarySortBy,
  type CommercialTransaction,
} from "@/lib/importer-api";

export const Route = createFileRoute("/vendas")({
  component: VendasPage,
});

type PeriodPreset = "today" | "week" | "month" | "quarter" | "year" | "custom";
type ViewMode = "summary" | "items";

const periodOptions: Array<{ value: PeriodPreset; label: string }> = [
  { value: "today", label: "Hoje" },
  { value: "week", label: "Semana" },
  { value: "month", label: "Mês" },
  { value: "quarter", label: "Trimestre" },
  { value: "year", label: "Ano" },
  { value: "custom", label: "Personalizado" },
];

function formatDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value || "N/A";
  return date.toLocaleDateString("pt-BR");
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value ?? 0);
}

function formatDecimal(value: number): string {
  return new Intl.NumberFormat("pt-BR", { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(value ?? 0);
}

function formatDelta(value: number | null): string {
  if (value == null) return "N/A";
  return `${value > 0 ? "+" : ""}${value.toFixed(1)}%`;
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

function makeEmptySummary(): CommercialTransactionSummaryResponse {
  return {
    page: 1,
    pageSize: 20,
    totalItems: 0,
    granularity: "weekly",
    currentPeriodStart: "",
    previousPeriodStart: "",
    currentPeriodTotalAmount: 0,
    previousPeriodTotalAmount: 0,
    totalGrowthPercent: null,
    totalRecords: 0,
    totalAmount: 0,
    totalQuantity: 0,
    totalWeightKg: 0,
    totalCompanies: 0,
    items: [],
  };
}

function VendasPage() {
  const defaultPeriod = resolvePeriod("month");
  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>("month");
  const [dateFrom, setDateFrom] = useState(defaultPeriod.dateFrom);
  const [dateTo, setDateTo] = useState(defaultPeriod.dateTo);
  const [customerName, setCustomerName] = useState("");
  const [productCode, setProductCode] = useState("");
  const [documentNumber, setDocumentNumber] = useState("");
  const [city, setCity] = useState("");
  const [companyName, setCompanyName] = useState("");
  const [productGroup, setProductGroup] = useState("");
  const [transactionType, setTransactionType] = useState("");
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const [viewMode, setViewMode] = useState<ViewMode>("summary");
  const [companySortBy, setCompanySortBy] = useState<SummarySortBy>("amount");
  const [summaryGranularity, setSummaryGranularity] = useState<SummaryGranularity>("weekly");
  const [summaryPage, setSummaryPage] = useState(1);
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<CommercialTransaction[]>([]);
  const [total, setTotal] = useState(0);
  const [summary, setSummary] = useState<CommercialTransactionSummaryResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");

  const pageSize = 20;
  const summaryPageSize = 20;
  const effectiveCustomerName = customerName.trim() || companyName.trim();
  const hasAdvancedFilters = Boolean(documentNumber || city || companyName || productGroup || transactionType || periodPreset === "custom");
  const totalPages = useMemo(() => Math.max(1, Math.ceil(total / pageSize)), [total]);
  const summaryTotalPages = useMemo(() => Math.max(1, Math.ceil((summary?.totalItems ?? 0) / summaryPageSize)), [summary?.totalItems]);
  const hasResults = (summary?.totalRecords ?? 0) > 0;

  const customerSuggestions = useMemo(
    () => Array.from(new Set(items.map((item) => item.customerName).filter(Boolean))).slice(0, 12),
    [items],
  );
  const productSuggestions = useMemo(
    () => Array.from(new Set(items.map((item) => `${item.productCode} - ${item.productDescription}`).filter(Boolean))).slice(0, 12),
    [items],
  );
  const chartData = useMemo(
    () => (summary?.items ?? []).slice(0, 8).map((item) => ({
      companyName: item.companyName,
      faturamento: item.totalAmount,
      quantidade: item.totalQuantity,
      crescimento: item.growthPercent ?? 0,
    })),
    [summary?.items],
  );
  const trendData = useMemo(() => {
    if (!summary) return [];
    return [
      { label: "Anterior", value: summary.previousPeriodTotalAmount },
      { label: "Atual", value: summary.currentPeriodTotalAmount },
    ];
  }, [summary]);

  const revenueComparisonText = useMemo(() => {
    if (!summary || summary.totalRecords === 0) return "Sem resultado para o período e filtros atuais.";
    if (summary.totalGrowthPercent == null) return "Sem base comparativa para o período anterior.";
    if (summary.totalGrowthPercent < 0) return "Faturamento abaixo do período anterior.";
    if (summary.totalGrowthPercent > 0) return "Faturamento acima do período anterior.";
    return "Faturamento igual ao período anterior.";
  }, [summary]);

  async function loadData(targetPage = page, targetSummaryPage = summaryPage) {
    setLoading(true);
    setMessage("");
    try {
      const [itemsData, summaryData] = await Promise.all([
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
        }),
        fetchCommercialTransactionsSummary({
          granularity: summaryGranularity,
          sortBy: companySortBy,
          page: targetSummaryPage,
          pageSize: summaryPageSize,
          documentNumber,
          customerName: effectiveCustomerName,
          productCode,
          city,
          productGroup,
          transactionType,
          dateFrom,
          dateTo,
          referenceDate: dateTo,
        }),
      ]);

      setItems(itemsData.items);
      setPage(itemsData.page);
      setTotal(itemsData.total);
      setSummary(summaryData);
      setSummaryPage(summaryData.page);
    } catch (error) {
      setItems([]);
      setTotal(0);
      setSummary(null);
      setMessage((error as Error).message);
    } finally {
      setLoading(false);
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
    const next = resolvePeriod("month");
    setPeriodPreset("month");
    setDateFrom(next.dateFrom);
    setDateTo(next.dateTo);
    setCustomerName("");
    setProductCode("");
    setDocumentNumber("");
    setCity("");
    setCompanyName("");
    setProductGroup("");
    setTransactionType("");
    setSummaryPage(1);
    setPage(1);
  }

  useEffect(() => {
    setSummaryPage(1);
    setPage(1);
  }, [dateFrom, dateTo, customerName, productCode, documentNumber, city, companyName, productGroup, transactionType, summaryGranularity, companySortBy]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadData(1, 1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [dateFrom, dateTo, customerName, productCode, documentNumber, city, companyName, productGroup, transactionType, summaryGranularity, companySortBy]);

  useEffect(() => {
    void loadData(page, summaryPage);
  }, [page, summaryPage]);

  return (
    <div className="page-shell space-y-6">
      <header className="animate-soft-enter space-y-4">
        <div>
          <span className="page-header-kicker">Smart Core / Vendas</span>
          <h1 className="mt-2 text-4xl font-display tracking-tight">Vendas</h1>
          <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
            Dashboard analítico para descobrir desempenho, ranking e itens importados sem começar por um formulário.
          </p>
        </div>

        <section className="space-y-3 rounded-lg border border-border bg-surface p-3">
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

          <div className="grid grid-cols-1 gap-3 lg:grid-cols-[1fr_1fr_auto] lg:items-end">
            <div className="space-y-1">
              <Label htmlFor="sales-customer">Cliente</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input id="sales-customer" list="sales-customer-options" className="pl-9" value={customerName} onChange={(event) => setCustomerName(event.target.value)} placeholder="Buscar cliente ou empresa" />
              </div>
              <datalist id="sales-customer-options">
                {customerSuggestions.map((value) => <option key={value} value={value} />)}
              </datalist>
            </div>

            <div className="space-y-1">
              <Label htmlFor="sales-product">Produto</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input id="sales-product" list="sales-product-options" className="pl-9" value={productCode} onChange={(event) => setProductCode(event.target.value.split(" - ")[0] ?? event.target.value)} placeholder="Buscar código ou descrição" />
              </div>
              <datalist id="sales-product-options">
                {productSuggestions.map((value) => <option key={value} value={value} />)}
              </datalist>
            </div>

            <Button type="button" variant="outline" onClick={() => setAdvancedOpen((value) => !value)}>
              <SlidersHorizontal className="mr-2 size-4" />
              Filtros avançados
              {hasAdvancedFilters && <Badge className="ml-2" variant="secondary">ativo</Badge>}
              <ChevronDown className={`ml-2 size-4 transition-transform ${advancedOpen ? "rotate-180" : ""}`} />
            </Button>
          </div>

          {advancedOpen && (
            <div className="grid grid-cols-1 gap-3 border-t border-border pt-3 md:grid-cols-2 xl:grid-cols-4">
              <Field label="Documento" value={documentNumber} onChange={setDocumentNumber} placeholder="NF, pedido ou documento" />
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

      <section className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          loading={loading}
          error={Boolean(message)}
          empty={!hasResults}
          title="Registros"
          value={formatKpiCompactNumber(summary?.totalRecords ?? 0)}
          tooltip={new Intl.NumberFormat("pt-BR").format(summary?.totalRecords ?? 0)}
          periodLabel="Vendas encontradas"
          icon={Boxes}
        />
        <MetricCard
          loading={loading}
          error={Boolean(message)}
          empty={!hasResults}
          title="Faturamento"
          value={formatKpiCompactCurrency(summary?.totalAmount ?? 0)}
          tooltip={formatCurrency(summary?.totalAmount ?? 0)}
          percentageChange={summary?.totalGrowthPercent ?? null}
          periodLabel={revenueComparisonText}
          icon={DollarSign}
          trendData={[summary?.previousPeriodTotalAmount ?? 0, summary?.currentPeriodTotalAmount ?? 0]}
        />
        <MetricCard
          loading={loading}
          error={Boolean(message)}
          empty={!hasResults}
          title="Quantidade"
          value={formatKpiCompactNumber(summary?.totalQuantity ?? 0)}
          tooltip={formatDecimal(summary?.totalQuantity ?? 0)}
          periodLabel="Somatório de itens vendidos"
          icon={Scale}
        />
        <MetricCard
          loading={loading}
          error={Boolean(message)}
          empty={!hasResults}
          title="Peso bruto"
          value={formatKpiCompactNumber(summary?.totalWeightKg ?? 0)}
          tooltip={`${formatDecimal(summary?.totalWeightKg ?? 0)} kg`}
          periodLabel="Peso bruto acumulado"
          icon={Weight}
        />
      </section>

      <section className="grid grid-cols-1 gap-3 xl:grid-cols-[0.85fr_1.15fr]">
        <Card className="border-border/80 bg-card/95">
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <CardTitle>Faturamento no período</CardTitle>
              <LineChartIcon className="size-5 text-primary" />
            </div>
          </CardHeader>
          <CardContent>
            {loading ? (
              <SkeletonTable rows={4} columns={2} />
            ) : !hasResults ? (
              <EmptyState text="Sem resultado para gerar gráfico de faturamento." />
            ) : (
              <ChartContainer config={{ value: { label: "Faturamento", color: "var(--primary)" } }} className="h-[260px] min-h-[260px] w-full">
                <LineChart data={trendData} margin={{ left: 8, right: 12, top: 10, bottom: 20 }}>
                  <CartesianGrid vertical={false} />
                  <XAxis dataKey="label" />
                  <YAxis width={86} tickFormatter={(value) => formatKpiCompactCurrency(Number(value))} />
                  <ChartTooltip content={<ChartTooltipContent formatter={(value) => formatCurrency(Number(value))} />} />
                  <Line dataKey="value" name="Faturamento" type="monotone" stroke="var(--color-value)" strokeWidth={2.6} dot={{ r: 4 }} />
                </LineChart>
              </ChartContainer>
            )}
          </CardContent>
        </Card>

        <Card className="border-border/80 bg-card/95">
          <CardHeader>
            <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
              <CardTitle>Ranking por empresa</CardTitle>
              <div className="flex flex-wrap gap-2">
                <select className="h-9 rounded-md border border-border bg-background px-2 text-sm" value={summaryGranularity} onChange={(event) => setSummaryGranularity(event.target.value as SummaryGranularity)}>
                  <option value="daily">Diário</option>
                  <option value="weekly">Semanal</option>
                  <option value="monthly">Mensal</option>
                </select>
                <select className="h-9 rounded-md border border-border bg-background px-2 text-sm" value={companySortBy} onChange={(event) => setCompanySortBy(event.target.value as SummarySortBy)}>
                  <option value="amount">Maior faturamento</option>
                  <option value="growth">Maior crescimento</option>
                  <option value="weight">Maior peso</option>
                  <option value="quantity">Maior quantidade</option>
                </select>
              </div>
            </div>
          </CardHeader>
          <CardContent>
            {loading ? (
              <SkeletonTable rows={4} columns={3} />
            ) : !hasResults ? (
              <EmptyState text="Sem empresas para o ranking atual." />
            ) : (
              <ChartContainer config={{ faturamento: { label: "Faturamento", color: "var(--primary)" } }} className="h-[260px] min-h-[260px] w-full">
                <BarChart data={chartData} margin={{ left: 8, right: 12, top: 10, bottom: 38 }}>
                  <CartesianGrid vertical={false} />
                  <XAxis dataKey="companyName" interval={0} tick={{ fontSize: 11 }} angle={-18} textAnchor="end" height={58} />
                  <YAxis width={86} tickFormatter={(value) => formatKpiCompactCurrency(Number(value))} />
                  <ChartTooltip content={<ChartTooltipContent formatter={(value) => formatCurrency(Number(value))} />} />
                  <Bar dataKey="faturamento" name="Faturamento" fill="var(--color-faturamento)" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ChartContainer>
            )}
          </CardContent>
        </Card>
      </section>

      <Card className="animate-soft-enter border-border/80 bg-card/95">
        <CardHeader className="pb-3">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <CardTitle>{viewMode === "summary" ? "Ranking detalhado" : "Itens de venda"}</CardTitle>
              <p className="mt-1 text-xs text-muted-foreground">
                {loading ? "Carregando..." : `${viewMode === "summary" ? summary?.totalItems ?? 0 : total} registro(s) nesta visualização`}
              </p>
            </div>
            <div className="inline-flex w-fit rounded-md border border-border p-1">
              <Button type="button" size="sm" variant={viewMode === "summary" ? "default" : "ghost"} onClick={() => setViewMode("summary")}>
                Resumo
              </Button>
              <Button type="button" size="sm" variant={viewMode === "items" ? "default" : "ghost"} onClick={() => setViewMode("items")}>
                Itens
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          {viewMode === "summary" ? (
            <SummaryTable
              loading={loading}
              summary={summary ?? makeEmptySummary()}
              summaryPage={summaryPage}
              summaryTotalPages={summaryTotalPages}
              onPrevious={() => setSummaryPage((value) => Math.max(1, value - 1))}
              onNext={() => setSummaryPage((value) => value + 1)}
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
  return (
    <div className="flex min-h-[180px] items-center justify-center rounded-lg border border-dashed border-border bg-muted/30 p-6 text-center text-sm text-muted-foreground">
      {text}
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
  icon: typeof Boxes;
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

function SummaryTable({
  loading,
  summary,
  summaryPage,
  summaryTotalPages,
  onPrevious,
  onNext,
}: {
  loading: boolean;
  summary: CommercialTransactionSummaryResponse;
  summaryPage: number;
  summaryTotalPages: number;
  onPrevious: () => void;
  onNext: () => void;
}) {
  if (loading) return <SkeletonTable rows={6} columns={5} />;

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Empresa</TableHead>
            <TableHead>Faturamento</TableHead>
            <TableHead>Quantidade</TableHead>
            <TableHead>Peso (kg)</TableHead>
            <TableHead>Variação</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {summary.items.length === 0 && (
            <TableRow>
              <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                Sem dados para montar ranking.
              </TableCell>
            </TableRow>
          )}
          {summary.items.map((row) => (
            <TableRow key={row.companyName}>
              <TableCell>{row.companyName}</TableCell>
              <TableCell>{formatCurrency(row.totalAmount)}</TableCell>
              <TableCell>{formatDecimal(row.totalQuantity)}</TableCell>
              <TableCell>{formatDecimal(row.totalWeightKg)}</TableCell>
              <TableCell>
                <Badge
                  variant={row.growthPercent == null ? "outline" : row.growthPercent >= 0 ? "secondary" : "destructive"}
                  className={
                    row.growthPercent == null
                      ? "text-muted-foreground"
                      : row.growthPercent >= 0
                        ? "border-[color:var(--success)]/20 bg-[color:var(--success)]/10 text-[color:var(--success)]"
                        : "border-[color:var(--danger)]/20 bg-[color:var(--danger)]/10 text-[color:var(--danger)]"
                  }
                >
                  {formatDelta(row.growthPercent)}
                </Badge>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <Pager page={summaryPage} totalPages={summaryTotalPages} disabled={loading} onPrevious={onPrevious} onNext={onNext} />
    </>
  );
}

function ItemsTable({ loading, items, page, totalPages, onPrevious, onNext }: { loading: boolean; items: CommercialTransaction[]; page: number; totalPages: number; onPrevious: () => void; onNext: () => void }) {
  if (loading) return <SkeletonTable rows={8} columns={8} />;

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Data</TableHead>
            <TableHead>Cliente</TableHead>
            <TableHead>Produto</TableHead>
            <TableHead>Qtd</TableHead>
            <TableHead>Unitário</TableHead>
            <TableHead>Total</TableHead>
            <TableHead>Operação</TableHead>
            <TableHead>Cidade</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.length === 0 && (
            <TableRow>
              <TableCell colSpan={8} className="py-8 text-center text-muted-foreground">
                Nenhuma venda encontrada.
              </TableCell>
            </TableRow>
          )}
          {items.map((item) => (
            <TableRow key={item.id} className="animate-soft-enter">
              <TableCell>{formatDate(item.transactionDate)}</TableCell>
              <TableCell>{item.customerName}</TableCell>
              <TableCell>{item.productCode}</TableCell>
              <TableCell>{formatDecimal(item.quantity)}</TableCell>
              <TableCell>{formatCurrency(item.unitPrice)}</TableCell>
              <TableCell>{formatCurrency(item.totalAmount)}</TableCell>
              <TableCell>{item.transactionType || "-"}</TableCell>
              <TableCell>{item.city || "-"}</TableCell>
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
