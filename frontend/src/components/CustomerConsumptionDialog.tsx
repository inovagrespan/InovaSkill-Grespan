import { useEffect, useState } from "react";
import {
  Building2,
  CalendarDays,
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
  type CustomerConsumptionSummary,
} from "@/lib/importer-api";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";

const weightFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 3 });
const percentageFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 });
const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const compactWeight = (value: number) => `${formatKpiCompactNumber(value)} kg`;
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

const addressStatusLabels = {
  INVALID_DOCUMENT: "CNPJ inválido",
  NOT_FOUND: "Endereço não encontrado",
  FAILED: "Falha na consulta do endereço",
} as const;

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
  const [error, setError] = useState("");
  const [documentId, setDocumentId] = useState<string | null>(null);
  const [chartMetric, setChartMetric] = useState<ChartMetric | null>(null);

  useEffect(() => {
    if (!open || !id) return;
    setData(null);
    setError("");
    fetchCustomerConsumptionSummary(id)
      .then(setData)
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
                  <div className="flex items-start gap-3 border-t border-border pt-3 sm:col-span-2 lg:col-span-4">
                    <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground"><MapPin className="size-4" /></span>
                    <div className="min-w-0">
                      <p className="text-xs text-muted-foreground">Endereço cadastral</p>
                      {!data.customer.registrationAddress ? (
                        <p className="text-sm font-semibold">Não consultado</p>
                      ) : data.customer.registrationAddress.status !== "RESOLVED" ? (
                        <p className="text-sm font-semibold">
                          {addressStatusLabels[data.customer.registrationAddress.status]}
                        </p>
                      ) : (
                        <div className="text-sm font-semibold">
                          <p>{[data.customer.registrationAddress.street, data.customer.registrationAddress.number]
                            .filter(Boolean).join(", ") || "Logradouro não informado"}</p>
                          <p className="font-normal text-muted-foreground">
                            {[data.customer.registrationAddress.neighborhood, data.customer.registrationAddress.complement]
                              .filter(Boolean).join(" · ") || "Bairro e complemento não informados"}
                          </p>
                          <p className="font-normal text-muted-foreground">
                            {[data.customer.registrationAddress.city, data.customer.registrationAddress.stateCode]
                              .filter(Boolean).join(" / ")}
                            {data.customer.registrationAddress.postalCode
                              ? ` · CEP ${data.customer.registrationAddress.postalCode}` : ""}
                          </p>
                        </div>
                      )}
                    </div>
                  </div>
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
