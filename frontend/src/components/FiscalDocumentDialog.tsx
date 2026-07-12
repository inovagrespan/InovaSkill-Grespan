import { useEffect, useState } from "react";
import {
  Building2,
  CalendarDays,
  DollarSign,
  FileText,
  Hash,
  Layers3,
  MapPin,
  Package,
  Scale,
  TrendingUp,
} from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { KpiCard } from "@/components/ui/kpi-card";
import { SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { fetchFiscalDocument, type FiscalDocumentDetails } from "@/lib/importer-api";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";

const numberFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 3 });
const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const percentageFormatter = new Intl.NumberFormat("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 });

function formatDate(value: string | null | undefined): string {
  if (!value) return "Data não informada";
  const date = new Date(`${value}T12:00:00`);
  return Number.isNaN(date.getTime()) ? "Data indisponível" : date.toLocaleDateString("pt-BR");
}

function formatSignedPercentage(value: number | null): string {
  if (value == null) return "N/A";
  return `${value > 0 ? "+" : ""}${percentageFormatter.format(value)}%`;
}

function commercialQualityTone(classification: string): "neutral" | "success" | "danger" | "info" {
  if (classification === "Boa venda") return "success";
  if (classification === "Venda de atenção") return "danger";
  if (classification === "Sem histórico suficiente" || classification === "Não aplicável") return "info";
  return "neutral";
}

export function FiscalDocumentDialog({
  id,
  open,
  onOpenChange,
}: {
  id: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const [data, setData] = useState<FiscalDocumentDetails | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!open || !id) return;
    setData(null);
    setError("");
    fetchFiscalDocument(id).then(setData).catch(reason => setError((reason as Error).message));
  }, [id, open]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="custom-scrollbar max-h-[92vh] w-[96vw] max-w-6xl overflow-y-auto border-border/80 p-0">
        <DialogHeader className="border-b border-border bg-[linear-gradient(135deg,var(--soft-red-background),transparent_65%)] px-6 py-6 pr-14 sm:px-8">
          <div className="mb-3 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-primary">
            <FileText className="size-4" />
            Documento fiscal
          </div>
          <DialogTitle className="text-2xl font-display sm:text-3xl">
            {data ? `Nota ${data.documentNumber}${data.series ? ` — Série ${data.series}` : ""}` : "Detalhes da nota"}
          </DialogTitle>
          <DialogDescription className="flex flex-wrap items-center gap-2 text-sm sm:text-base">
            {data ? (
              <>
                <span>{formatDate(data.issueDate)}</span>
                <span aria-hidden="true">•</span>
                <Badge variant={data.operationCategory === "Sale" ? "default" : "outline"}>
                  {data.operationDescription || data.operationCategory}
                </Badge>
              </>
            ) : "Identificação, totais calculados e itens do documento."}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-6 px-6 pb-7 sm:px-8">
          {error ? (
            <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>
          ) : !data ? (
            <SkeletonTable rows={5} columns={6} />
          ) : (
            <>
              <section className="-mt-3 grid gap-3 rounded-xl border border-border bg-surface p-4 shadow-xs sm:grid-cols-2 lg:grid-cols-4">
                <div className="flex items-center gap-3">
                  <span className="flex size-9 items-center justify-center rounded-lg bg-muted text-muted-foreground"><Building2 className="size-4" /></span>
                  <div className="min-w-0"><p className="text-xs text-muted-foreground">Cliente</p><p className="truncate text-sm font-semibold" title={data.customerNameAtIssue}>{data.customerNameAtIssue || "Não informado"}</p></div>
                </div>
                <div className="flex items-center gap-3">
                  <span className="flex size-9 items-center justify-center rounded-lg bg-muted text-muted-foreground"><Hash className="size-4" /></span>
                  <div><p className="text-xs text-muted-foreground">Código / Loja</p><p className="font-mono text-sm font-semibold">{data.customerCodeAtIssue || "-"} / {data.branchCodeAtIssue || "-"}</p></div>
                </div>
                <div className="flex items-center gap-3">
                  <span className="flex size-9 items-center justify-center rounded-lg bg-muted text-muted-foreground"><MapPin className="size-4" /></span>
                  <div className="min-w-0"><p className="text-xs text-muted-foreground">Localização</p><p className="truncate text-sm font-semibold">{data.cityNameAtIssue || "Não informada"}{data.stateCodeAtIssue ? ` / ${data.stateCodeAtIssue}` : ""}</p></div>
                </div>
                <div className="flex items-center gap-3">
                  <span className="flex size-9 items-center justify-center rounded-lg bg-muted text-muted-foreground"><CalendarDays className="size-4" /></span>
                  <div><p className="text-xs text-muted-foreground">Emissão</p><p className="text-sm font-semibold">{formatDate(data.issueDate)}</p></div>
                </div>
                {data.originalDocumentNumber ? (
                  <div className="flex items-center gap-3 sm:col-span-2 lg:col-span-4">
                    <span className="flex size-9 items-center justify-center rounded-lg bg-muted text-muted-foreground"><FileText className="size-4" /></span>
                    <div><p className="text-xs text-muted-foreground">Documento original</p><p className="font-mono text-sm font-semibold">{data.originalDocumentNumber}</p></div>
                  </div>
                ) : null}
              </section>

              <section>
                <div className="mb-3">
                  <h3 className="font-display text-lg font-semibold">Resumo da nota</h3>
                  <p className="text-xs text-muted-foreground">Totais derivados dos itens; o valor usa quantidade × valor unitário.</p>
                </div>
                <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                  <KpiCard className="w-full" title="Itens"
                    value={formatKpiCompactNumber(data.itemCount)}
                    valueTooltip={numberFormatter.format(data.itemCount)} periodLabel="Linhas de produto"
                    icon={Layers3} showPercentageChange={false} />
                  <KpiCard className="w-full" title="Quantidade total"
                    value={formatKpiCompactNumber(data.totalQuantity)}
                    valueTooltip={numberFormatter.format(data.totalQuantity)} periodLabel="Soma das quantidades"
                    icon={Package} showPercentageChange={false} />
                  <KpiCard className="w-full" title="Peso bruto"
                    value={`${formatKpiCompactNumber(data.grossWeightKg)} kg`}
                    valueTooltip={`${numberFormatter.format(data.grossWeightKg)} kg`} periodLabel="Soma do peso dos itens"
                    icon={Scale} showPercentageChange={false} />
                  <KpiCard className="w-full" title="Valor calculado"
                    value={formatKpiCompactCurrency(data.calculatedTotalAmount)}
                    valueTooltip={currencyFormatter.format(data.calculatedTotalAmount)}
                    periodLabel="Quantidade × valor unitário" icon={DollarSign}
                    showPercentageChange={false} allowWrapValue />
                </div>
              </section>

              <section>
                <div className="mb-3">
                  <h3 className="font-display text-lg font-semibold">Qualidade comercial da venda</h3>
                  <p className="text-xs text-muted-foreground">
                    Compara o ticket desta nota com o ticket médio histórico de vendas do mesmo cliente.
                  </p>
                </div>
                <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                  <KpiCard className="w-full" title="Qualidade comercial"
                    value={data.commercialQuality.classification}
                    valueTooltip={data.commercialQuality.reason}
                    periodLabel={data.commercialQuality.reason}
                    icon={TrendingUp} showPercentageChange={false}
                    tone={commercialQualityTone(data.commercialQuality.classification)}
                    allowWrapValue />
                  <KpiCard className="w-full" title="Ticket médio do cliente"
                    value={data.commercialQuality.customerAverageTicket == null ? "N/A" : formatKpiCompactCurrency(data.commercialQuality.customerAverageTicket)}
                    valueTooltip={data.commercialQuality.customerAverageTicket == null ? "Sem histórico suficiente" : currencyFormatter.format(data.commercialQuality.customerAverageTicket)}
                    periodLabel={`${data.commercialQuality.historicalSaleDocumentCount} venda(s) anteriores na base`}
                    icon={DollarSign} showPercentageChange={false}
                    tone={data.commercialQuality.customerAverageTicket == null ? "info" : "neutral"}
                    allowWrapValue />
                  <KpiCard className="w-full" title="Ticket da NF vs média"
                    value={formatSignedPercentage(data.commercialQuality.ticketVariationPercentage)}
                    valueTooltip={formatSignedPercentage(data.commercialQuality.ticketVariationPercentage)}
                    periodLabel="Diferença contra o histórico do cliente"
                    icon={TrendingUp} showPercentageChange={false}
                    tone={commercialQualityTone(data.commercialQuality.classification)} />
                </div>
              </section>

              <section>
                <div className="mb-3 flex items-end justify-between gap-3">
                  <div>
                    <h3 className="font-display text-lg font-semibold">Itens da nota</h3>
                    <p className="text-xs text-muted-foreground">Produtos e valores que formam os totais do documento.</p>
                  </div>
                  <Badge variant="outline">{data.itemCount} item(ns)</Badge>
                </div>
                <div className="overflow-x-auto rounded-xl border border-border">
                  <Table className="min-w-[960px]">
                    <TableHeader>
                      <TableRow className="bg-muted/45">
                        <TableHead className="w-20">Item</TableHead>
                        <TableHead className="w-32">Produto</TableHead>
                        <TableHead>Descrição</TableHead>
                        <TableHead>Grupo</TableHead>
                        <TableHead className="text-right">Quantidade</TableHead>
                        <TableHead className="text-right">Peso bruto</TableHead>
                        <TableHead className="text-right">Valor unitário</TableHead>
                        <TableHead className="text-right">Subtotal</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {data.items.map(item => (
                        <TableRow key={item.id}>
                          <TableCell className="font-mono text-muted-foreground">{item.itemNumber}</TableCell>
                          <TableCell className="font-mono font-medium">{item.productCode}</TableCell>
                          <TableCell className="max-w-72 truncate" title={item.productDescription}>{item.productDescription || "-"}</TableCell>
                          <TableCell className="max-w-52 truncate" title={item.productGroupDescription || item.productGroupCode}>{item.productGroupDescription || item.productGroupCode || "-"}</TableCell>
                          <TableCell className="text-right">{numberFormatter.format(item.quantity)}</TableCell>
                          <TableCell className="whitespace-nowrap text-right">{numberFormatter.format(item.grossWeightKg)} kg</TableCell>
                          <TableCell className="whitespace-nowrap text-right">{item.unitValue == null ? "-" : currencyFormatter.format(item.unitValue)}</TableCell>
                          <TableCell className="whitespace-nowrap text-right font-semibold">{currencyFormatter.format(item.calculatedAmount)}</TableCell>
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
  );
}
