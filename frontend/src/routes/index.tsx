import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowRight, Activity, ReceiptText, TrendingUp, Truck } from "lucide-react";
import { KpiCard } from "@/components/ui/kpi-card";

export const Route = createFileRoute("/")({ component: Dashboard });

function Dashboard() {
  return (
    <div className="page-shell">
      <header className="mb-10 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between animate-soft-enter">
        <div>
          <span className="page-header-kicker">Smart Core / Visão Executiva</span>
          <h1 className="mt-2 mb-2 text-4xl font-display tracking-tight text-balance">Dashboard Operacional</h1>
          <p className="max-w-[60ch] text-muted-foreground text-pretty">
            Bem-vindo ao AAI Seguri. Revise vendas, finanças e operação para visualizar gargalos e preparar decisões de reunião.
          </p>
        </div>
        <span className="text-[10px] font-mono uppercase tracking-[0.2em] text-muted-foreground">Versão 4.2.0</span>
      </header>

      <section className="metric-row mb-8 animate-soft-enter">
        {[
          { label: "Faturamento Total", value: "R$ 187,6 mil", icon: ReceiptText, pct: 7.7, trend: [132, 146, 151, 168, 174, 188] },
          { label: "Frota Operacional", value: "86", icon: Truck, pct: -1.3, trend: [91, 90, 90, 89, 87, 86] },
          { label: "Eficiência S&OP", value: "98,1%", icon: TrendingUp, pct: 0.4, trend: [96.8, 97.2, 97.6, 97.9, 98.0, 98.1] },
          { label: "Simulações no Mês", value: "27", icon: Activity, pct: 8.0, trend: [14, 15, 17, 20, 24, 27] },
        ].map((s) => (
          <KpiCard key={s.label} title={s.label} value={s.value} percentageChange={s.pct} trendData={s.trend} periodLabel="Comparado ao período anterior" icon={s.icon} />
        ))}
      </section>

      <section className="grid grid-cols-1 gap-6 xl:grid-cols-2 animate-soft-enter">
        <Link to="/vendas" className="group glass-card rounded-2xl border border-border bg-surface p-8 transition-all duration-200 hover:border-primary/25 hover:-translate-y-0.5">
          <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">Passo 01</span>
          <h3 className="mt-3 mb-3 text-2xl font-display">Vendas com dados demo</h3>
          <p className="mb-6 max-w-[48ch] text-pretty text-sm text-muted-foreground">Confira faturamento, médias semanal/mensal e ranking por empresa com base fictícia do front.</p>
          <span className="inline-flex items-center gap-2 text-sm font-medium text-primary">Abrir vendas <ArrowRight className="size-4 transition-transform duration-200 group-hover:translate-x-0.5" /></span>
        </Link>

        <Link to="/relatorios" className="group glass-card rounded-2xl border-2 border-primary/40 bg-surface p-8 ring-4 ring-primary/5 transition-all duration-200 hover:-translate-y-0.5 hover:ring-primary/10">
          <span className="text-[10px] font-mono uppercase tracking-widest text-primary">Passo 02 - Recomendado</span>
          <h3 className="mt-3 mb-3 text-2xl font-display">Relatórios para reunião</h3>
          <p className="mb-6 max-w-[48ch] text-pretty text-sm text-muted-foreground">Selecione métricas por área e imprima o resumo filtrado para apresentação.</p>
          <span className="inline-flex items-center gap-2 text-sm font-bold text-primary">Emitir relatório <ArrowRight className="size-4 transition-transform duration-200 group-hover:translate-x-0.5" /></span>
        </Link>
      </section>
    </div>
  );
}
