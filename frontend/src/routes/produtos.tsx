import { useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { fetchProducts, type Product } from "@/lib/importer-api";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { ChevronLeft, ChevronRight, PackageSearch, Search } from "lucide-react";

export const Route = createFileRoute("/produtos")({
  component: ProdutosPage,
});

const DEFAULT_PAGE_SIZE = 20;
const PRODUCT_SEARCH_DEBOUNCE_MS = 300;

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value ?? 0);
}

function formatDate(value: string): string {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleDateString("pt-BR");
}

function ProdutosPage() {
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [products, setProducts] = useState<Product[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const debouncedSearch = useDebouncedValue(search, PRODUCT_SEARCH_DEBOUNCE_MS);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(total / DEFAULT_PAGE_SIZE)), [total]);

  useEffect(() => {
    setPage(1);
  }, [debouncedSearch]);

  useEffect(() => {
    const controller = new AbortController();

    async function loadProducts() {
      setLoading(true);
      try {
        const result = await fetchProducts({
          page,
          pageSize: DEFAULT_PAGE_SIZE,
          search: debouncedSearch,
          signal: controller.signal,
        });
        setProducts(result.items);
        setTotal(result.total);
        setMessage("");
      } catch (error) {
        if (error instanceof DOMException && error.name === "AbortError") return;
        setProducts([]);
        setTotal(0);
        setMessage(error instanceof Error ? error.message : "Não foi possível carregar os produtos.");
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      }
    }

    void loadProducts();
    return () => controller.abort();
  }, [debouncedSearch, page]);

  return (
    <div className="page-shell space-y-8">
      <header className="animate-soft-enter space-y-5">
        <div className="max-w-3xl">
          <span className="page-header-kicker">Smart Core / Produtos</span>
          <h1 className="mt-2 text-3xl font-display tracking-tight text-foreground md:text-4xl">Produtos</h1>
          <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
            Consulte os produtos cadastrados pela importação, com busca por SKU ou descrição.
          </p>
        </div>

        <section className="space-y-4 rounded-xl border border-border/80 bg-surface/95 p-4 shadow-xs">
          <div className="grid grid-cols-1 gap-4 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-end">
            <div className="space-y-1.5">
              <Label htmlFor="product-search">Buscar produto</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="product-search"
                  className="pl-9 pr-16"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="SKU ou descrição do produto"
                />
                {search && (
                  <Button type="button" variant="ghost" size="sm" className="absolute right-1 top-1/2 h-8 -translate-y-1/2 px-2 text-xs" onClick={() => setSearch("")}>
                    Limpar
                  </Button>
                )}
              </div>
            </div>
            <Button type="button" variant="outline" className="h-10" onClick={() => setSearch("")} disabled={!search}>
              Limpar busca
            </Button>
          </div>
        </section>
      </header>

      {message && (
        <Alert variant="destructive" className="animate-soft-enter">
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <Card className="animate-soft-enter border-border/80 bg-card/95 hover:translate-y-0 hover:border-border/80 hover:shadow-sm">
        <CardHeader className="pb-3">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <PackageSearch className="size-5 text-primary" />
                Produtos cadastrados
              </CardTitle>
              <p className="mt-1 text-xs text-muted-foreground">
                {loading ? "Carregando..." : `${total} produto(s) encontrado(s)`}
              </p>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          {loading ? (
            <SkeletonTable rows={8} columns={5} />
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>SKU</TableHead>
                    <TableHead>Produto</TableHead>
                    <TableHead>Preço</TableHead>
                    <TableHead>Importado em</TableHead>
                    <TableHead>Job</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {products.length === 0 && (
                    <TableRow>
                      <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                        Nenhum produto encontrado.
                      </TableCell>
                    </TableRow>
                  )}
                  {products.map((product) => (
                    <TableRow key={product.id} className="animate-soft-enter">
                      <TableCell>{product.sku}</TableCell>
                      <TableCell>{product.name}</TableCell>
                      <TableCell>{formatCurrency(product.price)}</TableCell>
                      <TableCell>{formatDate(product.createdAt)}</TableCell>
                      <TableCell>{product.sourceFileJobId}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
              <div className="flex items-center justify-end gap-2">
                <Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => setPage((value) => Math.max(1, value - 1))}>
                  <ChevronLeft className="mr-1 size-4" />
                  Anterior
                </Button>
                <span className="text-xs text-muted-foreground">Página {page} de {totalPages}</span>
                <Button variant="outline" size="sm" disabled={page >= totalPages || loading} onClick={() => setPage((value) => value + 1)}>
                  Próxima
                  <ChevronRight className="ml-1 size-4" />
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
