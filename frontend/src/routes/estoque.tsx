import { useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { ArrowDownUp, Boxes, PackageCheck, PackageX, Search, TrendingDown, TrendingUp } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { KpiCard } from "@/components/ui/kpi-card";
import { Label } from "@/components/ui/label";
import { SkeletonMetricCard, SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  fetchInventory,
  fetchInventoryFilters,
  fetchInventorySummary,
  fetchProductFilters,
  type InventoryItem,
  type InventorySummary,
} from "@/lib/importer-api";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";

export const Route = createFileRoute("/estoque")({ component: EstoquePage });

const PAGE_SIZE = 25;
const numberFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 3 });

function formatDate(value: string | null): string {
  if (!value) return "Sem registro";
  const date = new Date(`${value}T12:00:00`);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString("pt-BR");
}

function statusBadge(item: InventoryItem) {
  if (item.availableQuantity <= 0) {
    return <Badge variant="outline" className="border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">Ruptura</Badge>;
  }
  return <Badge variant="outline" className="border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300">Disponível</Badge>;
}

function EstoquePage() {
  const [summary, setSummary] = useState<InventorySummary | null>(null);
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [type, setType] = useState("");
  const [group, setGroup] = useState("");
  const [warehouse, setWarehouse] = useState("");
  const [status, setStatus] = useState("");
  const [sort, setSort] = useState("available_asc");
  const [types, setTypes] = useState<string[]>([]);
  const [groups, setGroups] = useState<string[]>([]);
  const [warehouses, setWarehouses] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const debouncedSearch = useDebouncedValue(search, TEXT_SEARCH_DEBOUNCE_MS);
  const pages = useMemo(() => Math.max(1, Math.ceil(total / PAGE_SIZE)), [total]);

  useEffect(() => {
    fetchProductFilters().then((result) => { setTypes(result.types); setGroups(result.groups); }).catch(() => undefined);
    fetchInventoryFilters().then((result) => setWarehouses(result.warehouses)).catch(() => undefined);
  }, []);

  useEffect(() => setPage(1), [debouncedSearch, type, group, warehouse, status, sort]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError("");
    Promise.all([
      fetchInventorySummary(),
      fetchInventory({ page, pageSize: PAGE_SIZE, search: debouncedSearch, type, group, warehouse, status, sort }),
    ])
      .then(([summaryResult, inventoryResult]) => {
        if (!active) return;
        setSummary(summaryResult);
        setItems(inventoryResult.items);
        setTotal(inventoryResult.total);
      })
      .catch((reason) => active && setError((reason as Error).message))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [page, debouncedSearch, type, group, warehouse, status, sort]);

  return (
    <div className="page-shell app-background min-w-0 max-w-full overflow-x-hidden">
      <header>
        <span className="page-header-kicker">Estoque</span>
        <h1 className="mt-1 text-3xl font-display font-semibold">Visão de Estoque</h1>
        <p className="mt-1 text-sm text-muted-foreground">Saldo atual, empenho e produção diária a partir das importações publicadas.</p>
      </header>

      {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}

      <section className="metric-row">
        {loading && !summary ? (
          <>
            <SkeletonMetricCard /><SkeletonMetricCard /><SkeletonMetricCard /><SkeletonMetricCard /><SkeletonMetricCard />
          </>
        ) : (
          <>
            <KpiCard className="metric-card-item" title="Rupturas" value={formatKpiCompactNumber(summary?.stockouts ?? 0)} valueTooltip={String(summary?.stockouts ?? 0)} icon={PackageX} periodLabel="Disponível <= 0" showPercentageChange={false} allowWrapValue />
            <KpiCard className="metric-card-item" title="Estoque comprometido" value={`${(summary?.committedPercent ?? 0).toFixed(1).replace(".", ",")}%`} valueTooltip={`${(summary?.committedPercent ?? 0).toFixed(2).replace(".", ",")}%`} icon={PackageCheck} periodLabel="Empenhado / físico" showPercentageChange={false} allowWrapValue />
            <KpiCard className="metric-card-item" title="Produção" value={formatKpiCompactNumber(summary?.lastProduction ?? 0)} valueTooltip={numberFormatter.format(summary?.lastProduction ?? 0)} icon={TrendingUp} periodLabel={`Último registro: ${formatDate(summary?.lastDailyDate ?? null)}`} showPercentageChange={false} allowWrapValue />
            <KpiCard className="metric-card-item" title="Saída" value={formatKpiCompactNumber(summary?.lastOutbound ?? 0)} valueTooltip={numberFormatter.format(summary?.lastOutbound ?? 0)} icon={TrendingDown} periodLabel={`Último registro: ${formatDate(summary?.lastDailyDate ?? null)}`} showPercentageChange={false} allowWrapValue />
            <KpiCard className="metric-card-item" title="Saldo operacional" value={formatKpiCompactNumber(summary?.operationalBalance ?? 0)} valueTooltip={numberFormatter.format(summary?.operationalBalance ?? 0)} icon={ArrowDownUp} periodLabel="Produção - saída" showPercentageChange={false} allowWrapValue />
          </>
        )}
      </section>

      <Card>
        <CardHeader className="gap-4 xl:flex-row xl:items-end xl:justify-between">
          <div>
            <CardTitle>Estoque atual</CardTitle>
            <p className="mt-1 text-xs text-muted-foreground">{total} registro(s) encontrado(s)</p>
          </div>
          <div className="grid w-full gap-2 xl:max-w-6xl xl:grid-cols-[1.4fr_0.65fr_0.65fr_0.65fr_0.75fr_0.9fr]">
            <div className="space-y-1">
              <Label htmlFor="inventory-search">Buscar</Label>
              <div className="relative">
                <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input id="inventory-search" className="pl-9" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Produto ou código..." />
              </div>
            </div>
            <Filter label="Tipo" value={type} onChange={setType} options={types} />
            <Filter label="Grupo" value={group} onChange={setGroup} options={groups} />
            <Filter label="Armazém" value={warehouse} onChange={setWarehouse} options={warehouses} />
            <Filter label="Status" value={status} onChange={setStatus} options={[{ value: "AVAILABLE", label: "Disponível" }, { value: "STOCKOUT", label: "Ruptura" }]} />
            <Filter label="Ordenar" value={sort} onChange={setSort} options={[{ value: "available_asc", label: "Menor disponível" }, { value: "committed_desc", label: "Maior empenho" }, { value: "committed_percent_desc", label: "Maior comprometido" }]} />
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {loading ? <SkeletonTable rows={8} columns={8} /> : (
            <div className="overflow-x-auto rounded-lg border border-border">
              <Table className="min-w-[1100px]">
                <TableHeader>
                  <TableRow className="bg-muted/45">
                    <TableHead>Código</TableHead>
                    <TableHead>Produto</TableHead>
                    <TableHead>Armazém</TableHead>
                    <TableHead className="text-right">Saldo físico</TableHead>
                    <TableHead className="text-right">Empenhado</TableHead>
                    <TableHead className="text-right">Disponível</TableHead>
                    <TableHead className="text-right">Comprometido</TableHead>
                    <TableHead className="text-right">Valor</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.length === 0 && <TableRow><TableCell colSpan={9} className="py-10 text-center text-muted-foreground">Nenhum estoque encontrado.</TableCell></TableRow>}
                  {items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell><div className="font-mono text-xs">{item.erpCode}</div><div className="font-mono text-[11px] text-muted-foreground">{item.operationalCode || "-"}</div></TableCell>
                      <TableCell className="max-w-96 truncate font-medium" title={item.productName}>{item.productName}</TableCell>
                      <TableCell>{item.branchCode} / {item.warehouseCode}</TableCell>
                      <TableCell className="text-right">{numberFormatter.format(item.onHandQuantity)}</TableCell>
                      <TableCell className="text-right">{numberFormatter.format(item.committedQuantity)}</TableCell>
                      <TableCell className="text-right font-semibold">{numberFormatter.format(item.availableQuantity)}</TableCell>
                      <TableCell className="text-right">{item.committedPercent == null ? "-" : `${item.committedPercent.toFixed(1).replace(".", ",")}%`}</TableCell>
                      <TableCell className="text-right">{formatKpiCompactCurrency(item.stockValue)}</TableCell>
                      <TableCell>{statusBadge(item)}</TableCell>
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
          const value = typeof option === "string" ? option : option.value;
          const label = typeof option === "string" ? option : option.label;
          return <option key={value} value={value}>{label}</option>;
        })}
      </select>
    </div>
  );
}
