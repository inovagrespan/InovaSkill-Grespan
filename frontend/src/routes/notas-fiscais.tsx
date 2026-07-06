import { useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Search } from "lucide-react";
import { FiscalDocumentDialog } from "@/components/FiscalDocumentDialog";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { fetchFiscalDocuments, type FiscalDocumentListItem } from "@/lib/importer-api";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";

export const Route = createFileRoute("/notas-fiscais")({ component: FiscalDocumentsPage });
const PAGE_SIZE = 25;
const weight = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 3 });

function FiscalDocumentsPage() {
  const [search, setSearch] = useState(""); const debounced = useDebouncedValue(search, TEXT_SEARCH_DEBOUNCE_MS);
  const [category, setCategory] = useState(""); const [page, setPage] = useState(1);
  const [items, setItems] = useState<FiscalDocumentListItem[]>([]); const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true); const [error, setError] = useState(""); const [selected, setSelected] = useState<string | null>(null);
  useEffect(() => setPage(1), [debounced, category]);
  useEffect(() => { let active = true; setLoading(true); setError("");
    fetchFiscalDocuments(page, PAGE_SIZE, debounced, category).then(result => { if (active) { setItems(result.items); setTotal(result.total); } })
      .catch(reason => active && setError((reason as Error).message)).finally(() => active && setLoading(false));
    return () => { active = false; }; }, [page, debounced, category]);
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  return <div className="page-shell app-background min-w-0 max-w-full overflow-x-hidden">
    <header><span className="page-header-kicker">Movimentações</span><h1 className="mt-1 text-3xl font-display font-semibold">Notas Fiscais</h1>
      <p className="mt-1 text-sm text-muted-foreground">Explore os fatos fiscais importados e seus itens.</p></header>
    {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}
    <Card><CardHeader className="gap-4 sm:flex-row sm:items-center sm:justify-between"><CardTitle>Documentos fiscais</CardTitle>
      <div className="flex w-full gap-2 sm:max-w-2xl"><div className="relative flex-1"><Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input aria-label="Buscar notas fiscais" className="pl-9" value={search} onChange={event => setSearch(event.target.value)} placeholder="Buscar por documento, cliente ou cidade..." /></div>
        <select aria-label="Filtrar por operação" className="rounded-md border border-input bg-background px-3 text-sm" value={category} onChange={event => setCategory(event.target.value)}>
          <option value="">Todas as operações</option><option value="Sale">Venda</option><option value="Return">Devolução</option><option value="Bonus">Bonificação</option><option value="Loan">Comodato</option><option value="Exchange">Troca</option><option value="Unknown">Desconhecida</option>
        </select></div></CardHeader>
      <CardContent className="space-y-4">{loading ? <SkeletonTable rows={8} columns={7} /> : items.length === 0 ? <p className="py-10 text-center text-sm text-muted-foreground">Nenhuma nota fiscal encontrada.</p> :
        <Table><TableHeader><TableRow><TableHead>Data</TableHead><TableHead>Documento / Série</TableHead><TableHead>Cliente</TableHead><TableHead>Cidade</TableHead><TableHead>Operação</TableHead><TableHead>Itens</TableHead><TableHead>Peso</TableHead></TableRow></TableHeader>
          <TableBody>{items.map(item => <TableRow key={item.id} className="cursor-pointer" tabIndex={0} onClick={() => setSelected(item.id)} onKeyDown={event => { if (event.key === "Enter") setSelected(item.id); }}>
            <TableCell>{new Date(`${item.issueDate}T12:00:00`).toLocaleDateString("pt-BR")}</TableCell><TableCell>{item.documentNumber}{item.series ? ` / ${item.series}` : ""}</TableCell>
            <TableCell>{item.customerNameAtIssue || item.customerCodeAtIssue}</TableCell><TableCell>{item.cityNameAtIssue}</TableCell><TableCell>{item.operationDescription || item.operationCategory}</TableCell><TableCell>{item.itemCount}</TableCell><TableCell>{weight.format(item.grossWeightKg)} kg</TableCell>
          </TableRow>)}</TableBody></Table>}
        <div className="flex items-center justify-between text-sm text-muted-foreground"><span>{total} nota(s)</span><div className="flex items-center gap-2"><Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => setPage(page - 1)}>Anterior</Button><span>Página {page} de {pages}</span><Button variant="outline" size="sm" disabled={page >= pages || loading} onClick={() => setPage(page + 1)}>Próxima</Button></div></div>
      </CardContent></Card>
    <FiscalDocumentDialog id={selected} open={selected !== null} onOpenChange={open => !open && setSelected(null)} />
  </div>;
}
