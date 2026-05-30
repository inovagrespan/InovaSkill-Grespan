import { createFileRoute, Link } from "@tanstack/react-router";
import { AlertTriangle, ArrowRight, Users, TrendingDown, Briefcase } from "lucide-react";

export const Route = createFileRoute("/rh")({ component: RHAtual });

function RHAtual() {
  return (
    <div className="p-12">
      <header className="mb-16 flex items-end justify-between animate-fade-in">
        <div>
          <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-muted-foreground">Smart Core / Recursos Humanos</span>
          <h1 className="mt-2 mb-2 text-4xl font-display tracking-tight text-balance">Cenário Atual do RH</h1>
          <p className="max-w-[60ch] text-muted-foreground text-pretty">Linha de base do capital humano. Estes são os indicadores que a IA usa para calcular a viabilidade dos cenários de expansão.</p>
        </div>
        <span className="text-[10px] font-mono uppercase tracking-[0.2em] text-muted-foreground">Atualizado há 12 min</span>
      </header>

      <section className="mb-12 grid grid-cols-3 gap-6">
        <div className="rounded-xl border border-border bg-surface p-6">
          <div className="mb-4 flex items-start justify-between"><Users className="size-5 text-primary" /><span className="rounded bg-primary/10 px-2 py-0.5 text-[10px] font-mono text-primary">ATIVOS</span></div>
          <p className="mb-2 text-xs text-muted-foreground">Funcionários CLT</p>
          <p className="text-4xl font-display tabular-nums">142</p>
          <div className="mt-4 h-1.5 w-full overflow-hidden rounded-full bg-muted"><div className="h-full w-[92%] bg-primary" /></div>
          <p className="mt-2 text-[10px] font-mono text-muted-foreground">92% DA CAPACIDADE INSTALADA</p>
        </div>

        <div className="rounded-xl border border-danger/30 bg-surface p-6">
          <div className="mb-4 flex items-start justify-between"><TrendingDown className="size-5 text-danger" /><span className="rounded bg-danger/10 px-2 py-0.5 text-[10px] font-mono text-danger">CRÍTICO</span></div>
          <p className="mb-2 text-xs text-muted-foreground">Turnover últimos 90d</p>
          <p className="text-4xl font-display tabular-nums text-danger">8.4%</p>
          <div className="mt-4 flex gap-1"><div className="h-1 flex-1 rounded-full bg-danger" /><div className="h-1 flex-1 rounded-full bg-danger" /><div className="h-1 flex-1 rounded-full bg-muted" /></div>
          <p className="mt-2 text-[10px] font-mono text-danger/80">+2.1% ACIMA DA MÉDIA SETORIAL</p>
        </div>

        <div className="rounded-xl border border-border bg-surface p-6">
          <div className="mb-4 flex items-start justify-between"><Briefcase className="size-5 text-muted-foreground" /><span className="rounded bg-muted px-2 py-0.5 text-[10px] font-mono text-muted-foreground">EM ABERTO</span></div>
          <p className="mb-2 text-xs text-muted-foreground">Vagas em recrutamento</p>
          <p className="text-4xl font-display tabular-nums">12</p>
          <p className="mt-6 text-[10px] font-mono text-muted-foreground">08 OPERACIONAL · 04 ADMINISTRATIVO</p>
        </div>
      </section>

      <section className="rounded-2xl border border-primary/30 bg-surface p-8 ring-4 ring-primary/5">
        <div className="flex items-start gap-4">
          <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10"><AlertTriangle className="size-5 text-primary" /></div>
          <div className="flex-1">
            <h3 className="mb-1 text-lg font-display">A operação está no limite. E se as vendas crescerem?</h3>
            <p className="max-w-[60ch] text-sm text-muted-foreground">Com 92% de capacidade e turnover de 8.4%, qualquer pico de demanda gera gargalo imediato. Simule cenários para ver o impacto financeiro.</p>
          </div>
          <Link to="/simulacao" className="inline-flex shrink-0 items-center gap-2 rounded-lg bg-primary px-6 py-3 text-xs font-bold uppercase tracking-widest text-primary-foreground transition-all hover:brightness-110">Iniciar Simulação <ArrowRight className="size-4" /></Link>
        </div>
      </section>
    </div>
  );
}
