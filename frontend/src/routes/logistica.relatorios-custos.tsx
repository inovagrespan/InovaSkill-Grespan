import { useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { RefreshCw, WalletCards } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SkeletonList } from "@/components/ui/skeleton";
import { fetchDailyRouteCosts, type DailyRouteCosts, type RouteCostItem, type RouteCostScenario } from "@/lib/importer-api";
import { getCurrentLocalDate } from "@/lib/route-snapshot-history";
import { groupRouteCostItems, type RouteCostGrouping } from "@/lib/route-cost-view";

export const Route = createFileRoute("/logistica/relatorios-custos")({ component: LogisticsCostReportPage });

const METERS_PER_KILOMETER = 1_000;
const WEEKDAY_BY_INDEX = ["SUNDAY", "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY"] as const;
const weekdayLabels: Record<string, string> = { MONDAY: "Segunda", TUESDAY: "Terça", WEDNESDAY: "Quarta", THURSDAY: "Quinta", FRIDAY: "Sexta", SATURDAY: "Sábado", SUNDAY: "Domingo" };
const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const numberFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 });

function weekdayForDate(date: string): string {
  const parsed = new Date(`${date}T12:00:00`);
  return Number.isNaN(parsed.getTime()) ? "MONDAY" : WEEKDAY_BY_INDEX[parsed.getDay()];
}

function formatCurrencyRange(minimum: number | null, maximum: number | null): string {
  if (minimum === null || maximum === null) return "Indisponível";
  const minimumText = currencyFormatter.format(minimum);
  return minimum === maximum ? minimumText : `${minimumText} a ${currencyFormatter.format(maximum)}`;
}

function formatDistance(distanceMeters: number | null): string {
  return distanceMeters === null ? "Indisponível" : `${numberFormatter.format(distanceMeters / METERS_PER_KILOMETER)} km`;
}

function scenarioLabel(value: "actual" | "optimized"): string {
  return value === "actual" ? "Rotas atuais" : "Cenário otimizado";
}

function ReportKpi({ title, value, detail, accent = false }: { title: string; value: string; detail: string; accent?: boolean }) {
  return <div className={`rounded-xl border p-4 shadow-sm ${accent ? "border-primary/30 bg-primary/5" : "border-border/80 bg-background/30"}`}><p className="text-xs font-semibold uppercase tracking-wider text-primary">{title}</p><p className="mt-2 text-xl font-display font-semibold tracking-tight">{value}</p><p className="mt-2 text-xs text-muted-foreground">{detail}</p></div>;
}

function RouteCostTable({ items, grouping }: { items: RouteCostItem[]; grouping: RouteCostGrouping }) {
  const rows = groupRouteCostItems(items, grouping);
  return <div className="overflow-x-auto rounded-xl border border-border"><table className="w-full min-w-[760px] text-sm"><thead className="bg-muted/40 text-left text-xs uppercase tracking-wider text-muted-foreground"><tr><th className="px-4 py-3">{grouping === "route" ? "Rota / veículo" : "Tipo de caminhão"}</th><th className="px-4 py-3">Rotas</th><th className="px-4 py-3">Distância</th><th className="px-4 py-3">Combustível</th><th className="px-4 py-3">Pedágio</th><th className="px-4 py-3 text-right">Total</th></tr></thead><tbody className="divide-y divide-border">{rows.map((item) => <tr key={item.id} className="align-top"><td className="px-4 py-3"><p className="font-medium">{item.label}</p><p className="mt-1 text-xs text-muted-foreground">{grouping === "route" ? `${item.vehicleType ?? "Veículo não informado"} · ${weekdayLabels[item.weekday] ?? item.weekday}` : `${item.unavailableItemCount} indisponível(is)`}</p>{!item.isAvailable && <p className="mt-1 text-xs text-amber-700 dark:text-amber-300">{item.unavailableReason ?? "Custo indisponível"}</p>}</td><td className="px-4 py-3">{item.routeCount}</td><td className="px-4 py-3">{formatDistance(item.distanceMeters)}</td><td className="px-4 py-3">{formatCurrencyRange(item.minimumFuelCost, item.maximumFuelCost)}</td><td className="px-4 py-3">{item.isAvailable ? currencyFormatter.format(item.tollCost) : "Indisponível"}<p className="text-xs text-muted-foreground">{item.isAvailable ? `${item.tollPassages} passagem(ns)` : ""}</p></td><td className="px-4 py-3 text-right font-semibold">{formatCurrencyRange(item.minimumTotalCost, item.maximumTotalCost)}</td></tr>)}</tbody></table>{rows.length === 0 && <p className="p-6 text-sm text-muted-foreground">Nenhum custo consolidado para os filtros selecionados.</p>}</div>;
}

function LogisticsCostReportPage() {
  const currentDate = getCurrentLocalDate();
  const [referenceDate, setReferenceDate] = useState(currentDate);
  const [weekday, setWeekday] = useState(() => weekdayForDate(currentDate));
  const [period, setPeriod] = useState<"daily" | "weekly">("daily");
  const [grouping, setGrouping] = useState<RouteCostGrouping>("route");
  const [selectedScenario, setSelectedScenario] = useState<"actual" | "optimized">("actual");
  const [data, setData] = useState<DailyRouteCosts | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const scenario: RouteCostScenario | null = useMemo(() => selectedScenario === "actual" ? data?.actual ?? null : data?.optimized ?? null, [data, selectedScenario]);

  async function load() {
    setLoading(true);
    setError(null);
    try { setData(await fetchDailyRouteCosts({ date: referenceDate, weekday: period === "daily" ? weekday : undefined })); }
    catch (reason) { setData(null); setError((reason as Error).message); }
    finally { setLoading(false); }
  }

  useEffect(() => { void load(); }, [referenceDate, weekday, period]);

  return <div className="page-shell app-background space-y-6">
    <header className="animate-fade-in flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between"><div><span className="page-header-kicker">Logística / Relatórios</span><h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">Relatório de custos</h1><p className="mt-1 max-w-2xl text-sm text-muted-foreground">Compare os custos oficiais das rotas atuais com o último cenário calculado pelo otimizador.</p></div><Button type="button" variant="outline" onClick={() => void load()} disabled={loading}><RefreshCw className={loading ? "size-4 animate-spin" : "size-4"} />Atualizar relatório</Button></header>
    <Card className="border-border bg-surface"><CardHeader><CardTitle className="flex items-center gap-2"><WalletCards className="size-5 text-primary" />Filtros do relatório</CardTitle></CardHeader><CardContent className="grid grid-cols-1 gap-3 md:grid-cols-5">
      <label className="space-y-1.5 text-xs font-semibold">Periodicidade<Select value={period} onValueChange={(value) => setPeriod(value as "daily" | "weekly")}><SelectTrigger aria-label="Periodicidade do relatório"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="daily">Diário</SelectItem><SelectItem value="weekly">Semanal</SelectItem></SelectContent></Select></label>
      <label className="space-y-1.5 text-xs font-semibold">Data de referência<Input aria-label="Data de referência do relatório" type="date" value={referenceDate} onChange={(event) => { const nextDate = event.target.value; setReferenceDate(nextDate); setWeekday(weekdayForDate(nextDate)); }} /></label>
      <label className="space-y-1.5 text-xs font-semibold">Dia da semana<Select value={weekday} onValueChange={setWeekday} disabled={period === "weekly"}><SelectTrigger aria-label="Dia da semana do relatório"><SelectValue /></SelectTrigger><SelectContent>{Object.entries(weekdayLabels).map(([value, label]) => <SelectItem key={value} value={value}>{label}</SelectItem>)}</SelectContent></Select></label>
      <label className="space-y-1.5 text-xs font-semibold">Cenário<Select value={selectedScenario} onValueChange={(value) => setSelectedScenario(value as "actual" | "optimized")}><SelectTrigger aria-label="Cenário do relatório"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="actual">Rotas atuais</SelectItem><SelectItem value="optimized">Cenário otimizado</SelectItem></SelectContent></Select></label>
      <label className="space-y-1.5 text-xs font-semibold">Consolidar por<Select value={grouping} onValueChange={(value) => setGrouping(value as RouteCostGrouping)}><SelectTrigger aria-label="Agrupamento do relatório"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="route">Rota</SelectItem><SelectItem value="vehicle">Tipo de caminhão</SelectItem></SelectContent></Select></label>
    </CardContent></Card>
    {error && <p role="alert" className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">{error}</p>}
    {loading ? <SkeletonList rows={4} /> : data?.snapshotId && scenario ? <>
      <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4"><ReportKpi title="Gasto total" value={formatCurrencyRange(scenario.totals.minimumTotalCost, scenario.totals.maximumTotalCost)} detail={`${scenario.totals.availableItemCount} de ${scenario.totals.itemCount} item(ns) disponível(is)`} accent /><ReportKpi title="Combustível" value={formatCurrencyRange(scenario.totals.minimumFuelCost, scenario.totals.maximumFuelCost)} detail={data.dieselPricePerLiter === null ? "Preço do diesel indisponível" : `Diesel de referência: ${currencyFormatter.format(data.dieselPricePerLiter)}/L`} /><ReportKpi title="Pedágios" value={currencyFormatter.format(scenario.totals.tollCost)} detail={`${scenario.totals.tollPassages} passagem(ns) · catálogo ${data.tollCatalogVersion ?? "não informado"}`} /><ReportKpi title="Distância" value={formatDistance(scenario.totals.distanceMeters)} detail={`${scenarioLabel(selectedScenario)} · ${period === "daily" ? weekdayLabels[weekday] : "todos os dias"}`} /></section>
      <div className="rounded-lg border border-border bg-muted/20 p-4 text-xs text-muted-foreground"><p><strong className="text-foreground">Base do percurso:</strong> {scenario.pathBasis ?? "não informada"}</p><p className="mt-1">Rotas atuais usam clientes com coordenadas exatas; o cenário otimizado usa a sequência de blocos municipais. A comparação é global para o dia e não representa substituição 1:1 de uma rota.</p><p className="mt-1">{data.calculatedAt ? `Calculado em ${new Date(data.calculatedAt).toLocaleString("pt-BR")}` : "Data do cálculo não informada"}{data.tollEffectiveFrom ? ` · tarifas vigentes desde ${new Date(`${data.tollEffectiveFrom}T12:00:00`).toLocaleDateString("pt-BR")}` : " · vigência tarifária não informada"}.</p></div>
      {scenario.totals.unavailableItemCount > 0 && <p className="text-xs text-amber-700 dark:text-amber-300">{scenario.totals.unavailableItemCount} item(ns) sem custo por falta de coordenadas ou configuração operacional.</p>}
      <Card className="border-border bg-surface"><CardHeader className="flex flex-row items-center justify-between"><CardTitle>Detalhamento</CardTitle><Badge variant="outline">{grouping === "route" ? "Por rota" : "Por caminhão"}</Badge></CardHeader><CardContent><RouteCostTable items={scenario.items} grouping={grouping} /></CardContent></Card>
    </> : <p className="rounded-lg border border-amber-300/40 bg-amber-50/5 p-4 text-sm text-muted-foreground">Dados insuficientes: ainda não existem custos consolidados{selectedScenario === "optimized" ? " ou cenário otimizado válido" : ""} para este período.</p>}
  </div>;
}
