import { useEffect, useMemo, useState } from "react";
import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowLeft, TrendingDown, TrendingUp, Target } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { SkeletonTable } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  fetchCustomerFinanceImpact,
  formatImpactActionPercent,
  getImpactCustomerName,
  getImpactList,
  getImpactListTitle,
  type CustomerFinanceImpactListType,
} from "@/lib/customer-finance-impact";
import type { CustomerFinanceImpactData } from "@/lib/customer-finance-impact-demo";

export const Route = createFileRoute("/clientes-impacto")({
  validateSearch: (search: Record<string, unknown>) => ({
    tipo: isImpactListType(search.tipo) ? search.tipo : "risco",
  }),
  component: ClientesImpactoPage,
});

function isImpactListType(value: unknown): value is CustomerFinanceImpactListType {
  return value === "risco" || value === "crescimento" || value === "oportunidades";
}

function formatCurrency(value: number | null | undefined): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value ?? 0);
}

function formatDecimal(value: number | null | undefined): string {
  return new Intl.NumberFormat("pt-BR", { minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(value ?? 0);
}

function ClientesImpactoPage() {
  const { tipo } = Route.useSearch();
  const [data, setData] = useState<CustomerFinanceImpactData | null>(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function loadImpact() {
      setLoading(true);
      setMessage("");
      try {
        const result = await fetchCustomerFinanceImpact();
        if (!cancelled) setData(result);
      } catch (error) {
        if (!cancelled) setMessage((error as Error).message);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadImpact();
    return () => {
      cancelled = true;
    };
  }, []);

  const rows = useMemo(() => (data ? getImpactList(data, tipo) : []), [data, tipo]);
  const title = getImpactListTitle(tipo);
  const Icon = tipo === "risco" ? TrendingDown : tipo === "crescimento" ? TrendingUp : Target;

  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <span className="page-header-kicker">Finanças / Impacto</span>
          <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">{title}</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Lista completa dos clientes analisados na base financeira real.
          </p>
        </div>
        <Button variant="outline" asChild>
          <Link to="/clientes">
            <ArrowLeft className="mr-2 size-4" />
            Voltar para impacto
          </Link>
        </Button>
      </header>

      {message && (
        <Alert variant="destructive">
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <Card className="animate-soft-enter">
        <CardHeader className="flex flex-row items-center justify-between gap-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <Icon className="size-4 text-primary" />
            {title}
          </CardTitle>
          <span className="text-sm text-muted-foreground">{rows.length} cliente(s)</span>
        </CardHeader>
        <CardContent>
          {loading ? (
            <SkeletonTable rows={8} columns={tipo === "oportunidades" ? 6 : 5} />
          ) : rows.length === 0 ? (
            <div className="py-10 text-center text-sm text-muted-foreground">
              Nenhum cliente encontrado para esta análise.
            </div>
          ) : tipo === "risco" ? (
            <RiskTable rows={rows} />
          ) : tipo === "crescimento" ? (
            <GrowthTable rows={rows} />
          ) : (
            <OpportunityTable rows={rows} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function RiskTable({ rows }: { rows: any[] }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-border/70">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Cliente</TableHead>
            <TableHead>Nível</TableHead>
            <TableHead>Variação</TableHead>
            <TableHead>Impacto/mês</TableHead>
            <TableHead>Faturamento 12M</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((customer) => (
            <TableRow key={customer.clienteId ?? customer.ClienteId ?? getImpactCustomerName(customer)}>
              <TableCell className="font-medium">{getImpactCustomerName(customer)}</TableCell>
              <TableCell>{customer.nivelRisco ?? "—"}</TableCell>
              <TableCell>{formatImpactActionPercent(customer, "risco")}</TableCell>
              <TableCell>{formatCurrency(customer.impactoFinanceiro ?? 0)}</TableCell>
              <TableCell>{formatCurrency(customer.faturamento12M ?? customer.Faturamento12M ?? 0)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function GrowthTable({ rows }: { rows: any[] }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-border/70">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Cliente</TableHead>
            <TableHead>Potencial</TableHead>
            <TableHead>Crescimento</TableHead>
            <TableHead>Valor gerado</TableHead>
            <TableHead>Faturamento 12M</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((customer) => (
            <TableRow key={customer.clienteId ?? customer.ClienteId ?? getImpactCustomerName(customer)}>
              <TableCell className="font-medium">{getImpactCustomerName(customer)}</TableCell>
              <TableCell>{customer.potencialFuturo ?? customer.potencial ?? "—"}</TableCell>
              <TableCell>{formatImpactActionPercent(customer, "crescimento", true)}</TableCell>
              <TableCell>{formatCurrency(customer.valorGerado ?? 0)}</TableCell>
              <TableCell>{formatCurrency(customer.faturamento12M ?? customer.Faturamento12M ?? 0)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function OpportunityTable({ rows }: { rows: any[] }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-border/70">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Cliente</TableHead>
            <TableHead>Potencial</TableHead>
            <TableHead>Score</TableHead>
            <TableHead>Crescimento</TableHead>
            <TableHead>Ticket médio</TableHead>
            <TableHead>Frequência</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((customer) => (
            <TableRow key={customer.clienteId ?? customer.ClienteId ?? getImpactCustomerName(customer)}>
              <TableCell className="font-medium">{getImpactCustomerName(customer)}</TableCell>
              <TableCell>{customer.potencial ?? customer.potencialFuturo ?? "—"}</TableCell>
              <TableCell>{customer.scorePotencial ?? customer.ScorePotencial ?? "—"}</TableCell>
              <TableCell>{formatImpactActionPercent(customer, "oportunidades", true)}</TableCell>
              <TableCell>{formatCurrency(customer.ticketMedioGeral ?? customer.TicketMedioGeral ?? 0)}</TableCell>
              <TableCell>{formatDecimal(customer.frequenciaCompra ?? customer.FrequenciaCompra ?? 0)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
