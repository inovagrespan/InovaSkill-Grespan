import { useEffect, useState } from "react";
import {
  Building2,
  CalendarDays,
  CircleAlert,
  ChevronRight,
  DollarSign,
  Hash,
  Gift,
  MapPin,
  ReceiptText,
  RotateCcw,
  Scale,
  ShoppingBag,
  TrendingUp,
} from "lucide-react";
import { CartesianGrid, Line, LineChart, XAxis, YAxis } from "recharts";
import { FiscalDocumentDialog } from "@/components/FiscalDocumentDialog";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { KpiCard } from "@/components/ui/kpi-card";
import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import { SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  fetchCustomerConsumptionSummary,
  fetchCustomerProjection,
  type CustomerConsumptionSummary,
  type CustomerProjectionResponse,
  type CustomerProjectionSeries,
} from "@/lib/importer-api";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";

const weightFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 3 });
const percentageFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 });
const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const compactWeight = (value: number) => `${formatKpiCompactNumber(value)} kg`;
const signedPercentage = (value: number) =>
  `${value > 0 ? "+" : ""}${percentageFormatter.format(value)}%`;
type ChartMetric = "salesWeightKg" | "salesDocumentCount" | "averageSalesWeightPerDocumentKg" |
  "calculatedSalesAmount" | "returnWeightKg" | "bonusWeightKg";

const chartLabels: Record<ChartMetric, string> = {
  salesWeightKg: "Peso vendido por mês",
  salesDocumentCount: "Notas de venda por mês",
  averageSalesWeightPerDocumentKg: "Peso médio por nota de venda",
  calculatedSalesAmount: "Faturamento calculado por mês",
  returnWeightKg: "Peso de devoluções por mês",
  bonusWeightKg: "Peso de bonificações por mês",
};

const projectionQualityLabels: Record<CustomerProjectionSeries["quality"], string> = {
  HIGH: "Alta",
  MODERATE: "Moderada",
  LOW: "Baixa",
  INSUFFICIENT: "Insuficiente",
};

function projectionQualityClass(quality: CustomerProjectionSeries["quality"]): string {
  if (quality === "HIGH") return "border-[color:var(--success)]/35 bg-[color-mix(in_srgb,var(--success)_10%,transparent)] text-[var(--success)]";
  if (quality === "MODERATE") return "border-[color:var(--warning)]/35 bg-[color-mix(in_srgb,var(--warning)_10%,transparent)] text-[var(--warning)]";
  return "border-[color:var(--error)]/35 bg-[color-mix(in_srgb,var(--error)_10%,transparent)] text-[var(--error)]";
}

function ProjectionChart({
  title,
  projection,
  series,
  kind,
}: {
  title: string;
  projection: CustomerProjectionResponse;
  series: CustomerProjectionSeries;
  kind: "weight" | "revenue";
}) {
  const historical = projection.historical ?? [];
  const actualKey = kind === "weight" ? "salesWeightKg" : "calculatedSalesAmount";
  const lastHistorical = historical.at(-1);
  const points = [
    ...historical.map(point => ({ month: point.month, actual: point[actualKey], forecast: null, lower: null, upper: null })),
    ...(lastHistorical ? [{
      month: lastHistorical.month,
      actual: null,
      forecast: lastHistorical[actualKey],
      lower: null,
      upper: null,
    }] : []),
    ...series.forecast.map(point => ({
      month: point.month,
      actual: null,
      forecast: point.forecast,
      lower: point.lowerBound,
      upper: point.upperBound,
    })),
  ];
  const formatValue = (value: number) => kind === "revenue"
    ? currencyFormatter.format(value)
    : `${weightFormatter.format(value)} kg`;
  return (
    <div className="rounded-xl border border-border bg-surface p-4">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h4 className="font-semibold">{title}</h4>
        <Badge variant="outline" className={projectionQualityClass(series.quality)}>
          R² {percentageFormatter.format(series.rSquared * 100)}%
        </Badge>
      </div>
      <ChartContainer config={{
        actual: { label: "Realizado", color: "var(--primary)" },
        forecast: { label: "Projetado", color: "var(--info)" },
      }} className="h-[280px] min-h-[280px]">
        <LineChart data={points} margin={{ top: 10, right: 16, left: 6, bottom: 4 }}>
          <CartesianGrid vertical={false} stroke="var(--border)" />
          <XAxis dataKey="month" tickLine={false} axisLine={false}
            tickFormatter={value => new Date(`${String(value).slice(0, 7)}-01T12:00:00`).toLocaleDateString("pt-BR", { month: "short" })} />
          <YAxis tickLine={false} axisLine={false} width={76}
            tickFormatter={value => kind === "revenue"
              ? new Intl.NumberFormat("pt-BR", { notation: "compact", style: "currency", currency: "BRL" }).format(Number(value))
              : new Intl.NumberFormat("pt-BR", { notation: "compact" }).format(Number(value))} />
          <ChartTooltip content={<ChartTooltipContent
            labelFormatter={value => new Date(`${String(value).slice(0, 7)}-01T12:00:00`).toLocaleDateString("pt-BR", { month: "long", year: "numeric" })}
            formatter={(value, name) => <span className="font-semibold">{name}: {formatValue(Number(value))}</span>} />} />
          <Line type="monotone" dataKey="actual" name="Realizado" connectNulls={false}
            stroke="var(--primary)" strokeWidth={3} dot={{ r: 2.5 }} isAnimationActive={false} />
          <Line type="monotone" dataKey="forecast" name="Projetado" connectNulls
            stroke="var(--info)" strokeWidth={3} strokeDasharray="7 5" dot={{ r: 3 }} isAnimationActive={false} />
          <Line type="monotone" dataKey="lower" name="Limite inferior" connectNulls
            stroke="var(--muted-foreground)" strokeWidth={1.5} strokeDasharray="3 5" dot={false} isAnimationActive={false} />
          <Line type="monotone" dataKey="upper" name="Limite superior" connectNulls
            stroke="var(--muted-foreground)" strokeWidth={1.5} strokeDasharray="3 5" dot={false} isAnimationActive={false} />
        </LineChart>
      </ChartContainer>
    </div>
  );
}

function formatDate(value: string | null | undefined): string {
  if (!value) return "Sem registro";
  const date = new Date(`${value}T12:00:00`);
  return Number.isNaN(date.getTime()) ? "Data indisponível" : date.toLocaleDateString("pt-BR");
}

function operationLabel(category: string, description: string): string {
  return description || ({
    Sale: "Venda",
    Return: "Devolução",
    Bonus: "Bonificação",
    Loan: "Comodato",
    Exchange: "Troca",
    Unknown: "Desconhecida",
  }[category] ?? category);
}

export function CustomerConsumptionDialog({
  id,
  open,
  onOpenChange,
}: {
  id: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const [data, setData] = useState<CustomerConsumptionSummary | null>(null);
  const [projection, setProjection] = useState<CustomerProjectionResponse | null>(null);
  const [error, setError] = useState("");
  const [documentId, setDocumentId] = useState<string | null>(null);
  const [chartMetric, setChartMetric] = useState<ChartMetric | null>(null);

  useEffect(() => {
    if (!open || !id) return;
    setData(null);
    setProjection(null);
    setError("");
    Promise.all([fetchCustomerConsumptionSummary(id), fetchCustomerProjection(id)])
      .then(([summary, projectionResult]) => {
        setData(summary);
        setProjection(projectionResult);
      })
      .catch(reason => setError((reason as Error).message));
  }, [id, open]);

  const variation = data?.metrics.variationPercentage ?? null;
  const variationText = variation === null
    ? (data?.metrics.variationStatus === "NEW_ACTIVITY" ? "Nova atividade" : "Sem comparação")
    : `${variation > 0 ? "+" : ""}${percentageFormatter.format(variation)}%`;

  return (
    <>
      <Dialog open={open && documentId === null && chartMetric === null} onOpenChange={onOpenChange}>
        <DialogContent className="custom-scrollbar max-h-[92vh] w-[95vw] max-w-6xl overflow-y-auto border-border/80 p-0">
          <DialogHeader className="border-b border-border bg-[linear-gradient(135deg,var(--soft-red-background),transparent_65%)] px-6 py-6 pr-14 sm:px-8">
            <div className="mb-3 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-primary">
              <Building2 className="size-4" />
              Visão do cliente
            </div>
            <DialogTitle className="text-2xl font-display sm:text-3xl">
              {data?.customer.tradeName || "Detalhes do cliente"}
            </DialogTitle>
            <DialogDescription className="text-sm sm:text-base">
              {data?.customer.legalName || "Consumo histórico em vendas e últimas movimentações."}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-6 px-6 pb-7 sm:px-8">
            {error ? (
              <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>
            ) : !data ? (
              <SkeletonTable rows={5} columns={4} />
            ) : (
              <>
                <section className="-mt-3 grid gap-3 rounded-xl border border-border bg-surface p-4 shadow-xs sm:grid-cols-2 lg:grid-cols-4">
                  <div className="flex items-center gap-3">
                    <span className="flex size-9 items-center justify-center rounded-lg bg-muted text-muted-foreground"><Hash className="size-4" /></span>
                    <div><p className="text-xs text-muted-foreground">Código / Loja</p><p className="font-mono text-sm font-semibold">{data.customer.externalCode} / {data.customer.branchCode}</p></div>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="flex size-9 items-center justify-center rounded-lg bg-muted text-muted-foreground"><Building2 className="size-4" /></span>
                    <div><p className="text-xs text-muted-foreground">{data.customer.documentType}</p><p className="text-sm font-semibold">{data.customer.documentNumber || "Não informado"}</p></div>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="flex size-9 items-center justify-center rounded-lg bg-muted text-muted-foreground"><MapPin className="size-4" /></span>
                    <div><p className="text-xs text-muted-foreground">Localização</p><p className="text-sm font-semibold">{data.customer.municipalityName} / {data.customer.stateCode}</p></div>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="flex size-9 items-center justify-center rounded-lg bg-muted text-muted-foreground"><ShoppingBag className="size-4" /></span>
                    <div><p className="text-xs text-muted-foreground">Tipo de cliente</p><p className="text-sm font-semibold">{data.customer.customerType || "Não informado"}</p></div>
                  </div>
                </section>

                <section className="space-y-4">
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
                    <div>
                      <h3 className="font-display text-lg font-semibold">Projeção de impacto</h3>
                      <p className="text-xs text-muted-foreground">
                        Tendência linear sobre 12 meses completos, com previsão para os próximos 3 meses e faixa de 95%.
                      </p>
                    </div>
                    {projection?.available && projection.weight ? (
                      <Badge variant="outline" className={projectionQualityClass(projection.weight.quality)}>
                        Qualidade do peso: {projectionQualityLabels[projection.weight.quality]}
                      </Badge>
                    ) : null}
                  </div>
                  {!projection?.available || !projection.weight || !projection.revenue ? (
                    <Alert>
                      <CircleAlert className="size-4" />
                      <AlertDescription>Histórico fiscal insuficiente para calcular projeções.</AlertDescription>
                    </Alert>
                  ) : (
                    <>
                      {(projection.weight.quality === "LOW" || projection.weight.quality === "INSUFFICIENT" ||
                        projection.revenue.quality === "LOW" || projection.revenue.quality === "INSUFFICIENT") ? (
                        <Alert>
                          <CircleAlert className="size-4" />
                          <AlertDescription>
                            Projeção exploratória: a série possui baixa regularidade ou poucos meses ativos.
                            Use a faixa estimada e não apenas o valor central.
                          </AlertDescription>
                        </Alert>
                      ) : null}
                      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                        <KpiCard className="w-full" title="Peso projetado — próximo mês"
                          value={compactWeight(projection.weight.forecast[0]?.forecast ?? 0)}
                          valueTooltip={`${weightFormatter.format(projection.weight.forecast[0]?.forecast ?? 0)} kg`}
                          periodLabel={`Faixa: ${weightFormatter.format(projection.weight.forecast[0]?.lowerBound ?? 0)}–${weightFormatter.format(projection.weight.forecast[0]?.upperBound ?? 0)} kg`}
                          icon={Scale} showPercentageChange={false} />
                        <KpiCard className="w-full" title="Variação mensal do peso"
                          value={`${formatKpiCompactNumber(projection.weight.monthlyChange)} kg/mês`}
                          valueTooltip={`${projection.weight.monthlyChange > 0 ? "+" : ""}${weightFormatter.format(projection.weight.monthlyChange)} kg/mês`}
                          periodLabel={`${projection.weight.monthlyChangePercentage == null ? "Sem base percentual" : `${signedPercentage(projection.weight.monthlyChangePercentage)} da média`}`}
                          periodLabelClassName={projection.weight.monthlyChange > 0 ? "text-[var(--success)]" : projection.weight.monthlyChange < 0 ? "text-[var(--error)]" : undefined}
                          showPercentageChange={false} />
                        <KpiCard className="w-full" title="Faturamento projetado — próximo mês"
                          value={formatKpiCompactCurrency(projection.revenue.forecast[0]?.forecast ?? 0)}
                          valueTooltip={currencyFormatter.format(projection.revenue.forecast[0]?.forecast ?? 0)}
                          periodLabel={`Faixa: ${currencyFormatter.format(projection.revenue.forecast[0]?.lowerBound ?? 0)}–${currencyFormatter.format(projection.revenue.forecast[0]?.upperBound ?? 0)}`}
                          icon={DollarSign} showPercentageChange={false} />
                        <KpiCard className="w-full" title="Variação mensal do faturamento"
                          value={`${projection.revenue.monthlyChange > 0 ? "+" : ""}${formatKpiCompactCurrency(projection.revenue.monthlyChange)}/mês`}
                          valueTooltip={`${projection.revenue.monthlyChange > 0 ? "+" : ""}${currencyFormatter.format(projection.revenue.monthlyChange)}/mês`}
                          periodLabel={`${projection.revenue.monthlyChangePercentage == null ? "Sem base percentual" : `${signedPercentage(projection.revenue.monthlyChangePercentage)} da média`}`}
                          periodLabelClassName={projection.revenue.monthlyChange > 0 ? "text-[var(--success)]" : projection.revenue.monthlyChange < 0 ? "text-[var(--error)]" : undefined}
                          showPercentageChange={false} />
                      </div>
                      <div className="grid gap-4 xl:grid-cols-2">
                        <ProjectionChart title="Peso mensal: realizado × projetado"
                          projection={projection} series={projection.weight} kind="weight" />
                        <ProjectionChart title="Faturamento mensal: realizado × projetado"
                          projection={projection} series={projection.revenue} kind="revenue" />
                      </div>
                      <p className="text-[11px] text-muted-foreground">
                        Base de {formatDate(projection.baseStartMonth)} a {formatDate(projection.baseEndMonth)}.
                        O mês parcial da cobertura fiscal ({formatDate(projection.sourceCoverageDate)}) não entra no ajuste.
                      </p>
                    </>
                  )}
                </section>

                <section>
                  <div className="mb-3">
                    <h3 className="font-display text-lg font-semibold">Indicadores de consumo</h3>
                    <p className="text-xs text-muted-foreground">Somente peso bruto de operações classificadas como venda.</p>
                  </div>
                  <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                    <button type="button" className="text-left" onClick={() => setChartMetric("salesWeightKg")} aria-label="Ver evolução mensal do peso vendido">
                    <KpiCard className="w-full" title="Consumo em vendas — 30d"
                      value={compactWeight(data.metrics.salesWeightLast30Days)}
                      valueTooltip={`${weightFormatter.format(data.metrics.salesWeightLast30Days)} quilogramas`}
                      periodLabel="Últimos 30 dias" icon={Scale} showPercentageChange={false} />
                    </button>
                    <button type="button" className="text-left" onClick={() => setChartMetric("salesWeightKg")} aria-label="Ver evolução mensal da variação de peso">
                    <KpiCard className="w-full" title="Variação histórica"
                      value={variationText} periodLabel="Últimos 30d vs. 30d anteriores"
                      icon={TrendingUp} showPercentageChange={false}
                      valueClassName={variation !== null && variation < 0 ? "text-[var(--error)]" : variation !== null && variation > 0 ? "text-[var(--success)]" : undefined} />
                    </button>
                    <button type="button" className="text-left" onClick={() => setChartMetric("salesWeightKg")} aria-label="Ver evolução da média mensal de peso">
                    <KpiCard className="w-full" title="Média mensal de peso"
                      value={`${formatKpiCompactNumber(data.metrics.averageMonthlySalesWeight12Months)} kg/mês`}
                      valueTooltip={`${weightFormatter.format(data.metrics.averageMonthlySalesWeight12Months)} quilogramas por mês`}
                      periodLabel="Média dos últimos 12 meses" icon={CalendarDays}
                      showPercentageChange={false} />
                    </button>
                    <KpiCard className="w-full" title="Última compra"
                      value={formatDate(data.metrics.lastPurchaseDate)}
                      periodLabel="Última operação classificada como venda" icon={ShoppingBag}
                      showPercentageChange={false} />
                    <button type="button" className="text-left" onClick={() => setChartMetric("salesDocumentCount")} aria-label="Ver evolução mensal das notas de venda">
                    <KpiCard className="w-full" title="Notas de venda — 30d"
                      value={formatKpiCompactNumber(data.metrics.saleDocumentsLast30Days)}
                      valueTooltip={weightFormatter.format(data.metrics.saleDocumentsLast30Days)}
                      periodLabel="Documentos nos últimos 30 dias" icon={ReceiptText}
                      showPercentageChange={false} />
                    </button>
                    <button type="button" className="text-left" onClick={() => setChartMetric("averageSalesWeightPerDocumentKg")} aria-label="Ver evolução mensal do peso médio por nota">
                    <KpiCard className="w-full" title="Peso médio por nota"
                      value={compactWeight(data.metrics.averageSalesWeightPerDocument12Months)}
                      valueTooltip={`${weightFormatter.format(data.metrics.averageSalesWeightPerDocument12Months)} kg`}
                      periodLabel="Média dos últimos 12 meses" icon={Scale}
                      showPercentageChange={false} />
                    </button>
                    <button type="button" className="text-left" onClick={() => setChartMetric("calculatedSalesAmount")} aria-label="Ver evolução mensal do faturamento calculado">
                    <KpiCard className="w-full" title="Faturamento médio mensal"
                      value={formatKpiCompactCurrency(data.metrics.averageMonthlyCalculatedSalesAmount12Months)}
                      valueTooltip={`${currencyFormatter.format(data.metrics.averageMonthlyCalculatedSalesAmount12Months)} por mês`}
                      periodLabel="Quantidade × valor unitário • 12 meses" icon={DollarSign}
                      showPercentageChange={false} />
                    </button>
                    <button type="button" className="text-left" onClick={() => setChartMetric("returnWeightKg")} aria-label="Ver evolução mensal das devoluções">
                    <KpiCard className="w-full" title="Devoluções — 12 meses"
                      value={compactWeight(data.metrics.returnWeight12Months)}
                      valueTooltip={`${weightFormatter.format(data.metrics.returnWeight12Months)} kg`}
                      periodLabel="Fluxo reverso, fora do consumo" icon={RotateCcw}
                      showPercentageChange={false} />
                    </button>
                    <button type="button" className="text-left" onClick={() => setChartMetric("bonusWeightKg")} aria-label="Ver evolução mensal das bonificações">
                    <KpiCard className="w-full" title="Bonificações — 12 meses"
                      value={compactWeight(data.metrics.bonusWeight12Months)}
                      valueTooltip={`${weightFormatter.format(data.metrics.bonusWeight12Months)} kg`}
                      periodLabel="Movimento físico, fora do consumo" icon={Gift}
                      showPercentageChange={false} />
                    </button>
                  </div>
                  <p className="mt-2 text-[11px] text-muted-foreground">Clique nos indicadores com evolução disponível para abrir o histórico mensal.</p>
                </section>

                <section>
                  <div className="mb-3 flex items-end justify-between gap-3">
                    <div>
                      <h3 className="font-display text-lg font-semibold">Últimas movimentações</h3>
                      <p className="text-xs text-muted-foreground">Vendas, devoluções e demais operações vinculadas ao cliente.</p>
                    </div>
                    <Badge variant="outline">{data.recentMovements.length} registro(s)</Badge>
                  </div>
                  <div className="overflow-hidden rounded-xl border border-border">
                    <Table>
                      <TableHeader>
                        <TableRow className="bg-muted/45">
                          <TableHead>Data</TableHead>
                          <TableHead>Documento</TableHead>
                          <TableHead>Operação</TableHead>
                          <TableHead className="text-right">Itens</TableHead>
                          <TableHead className="text-right">Peso bruto</TableHead>
                          <TableHead className="w-12"><span className="sr-only">Abrir</span></TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {data.recentMovements.length === 0 ? (
                          <TableRow><TableCell colSpan={6} className="py-10 text-center text-muted-foreground">Nenhuma movimentação vinculada.</TableCell></TableRow>
                        ) : data.recentMovements.map(item => (
                          <TableRow key={item.id} className="cursor-pointer transition-colors hover:bg-muted/55"
                            tabIndex={0} onClick={() => setDocumentId(item.id)}
                            onKeyDown={event => { if (event.key === "Enter") setDocumentId(item.id); }}>
                            <TableCell className="whitespace-nowrap font-medium">{formatDate(item.issueDate)}</TableCell>
                            <TableCell className="font-mono">{item.documentNumber}{item.series ? ` / ${item.series}` : ""}</TableCell>
                            <TableCell><Badge variant={item.operationCategory === "Sale" ? "default" : "outline"}>{operationLabel(item.operationCategory, item.operationDescription)}</Badge></TableCell>
                            <TableCell className="text-right">{item.itemCount}</TableCell>
                            <TableCell className="whitespace-nowrap text-right font-semibold">{weightFormatter.format(item.grossWeightKg)} kg</TableCell>
                            <TableCell><ChevronRight className="size-4 text-muted-foreground" /></TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </div>
                </section>
              </>
            )}
          </div>
        </DialogContent>
      </Dialog>
      <Dialog open={chartMetric !== null} onOpenChange={next => { if (!next) setChartMetric(null); }}>
        <DialogContent className="max-w-4xl">
          <DialogHeader>
            <DialogTitle>{chartMetric ? chartLabels[chartMetric] : "Evolução mensal"}</DialogTitle>
            <DialogDescription>Últimos 12 meses • {data?.customer.tradeName || "Cliente"}</DialogDescription>
          </DialogHeader>
          {data && chartMetric ? (
            <ChartContainer
              config={{ value: { label: chartLabels[chartMetric], color: "var(--primary)" } }}
              className="h-[340px] min-h-[340px]"
            >
              <LineChart data={data.monthlyTimeline} margin={{ top: 12, right: 18, left: 8, bottom: 8 }}>
                <CartesianGrid vertical={false} stroke="var(--border)" />
                <XAxis dataKey="month" tickLine={false} axisLine={false}
                  tickFormatter={value => new Date(`${value}-01T12:00:00`).toLocaleDateString("pt-BR", { month: "short" })} />
                <YAxis tickLine={false} axisLine={false} width={72}
                  tickFormatter={value => chartMetric === "salesDocumentCount"
                    ? weightFormatter.format(Number(value))
                    : chartMetric === "calculatedSalesAmount"
                      ? currencyFormatter.format(Number(value))
                      : `${weightFormatter.format(Number(value))} kg`} />
                <ChartTooltip content={<ChartTooltipContent
                  labelFormatter={value => new Date(`${value}-01T12:00:00`).toLocaleDateString("pt-BR", { month: "long", year: "numeric" })}
                  formatter={value => (
                    <span className="font-semibold">
                      {chartMetric === "salesDocumentCount"
                        ? weightFormatter.format(Number(value))
                        : chartMetric === "calculatedSalesAmount"
                          ? currencyFormatter.format(Number(value))
                          : `${weightFormatter.format(Number(value))} kg`}
                    </span>
                  )} />} />
                <Line type="monotone" dataKey={chartMetric} name={chartLabels[chartMetric]}
                  stroke="var(--primary)" strokeWidth={3} dot={{ r: 3, fill: "var(--primary)" }}
                  activeDot={{ r: 5 }} isAnimationActive={false} />
              </LineChart>
            </ChartContainer>
          ) : null}
        </DialogContent>
      </Dialog>
      <FiscalDocumentDialog id={documentId} open={documentId !== null}
        onOpenChange={next => { if (!next) setDocumentId(null); }} />
    </>
  );
}
