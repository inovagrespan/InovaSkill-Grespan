import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowRight, Users, Truck, Activity, TrendingUp } from "lucide-react";
import { KpiCard } from "@/components/ui/kpi-card";

export const Route = createFileRoute("/")({
  component: Dashboard,
});

function Dashboard() {
  return (
    <div className="page-shell">
      <header className="flex flex-col lg:flex-row lg:justify-between lg:items-end gap-4 mb-10 animate-soft-enter">
        <div>
          <span className="page-header-kicker">Smart Core / Visão Executiva</span>
          <h1 className="text-4xl font-display tracking-tight text-balance mt-2 mb-2">
            Dashboard Operacional
          </h1>
          <p className="text-muted-foreground max-w-[60ch] text-pretty">
            Bem-vindo ao Grespan. Comece revisando o cenário atual do RH e em seguida
            simule picos de demanda para visualizar o efeito cascata.
          </p>
        </div>
        <span className="text-[10px] font-mono text-muted-foreground uppercase tracking-[0.2em]">
          Versão 4.2.0
        </span>
      </header>

      <section className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4 mb-8 animate-soft-enter">
        {[
          { label: "Funcionários Ativos", value: "142", icon: Users, pct: 2.1, trend: [132, 136, 138, 139, 141, 142] },
          { label: "Frota Operacional", value: "86", icon: Truck, pct: -1.3, trend: [91, 90, 90, 89, 87, 86] },
          { label: "Eficácia S&OP", value: "98,1%", icon: TrendingUp, pct: 0.4, trend: [96.8, 97.2, 97.6, 97.9, 98.0, 98.1] },
          { label: "Simulações no Mês", value: "27", icon: Activity, pct: 8.0, trend: [14, 15, 17, 20, 24, 27] },
        ].map((s) => (
          <KpiCard
            key={s.label}
            title={s.label}
            value={s.value}
            percentageChange={s.pct}
            trendData={s.trend}
            periodLabel="Comparado ao período anterior"
            icon={s.icon}
          />
        ))}
      </section>

      <section className="grid grid-cols-1 xl:grid-cols-2 gap-6 animate-soft-enter">
        <Link
          to="/rh"
          className="group glass-card bg-surface border border-border p-8 rounded-2xl transition-all duration-200 hover:border-white/20 hover:-translate-y-0.5"
        >
          <span className="text-[10px] font-mono text-muted-foreground uppercase tracking-widest">
            Passo 01
          </span>
          <h3 className="text-2xl font-display mt-3 mb-3">Contexto Atual do RH</h3>
          <p className="text-sm text-muted-foreground mb-6 text-pretty max-w-[48ch]">
            Confira o quadro real de funcionários, turnover e vagas em aberto antes de
            estressar a operação.
          </p>
          <span className="inline-flex items-center gap-2 text-sm font-medium text-primary">
            Abrir visão de RH <ArrowRight className="size-4 transition-transform duration-200 group-hover:translate-x-0.5" />
          </span>
        </Link>

        <Link
          to="/simulacao"
          className="group glass-card bg-surface border-2 border-primary/40 ring-4 ring-primary/5 hover:ring-primary/10 p-8 rounded-2xl transition-all duration-200 hover:-translate-y-0.5"
        >
          <span className="text-[10px] font-mono text-primary uppercase tracking-widest">
            Passo 02 - Recomendado
          </span>
          <h3 className="text-2xl font-display mt-3 mb-3">Simulador de Demanda</h3>
          <p className="text-sm text-muted-foreground mb-6 text-pretty max-w-[48ch]">
            Arraste o slider para projetar picos de venda e veja a IA recomendar
            soluções de logística e contratação.
          </p>
          <span className="inline-flex items-center gap-2 text-sm font-bold text-primary">
            Iniciar simulação <ArrowRight className="size-4 transition-transform duration-200 group-hover:translate-x-0.5" />
          </span>
        </Link>
      </section>

      <section className="mt-8 grid grid-cols-1 xl:grid-cols-2 gap-6 animate-soft-enter">
        <Link
          to="/logistica"
          className="glass-card bg-surface/50 border border-border p-6 rounded-xl transition-all duration-200 hover:border-primary/40 hover:-translate-y-0.5"
        >
          <div className="flex items-center justify-between">
            <div>
              <p className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
                Módulo Logística
              </p>
              <h4 className="text-lg font-display mt-1">Frota, Rotas e CDs</h4>
              <p className="text-xs text-muted-foreground mt-2 max-w-[36ch]">
                Ocupação da frota, custo por rota e status dos centros de distribuição em tempo real.
              </p>
            </div>
            <Truck className="size-5 text-primary" />
          </div>
        </Link>
        <Link
          to="/vendas"
          className="glass-card bg-surface/50 border border-border p-6 rounded-xl transition-all duration-200 hover:border-primary/40 hover:-translate-y-0.5"
        >
          <div className="flex items-center justify-between">
            <div>
              <p className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
                Módulo Vendas
              </p>
              <h4 className="text-lg font-display mt-1">Histórico e Previsão IA</h4>
              <p className="text-xs text-muted-foreground mt-2 max-w-[36ch]">
                12 meses de vendas com projeção de 6 meses por cliente ativo e novos clientes.
              </p>
            </div>
            <TrendingUp className="size-5 text-primary" />
          </div>
        </Link>
      </section>
    </div>
  );
}

