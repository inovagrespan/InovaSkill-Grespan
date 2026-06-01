import { FormEvent, useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { KpiCard } from "@/components/ui/kpi-card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { SkeletonMetricCard, SkeletonTable } from "@/components/ui/skeleton";
import { Boxes, DollarSign, Scale, Weight } from "lucide-react";
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

function formatDate(value: string): string {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleDateString("pt-BR");
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value ?? 0);
}

function toInputDate(value: Date): string {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, "0");
  const day = String(value.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function VendasPage() {
  const [viewMode, setViewMode] = useState<"summary" | "items">("summary");
  const [companySortBy, setCompanySortBy] = useState<SummarySortBy>("growth");
  const [summaryGranularity, setSummaryGranularity] = useState<SummaryGranularity>("weekly");
  const [summaryReferenceDate, setSummaryReferenceDate] = useState(() => toInputDate(new Date()));
  const [summaryPage, setSummaryPage] = useState(1);
  const summaryPageSize = 20;
  const [items, setItems] = useState<CommercialTransaction[]>([]);
  const [summary, setSummary] = useState<CommercialTransactionSummaryResponse>({
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
  });
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const pageSize = 20;
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");

  const [documentNumber, setDocumentNumber] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [productCode, setProductCode] = useState("");
  const [city, setCity] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");

  const totalPages = useMemo(() => Math.max(1, Math.ceil(total / pageSize)), [total]);
  const summaryTotalPages = useMemo(
    () => Math.max(1, Math.ceil((summary.totalItems || 0) / summaryPageSize)),
    [summary.totalItems],
  );

  function formatDelta(value: number | null): string {
    if (value == null) return "N/A";
    return `${value > 0 ? "+" : ""}${value.toFixed(1)}%`;
  }

  function formatDecimal3(value: number): string {
    return new Intl.NumberFormat("pt-BR", { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(value ?? 0);
  }

  const revenueComparisonText = useMemo(() => {
    if (summary.totalGrowthPercent == null) return "Sem base comparativa para o período anterior.";
    if (summary.totalGrowthPercent < 0) return "O faturamento do período selecionado foi pior que o anterior.";
    if (summary.totalGrowthPercent > 0) return "O faturamento do período selecionado foi melhor que o anterior.";
    return "O faturamento do período selecionado ficou igual ao anterior.";
  }, [summary.totalGrowthPercent]);

  async function load(targetPage: number) {
    setLoading(true);
    setMessage("");
    try {
      const data = await fetchCommercialTransactions({
        page: targetPage,
        pageSize,
        documentNumber,
        customerName,
        productCode,
        city,
        dateFrom,
        dateTo,
      });
      setItems(data.items);
      setPage(data.page);
      setTotal(data.total);
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  async function loadSummaryData() {
    const data = await fetchCommercialTransactionsSummary({
        granularity: summaryGranularity,
        sortBy: companySortBy,
        page: summaryPage,
        pageSize: summaryPageSize,
        documentNumber,
        customerName,
        productCode,
        city,
        dateFrom,
        dateTo,
        referenceDate: summaryReferenceDate,
      });
    setSummary(data);
  }

  useEffect(() => {
    void Promise.all([load(1), loadSummaryData()]);
  }, []);

  useEffect(() => {
    setSummaryPage(1);
  }, [summaryGranularity, companySortBy, summaryReferenceDate, documentNumber, customerName, productCode, city, dateFrom, dateTo]);

  useEffect(() => {
    void loadSummaryData();
  }, [summaryGranularity, companySortBy, summaryReferenceDate, summaryPage]);

  function handleFilterSubmit(e: FormEvent) {
    e.preventDefault();
    setSummaryPage(1);
    void Promise.all([load(1), loadSummaryData()]);
  }

  return (
    <div className="page-shell">
      <header className="animate-soft-enter">
        <span className="page-header-kicker">Smart Core / Vendas</span>
        <h1 className="text-4xl font-display tracking-tight mt-2">Vendas Importadas</h1>
        <p className="text-sm text-muted-foreground mt-2 max-w-2xl">
          Consulte as vendas processadas com filtros rápidos e navegação por páginas.
        </p>
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
          <form onSubmit={handleFilterSubmit} className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div className="space-y-1">
              <Label>Documento</Label>
              <Input value={documentNumber} onChange={(e) => setDocumentNumber(e.target.value)} placeholder="Ex.: 000123" />
            </div>
            <div className="space-y-1">
              <Label>Cliente</Label>
              <Input value={customerName} onChange={(e) => setCustomerName(e.target.value)} placeholder="Nome do cliente" />
            </div>
            <div className="space-y-1">
              <Label>Produto</Label>
              <Input value={productCode} onChange={(e) => setProductCode(e.target.value)} placeholder="Código do produto" />
            </div>
            <div className="space-y-1">
              <Label>Cidade</Label>
              <Input value={city} onChange={(e) => setCity(e.target.value)} placeholder="Cidade" />
            </div>
            <div className="space-y-1">
              <Label>Data inicial</Label>
              <Input type="date" value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>Data final</Label>
              <Input type="date" value={dateTo} onChange={(e) => setDateTo(e.target.value)} />
            </div>
            <div className="md:col-span-3 flex justify-end gap-2 pt-1">
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setDocumentNumber("");
                  setCustomerName("");
                  setProductCode("");
                  setCity("");
                  setDateFrom("");
                  setDateTo("");
                  setSummaryPage(1);
                  void Promise.all([load(1), loadSummaryData()]);
                }}
              >
                Limpar
              </Button>
              <Button type="submit" disabled={loading}>{loading ? "Filtrando..." : "Filtrar"}</Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card className="animate-soft-enter">
        <CardHeader className="pb-3">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <CardTitle>Resultados</CardTitle>
            <div className="inline-flex rounded-md border border-border p-1">
              <Button
                type="button"
                size="sm"
                variant={viewMode === "summary" ? "default" : "ghost"}
                onClick={() => setViewMode("summary")}
              >
                Resumo
              </Button>
              <Button
                type="button"
                size="sm"
                variant={viewMode === "items" ? "default" : "ghost"}
                onClick={() => setViewMode("items")}
              >
                Itens
              </Button>
            </div>
          </div>
          <p className="text-xs text-muted-foreground">Total encontrado: {loading ? "..." : total}</p>
        </CardHeader>

        <CardContent className="space-y-3">
          {viewMode === "summary" ? (
            <div className="space-y-3">
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
                <KpiCard
                  title="Registros no filtro"
                  value={formatKpiCompactNumber(summary.totalRecords)}
                  valueTooltip={new Intl.NumberFormat("pt-BR").format(summary.totalRecords)}
                  showPercentageChange={false}
                  percentageChange={null}
                  trendDirection="stable"
                  trendData={[Math.max(1, summary.totalRecords * 0.92), summary.totalRecords]}
                  periodLabel="Conjunto retornado pelos filtros aplicados"
                  icon={Boxes}
                />
                <KpiCard
                  title="Faturamento no filtro"
                  value={formatKpiCompactCurrency(summary.totalAmount)}
                  valueTooltip={formatCurrency(summary.totalAmount)}
                  percentageChange={summary.totalGrowthPercent}
                  trendData={[summary.previousPeriodTotalAmount, summary.currentPeriodTotalAmount]}
                  periodLabel={`${summary.currentPeriodStart ? formatDate(summary.currentPeriodStart) : "-"} vs ${summary.previousPeriodStart ? formatDate(summary.previousPeriodStart) : "-"}`}
                  icon={DollarSign}
                  description={`${revenueComparisonText} Atual: ${formatCurrency(summary.currentPeriodTotalAmount)} | Anterior: ${formatCurrency(summary.previousPeriodTotalAmount)}.`}
                />
                <KpiCard
                  title="Quantidade no filtro"
                  value={formatKpiCompactNumber(summary.totalQuantity)}
                  valueTooltip={formatDecimal3(summary.totalQuantity)}
                  showPercentageChange={false}
                  percentageChange={null}
                  trendDirection="stable"
                  trendData={[Math.max(1, summary.totalQuantity * 0.96), summary.totalQuantity]}
                  periodLabel="Somatório de itens vendidos no período filtrado"
                  icon={Scale}
                />
                <KpiCard
                  title="Peso bruto no filtro (kg)"
                  value={formatKpiCompactNumber(summary.totalWeightKg)}
                  valueTooltip={formatDecimal3(summary.totalWeightKg)}
                  showPercentageChange={false}
                  percentageChange={null}
                  trendDirection="stable"
                  trendData={[Math.max(1, summary.totalWeightKg * 0.95), summary.totalWeightKg]}
                  periodLabel="Somatório de peso bruto no período filtrado"
                  icon={Weight}
                />
                </>
                )}
              </div>

              <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
                <p className="text-sm font-medium">
                  Resumo por empresa ({summaryGranularity === "weekly" ? "semana selecionada vs anterior" : summaryGranularity === "monthly" ? "mês selecionado vs anterior" : "dia selecionado vs anterior"})
                </p>
                <div className="flex gap-2">
                  <select
                    className="h-9 rounded-md border border-border bg-background px-2 text-sm"
                    value={summaryGranularity}
                    onChange={(e) => setSummaryGranularity(e.target.value as SummaryGranularity)}
                  >
                    <option value="daily">Diário</option>
                    <option value="weekly">Semanal</option>
                    <option value="monthly">Mensal</option>
                  </select>
                  <Input
                    type="date"
                    value={summaryReferenceDate}
                    onChange={(e) => setSummaryReferenceDate(e.target.value)}
                    className="w-[170px]"
                  />
                  <select
                    className="h-9 rounded-md border border-border bg-background px-2 text-sm"
                    value={companySortBy}
                    onChange={(e) => setCompanySortBy(e.target.value as SummarySortBy)}
                  >
                    <option value="growth">Maior crescimento</option>
                    <option value="amount">Maior faturamento</option>
                    <option value="weight">Maior peso</option>
                    <option value="quantity">Maior quantidade</option>
                  </select>
                </div>
              </div>

              {loading ? (
                <SkeletonTable rows={6} columns={5} />
              ) : (
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
                  {!loading && summary.items.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                        Sem dados para montar resumo por empresa.
                      </TableCell>
                    </TableRow>
                  )}
                  {summary.items.map((row) => (
                    <TableRow key={row.companyName}>
                      <TableCell>{row.companyName}</TableCell>
                      <TableCell>{formatCurrency(row.totalAmount)}</TableCell>
                      <TableCell>{formatDecimal3(row.totalQuantity)}</TableCell>
                      <TableCell>{formatDecimal3(row.totalWeightKg)}</TableCell>
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
              )}
              <div className="flex items-center justify-end gap-2">
                <Button variant="outline" size="sm" disabled={summaryPage <= 1 || loading} onClick={() => setSummaryPage((x) => x - 1)}>
                  Anterior
                </Button>
                <span className="text-xs text-muted-foreground">Página {summaryPage} de {summaryTotalPages}</span>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={summaryPage >= summaryTotalPages || loading}
                  onClick={() => setSummaryPage((x) => x + 1)}
                >
                  Próxima
                </Button>
              </div>
            </div>
          ) : (
            <>
              {loading ? (
                <SkeletonTable rows={8} columns={8} />
              ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Documento</TableHead>
                    <TableHead>Data</TableHead>
                    <TableHead>Cliente</TableHead>
                    <TableHead>Produto</TableHead>
                    <TableHead>Qtd</TableHead>
                    <TableHead>Unitário</TableHead>
                    <TableHead>Total</TableHead>
                    <TableHead>Cidade</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {!loading && items.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={8} className="py-8 text-center text-muted-foreground">
                        Nenhuma venda encontrada.
                      </TableCell>
                    </TableRow>
                  )}
                  {items.map((item) => (
                    <TableRow key={item.id} className="animate-soft-enter">
                      <TableCell>{item.documentNumber}</TableCell>
                      <TableCell>{formatDate(item.transactionDate)}</TableCell>
                      <TableCell>{item.customerName}</TableCell>
                      <TableCell>{item.productCode}</TableCell>
                      <TableCell>{formatDecimal3(item.quantity)}</TableCell>
                      <TableCell>{formatCurrency(item.unitPrice)}</TableCell>
                      <TableCell>{formatCurrency(item.totalAmount)}</TableCell>
                      <TableCell>{item.city}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              )}

              <div className="flex items-center justify-end gap-2">
                <Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => void load(page - 1)}>
                  Anterior
                </Button>
                <span className="text-xs text-muted-foreground">Página {page} de {totalPages}</span>
                <Button variant="outline" size="sm" disabled={page >= totalPages || loading} onClick={() => void load(page + 1)}>
                  Próxima
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
