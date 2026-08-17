import { useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Loader2, MapPin, Search } from "lucide-react";
import { CustomerConsumptionDialog } from "@/components/CustomerConsumptionDialog";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { SkeletonTable } from "@/components/ui/skeleton";
import { FeedbackMessage } from "@/components/ui/feedback-message";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { fetchCurrentCustomers, runOperationalJob, type CurrentCustomerListItem } from "@/lib/importer-api";
import { canCurrentUserAccessProcessingArea } from "@/lib/auth";
import { TEXT_SEARCH_DEBOUNCE_MS, useDebouncedValue } from "@/lib/use-debounced-value";

export const Route = createFileRoute("/clientes")({ component: CustomersPage });

const PAGE_SIZE = 25;
const CUSTOMER_ADDRESS_JOB_TYPE = "CUSTOMER_REGISTRATION_ADDRESS_ENRICHMENT";
const CUSTOMER_ADDRESS_JOB_CONTRACT_VERSION = 1;
function CustomersPage() {
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, TEXT_SEARCH_DEBOUNCE_MS);
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<CurrentCustomerListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [selectedCustomerId, setSelectedCustomerId] = useState<string | null>(null);
  const [startingAddressJob, setStartingAddressJob] = useState(false);
  const [feedback, setFeedback] = useState<{ message: string; type: "success" | "error" } | null>(null);
  const canRunAddressJob = canCurrentUserAccessProcessingArea();

  async function startAddressEnrichment() {
    setStartingAddressJob(true);
    setFeedback(null);
    try {
      await runOperationalJob(CUSTOMER_ADDRESS_JOB_TYPE, CUSTOMER_ADDRESS_JOB_CONTRACT_VERSION, {
        customerStatus: "ACTIVE",
      });
      setFeedback({
        message: "Enriquecimento iniciado. Acompanhe a execução na Central de Processamentos e atualize a lista ao concluir.",
        type: "success",
      });
    } catch (reason) {
      setFeedback({ message: (reason as Error).message, type: "error" });
    } finally {
      setStartingAddressJob(false);
    }
  }

  useEffect(() => setPage(1), [debouncedSearch]);
  useEffect(() => {
    let active = true;
    setLoading(true);
    setError("");
    fetchCurrentCustomers(page, PAGE_SIZE, debouncedSearch)
      .then(result => {
        if (!active) return;
        setItems(result.items);
        setTotal(result.total);
      })
      .catch(reason => {
        if (active) setError((reason as Error).message);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [page, debouncedSearch]);

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  return (
    <div className="page-shell app-background min-w-0 max-w-full overflow-x-hidden">
      <header className="animate-soft-enter flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <span className="page-header-kicker">Cadastros</span>
          <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">Clientes</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Consulte os clientes da importação atualmente publicada.
          </p>
        </div>
        {canRunAddressJob && (
          <Button onClick={() => void startAddressEnrichment()} disabled={startingAddressJob}>
            {startingAddressJob ? <Loader2 className="mr-2 size-4 animate-spin" /> : <MapPin className="mr-2 size-4" />}
            Enriquecer endereços
          </Button>
        )}
      </header>

      <FeedbackMessage message={feedback?.message ?? null} type={feedback?.type} onDismiss={() => setFeedback(null)} />

      {error && <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert>}

      <Card className="min-w-0 overflow-hidden border-border bg-surface">
        <CardHeader className="gap-4 sm:flex-row sm:items-center sm:justify-between sm:space-y-0">
          <CardTitle>Clientes cadastrados</CardTitle>
          <div className="relative w-full sm:max-w-md">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              aria-label="Buscar clientes"
              value={search}
              onChange={event => setSearch(event.target.value)}
              placeholder="Buscar por código, nome, documento ou cidade..."
              className="w-full pl-9"
            />
          </div>
        </CardHeader>
        <CardContent className="min-w-0 space-y-4">
          {loading ? <SkeletonTable rows={8} columns={7} /> : items.length === 0 ? (
            <p className="py-10 text-center text-sm text-muted-foreground">
              {debouncedSearch ? "Nenhum cliente encontrado para esta busca." : "Nenhum cliente importado."}
            </p>
          ) : (
            <Table className="min-w-[1040px] table-fixed">
              <TableHeader>
                <TableRow>
                  <TableHead className="w-24">Código</TableHead>
                  <TableHead className="w-52">Nome Fantasia</TableHead>
                  <TableHead className="w-64">Razão Social</TableHead>
                  <TableHead className="w-40">Documento</TableHead>
                  <TableHead className="w-28">Tipo</TableHead>
                  <TableHead className="w-16">UF</TableHead>
                  <TableHead className="w-44">Município</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map(item => (
                  <TableRow key={item.id} className="cursor-pointer" tabIndex={0}
                    onClick={() => setSelectedCustomerId(item.id)}
                    onKeyDown={event => { if (event.key === "Enter") setSelectedCustomerId(item.id); }}>
                    <TableCell className="truncate font-mono" title={item.externalCode}>{item.externalCode}</TableCell>
                    <TableCell className="truncate" title={item.tradeName}>{item.tradeName || "-"}</TableCell>
                    <TableCell className="truncate" title={item.legalName}>{item.legalName || "-"}</TableCell>
                    <TableCell className="truncate font-mono" title={item.documentNumber}>{item.documentNumber || "-"}</TableCell>
                    <TableCell className="truncate" title={item.customerType}>{item.customerType || "-"}</TableCell>
                    <TableCell>{item.stateCode}</TableCell>
                    <TableCell className="truncate" title={item.municipalityName}>{item.municipalityName}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}

          <div className="flex flex-col gap-3 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
            <span>{total} cliente(s)</span>
            <div className="flex flex-wrap items-center gap-2">
              <Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => setPage(value => value - 1)}>
                Anterior
              </Button>
              <span>Página {page} de {totalPages}</span>
              <Button variant="outline" size="sm" disabled={page >= totalPages || loading} onClick={() => setPage(value => value + 1)}>
                Próxima
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
      <CustomerConsumptionDialog id={selectedCustomerId} open={selectedCustomerId !== null}
        onOpenChange={open => { if (!open) setSelectedCustomerId(null); }} />
    </div>
  );
}
