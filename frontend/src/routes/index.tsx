import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowRight, Users, Truck, Activity, TrendingUp } from "lucide-react";

export const Route = createFileRoute("/")({
  component: Dashboard,
});

function Dashboard() {
  return (
    <div className="p-12">
      <header className="flex justify-between items-end mb-16 animate-fade-in">
        <div>
          <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-muted-foreground">
            Smart Core / Visão Executiva
          </span>
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

      <section className="grid grid-cols-4 gap-4 mb-12 animate-fade-in [animation-delay:100ms]">
        {[
          { label: "Funcionários Ativos", value: "142", icon: Users },
          { label: "Frota Operacional", value: "86", icon: Truck },
          { label: "Eficácia S&OP", value: "98.1%", icon: TrendingUp, accent: true },
          { label: "Simulações no Mês", value: "27", icon: Activity },
        ].map((s) => (
          <div key={s.label} className="border border-border bg-surface p-5 rounded-xl">
            <div className="flex justify-between items-start mb-3">
              <p className="text-[10px] text-muted-foreground font-mono uppercase tracking-wider">
                {s.label}
              </p>
              <s.icon className="size-3.5 text-muted-foreground" />
            </div>
            <p
              className={
                "text-2xl font-display tabular-nums " +
                (s.accent ? "text-primary" : "text-foreground")
              }
            >
              {s.value}
            </p>
          </div>
        ))}
      </section>

      <section className="grid grid-cols-2 gap-6 animate-fade-in [animation-delay:200ms]">
        <Link
          to="/rh"
          className="group bg-surface border border-border hover:border-white/20 p-8 rounded-2xl transition-colors"
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
            Abrir visão de RH <ArrowRight className="size-4" />
          </span>
        </Link>

        <Link
          to="/simulacao"
          className="group bg-surface border-2 border-primary/40 ring-4 ring-primary/5 hover:ring-primary/10 p-8 rounded-2xl transition-all"
        >
          <span className="text-[10px] font-mono text-primary uppercase tracking-widest">
            Passo 02 — Recomendado
          </span>
          <h3 className="text-2xl font-display mt-3 mb-3">Simulador de Demanda</h3>
          <p className="text-sm text-muted-foreground mb-6 text-pretty max-w-[48ch]">
            Arraste o slider para projetar picos de venda e veja a IA recomendar
            soluções de logística e contratação.
          </p>
          <span className="inline-flex items-center gap-2 text-sm font-bold text-primary">
            Iniciar simulação <ArrowRight className="size-4" />
          </span>
        </Link>
      </section>

      <section className="mt-12 grid grid-cols-2 gap-6 animate-fade-in [animation-delay:300ms]">
        <Link
          to="/logistica"
          className="bg-surface/50 border border-border hover:border-primary/40 p-6 rounded-xl transition-colors"
        >
          <div className="flex items-center justify-between">
            <div>
              <p className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
                Módulo Logística
              </p>
              <h4 className="text-lg font-display mt-1">Frota, Rotas & CDs</h4>
              <p className="text-xs text-muted-foreground mt-2 max-w-[36ch]">
                Ocupação da frota, custo por rota e status dos centros de distribuição em tempo real.
              </p>
            </div>
            <Truck className="size-5 text-primary" />
          </div>
        </Link>
        <Link
          to="/vendas"
          className="bg-surface/50 border border-border hover:border-primary/40 p-6 rounded-xl transition-colors"
        >
          <div className="flex items-center justify-between">
            <div>
              <p className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
                Módulo Vendas
              </p>
              <h4 className="text-lg font-display mt-1">Histórico & Previsão IA</h4>
              <p className="text-xs text-muted-foreground mt-2 max-w-[36ch]">
                12 meses de vendas + projeção de 6 meses por cliente ativo e novos clientes.
              </p>
            </div>
            <TrendingUp className="size-5 text-primary" />
          </div>
        </Link>
      </section>
    </div>
  );
}


