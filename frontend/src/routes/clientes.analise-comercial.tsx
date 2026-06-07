import { FormEvent, useEffect, useMemo, useState } from "react";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { AlertTriangle, Activity, CalendarClock, Package, ShieldCheck, Target, TrendingUp, UserRound, type LucideIcon } from "lucide-react";
import { Line, LineChart, CartesianGrid, XAxis, YAxis } from "recharts";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SkeletonMetricCard, SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { fetchCustomerCommercialHealth, type CustomerCommercialHealthBlock, type CustomerCommercialHealthReport } from "@/lib/importer-api";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";

export const Route = createFileRoute("/clientes/analise-comercial")({
  validateSearch: (search: Record<string, unknown>) => ({
    cliente: typeof search.cliente === "string" ? search.cliente : undefined,
  }),
  component: CustomerCommercialAnalysisPage,
});

function formatCurrency(value: number | null | undefined): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value ?? 0);
}

function formatDecimal(value: number | null | undefined): string {
  return new Intl.NumberFormat("pt-BR", { minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(value ?? 0);
}

function formatDate(value: string | null | undefined): string {
  if (!value) return "N/A";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleDateString("pt-BR");
}

function formatMonth(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleDateString("pt-BR", { month: "short", year: "2-digit" }).replace(".", "");
}

function formatPercent(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${value > 0 ? "+" : ""}${value.toFixed(1)}%`;
}

function toneClass(tone: string): string {
  if (tone === "success") return "border-[var(--success)]/35 bg-[var(--success)]/10 text-[var(--success)]";
  if (tone === "warning") return "border-amber-500/35 bg-amber-500/10 text-amber-300";
  if (tone === "danger") return "border-[var(--danger)]/35 bg-[var(--danger)]/10 text-[var(--danger)]";
  return "border-border bg-muted/40 text-muted-foreground";
}

function severityVariant(severity: string): "default" | "secondary" | "destructive" | "outline" {
  if (severity === "critical") return "destructive";
  if (severity === "warning") return "secondary";
  return "outline";
}

function scoreWidth(value: number): string {
  return `${Math.max(0, Math.min(100, value))}%`;
}

function CustomerCommercialAnalysisPage() {
  const { cliente } = Route.useSearch();
  const navigate = useNavigate();
  const [customerInput, setCustomerInput] = useState(cliente ?? "");
  const [report, setReport] = useState<CustomerCommercialHealthReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");

  const chartData = useMemo(
    () => (report?.evolution ?? []).map((point) => ({
      ...point,
      revenueLabel: formatCurrency(point.revenue),
    })),
    [report?.evolution],
  );

  async function load(customerId: string) {
    const normalized = customerId.trim();
    if (!normalized) {
      setMessage("Informe um cliente para carregar a análise comercial.");
      setReport(null);
      return;
    }

    setLoading(true);
    setMessage("");
    try {
      const data = await fetchCustomerCommercialHealth({ customerId: normalized });
      setReport(data);
      setCustomerInput(normalized);
    } catch (error) {
      setReport(null);
      setMessage((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const normalized = customerInput.trim();
    void navigate({
      to: "/clientes/analise-comercial",
      search: { cliente: normalized || undefined },
      replace: false,
    });
    void load(normalized);
  }

  useEffect(() => {
    setCustomerInput(cliente ?? "");
    if (cliente) void load(cliente);
  }, [cliente]);

  return (
    <div className="page-shell space-y-6">
      <header className="space-y-4">
        <div>
          <span className="page-header-kicker">Clientes / Análise Comercial</span>
          <h1 className="mt-2 text-4xl font-display tracking-tight">Análise Comercial</h1>
          <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
            Prontuário comercial do cliente com saúde, risco, tendência, potencial e recomendações de ação.
          </p>
        </div>

        <form onSubmit={handleSubmit} className="flex max-w-3xl flex-col gap-2 rounded-lg border border-border bg-surface p-3 sm:flex-row sm:items-end">
          <div className="flex-1 space-y-1">
            <Label htmlFor="customer-analysis-input">Cliente</Label>
            <Input id="customer-analysis-input" value={customerInput} onChange={(event) => setCustomerInput(event.target.value)} placeholder="Código ou nome do cliente" />
          </div>
          <Button type="submit" disabled={loading}>
            <Activity className="mr-2 size-4" />
            Analisar
          </Button>
        </form>
      </header>

      {message && (
        <Alert variant="destructive">
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      {loading && !report && (
        <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
          <SkeletonMetricCard />
          <SkeletonMetricCard />
          <SkeletonMetricCard />
          <SkeletonMetricCard />
        </div>
      )}

      {!loading && !report && !message && (
        <Card>
          <CardContent className="py-8 text-sm text-muted-foreground">
            Informe um cliente para abrir o painel executivo.
          </CardContent>
        </Card>
      )}

      {report && (
        <>
          <section className="grid grid-cols-1 gap-3 xl:grid-cols-[1.3fr_0.9fr_0.9fr]">
            <Card className="border-border/80 bg-card/95">
              <CardContent className="p-5">
                <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                  <div className="min-w-0">
                    <div className="inline-flex items-center gap-2 rounded-md border border-border bg-muted/40 px-2.5 py-1 text-xs text-muted-foreground">
                      <UserRound className="size-3.5" />
                      {report.header.customerCode}
                    </div>
                    <h2 className="mt-3 truncate text-2xl font-semibold tracking-tight" title={report.header.customerName}>
                      {report.header.customerName}
                    </h2>
                    <p className="mt-1 text-sm text-muted-foreground">{report.header.city || "Cidade não informada"} · {report.header.linkedCompany || "Empresa não vinculada"}</p>
                  </div>
                  <Badge className={toneClass(report.health.tone)}>{report.header.commercialStatus}</Badge>
                </div>
                <div className="mt-5 grid grid-cols-2 gap-3 md:grid-cols-4">
                  <MiniMetric label="Última compra" value={formatDate(report.header.lastPurchaseDate)} />
                  <MiniMetric label="Dias sem comprar" value={String(report.header.daysWithoutPurchase)} />
                  <MiniMetric label="Frequência histórica" value={report.header.averageDaysBetweenPurchases == null ? "N/A" : `${formatDecimal(report.header.averageDaysBetweenPurchases)} dias`} />
                  <MiniMetric label="Status comercial" value={report.header.commercialStatus} />
                </div>
              </CardContent>
            </Card>

            <Card className="border-border/80 bg-card/95">
              <CardContent className="p-5">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-sm text-muted-foreground">Score comercial</p>
                    <p className="mt-2 text-5xl font-display font-semibold">{report.score.value}</p>
                  </div>
                  <ShieldCheck className="size-8 text-primary" />
                </div>
                <div className="mt-4 h-2 rounded-full bg-muted">
                  <div className="h-full rounded-full bg-primary" style={{ width: scoreWidth(report.score.value) }} />
                </div>
                <p className="mt-3 text-sm font-medium">{report.score.label}</p>
                <p className="mt-1 text-xs text-muted-foreground">{report.score.explanation}</p>
              </CardContent>
            </Card>

            <InsightBlock title="Saúde do Cliente" icon={AlertTriangle} block={report.health} />
          </section>

          <section className="grid grid-cols-1 gap-3 lg:grid-cols-3">
            <InsightBlock title="Tendência" icon={TrendingUp} block={report.trend} />
            <Card className="border-border/80 bg-card/95">
              <CardContent className="p-5">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-sm font-semibold">Potencial esperado</p>
                    <p className="mt-2 text-2xl font-display font-semibold">{report.potential.expectedRevenue == null ? "Sem base" : formatKpiCompactCurrency(report.potential.expectedRevenue)}</p>
                  </div>
                  <Target className="size-6 text-primary" />
                </div>
                <p className="mt-3 text-sm text-muted-foreground">{report.potential.explanation}</p>
                <p className="mt-2 text-xs text-muted-foreground">Volume esperado: {report.potential.expectedQuantity == null ? "N/A" : formatKpiCompactNumber(report.potential.expectedQuantity)}</p>
              </CardContent>
            </Card>
            <Card className="border-border/80 bg-card/95">
              <CardContent className="p-5">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-sm font-semibold">Dependência</p>
                    <p className="mt-2 text-xl font-semibold">{report.dependency.status}</p>
                  </div>
                  <Package className="size-6 text-primary" />
                </div>
                <p className="mt-3 text-sm text-muted-foreground">{report.dependency.explanation}</p>
                <p className="mt-2 text-xs text-muted-foreground">Produto líder: {formatPercent(report.dependency.topProductSharePercent)}</p>
              </CardContent>
            </Card>
          </section>

          <section className="grid grid-cols-1 gap-3 xl:grid-cols-[0.9fr_1.1fr]">
            <Card className="border-border/80 bg-card/95">
              <CardHeader>
                <CardTitle>Alertas inteligentes</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {report.alerts.map((alert) => (
                  <div key={`${alert.title}-${alert.detail}`} className="rounded-lg border border-border bg-surface p-3">
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm font-semibold">{alert.title}</p>
                      <Badge variant={severityVariant(alert.severity)}>{alert.severity}</Badge>
                    </div>
                    <p className="mt-2 text-sm text-muted-foreground">{alert.detail}</p>
                  </div>
                ))}
              </CardContent>
            </Card>

            <Card className="border-border/80 bg-card/95">
              <CardHeader>
                <CardTitle>Evolução temporal</CardTitle>
              </CardHeader>
              <CardContent>
                <ChartContainer config={{ revenue: { label: "Faturamento", color: "var(--primary)" }, quantity: { label: "Quantidade", color: "hsl(var(--chart-4))" } }} className="h-[320px] min-h-[320px] w-full">
                  <LineChart data={chartData} margin={{ left: 8, right: 12, top: 10, bottom: 20 }}>
                    <CartesianGrid vertical={false} />
                    <XAxis dataKey="periodStart" tickFormatter={(value) => formatMonth(String(value))} minTickGap={20} />
                    <YAxis yAxisId="left" width={86} tickFormatter={(value) => formatKpiCompactCurrency(Number(value))} />
                    <YAxis yAxisId="right" orientation="right" width={64} tickFormatter={(value) => formatKpiCompactNumber(Number(value))} />
                    <ChartTooltip content={<ChartTooltipContent labelFormatter={(value) => formatMonth(String(value))} formatter={(value, name) => name === "quantity" ? formatKpiCompactNumber(Number(value)) : formatCurrency(Number(value))} />} />
                    <Line yAxisId="left" dataKey="revenue" name="Faturamento" type="monotone" stroke="var(--color-revenue)" strokeWidth={2.4} dot={{ r: 3 }} />
                    <Line yAxisId="right" dataKey="quantity" name="Quantidade" type="monotone" stroke="var(--color-quantity)" strokeWidth={2.2} dot={{ r: 3 }} />
                  </LineChart>
                </ChartContainer>
              </CardContent>
            </Card>
          </section>

          <section className="grid grid-cols-1 gap-3 xl:grid-cols-2">
            <Card className="border-border/80 bg-card/95">
              <CardHeader>
                <CardTitle>Produtos sustentadores</CardTitle>
              </CardHeader>
              <CardContent>
                {report.products.length === 0 ? <SkeletonTable rows={3} columns={4} /> : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Produto</TableHead>
                        <TableHead>Participação</TableHead>
                        <TableHead>Quantidade</TableHead>
                        <TableHead>Faturamento</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {report.products.map((product) => (
                        <TableRow key={`${product.productCode}-${product.productDescription}`}>
                          <TableCell>{product.productDescription || product.productCode}</TableCell>
                          <TableCell>{formatPercent(product.sharePercent)}</TableCell>
                          <TableCell>{formatDecimal(product.quantity)}</TableCell>
                          <TableCell>{formatCurrency(product.revenue)}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>

            <Card className="border-border/80 bg-card/95">
              <CardHeader>
                <CardTitle>Recomendações comerciais</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {report.recommendations.map((recommendation) => (
                  <div key={`${recommendation.priority}-${recommendation.title}`} className="rounded-lg border border-border bg-surface p-4">
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm font-semibold">{recommendation.title}</p>
                      <Badge variant={recommendation.priority === "Alta" ? "destructive" : recommendation.priority === "Média" ? "secondary" : "outline"}>{recommendation.priority}</Badge>
                    </div>
                    <p className="mt-2 text-sm text-muted-foreground">{recommendation.detail}</p>
                  </div>
                ))}
              </CardContent>
            </Card>
          </section>

          <section className="grid grid-cols-1 gap-3 xl:grid-cols-[0.75fr_1.25fr]">
            <Card className="border-border/80 bg-card/95">
              <CardHeader>
                <CardTitle>Timeline comercial</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                {report.timeline.map((item) => (
                  <div key={`${item.date}-${item.orders}`} className="flex items-center justify-between gap-3 rounded-lg border border-border bg-surface p-3">
                    <div>
                      <p className="text-sm font-semibold">{formatDate(item.date)}</p>
                      <p className="text-xs text-muted-foreground">{item.orders} pedido(s)</p>
                    </div>
                    <p className="text-sm font-medium">{formatCurrency(item.revenue)}</p>
                  </div>
                ))}
              </CardContent>
            </Card>

            <Card className="border-border/80 bg-card/95">
              <CardHeader>
                <CardTitle>Comparativos</CardTitle>
              </CardHeader>
              <CardContent>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Período</TableHead>
                      <TableHead>Faturamento</TableHead>
                      <TableHead>Quantidade</TableHead>
                      <TableHead>Pedidos</TableHead>
                      <TableHead>Ticket médio</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {report.comparisons.map((item) => (
                      <TableRow key={item.label}>
                        <TableCell>{item.label}</TableCell>
                        <TableCell>{formatPercent(item.revenueVariationPercent)}</TableCell>
                        <TableCell>{formatPercent(item.quantityVariationPercent)}</TableCell>
                        <TableCell>{formatPercent(item.ordersVariationPercent)}</TableCell>
                        <TableCell>{formatPercent(item.averageTicketVariationPercent)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          </section>
        </>
      )}
    </div>
  );
}

function MiniMetric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-border bg-surface p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 truncate text-sm font-semibold" title={value}>{value}</p>
    </div>
  );
}

function InsightBlock({ title, icon: Icon, block }: { title: string; icon: LucideIcon; block: CustomerCommercialHealthBlock }) {
  return (
    <Card className="border-border/80 bg-card/95">
      <CardContent className="p-5">
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="text-sm text-muted-foreground">{title}</p>
            <p className="mt-2 text-xl font-semibold">{block.status}</p>
          </div>
          <span className={`inline-flex size-10 items-center justify-center rounded-md border ${toneClass(block.tone)}`}>
            <Icon className="size-5" />
          </span>
        </div>
        <p className="mt-3 text-sm font-medium">{block.summary}</p>
        <p className="mt-2 text-sm text-muted-foreground">{block.detail}</p>
      </CardContent>
    </Card>
  );
}
