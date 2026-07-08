import { useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { ArrowDownUp, Calendar, Factory, PackageOpen, Search, TrendingDown, TrendingUp } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { KpiCard } from "@/components/ui/kpi-card";
import { Label } from "@/components/ui/label";
import { SkeletonMetricCard, SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  fetchProduction,
  fetchProductionFilters,
  fetchProductionSummary,
  type ProductionItem,
  type ProductionSummary,
} from "@/lib/importer-api";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";
import { formatKpiCompactNumber } from "@/lib/vendas-formatters";

export const Route = createFileRoute("/producao")({ component: ProducaoPage });

const PAGE_SIZE = 25;
const numberFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 3 });

function formatDate(value: string | null): string {
  if (!value) return "Sem registro";
  const date = new Date(`${value}T12:00:00`);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString("pt-BR");
}

function ProducaoPage() {
  const [summary, setSummary] = useState<ProductionSummary | null>(null);
  const [items, setItems] = useState<ProductionItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [type, setType] = useState("");
  const [group, setGroup] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [sort, setSort] = useState("date_desc");
  const [types, setTypes] = useState<string[]>([]);
  const [groups, setGroups] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const debouncedSearch = useDebouncedValue(search, TEXT_SEARCH_DEBOUNCE_MS);
  const pages = useMemo(() => Math.max(1, Math.ceil(total / PAGE_SIZE)), [total]);

  useEffect(() => {
    fetchProductionFilters().then((result) => { setTypes(result.types); setGroups(result.groups); }).catch(() => undefined);
  }, []);

  useEffect(() => setPage(1), [debouncedSearch, type, group, dateFrom, dateTo, sort]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError("");
    Promise.all([
      fetchProductionSummary(),
      fetchProduction({ page, pageSize: PAGE_SIZE, search: debouncedSearch, dateFrom, dateTo, sort }),
    ])
      .then(([summaryResult, productionResult]) => {
        if (!active) return;
        setSummary(summaryResult);
        setItems(productionResult.items);
        setTotal(productionResult.total);
      })
      .catch((reason) => active && setError((reason as Error).message))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [page, debouncedSearch, type, group, dateFrom, dateTo, sort]);

  return (
    <div className="page-shell app-background min-w-0 max-w-full overflow-x-hidden">
      <header>
        <span className="page-header-kicker">Produção</span>
        <h1 className="mt-1 text-3xl font-display font-semibold">Visão de Produção</h1>
        <p className="mt-1 text-sm text-muted-foreground">Produção, saída e saldo operacional por produto a partir do controle diário de estoque.</p>
      </header>

      {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}

      <section className="metric-row">
        {loading && !summary ? (
          <><SkeletonMetricCard /><SkeletonMetricCard /><SkeletonMetricCard /><SkeletonMetricCard /><SkeletonMetricCard /></>
        ) : (
          <>
            <KpiCard className="metric-card-item" title="Produção (último dia)" value={formatKpiCompactNumber(summary?.lastProduction ?? 0)} valueTooltip={numberFormatter.format(summary?.lastProduction ?? 0)} icon={TrendingUp} periodLabel={`Data: ${formatDate(summary?.lastDailyDate ?? null)}`} showPercentageChange={false} allowWrapValue />
            <KpiCard className="metric-card-item" title="Saída (último dia)" value={formatKpiCompactNumber(summary?.lastOutbound ?? 0)} valueTooltip={numberFormatter.format(summary?.lastOutbound ?? 0)} icon={TrendingDown} periodLabel={`Data: ${formatDate(summary?.lastDailyDate ?? null)}`} showPercentageChange={false} allowWrapValue />
            <KpiCard className="metric-card-item" title="Saldo operacional" value={formatKpiCompactNumber(summary?.operationalBalance ?? 0)} valueTooltip={numberFormatter.format(summary?.operationalBalance ?? 0)} icon={ArrowDownUp} periodLabel="Produção - saída" showPercentageChange={false} allowWrapValue />
            <KpiCard className="metric-card-item" title="Produção (mês)" value={formatKpiCompactNumber(summary?.totalProductionMonth ?? 0)} valueTooltip={numberFormatter.format(summary?.totalProductionMonth ?? 0)} icon={Factory} periodLabel="Acumulado no mês" showPercentageChange={false} allowWrapValue />
            <KpiCard className="metric-card-item" title="Saída (mês)" value={formatKpiCompactNumber(summary?.totalOutboundMonth ?? 0)} valueTooltip={numberFormatter.format(summary?.totalOutboundMonth ?? 0)} icon={PackageOpen} periodLabel="Acumulado no mês" showPercentageChange={false} allowWrapValue />
          </>
        )}
      </section>

      <Card>
        <CardHeader className="gap-4 xl:flex-row xl:items-end xl:justify-between">
          <div>
            <CardTitle>Produção diária</CardTitle>
            <p className="mt-1 text-xs text-muted-foreground">{total} registro(s) encontrado(s)</p>
          </div>
          <div className="grid w-full gap-2 xl:max-w-6xl xl:grid-cols-[1.2fr_0.55fr_0.55fr_0.55fr_0.55fr_0.6fr]">
            <div className="space-y-1">
              <Label htmlFor="production-search">Buscar</Label>
              <div className="relative">
                <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input id="production-search" className="pl-9" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Produto ou código..." />
              </div>
            </div>
            <Filter label="Tipo" value={type} onChange={setType} options={types} />
            <Filter label="Grupo" value={group} onChange={setGroup} options={groups} />
            <div className="space-y-1">
              <Label htmlFor="production-date-from">Data início</Label>
              <Input id="production-date-from" type="date" value={dateFrom} onChange={(event) => setDateFrom(event.target.value)} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="production-date-to">Data fim</Label>
              <Input id="production-date-to" type="date" value={dateTo} onChange={(event) => setDateTo(event.target.value)} />
            </div>
            <Filter label="Ordenar" value={sort} onChange={setSort} options={[
              { value: "date_desc", label: "Mais recente" },
              { value: "production_desc", label: "Maior produção" },
              { value: "production_asc", label: "Menor produção" },
            ]} />
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {loading ? <SkeletonTable rows={8} columns={7} /> : (
            <div className="overflow-x-auto rounded-lg border border-border">
              <Table className="min-w-[1000px]">
                <TableHeader>
                  <TableRow className="bg-muted/45">
                    <TableHead>Código</TableHead>
                    <TableHead>Produto</TableHead>
                    <TableHead>Data</TableHead>
                    <TableHead className="text-right">Produção</TableHead>
                    <TableHead className="text-right">Saída</TableHead>
                    <TableHead className="text-right">Ajuste</TableHead>
                    <TableHead className="text-right">Estoque final</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.length === 0 && <TableRow><TableCell colSpan={7} className="py-10 text-center text-muted-foreground">Nenhum registro de produção encontrado.</TableCell></TableRow>}
                  {items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell><div className="font-mono text-xs">{item.erpCode}</div><div className="font-mono text-[11px] text-muted-foreground">{item.operationalCode || "-"}</div></TableCell>
                      <TableCell className="max-w-80 truncate font-medium" title={item.productName}>{item.productName}</TableCell>
                      <TableCell className="text-nowrap">{formatDate(item.date)}</TableCell>
                      <TableCell className="text-right">{numberFormatter.format(item.productionQuantity)}</TableCell>
                      <TableCell className="text-right">{numberFormatter.format(item.outboundQuantity)}</TableCell>
                      <TableCell className="text-right">{numberFormatter.format(item.adjustmentQuantity)}</TableCell>
                      <TableCell className="text-right font-semibold">{numberFormatter.format(item.closingQuantity)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>{total} registro(s)</span>
            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => setPage(page - 1)}>Anterior</Button>
              <span>Página {page} de {pages}</span>
              <Button variant="outline" size="sm" disabled={page >= pages || loading} onClick={() => setPage(page + 1)}>Próxima</Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function Filter({ label, value, onChange, options }: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: Array<string | { value: string; label: string }>;
}) {
  return (
    <div className="space-y-1">
      <Label>{label}</Label>
      <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">Todos</option>
        {options.map((option) => {
          const optValue = typeof option === "string" ? option : option.value;
          const optLabel = typeof option === "string" ? option : option.label;
          return <option key={optValue} value={optValue}>{optLabel}</option>;
        })}
      </select>
    </div>
  );
}
