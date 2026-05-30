import { FormEvent, useEffect, useMemo, useState } from "react";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { KpiCard } from "@/components/ui/kpi-card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Building2, CalendarClock, DollarSign, MapPin, Receipt, TrendingUp, UserRound, Users } from "lucide-react";
import { Line, LineChart, CartesianGrid, XAxis, YAxis } from "recharts";
import {
  fetchCustomerAnalyticsSummary,
  fetchCustomerComparison,
  fetchCustomerDetailsSummary,
  fetchCustomerNewCustomersMonthly,
  fetchCustomerPurchaseHistory,
  fetchCustomerRanking,
  fetchCustomerTimeline,
  fetchCustomerTopProducts,
  type CustomerAnalyticsSummary,
  type CustomerComparisonItem,
  type CustomerDetailSummary,
  type CustomerNewCustomersMonthlyResponse,
  type CustomerPurchaseHistoryResponse,
  type CustomerRankingItem,
  type CustomerTimelineResponse,
  type CustomerTopProductItem,
} from "@/lib/importer-api";
import {
  formatNullableCurrency,
  formatNullableCurrencyTooltip,
  formatPurchaseFrequency,
  formatVariationPercent,
  resolveComparisonColor,
  resolveCustomerStatusVariant,
} from "@/lib/customer-details";
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

function formatDecimal(value: number): string {
  return new Intl.NumberFormat("pt-BR", { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(value ?? 0);
}

function toInputDate(value: Date): string {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, "0");
  const day = String(value.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
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
  const [loading, setLoading] = useState(false);
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
  const [history, setHistory] = useState<CustomerPurchaseHistoryResponse | null>(null);
  const [historyPage, setHistoryPage] = useState(1);
  const [timelineGranularity, setTimelineGranularity] = useState<"daily" | "weekly" | "monthly">("monthly");
  const [timelineMetric, setTimelineMetric] = useState<"revenue" | "quantity" | "weight" | "orders">("revenue");
  const [newCustomersOpen, setNewCustomersOpen] = useState(false);
  const [newCustomersLoading, setNewCustomersLoading] = useState(false);
  const [newCustomersMessage, setNewCustomersMessage] = useState("");
  const [newCustomersMonthly, setNewCustomersMonthly] = useState<CustomerNewCustomersMonthlyResponse | null>(null);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalItems / pageSize)), [totalItems, pageSize]);
  const historyTotalPages = useMemo(
    () => Math.max(1, Math.ceil((history?.totalItems ?? 0) / (history?.pageSize ?? 10))),
    [history?.totalItems, history?.pageSize],
  );

  async function load(targetPage: number) {
    setLoading(true);
    setMessage("");
    try {
      const [summaryData, rankingData] = await Promise.all([
        fetchCustomerAnalyticsSummary({ dateFrom, dateTo, customer, city, productGroup, productCode, transactionType }),
        fetchCustomerRanking({ page: targetPage, pageSize, sortBy, dateFrom, dateTo, customer, city, productGroup, productCode, transactionType }),
      ]);
      setSummary(summaryData);
      setItems(rankingData.items);
      setPage(rankingData.page);
      setTotalItems(rankingData.totalItems);
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  async function loadCustomerDetails(customerId: string, targetHistoryPage = 1, granularity = timelineGranularity, metric = timelineMetric) {
    setDetailsLoading(true);
    setDetailsMessage("");
    try {
      const [summaryData, timelineData, topProductsData, comparisonData, historyData] = await Promise.allSettled([
        fetchCustomerDetailsSummary({ customerId, dateFrom, dateTo }),
        fetchCustomerTimeline({ customerId, dateFrom, dateTo, granularity, metric }),
        fetchCustomerTopProducts({ customerId, dateFrom, dateTo }),
        fetchCustomerComparison({ customerId, referenceDate: dateTo }),
        fetchCustomerPurchaseHistory({ customerId, dateFrom, dateTo, page: targetHistoryPage, pageSize: 10 }),
      ]);

      if (summaryData.status === "rejected") {
        throw summaryData.reason;
      }

      setDetails(summaryData.value);
      setTimeline(timelineData.status === "fulfilled" ? timelineData.value : null);
      setTopProducts(topProductsData.status === "fulfilled" ? topProductsData.value : []);
      setComparison(comparisonData.status === "fulfilled" ? comparisonData.value.items : []);
      setHistory(historyData.status === "fulfilled" ? historyData.value : { page: targetHistoryPage, pageSize: 10, totalItems: 0, items: [] });
      setHistoryPage(targetHistoryPage);

      const partialErrors: string[] = [];
      if (timelineData.status === "rejected") partialErrors.push("evolução temporal");
      if (topProductsData.status === "rejected") partialErrors.push("produtos");
      if (comparisonData.status === "rejected") partialErrors.push("comparativo");
      if (historyData.status === "rejected") partialErrors.push("histórico");
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
  }, [timelineGranularity, timelineMetric]);

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
            <KpiCard title="Clientes ativos" value={formatKpiCompactNumber(summary?.activeCustomers ?? 0)} valueTooltip={String(summary?.activeCustomers ?? 0)} showPercentageChange={false} icon={Users} periodLabel="Clientes com compras no período" />
            <KpiCard title="Faturamento total" value={formatKpiCompactCurrency(summary?.totalRevenue ?? 0)} valueTooltip={formatCurrency(summary?.totalRevenue ?? 0)} showPercentageChange={false} icon={DollarSign} periodLabel={summary ? `${formatDate(summary.currentPeriodStart)} a ${formatDate(summary.currentPeriodEnd)}` : "Período atual"} />
            <KpiCard title="Ticket médio" value={formatKpiCompactCurrency(summary?.averageTicket ?? 0)} valueTooltip={formatCurrency(summary?.averageTicket ?? 0)} showPercentageChange={false} icon={Receipt} periodLabel="Faturamento dividido por pedidos" />
            <button
              type="button"
              className="text-left"
              onClick={openNewCustomersModal}
              aria-label="Abrir detalhamento de novos clientes por mês"
            >
              <KpiCard title="Novos clientes" value={formatKpiCompactNumber(summary?.newCustomers ?? 0)} valueTooltip={String(summary?.newCustomers ?? 0)} showPercentageChange={false} icon={TrendingUp} periodLabel={summary ? `Inativos no período: ${summary.inactiveCustomers}` : ""} />
            </button>
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
              {items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">{loading ? "Carregando clientes..." : "Sem dados para os filtros selecionados."}</TableCell>
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

          <div className="flex items-center justify-end gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => void load(page - 1)}>Anterior</Button>
            <span className="text-xs text-muted-foreground">Página {page} de {totalPages}</span>
            <Button variant="outline" size="sm" disabled={page >= totalPages || loading} onClick={() => void load(page + 1)}>Próxima</Button>
          </div>
        </CardContent>
      </Card>

      <Dialog open={detailsOpen} onOpenChange={handleDetailsOpenChange}>
        <DialogContent className="max-h-[90vh] w-[95vw] max-w-6xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Detalhes do Cliente</DialogTitle>
            <DialogDescription>Análise individual de comportamento de compra</DialogDescription>
          </DialogHeader>

          {detailsMessage && (
            <Alert variant="destructive" className="mt-4">
              <AlertDescription>{detailsMessage}</AlertDescription>
            </Alert>
          )}

          {details && (
            <div className="mt-4 space-y-4 pb-8">
              <Card className="border-primary/20 bg-gradient-to-r from-primary/10 via-background to-background shadow-sm">
                <CardContent className="space-y-4 p-5">
                  <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                    <div className="space-y-2">
                      <div className="inline-flex items-center gap-2 rounded-full border border-border bg-background px-3 py-1 text-xs text-muted-foreground">
                        <UserRound className="h-3.5 w-3.5" />
                        Código: {details.customerCode}
                      </div>
                      <h3 className="text-2xl font-semibold tracking-tight">{details.customerName}</h3>
                    </div>
                    <Badge variant={resolveCustomerStatusVariant(details.status)} className="w-fit text-sm">
                      {details.status}
                    </Badge>
                  </div>

                  <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                    <div className="rounded-lg border border-border/70 bg-background/80 p-3">
                      <div className="mb-1 flex items-center gap-2 text-xs text-muted-foreground">
                        <MapPin className="h-3.5 w-3.5" />
                        Cidade
                      </div>
                      <p className="font-medium">{details.city || "N/A"}</p>
                    </div>
                    <div className="rounded-lg border border-border/70 bg-background/80 p-3">
                      <div className="mb-1 flex items-center gap-2 text-xs text-muted-foreground">
                        <Building2 className="h-3.5 w-3.5" />
                        Empresa vinculada
                      </div>
                      <p className="font-medium">{details.linkedCompany || "N/A"}</p>
                    </div>
                    <div className="rounded-lg border border-border/70 bg-background/80 p-3">
                      <div className="mb-1 flex items-center gap-2 text-xs text-muted-foreground">
                        <CalendarClock className="h-3.5 w-3.5" />
                        Última compra
                      </div>
                      <p className="font-medium">{formatDate(details.lastPurchaseDate)}</p>
                    </div>
                  </div>
                </CardContent>
              </Card>

              <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
                <KpiCard title="Faturamento total" value={formatKpiCompactCurrency(details.totalRevenue)} valueTooltip={formatCurrency(details.totalRevenue)} showPercentageChange={false} icon={DollarSign} periodLabel="Faturamento acumulado no período selecionado" />
                <KpiCard title="Ticket médio" value={formatNullableCurrency(details.averageTicket, formatKpiCompactCurrency)} valueTooltip={formatNullableCurrencyTooltip(details.averageTicket, formatCurrency)} showPercentageChange={false} icon={Receipt} periodLabel="Faturamento total dividido pelo total de pedidos" />
                <KpiCard title="Média mensal" value={formatNullableCurrency(details.averageRevenueMonthly, formatKpiCompactCurrency)} valueTooltip={formatNullableCurrencyTooltip(details.averageRevenueMonthly, formatCurrency)} showPercentageChange={false} icon={CalendarClock} periodLabel="Média das receitas por meses com compra no período" />
                <KpiCard title="Média semanal" value={formatNullableCurrency(details.averageRevenueWeekly, formatKpiCompactCurrency)} valueTooltip={formatNullableCurrencyTooltip(details.averageRevenueWeekly, formatCurrency)} showPercentageChange={false} icon={CalendarClock} periodLabel="Média das receitas por semanas com compra no período" />
                <KpiCard title="Quantidade total" value={formatKpiCompactNumber(details.totalQuantity)} valueTooltip={formatDecimal(details.totalQuantity)} showPercentageChange={false} icon={TrendingUp} periodLabel="Soma das quantidades no período filtrado" />
                <KpiCard title="Peso total" value={formatKpiCompactNumber(details.totalWeight)} valueTooltip={formatDecimal(details.totalWeight)} showPercentageChange={false} icon={TrendingUp} periodLabel="Peso acumulado das compras no período" />
                <KpiCard title="Total de pedidos" value={formatKpiCompactNumber(details.totalOrders)} valueTooltip={String(details.totalOrders)} showPercentageChange={false} icon={UserRound} periodLabel="Pedidos distintos por documento no período" />
                <KpiCard title="Frequência média" value={formatPurchaseFrequency(details.averageDaysBetweenPurchases).value} valueTooltip={formatPurchaseFrequency(details.averageDaysBetweenPurchases).tooltip} showPercentageChange={false} icon={CalendarClock} periodLabel="Intervalo médio entre dias de compra no período" />
              </div>

              <Card>
                <CardHeader className="pb-2">
                  <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
                    <CardTitle>Evolução temporal</CardTitle>
                    <div className="flex gap-2">
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
                <CardContent>
                  {!timeline && <p className="text-sm text-muted-foreground">Dados insuficientes para evolução temporal neste período.</p>}
                  {timeline && timeline.points.length === 0 && <p className="text-sm text-muted-foreground">Sem pontos para o período selecionado.</p>}
                  <ChartContainer config={{ value: { label: "Valor", color: "hsl(var(--chart-1))" } }} className="h-[260px] w-full">
                    <LineChart data={timeline?.points ?? []} margin={{ left: 4, right: 4, top: 10, bottom: 4 }}>
                      <CartesianGrid vertical={false} />
                      <XAxis dataKey="periodStart" tickFormatter={(value) => new Date(value).toLocaleDateString("pt-BR")} minTickGap={24} />
                      <YAxis />
                      <ChartTooltip content={<ChartTooltipContent labelFormatter={(value) => String(value)} formatter={(value) => formatDecimal(Number(value))} />} />
                      <Line dataKey="value" type="monotone" stroke="var(--color-value)" strokeWidth={2} dot={false} />
                    </LineChart>
                  </ChartContainer>
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>Comparativo de períodos</CardTitle>
                </CardHeader>
                <CardContent>
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Período</TableHead>
                        <TableHead>Valor atual</TableHead>
                        <TableHead>Valor anterior</TableHead>
                        <TableHead>Variação</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {comparison.map((item) => (
                        <TableRow key={item.label}>
                          <TableCell>{item.label}</TableCell>
                          <TableCell>{formatCurrency(item.currentValue)}</TableCell>
                          <TableCell>{formatCurrency(item.previousValue)}</TableCell>
                          <TableCell className={resolveComparisonColor(item.variationPercent)}>{formatVariationPercent(item.variationPercent)}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </CardContent>
              </Card>

              <Card>
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
                          <TableCell>{item.productCode} - {item.productDescription}</TableCell>
                          <TableCell>{formatDecimal(item.quantity)}</TableCell>
                          <TableCell>{formatCurrency(item.revenue)}</TableCell>
                          <TableCell>{item.sharePercent.toFixed(1)}%</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </CardContent>
              </Card>

              <Card>
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
              <Skeleton className="h-10 w-1/2" />
              <Skeleton className="h-28 w-full" />
              <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
                {Array.from({ length: 8 }).map((_, idx) => (
                  <Skeleton key={idx} className="h-28 w-full" />
                ))}
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={newCustomersOpen} onOpenChange={setNewCustomersOpen}>
        <DialogContent className="max-h-[85vh] w-[95vw] max-w-4xl overflow-y-auto">
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
              <Skeleton className="h-20 w-full" />
              <Skeleton className="h-72 w-full" />
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
