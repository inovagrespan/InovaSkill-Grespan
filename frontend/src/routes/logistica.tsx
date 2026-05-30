import { createFileRoute, Link } from "@tanstack/react-router";
import { Truck, ArrowRight, MapPin, Fuel, Package, AlertTriangle, Clock, Activity } from "lucide-react";

export const Route = createFileRoute("/logistica")({ component: Logistica });

function Logistica() {
  return (
    <div className="p-12">
      <header className="mb-12 flex items-end justify-between animate-fade-in">
        <div>
          <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-muted-foreground">Smart Core / Logística</span>
          <h1 className="mt-2 mb-2 text-4xl font-display tracking-tight text-balance">Cenário Atual da Operação</h1>
          <p className="max-w-[60ch] text-muted-foreground text-pretty">Linha de base da frota, rotas e CDs. A IA cruza estes números com a previsão de vendas para projetar gargalos e custos.</p>
        </div>
        <span className="text-[10px] font-mono uppercase tracking-[0.2em] text-muted-foreground">Sync · há 4 min</span>
      </header>

      <section className="mb-10 grid grid-cols-4 gap-4">
        {[
          { l: "Frota Ativa", v: "86", sub: "de 92 veículos", icon: Truck, accent: false },
          { l: "Ocupação Média", v: "87%", sub: "meta 85%", icon: Activity, accent: true },
          { l: "Custo / Km", v: "R$ 5,42", sub: "+6.1% vs trimestre", icon: Fuel, accent: false },
          { l: "SLA Entregas", v: "93.1%", sub: "alerta em 2 rotas", icon: Clock, accent: false, warn: true },
        ].map((s) => (
          <div key={s.l} className="rounded-xl border border-border bg-surface p-5">
            <div className="mb-3 flex items-start justify-between"><p className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">{s.l}</p><s.icon className={"size-3.5 " + (s.warn ? "text-amber-600" : "text-muted-foreground")} /></div>
            <p className={"text-2xl font-display tabular-nums " + (s.accent ? "text-primary" : s.warn ? "text-amber-600" : "text-foreground")}>{s.v}</p>
            <p className="mt-1 text-[10px] font-mono uppercase tracking-wider text-muted-foreground">{s.sub}</p>
          </div>
        ))}
      </section>

      <section className="rounded-2xl border border-primary/30 bg-surface p-8 ring-4 ring-primary/5">
        <div className="flex items-start gap-4">
          <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10"><AlertTriangle className="size-5 text-primary" /></div>
          <div className="flex-1">
            <h3 className="mb-1 text-lg font-display">Frota a 87% e CD Central no limite. Comporta um pico?</h3>
            <p className="max-w-[60ch] text-sm text-muted-foreground">Com 2 rotas em alerta de SLA e o CD Central a 94%, qualquer aumento de demanda força terceirização. Rode a simulação para dimensionar.</p>
          </div>
          <Link to="/simulacao" className="inline-flex shrink-0 items-center gap-2 rounded-lg bg-primary px-6 py-3 text-xs font-bold uppercase tracking-widest text-primary-foreground transition-all hover:brightness-110">Simular Impacto <ArrowRight className="size-4" /></Link>
        </div>
      </section>
    </div>
  );
}
