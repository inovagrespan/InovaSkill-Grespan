import { useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { RefreshCw, WalletCards } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { SkeletonList } from "@/components/ui/skeleton";
import {
  fetchImportedRoutes,
  fetchLogisticsFuelSettings,
  fetchRouteRoadPath,
  type ImportedRouteItem,
  type RouteRoadPath,
} from "@/lib/importer-api";
import { estimateRouteTollCost } from "@/lib/route-toll-cost";
import {
  buildRouteCostReport,
  type RouteCostReport,
  type RouteCostReportGrouping,
  type RouteCostReportInput,
} from "@/lib/route-cost-report";
import { getCurrentLocalDate } from "@/lib/route-snapshot-history";

export const Route = createFileRoute("/logistica/relatorios-custos")({ component: LogisticsCostReportPage });

const REPORT_PAGE_SIZE = 100;
const METERS_PER_KILOMETER = 1_000;
const WEEKDAY_BY_INDEX = ["SUNDAY", "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY"] as const;
const BUSINESS_WEEKDAYS = new Set(["MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY"]);

type ReportPeriod = "daily" | "weekly";

const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const distanceFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 });

function formatCurrencyRange(minimum: number, maximum: number): string {
  const minimumText = currencyFormatter.format(minimum);
  const maximumText = currencyFormatter.format(maximum);
  return minimum === maximum ? minimumText : `${minimumText} a ${maximumText}`;
}

function formatDistance(distanceMeters: number): string {
  return `${distanceFormatter.format(distanceMeters / METERS_PER_KILOMETER)} km`;
}

function weekdayForDate(date: string): string | undefined {
  const parsed = new Date(`${date}T12:00:00`);
  if (Number.isNaN(parsed.getTime())) return undefined;
  const weekday = WEEKDAY_BY_INDEX[parsed.getDay()];
  return BUSINESS_WEEKDAYS.has(weekday) ? weekday : undefined;
}

async function fetchAllRoutes(filters: { date: string; weekday?: string }): Promise<ImportedRouteItem[]> {
  const firstPage = await fetchImportedRoutes(1, REPORT_PAGE_SIZE, filters);
  const pages = [firstPage.items];
  const pageCount = Math.ceil(firstPage.total / REPORT_PAGE_SIZE);
  for (let page = 2; page <= pageCount; page += 1) {
    const nextPage = await fetchImportedRoutes(page, REPORT_PAGE_SIZE, filters);
    pages.push(nextPage.items);
  }
  return pages.flat();
}

async function fetchRoutePathsSequentially(routes: ImportedRouteItem[]): Promise<PromiseSettledResult<RouteRoadPath>[]> {
  const results: PromiseSettledResult<RouteRoadPath>[] = [];
  for (const route of routes) {
    try {
      results.push({ status: "fulfilled", value: await fetchRouteRoadPath(route.id) });
    } catch (reason) {
      results.push({ status: "rejected", reason });
    }
  }
  return results;
}

function toCostInputs(
  routes: ImportedRouteItem[],
  paths: Map<string, RouteRoadPath>,
): RouteCostReportInput[] {
  return routes.map((route) => {
    const path = paths.get(route.id);
    const toll = path ? estimateRouteTollCost(path.geometry.coordinates) : null;
    return {
      routeId: route.id,
      routeName: route.name,
      weekday: route.weekday,
      vehicleType: route.vehicleType,
      distanceMeters: path?.distanceMeters ?? null,
      tollCost: toll?.totalAutomaticCost ?? 0,
      tollPassages: toll?.totalPassages ?? 0,
    };
  });
}

function ReportKpi({ title, value, detail, accent = false }: { title: string; value: string; detail: string; accent?: boolean }) {
  return (
    <div className={`rounded-xl border p-4 shadow-sm ${accent ? "border-primary/30 bg-primary/5" : "border-border/80 bg-background/30"}`}>
      <div className="flex items-center gap-2">
        <p className="text-xs font-semibold uppercase tracking-wider text-primary">{title}</p>
        <span className="h-px flex-1 bg-primary/20" aria-hidden="true" />
      </div>
      <p className="mt-2 text-xl font-display font-semibold tracking-tight text-foreground">{value}</p>
      <p className="mt-2 text-xs leading-relaxed text-muted-foreground">{detail}</p>
    </div>
  );
}

function ReportTable({ report, grouping }: { report: RouteCostReport; grouping: RouteCostReportGrouping }) {
  return (
    <div className="overflow-x-auto rounded-xl border border-border">
      <table className="w-full min-w-[760px] text-sm">
        <thead className="bg-muted/40 text-left text-xs uppercase tracking-wider text-muted-foreground">
          <tr>
            <th className="px-4 py-3">{grouping === "route" ? "Rota" : "Tipo de caminhão"}</th>
            <th className="px-4 py-3">Rotas</th>
            <th className="px-4 py-3">Distância</th>
            <th className="px-4 py-3">Combustível</th>
            <th className="px-4 py-3">Pedágio</th>
            <th className="px-4 py-3 text-right">Total</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {report.rows.map((row) => (
            <tr key={row.key} className="align-top">
              <td className="px-4 py-3">
                <p className="font-medium">{row.label}</p>
                {grouping === "route" && <p className="mt-1 text-xs text-muted-foreground">{row.vehicleType}</p>}
              </td>
              <td className="px-4 py-3 text-muted-foreground">{row.routeCount}</td>
              <td className="px-4 py-3">{formatDistance(row.distanceMeters)}</td>
              <td className="px-4 py-3">{row.minimumFuelCost === null ? "Indisponível" : formatCurrencyRange(row.minimumFuelCost, row.maximumFuelCost!)}</td>
              <td className="px-4 py-3">{currencyFormatter.format(row.tollCost)}</td>
              <td className="px-4 py-3 text-right font-semibold">{row.minimumTotalCost === null ? "Indisponível" : formatCurrencyRange(row.minimumTotalCost, row.maximumTotalCost!)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {report.rows.length === 0 && <p className="p-6 text-sm text-muted-foreground">Nenhuma rota encontrada para os filtros selecionados.</p>}
    </div>
  );
}

function LogisticsCostReportPage() {
  const [period, setPeriod] = useState<ReportPeriod>("daily");
  const [referenceDate, setReferenceDate] = useState(() => getCurrentLocalDate());
  const [grouping, setGrouping] = useState<RouteCostReportGrouping>("route");
  const [inputs, setInputs] = useState<RouteCostReportInput[]>([]);
  const [dieselPrice, setDieselPrice] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [unavailablePathCount, setUnavailablePathCount] = useState(0);

  const report = useMemo(
    () => buildRouteCostReport(inputs, dieselPrice, grouping),
    [dieselPrice, grouping, inputs],
  );

  async function load() {
    setLoading(true);
    setError(null);
    try {
      const routes = await fetchAllRoutes({
        date: referenceDate,
        weekday: period === "daily" ? weekdayForDate(referenceDate) : undefined,
      });
      const settings = await fetchLogisticsFuelSettings();
      const pathResults = await fetchRoutePathsSequentially(routes);
      const paths = new Map<string, RouteRoadPath>();
      pathResults.forEach((result) => {
        if (result.status === "fulfilled") paths.set(result.value.id, result.value);
      });
      setUnavailablePathCount(pathResults.filter((result) => result.status === "rejected").length);
      setInputs(toCostInputs(routes, paths));
      setDieselPrice(settings.dieselPricePerLiter);
    } catch (reason) {
      setInputs([]);
      setDieselPrice(null);
      setError((reason as Error).message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, [period, referenceDate]);

  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <span className="page-header-kicker">Logística / Relatórios</span>
          <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">Relatório de custos</h1>
          <p className="mt-1 max-w-2xl text-sm text-muted-foreground">Compare combustível, pedágios e gastos totais por rota ou tipo de caminhão.</p>
        </div>
        <Button type="button" variant="outline" onClick={() => void load()} disabled={loading}>
          <RefreshCw className={loading ? "size-4 animate-spin" : "size-4"} />
          Atualizar relatório
        </Button>
      </header>

      <Card className="border-border bg-surface">
        <CardHeader><CardTitle className="flex items-center gap-2"><WalletCards className="size-5 text-primary" />Filtros do relatório</CardTitle></CardHeader>
        <CardContent className="grid grid-cols-1 gap-3 md:grid-cols-3">
          <label className="space-y-1.5 text-xs font-semibold text-foreground">
            Periodicidade
            <Select value={period} onValueChange={(value) => setPeriod(value as ReportPeriod)}>
              <SelectTrigger aria-label="Periodicidade do relatório"><SelectValue /></SelectTrigger>
              <SelectContent><SelectItem value="daily">Diário</SelectItem><SelectItem value="weekly">Semanal</SelectItem></SelectContent>
            </Select>
          </label>
          <label className="space-y-1.5 text-xs font-semibold text-foreground">
            Data de referência
            <Input aria-label="Data de referência do relatório" type="date" value={referenceDate} onChange={(event) => setReferenceDate(event.target.value)} />
          </label>
          <label className="space-y-1.5 text-xs font-semibold text-foreground">
            Consolidar por
            <Select value={grouping} onValueChange={(value) => setGrouping(value as RouteCostReportGrouping)}>
              <SelectTrigger aria-label="Agrupamento do relatório"><SelectValue /></SelectTrigger>
              <SelectContent><SelectItem value="route">Rota</SelectItem><SelectItem value="vehicle">Tipo de caminhão</SelectItem></SelectContent>
            </Select>
          </label>
        </CardContent>
      </Card>

      {error && <p role="alert" className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">{error}</p>}
      {loading ? <SkeletonList rows={4} /> : (
        <>
          <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <ReportKpi title="Gasto total" value={formatCurrencyRange(report.minimumTotalCost, report.maximumTotalCost)} detail={`${report.routeCount} rota(s) analisada(s)`} accent />
            <ReportKpi title="Combustível" value={formatCurrencyRange(report.minimumFuelCost, report.maximumFuelCost)} detail={dieselPrice === null ? "Preço do diesel indisponível" : `Diesel de referência: ${currencyFormatter.format(dieselPrice)}/L`} />
            <ReportKpi title="Pedágios" value={currencyFormatter.format(report.tollCost)} detail={`${report.tollPassages} passagem(ns) com tarifa automática estimada`} />
            <ReportKpi title="Distância" value={formatDistance(report.totalDistanceMeters)} detail={`${period === "daily" ? "Período diário" : "Período semanal"} · ${referenceDate}`} />
          </section>
          {unavailablePathCount > 0 && <p className="text-xs text-muted-foreground">{unavailablePathCount} rota(s) não tiveram o percurso calculado e foram marcadas como indisponíveis.</p>}
          {report.unavailableRoutes > 0 && <p className="text-xs text-amber-700 dark:text-amber-300">{report.unavailableRoutes} rota(s) ficaram sem custo de combustível por falta de percurso ou preço configurado.</p>}
          <Card className="border-border bg-surface">
            <CardHeader className="flex flex-row items-center justify-between gap-3"><CardTitle>Detalhamento</CardTitle><Badge variant="outline">{grouping === "route" ? "Por rota" : "Por caminhão"}</Badge></CardHeader>
            <CardContent><ReportTable report={report} grouping={grouping} /></CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
