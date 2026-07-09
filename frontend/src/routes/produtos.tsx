import { useEffect, useMemo, useState, type ReactNode } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Boxes, FileText, Package, Search, Scale } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { KpiCard } from "@/components/ui/kpi-card";
import { Label } from "@/components/ui/label";
import { SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  fetchProductDetails,
  fetchProductFilters,
  fetchProducts,
  type Product,
  type ProductDetails,
} from "@/lib/importer-api";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";

export const Route = createFileRoute("/produtos")({ component: ProdutosPage });

const PAGE_SIZE = 25;
const numberFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 3 });
const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

function stockStatus(product: Product): { label: string; className: string } {
  if (!product.inventory) return { label: "Sem informação", className: "border-border bg-muted text-muted-foreground" };
  if (product.inventory.availableQuantity <= 0) return { label: "Ruptura", className: "border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300" };
  return { label: "Disponível", className: "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300" };
}

function formatDate(value: string): string {
  const date = new Date(`${value}T12:00:00`);
  return Number.isNaN(date.getTime()) ? value || "-" : date.toLocaleDateString("pt-BR");
}

function ProdutosPage() {
  const [items, setItems] = useState<Product[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [type, setType] = useState("");
  const [group, setGroup] = useState("");
  const [status, setStatus] = useState("");
  const [types, setTypes] = useState<string[]>([]);
  const [groups, setGroups] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const debouncedSearch = useDebouncedValue(search, TEXT_SEARCH_DEBOUNCE_MS);
  const totalPages = useMemo(() => Math.max(1, Math.ceil(total / PAGE_SIZE)), [total]);

  useEffect(() => {
    fetchProductFilters().then((result) => {
      setTypes(result.types);
      setGroups(result.groups);
    }).catch(() => undefined);
  }, []);

  useEffect(() => setPage(1), [debouncedSearch, type, group, status]);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setMessage("");
    fetchProducts({ page, pageSize: PAGE_SIZE, search: debouncedSearch, type, group, stockStatus: status })
      .then((response) => {
        if (!active) return;
        setItems(response.items);
        setTotal(response.total);
      })
      .catch((error) => active && setMessage((error as Error).message))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [page, debouncedSearch, type, group, status]);

  return (
    <div className="page-shell app-background min-w-0 max-w-full overflow-x-hidden">
      <header>
        <span className="page-header-kicker">Estoque / Produtos</span>
        <h1 className="mt-1 text-3xl font-display font-semibold">Produtos</h1>
        <p className="mt-1 text-sm text-muted-foreground">Cadastro mestre vinculado às notas fiscais, ao estoque e à produção diária.</p>
      </header>

      {message && <Alert variant="destructive"><AlertDescription>{message}</AlertDescription></Alert>}

      <Card>
        <CardHeader className="gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <CardTitle>Produtos cadastrados</CardTitle>
            <p className="mt-1 text-xs text-muted-foreground">{total} produto(s) encontrado(s)</p>
          </div>
          <div className="grid w-full gap-2 lg:max-w-5xl lg:grid-cols-[1.4fr_0.7fr_0.7fr_0.8fr]">
            <div className="space-y-1">
              <Label htmlFor="product-search">Buscar</Label>
              <div className="relative">
                <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input id="product-search" className="pl-9" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar por produto ou código..." />
              </div>
            </div>
            <FilterSelect label="Tipo" value={type} onChange={setType} options={types} allLabel="Todos" />
            <FilterSelect label="Grupo" value={group} onChange={setGroup} options={groups} allLabel="Todos" />
            <FilterSelect label="Status" value={status} onChange={setStatus} allLabel="Todos" options={[
              { value: "AVAILABLE", label: "Disponível" },
              { value: "STOCKOUT", label: "Ruptura" },
              { value: "NO_INFORMATION", label: "Sem informação" },
            ]} />
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {loading ? <SkeletonTable rows={8} columns={7} /> : (
            <div className="overflow-x-auto rounded-lg border border-border">
              <Table className="min-w-[980px]">
                <TableHeader>
                  <TableRow className="bg-muted/45">
                    <TableHead>Produto</TableHead>
                    <TableHead>Código ERP</TableHead>
                    <TableHead>Código operacional</TableHead>
                    <TableHead>Unidade</TableHead>
                    <TableHead>Grupo</TableHead>
                    <TableHead className="text-right">Disponível</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {items.length === 0 && <TableRow><TableCell colSpan={7} className="py-10 text-center text-muted-foreground">Nenhum produto encontrado.</TableCell></TableRow>}
                  {items.map((item) => {
                    const statusInfo = stockStatus(item);
                    return (
                      <TableRow key={item.id} className="cursor-pointer" tabIndex={0} onClick={() => setSelectedId(item.id)} onKeyDown={(event) => { if (event.key === "Enter") setSelectedId(item.id); }}>
                        <TableCell className="max-w-96 truncate font-medium" title={item.name}>{item.name || "-"}</TableCell>
                        <TableCell className="font-mono text-xs">{item.erpCode || "-"}</TableCell>
                        <TableCell className="font-mono text-xs">{item.operationalCode || "-"}</TableCell>
                        <TableCell>{item.unit || "-"}</TableCell>
                        <TableCell>{item.groupCode || "-"}</TableCell>
                        <TableCell className="text-right">{item.inventory ? numberFormatter.format(item.inventory.availableQuantity) : "-"}</TableCell>
                        <TableCell><Badge variant="outline" className={statusInfo.className}>{statusInfo.label}</Badge></TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}

          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>{total} produto(s)</span>
            <div className="flex items-center gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => setPage(page - 1)}>Anterior</Button>
              <span>Página {page} de {totalPages}</span>
              <Button variant="outline" size="sm" disabled={page >= totalPages || loading} onClick={() => setPage(page + 1)}>Próxima</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <ProductDetailsDialog id={selectedId} open={selectedId !== null} onOpenChange={(open) => !open && setSelectedId(null)} />
    </div>
  );
}

function FilterSelect({ label, value, onChange, options, allLabel }: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  options: Array<string | { value: string; label: string }>;
  allLabel: string;
}) {
  return (
    <div className="space-y-1">
      <Label>{label}</Label>
      <select className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm" value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">{allLabel}</option>
        {options.map((option) => {
          const value = typeof option === "string" ? option : option.value;
          const label = typeof option === "string" ? option : option.label;
          return <option key={value} value={value}>{label}</option>;
        })}
      </select>
    </div>
  );
}

function ProductDetailsDialog({ id, open, onOpenChange }: { id: string | null; open: boolean; onOpenChange: (open: boolean) => void }) {
  const [data, setData] = useState<ProductDetails | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    if (!open || !id) return;
    setData(null);
    setError("");
    fetchProductDetails(id).then(setData).catch((reason) => setError((reason as Error).message));
  }, [id, open]);
  const inventoryTotal = useMemo(() => data?.latestInventory.reduce((total, item) => total + item.availableQuantity, 0) ?? 0, [data]);
  const committedTotal = useMemo(() => data?.latestInventory.reduce((total, item) => total + item.committedQuantity, 0) ?? 0, [data]);
  const onHandTotal = useMemo(() => data?.latestInventory.reduce((total, item) => total + item.onHandQuantity, 0) ?? 0, [data]);
  const committedPercent = onHandTotal === 0 ? 0 : committedTotal / onHandTotal * 100;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="custom-scrollbar max-h-[92vh] w-[96vw] max-w-6xl overflow-y-auto border-border/80 p-0">
        <DialogHeader className="border-b border-border bg-[linear-gradient(135deg,var(--soft-red-background),transparent_65%)] px-6 py-6 pr-14 sm:px-8">
          <div className="mb-3 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-primary"><Package className="size-4" />Produto</div>
          <DialogTitle className="text-2xl font-display sm:text-3xl">{data?.product.name ?? "Detalhes do produto"}</DialogTitle>
          <DialogDescription>{data ? `${data.product.erpCode || "-"} / ${data.product.operationalCode || "-"}` : "Cadastro, estoque, produção e notas fiscais relacionadas."}</DialogDescription>
        </DialogHeader>
        <div className="space-y-6 px-6 pb-7 sm:px-8">
          {error ? <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert> : !data ? <SkeletonTable rows={5} columns={6} /> : (
            <>
              <section className="-mt-3 grid gap-3 rounded-lg border border-border bg-surface p-4 sm:grid-cols-2 lg:grid-cols-4">
                <Info label="Código ERP" value={data.product.erpCode || "-"} />
                <Info label="Código operacional" value={data.product.operationalCode || "-"} />
                <Info label="Unidade" value={data.product.unit || "-"} />
                <Info label="Grupo" value={data.product.groupCode || "-"} />
                <Info label="Tipo" value={data.product.type || "-"} />
                <Info label="Peso líquido" value={data.product.netWeightKg == null ? "-" : `${numberFormatter.format(data.product.netWeightKg)} kg`} />
                <Info label="Peso bruto" value={data.product.grossWeightKg == null ? "-" : `${numberFormatter.format(data.product.grossWeightKg)} kg`} />
                <Info label="GTIN" value={data.product.gtin || "-"} />
              </section>

              <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                <KpiCard title="Saldo físico" value={formatKpiCompactNumber(onHandTotal)} valueTooltip={numberFormatter.format(onHandTotal)} icon={Boxes} periodLabel="Estoque atual" showPercentageChange={false} />
                <KpiCard title="Empenhado" value={formatKpiCompactNumber(committedTotal)} valueTooltip={numberFormatter.format(committedTotal)} icon={Scale} periodLabel="Reservas atuais" showPercentageChange={false} />
                <KpiCard title="Disponível" value={formatKpiCompactNumber(inventoryTotal)} valueTooltip={numberFormatter.format(inventoryTotal)} icon={Package} periodLabel="Saldo disponível" showPercentageChange={false} />
                <KpiCard title="Comprometido" value={`${committedPercent.toFixed(1).replace(".", ",")}%`} valueTooltip={`${committedPercent.toFixed(2).replace(".", ",")}%`} icon={Scale} periodLabel="Empenhado / físico" showPercentageChange={false} />
              </section>

              <Tabs defaultValue="inventory" className="space-y-4">
                <TabsList>
                  <TabsTrigger value="inventory">Estoque</TabsTrigger>
                  <TabsTrigger value="daily">Produção e Saída</TabsTrigger>
                  <TabsTrigger value="fiscal">Notas Fiscais</TabsTrigger>
                </TabsList>
                <TabsContent value="inventory">
                  <DataTable columns={["Filial", "Armazém", "Saldo físico", "Empenhado", "Disponível", "Valor"]}>
                    {data.latestInventory.map((item) => (
                      <TableRow key={`${item.branchCode}-${item.warehouseCode}`}>
                        <TableCell>{item.branchCode || "-"}</TableCell><TableCell>{item.warehouseCode || "-"}</TableCell>
                        <TableCell className="text-right">{numberFormatter.format(item.onHandQuantity)}</TableCell>
                        <TableCell className="text-right">{numberFormatter.format(item.committedQuantity)}</TableCell>
                        <TableCell className="text-right">{numberFormatter.format(item.availableQuantity)}</TableCell>
                        <TableCell className="text-right">{currencyFormatter.format(item.stockValue)}</TableCell>
                      </TableRow>
                    ))}
                  </DataTable>
                </TabsContent>
                <TabsContent value="daily">
                  <DataTable columns={["Data", "Produção", "Saída", "Ajuste", "Estoque final"]}>
                    {data.dailyHistory.map((item) => (
                      <TableRow key={item.date}>
                        <TableCell>{formatDate(item.date)}</TableCell>
                        <TableCell className="text-right">{numberFormatter.format(item.productionQuantity)}</TableCell>
                        <TableCell className="text-right">{numberFormatter.format(item.outboundQuantity)}</TableCell>
                        <TableCell className="text-right">{numberFormatter.format(item.adjustmentQuantity)}</TableCell>
                        <TableCell className="text-right">{numberFormatter.format(item.closingQuantity)}</TableCell>
                      </TableRow>
                    ))}
                  </DataTable>
                </TabsContent>
                <TabsContent value="fiscal">
                  <DataTable columns={["Data", "Documento", "Cliente", "Operação", "Quantidade", "Peso", "Valor"]}>
                    {data.fiscalItems.map((item) => (
                      <TableRow key={item.id}>
                        <TableCell>{formatDate(item.issueDate)}</TableCell>
                        <TableCell className="font-mono text-xs">{item.documentNumber}{item.series ? ` / ${item.series}` : ""}</TableCell>
                        <TableCell className="max-w-72 truncate" title={item.customerName}>{item.customerName || "-"}</TableCell>
                        <TableCell>{item.operationCategory}</TableCell>
                        <TableCell className="text-right">{numberFormatter.format(item.quantity)}</TableCell>
                        <TableCell className="text-right">{numberFormatter.format(item.grossWeightKg)} kg</TableCell>
                        <TableCell className="text-right">{formatKpiCompactCurrency(item.calculatedAmount)}</TableCell>
                      </TableRow>
                    ))}
                  </DataTable>
                </TabsContent>
              </Tabs>
            </>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return <div><p className="text-xs text-muted-foreground">{label}</p><p className="truncate text-sm font-semibold" title={value}>{value}</p></div>;
}

function DataTable({ columns, children }: { columns: string[]; children: ReactNode }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-border">
      <Table className="min-w-[820px]">
        <TableHeader><TableRow className="bg-muted/45">{columns.map((column) => <TableHead key={column} className={column === columns[0] || column === "Cliente" || column === "Documento" ? undefined : "text-right"}>{column}</TableHead>)}</TableRow></TableHeader>
        <TableBody>{children}</TableBody>
      </Table>
    </div>
  );
}
