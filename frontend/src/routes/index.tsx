import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowRight, Users, Truck, Activity, TrendingUp } from "lucide-react";
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
            Bem-vindo ao AAI Seguri. Comece revisando o cenário atual do RH e em seguida simule picos de demanda para visualizar o efeito cascata.
          </p>
        </div>
        <span className="text-[10px] font-mono uppercase tracking-[0.2em] text-muted-foreground">Versão 4.2.0</span>
      </header>

      <section className="mb-8 grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4 animate-soft-enter">
        {[
          { label: "Funcionários Ativos", value: "142", icon: Users, pct: 2.1, trend: [132, 136, 138, 139, 141, 142] },
          { label: "Frota Operacional", value: "86", icon: Truck, pct: -1.3, trend: [91, 90, 90, 89, 87, 86] },
          { label: "Eficiência S&OP", value: "98,1%", icon: TrendingUp, pct: 0.4, trend: [96.8, 97.2, 97.6, 97.9, 98.0, 98.1] },
          { label: "Simulações no Mês", value: "27", icon: Activity, pct: 8.0, trend: [14, 15, 17, 20, 24, 27] },
        ].map((s) => (
          <KpiCard key={s.label} title={s.label} value={s.value} percentageChange={s.pct} trendData={s.trend} periodLabel="Comparado ao período anterior" icon={s.icon} />
        ))}
      </section>

      <section className="grid grid-cols-1 gap-6 xl:grid-cols-2 animate-soft-enter">
        <Link to="/rh" className="group glass-card rounded-2xl border border-border bg-surface p-8 transition-all duration-200 hover:border-primary/25 hover:-translate-y-0.5">
          <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">Passo 01</span>
          <h3 className="mt-3 mb-3 text-2xl font-display">Contexto Atual do RH</h3>
          <p className="mb-6 max-w-[48ch] text-pretty text-sm text-muted-foreground">Confira o quadro real de funcionários, turnover e vagas em aberto antes de estressar a operação.</p>
          <span className="inline-flex items-center gap-2 text-sm font-medium text-primary">Abrir visão de RH <ArrowRight className="size-4 transition-transform duration-200 group-hover:translate-x-0.5" /></span>
        </Link>

        <Link to="/simulacao" className="group glass-card rounded-2xl border-2 border-primary/40 bg-surface p-8 ring-4 ring-primary/5 transition-all duration-200 hover:-translate-y-0.5 hover:ring-primary/10">
          <span className="text-[10px] font-mono uppercase tracking-widest text-primary">Passo 02 - Recomendado</span>
          <h3 className="mt-3 mb-3 text-2xl font-display">Simulador de Demanda</h3>
          <p className="mb-6 max-w-[48ch] text-pretty text-sm text-muted-foreground">Arraste o slider para projetar picos de venda e veja a IA recomendar soluções de logística e contratação.</p>
          <span className="inline-flex items-center gap-2 text-sm font-bold text-primary">Iniciar simulação <ArrowRight className="size-4 transition-transform duration-200 group-hover:translate-x-0.5" /></span>
        </Link>
      </section>
    </div>
  );
}
