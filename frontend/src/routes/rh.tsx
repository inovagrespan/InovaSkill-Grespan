import { createFileRoute, Link } from "@tanstack/react-router";
import { AlertTriangle, ArrowRight, Users, TrendingDown, Briefcase } from "lucide-react";

export const Route = createFileRoute("/rh")({
  component: RHAtual,
});

function RHAtual() {
  return (
    <div className="p-12">
      <header className="flex justify-between items-end mb-16 animate-fade-in">
        <div>
          <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-muted-foreground">
            Smart Core / Recursos Humanos
          </span>
          <h1 className="text-4xl font-display tracking-tight text-balance mt-2 mb-2">
            CenÃ¡rio Atual do RH
          </h1>
          <p className="text-muted-foreground max-w-[60ch] text-pretty">
            Linha de base do capital humano. Estes sÃ£o os indicadores que a IA usa para
            calcular a viabilidade dos cenÃ¡rios de expansÃ£o.
          </p>
        </div>
        <span className="text-[10px] font-mono text-muted-foreground uppercase tracking-[0.2em]">
          Atualizado hÃ¡ 12 min
        </span>
      </header>

      <section className="grid grid-cols-3 gap-6 mb-12">
        <div className="bg-surface border border-border p-6 rounded-xl animate-fade-in [animation-delay:100ms]">
          <div className="flex justify-between items-start mb-4">
            <Users className="size-5 text-primary" />
            <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-primary/10 text-primary">
              ATIVOS
            </span>
          </div>
          <p className="text-xs text-muted-foreground mb-2">FuncionÃ¡rios CLT</p>
          <p className="text-4xl font-display tabular-nums text-foreground">142</p>
          <div className="mt-4 h-1.5 w-full bg-white/5 rounded-full overflow-hidden">
            <div className="h-full w-[92%] bg-primary" />
          </div>
          <p className="text-[10px] mt-2 text-muted-foreground font-mono">
            92% DA CAPACIDADE INSTALADA
          </p>
        </div>

        <div className="bg-surface border border-danger/30 p-6 rounded-xl animate-fade-in [animation-delay:200ms]">
          <div className="flex justify-between items-start mb-4">
            <TrendingDown className="size-5 text-danger" />
            <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-danger/10 text-danger">
              CRÃTICO
            </span>
          </div>
          <p className="text-xs text-muted-foreground mb-2">Turnover Ãºltimos 90d</p>
          <p className="text-4xl font-display tabular-nums text-danger">8.4%</p>
          <div className="mt-4 flex gap-1">
            <div className="h-1 flex-1 bg-danger rounded-full" />
            <div className="h-1 flex-1 bg-danger rounded-full" />
            <div className="h-1 flex-1 bg-white/10 rounded-full" />
          </div>
          <p className="text-[10px] mt-2 text-danger/80 font-mono">
            +2.1% ACIMA DA MÃ‰DIA SETORIAL
          </p>
        </div>

        <div className="bg-surface border border-border p-6 rounded-xl animate-fade-in [animation-delay:300ms]">
          <div className="flex justify-between items-start mb-4">
            <Briefcase className="size-5 text-muted-foreground" />
            <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-white/5 text-muted-foreground">
              EM ABERTO
            </span>
          </div>
          <p className="text-xs text-muted-foreground mb-2">Vagas em recrutamento</p>
          <p className="text-4xl font-display tabular-nums text-foreground">12</p>
          <p className="text-[10px] mt-6 text-muted-foreground font-mono">
            08 OPERACIONAL Â· 04 ADMINISTRATIVO
          </p>
        </div>
      </section>

      <section className="mb-12">
        <h2 className="text-xs font-mono uppercase tracking-[0.3em] text-muted-foreground mb-6">
          Quadro Operacional Detalhado
        </h2>
        <div className="bg-surface border border-border rounded-xl overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-white/[0.02] border-b border-border">
              <tr className="text-left text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
                <th className="px-6 py-3">FunÃ§Ã£o</th>
                <th className="px-6 py-3">Ativos</th>
                <th className="px-6 py-3">Vagas Abertas</th>
                <th className="px-6 py-3">Turnover</th>
                <th className="px-6 py-3">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {[
                { fn: "Motoristas", a: 48, v: 3, t: "11.2%", s: "atenÃ§Ã£o" },
                { fn: "Ajudantes de Carga", a: 36, v: 4, t: "14.5%", s: "crÃ­tico" },
                { fn: "Operadores de CD", a: 32, v: 1, t: "6.0%", s: "ok" },
                { fn: "Coordenadores", a: 14, v: 0, t: "2.1%", s: "ok" },
                { fn: "Administrativo", a: 12, v: 4, t: "5.8%", s: "atenÃ§Ã£o" },
              ].map((r) => (
                <tr key={r.fn} className="hover:bg-white/[0.02]">
                  <td className="px-6 py-4 font-medium">{r.fn}</td>
                  <td className="px-6 py-4 tabular-nums">{r.a}</td>
                  <td className="px-6 py-4 tabular-nums">{r.v}</td>
                  <td className="px-6 py-4 tabular-nums font-mono">{r.t}</td>
                  <td className="px-6 py-4">
                    <span
                      className={
                        "text-[10px] font-mono uppercase px-2 py-0.5 rounded " +
                        (r.s === "crÃ­tico"
                          ? "bg-danger/10 text-danger"
                          : r.s === "atenÃ§Ã£o"
                            ? "bg-yellow-500/10 text-yellow-400"
                            : "bg-primary/10 text-primary")
                      }
                    >
                      {r.s}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="bg-surface border border-primary/30 rounded-2xl p-8 ring-4 ring-primary/5 flex items-center justify-between gap-6">
        <div className="flex gap-4 items-start">
          <div className="size-10 rounded-lg bg-primary/10 flex items-center justify-center shrink-0">
            <AlertTriangle className="size-5 text-primary" />
          </div>
          <div>
            <h3 className="text-lg font-display mb-1">
              A operaÃ§Ã£o estÃ¡ no limite. E se as vendas crescerem?
            </h3>
            <p className="text-sm text-muted-foreground text-pretty max-w-[60ch]">
              Com 92% de capacidade e turnover de 8.4%, qualquer pico de demanda gera
              gargalo imediato. Simule cenÃ¡rios para ver o impacto financeiro.
            </p>
          </div>
        </div>
        <Link
          to="/simulacao"
          className="shrink-0 inline-flex items-center gap-2 bg-primary text-primary-foreground px-6 py-3 rounded-lg font-bold uppercase text-xs tracking-widest hover:brightness-110 transition-all"
        >
          Iniciar SimulaÃ§Ã£o <ArrowRight className="size-4" />
        </Link>
      </section>
    </div>
  );
}
