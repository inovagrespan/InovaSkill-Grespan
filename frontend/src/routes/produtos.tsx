import { FormEvent, useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Boxes, DollarSign, Package, Search } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { KpiCard } from "@/components/ui/kpi-card";
import { Label } from "@/components/ui/label";
import { SkeletonMetricCard, SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { fetchProducts, type Product } from "@/lib/importer-api";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";

export const Route = createFileRoute("/produtos")({
  component: ProdutosPage,
});

const PRODUCTS_PAGE_SIZE = 20;

function formatDate(value: string): string {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value || "N/A";
  return date.toLocaleDateString("pt-BR");
}

function averagePrice(items: Product[]): number {
  if (items.length === 0) return 0;
  return items.reduce((total, item) => total + item.price, 0) / items.length;
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value);
}

function ProdutosPage() {
  const [items, setItems] = useState<Product[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [priceMin, setPriceMin] = useState("");
  const [priceMax, setPriceMax] = useState("");
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");

  const totalPages = useMemo(() => Math.max(1, Math.ceil(total / PRODUCTS_PAGE_SIZE)), [total]);
  const pageAveragePrice = useMemo(() => averagePrice(items), [items]);
  const maxPagePrice = useMemo(() => items.reduce((max, item) => Math.max(max, item.price), 0), [items]);

  async function load(targetPage = page) {
    setLoading(true);
    setMessage("");
    try {
      const response = await fetchProducts({
        page: targetPage,
        pageSize: PRODUCTS_PAGE_SIZE,
        search,
        priceMin,
        priceMax,
      });
      setItems(response.items);
      setTotal(response.total);
      setPage(response.page);
    } catch (error) {
      setItems([]);
      setTotal(0);
      setMessage((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    void load(1);
  }

  function clearFilters() {
    setSearch("");
    setPriceMin("");
    setPriceMax("");
    setPage(1);
  }

  useEffect(() => {
    void load(1);
  }, []);

  return (
    <div className="page-shell space-y-6">
      <header className="animate-soft-enter">
        <span className="page-header-kicker">Smart Core / Produtos</span>
        <h1 className="mt-2 text-4xl font-display tracking-tight">Produtos</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Consulte os produtos cadastrados pela importação, com busca por SKU, nome e faixa de preço.
        </p>
      </header>

      {message && (
        <Alert variant="destructive">
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <section className="rounded-lg border border-border bg-surface p-4">
        <form onSubmit={handleSubmit} className="grid grid-cols-1 gap-3 lg:grid-cols-[1.4fr_0.8fr_0.8fr_auto_auto] lg:items-end">
          <div className="space-y-1">
            <Label htmlFor="products-search">Produto</Label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="products-search"
                className="pl-9"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Buscar por SKU ou nome"
              />
            </div>
          </div>

          <div className="space-y-1">
            <Label htmlFor="products-price-min">Preço mínimo</Label>
            <Input id="products-price-min" inputMode="decimal" value={priceMin} onChange={(event) => setPriceMin(event.target.value)} placeholder="0,00" />
          </div>

          <div className="space-y-1">
            <Label htmlFor="products-price-max">Preço máximo</Label>
            <Input id="products-price-max" inputMode="decimal" value={priceMax} onChange={(event) => setPriceMax(event.target.value)} placeholder="999,99" />
          </div>

          <Button type="submit">
            <Search className="size-4" />
            Filtrar
          </Button>

          <Button type="button" variant="outline" onClick={clearFilters}>
            Limpar
          </Button>
        </form>
      </section>

      <section className="grid grid-cols-1 gap-3 md:grid-cols-3">
        {loading ? (
          <>
            <SkeletonMetricCard />
            <SkeletonMetricCard />
            <SkeletonMetricCard />
          </>
        ) : (
          <>
            <KpiCard title="Produtos cadastrados" value={formatKpiCompactNumber(total)} valueTooltip={String(total)} showPercentageChange={false} icon={Package} periodLabel="Total encontrado nos filtros" />
            <KpiCard title="Preço médio" value={formatKpiCompactCurrency(pageAveragePrice)} valueTooltip={formatCurrency(pageAveragePrice)} showPercentageChange={false} icon={DollarSign} periodLabel="Média da página atual" />
            <KpiCard title="Maior preço" value={formatKpiCompactCurrency(maxPagePrice)} valueTooltip={formatCurrency(maxPagePrice)} showPercentageChange={false} icon={Boxes} periodLabel="Maior valor da página atual" />
          </>
        )}
      </section>

      <Card className="animate-soft-enter border-border/80 bg-card/95">
        <CardHeader>
          <CardTitle>Produtos cadastrados</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {loading ? (
            <SkeletonTable rows={8} columns={5} />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>SKU</TableHead>
                  <TableHead>Nome</TableHead>
                  <TableHead>Preço</TableHead>
                  <TableHead>Importado em</TableHead>
                  <TableHead>Arquivo</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                      Nenhum produto encontrado para os filtros atuais.
                    </TableCell>
                  </TableRow>
                )}
                {items.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell className="font-mono text-xs">{item.sku}</TableCell>
                    <TableCell className="font-medium">{item.name}</TableCell>
                    <TableCell>{formatCurrency(item.price)}</TableCell>
                    <TableCell>{formatDate(item.createdAt)}</TableCell>
                    <TableCell>#{item.sourceFileJobId}</TableCell>
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
        </CardContent>
      </Card>
    </div>
  );
}
