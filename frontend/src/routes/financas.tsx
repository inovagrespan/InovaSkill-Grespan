import { useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { CalendarDays, DollarSign, ReceiptText, Scale, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { KpiCard } from "@/components/ui/kpi-card";
import { Label } from "@/components/ui/label";
import { calculateFinanceMetrics, financeDemoTransactions, listFinanceCustomers } from "@/lib/finance-demo-metrics";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";

export const Route = createFileRoute("/financas")({
  component: FinancasPage,
});

const DEFAULT_DATE_FROM = "2026-01-01";
const DEFAULT_DATE_TO = "2026-06-30";

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value);
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 2 }).format(value);
}

function formatDate(value: string): string {
  return new Date(`${value}T00:00:00`).toLocaleDateString("pt-BR");
}

function FinancasPage() {
  const [customer, setCustomer] = useState("");
  const [dateFrom, setDateFrom] = useState(DEFAULT_DATE_FROM);
  const [dateTo, setDateTo] = useState(DEFAULT_DATE_TO);
  const [allTime, setAllTime] = useState(true);

  const customers = useMemo(() => listFinanceCustomers(financeDemoTransactions), []);
  const metrics = useMemo(
    () => calculateFinanceMetrics({ customer, dateFrom, dateTo, allTime }, financeDemoTransactions),
    [customer, dateFrom, dateTo, allTime],
  );
  const periodLabel = allTime ? "Tempo total" : `${formatDate(dateFrom)} até ${formatDate(dateTo)}`;

  function clearFilters() {
    setCustomer("");
    setDateFrom(DEFAULT_DATE_FROM);
    setDateTo(DEFAULT_DATE_TO);
    setAllTime(true);
  }

  return (
    <div className="page-shell space-y-6">
      <header className="animate-soft-enter">
        <span className="page-header-kicker">Smart Core / Finanças</span>
        <h1 className="mt-2 text-4xl font-display tracking-tight">Finanças</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Painel financeiro com dados fictícios para validar filtros de cliente, período e métricas de reunião.
        </p>
      </header>

      <section className="rounded-lg border border-border bg-surface p-4">
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-[1.2fr_0.9fr_0.9fr_auto_auto] lg:items-end">
          <div className="space-y-1">
            <Label htmlFor="finance-customer">Cliente</Label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="finance-customer"
                list="finance-customer-options"
                className="pl-9"
                value={customer}
                onChange={(event) => setCustomer(event.target.value)}
                placeholder="Todos os clientes"
              />
            </div>
            <datalist id="finance-customer-options">
              {customers.map((item) => <option key={item} value={item} />)}
            </datalist>
          </div>

          <div className="space-y-1">
            <Label htmlFor="finance-date-from">Data inicial</Label>
            <Input id="finance-date-from" type="date" value={dateFrom} disabled={allTime} onChange={(event) => setDateFrom(event.target.value)} />
          </div>

          <div className="space-y-1">
            <Label htmlFor="finance-date-to">Data final</Label>
            <Input id="finance-date-to" type="date" value={dateTo} disabled={allTime} onChange={(event) => setDateTo(event.target.value)} />
          </div>

          <Button type="button" variant={allTime ? "default" : "outline"} onClick={() => setAllTime((value) => !value)}>
            <CalendarDays className="size-4" />
            Tempo total
          </Button>

          <Button type="button" variant="outline" onClick={clearFilters}>
            Limpar
          </Button>
        </div>
      </section>

      <section className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <KpiCard
          title="Faturamento total"
          value={formatKpiCompactCurrency(metrics.totalRevenue)}
          valueTooltip={formatCurrency(metrics.totalRevenue)}
          showPercentageChange={false}
          periodLabel={periodLabel}
          icon={DollarSign}
        />
        <KpiCard
          title="Ticket médio"
          value={formatKpiCompactCurrency(metrics.averageTicket)}
          valueTooltip={formatCurrency(metrics.averageTicket)}
          showPercentageChange={false}
          periodLabel="Faturamento dividido pelos pedidos"
          icon={ReceiptText}
        />
        <KpiCard
          title="Peso / quantidade"
          value={formatKpiCompactNumber(metrics.totalQuantity)}
          valueTooltip={`${formatNumber(metrics.totalQuantity)} unidades compradas`}
          showPercentageChange={false}
          periodLabel="Quantidade comprada pelo cliente"
          icon={Scale}
        />
      </section>

      <section className="rounded-xl border border-border bg-surface p-5">
        <div className="mb-4">
          <h2 className="font-display text-xl">Base fictícia filtrada</h2>
          <p className="text-sm text-muted-foreground">{metrics.items.length} lançamento(s) financeiro(s) encontrados.</p>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-sm">
            <thead>
              <tr className="border-b border-border text-left text-xs uppercase tracking-wide text-muted-foreground">
                <th className="py-3 pr-4 font-medium">Cliente</th>
                <th className="py-3 pr-4 font-medium">Data</th>
                <th className="py-3 pr-4 font-medium">Faturamento</th>
                <th className="py-3 pr-4 font-medium">Pedidos</th>
                <th className="py-3 font-medium">Peso / quantidade</th>
              </tr>
            </thead>
            <tbody>
              {metrics.items.map((item) => (
                <tr key={`${item.customer}-${item.date}-${item.revenue}`} className="border-b border-border/70 last:border-0">
                  <td className="py-3 pr-4 font-medium">{item.customer}</td>
                  <td className="py-3 pr-4">{formatDate(item.date)}</td>
                  <td className="py-3 pr-4">{formatCurrency(item.revenue)}</td>
                  <td className="py-3 pr-4">{formatNumber(item.orders)}</td>
                  <td className="py-3">{formatNumber(item.quantity)}</td>
                </tr>
              ))}
              {metrics.items.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-8 text-center text-muted-foreground">Nenhuma métrica encontrada para os filtros atuais.</td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
