import { createFileRoute, Link } from "@tanstack/react-router";
import {
  Truck,
  ArrowRight,
  MapPin,
  Fuel,
  Package,
  AlertTriangle,
  Clock,
  Activity,
} from "lucide-react";
import {
  AreaChart,
  Area,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";

export const Route = createFileRoute("/logistica")({
  component: Logistica,
});

const utilization = [
  { d: "Seg", uso: 78, meta: 85 },
  { d: "Ter", uso: 82, meta: 85 },
  { d: "Qua", uso: 88, meta: 85 },
  { d: "Qui", uso: 91, meta: 85 },
  { d: "Sex", uso: 94, meta: 85 },
  { d: "SÃ¡b", uso: 73, meta: 85 },
  { d: "Dom", uso: 41, meta: 85 },
];

const routeCost = [
  { r: "R-01", custo: 4.8, sla: 98 },
  { r: "R-02", custo: 5.2, sla: 96 },
  { r: "R-03", custo: 6.1, sla: 89 },
  { r: "R-04", custo: 4.4, sla: 99 },
  { r: "R-05", custo: 7.3, sla: 82 },
  { r: "R-06", custo: 5.9, sla: 94 },
];

const fleet = [
  { id: "TRK-014", tipo: "Truck 14t", rota: "R-03 Â· SPâ†’Campinas", status: "rota", carga: 92, eta: "14:20" },
  { id: "TRK-022", tipo: "Truck 14t", rota: "CD Central", status: "carregando", carga: 35, eta: "â€”" },
  { id: "VAN-007", tipo: "VUC 3.5t", rota: "R-01 Â· Zona Sul", status: "rota", carga: 78, eta: "13:05" },
  { id: "TRK-031", tipo: "Truck 14t", rota: "R-05 Â· Litoral", status: "atraso", carga: 88, eta: "16:40" },
  { id: "VAN-012", tipo: "VUC 3.5t", rota: "â€”", status: "manutenÃ§Ã£o", carga: 0, eta: "â€”" },
  { id: "TRK-008", tipo: "Truck 14t", rota: "R-02 Â· ABC", status: "rota", carga: 81, eta: "13:48" },
];

function Logistica() {
  return (
    <div className="p-12">
      <header className="flex justify-between items-end mb-12 animate-fade-in">
        <div>
          <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-muted-foreground">
            Smart Core / LogÃ­stica
          </span>
          <h1 className="text-4xl font-display tracking-tight text-balance mt-2 mb-2">
            CenÃ¡rio Atual da OperaÃ§Ã£o
          </h1>
          <p className="text-muted-foreground max-w-[60ch] text-pretty">
            Linha de base da frota, rotas e CDs. A IA cruza estes nÃºmeros com a
            previsÃ£o de vendas para projetar gargalos e custos.
          </p>
        </div>
        <span className="text-[10px] font-mono text-muted-foreground uppercase tracking-[0.2em]">
          Sync Â· hÃ¡ 4 min
        </span>
      </header>

      <section className="grid grid-cols-4 gap-4 mb-10 animate-fade-in [animation-delay:100ms]">
        {[
          { l: "Frota Ativa", v: "86", sub: "de 92 veÃ­culos", icon: Truck, accent: false },
          { l: "OcupaÃ§Ã£o MÃ©dia", v: "87%", sub: "meta 85%", icon: Activity, accent: true },
          { l: "Custo / Km", v: "R$ 5,42", sub: "+6.1% vs trimestre", icon: Fuel, accent: false },
          { l: "SLA Entregas", v: "93.1%", sub: "alerta em 2 rotas", icon: Clock, accent: false, warn: true },
        ].map((s) => (
          <div key={s.l} className="border border-border bg-surface p-5 rounded-xl">
            <div className="flex justify-between items-start mb-3">
              <p className="text-[10px] text-muted-foreground font-mono uppercase tracking-wider">
                {s.l}
              </p>
              <s.icon className={"size-3.5 " + (s.warn ? "text-yellow-400" : "text-muted-foreground")} />
            </div>
            <p
              className={
                "text-2xl font-display tabular-nums " +
                (s.accent ? "text-primary" : s.warn ? "text-yellow-400" : "text-foreground")
              }
            >
              {s.v}
            </p>
            <p className="text-[10px] mt-1 text-muted-foreground font-mono uppercase tracking-wider">
              {s.sub}
            </p>
          </div>
        ))}
      </section>

      <section className="grid grid-cols-3 gap-6 mb-10">
        <div className="col-span-2 bg-surface border border-border rounded-xl p-6 animate-fade-in [animation-delay:200ms]">
          <div className="flex justify-between items-baseline mb-4">
            <div>
              <h3 className="font-display text-lg">OcupaÃ§Ã£o da Frota Â· 7d</h3>
              <p className="text-xs text-muted-foreground">% de capacidade utilizada vs meta</p>
            </div>
            <span className="text-[10px] font-mono text-primary uppercase tracking-widest">
              Live
            </span>
          </div>
          <ResponsiveContainer width="100%" height={240}>
            <AreaChart data={utilization}>
              <defs>
                <linearGradient id="g1" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#22ee5b" stopOpacity={0.4} />
                  <stop offset="100%" stopColor="#22ee5b" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid stroke="#ffffff10" vertical={false} />
              <XAxis dataKey="d" tick={{ fill: "#888", fontSize: 11 }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fill: "#888", fontSize: 11 }} axisLine={false} tickLine={false} />
              <Tooltip
                contentStyle={{
                  background: "#0d0d0d",
                  border: "1px solid #ffffff20",
                  borderRadius: 8,
                  fontSize: 12,
                }}
              />
              <Area type="monotone" dataKey="meta" stroke="#ffffff30" strokeDasharray="4 4" fill="none" />
              <Area type="monotone" dataKey="uso" stroke="#22ee5b" strokeWidth={2} fill="url(#g1)" />
            </AreaChart>
          </ResponsiveContainer>
        </div>

        <div className="bg-surface border border-border rounded-xl p-6 animate-fade-in [animation-delay:300ms]">
          <h3 className="font-display text-lg mb-1">Centros de DistribuiÃ§Ã£o</h3>
          <p className="text-xs text-muted-foreground mb-5">OcupaÃ§Ã£o atual dos CDs</p>
          <div className="space-y-5">
            {[
              { cd: "CD Central Â· SP", pct: 94, status: "crÃ­tico" },
              { cd: "CD Norte Â· Guarulhos", pct: 78, status: "ok" },
              { cd: "CD Sul Â· ABC", pct: 86, status: "atenÃ§Ã£o" },
              { cd: "CD Litoral Â· Santos", pct: 62, status: "ok" },
            ].map((c) => (
              <div key={c.cd}>
                <div className="flex justify-between text-xs mb-1.5">
                  <span className="text-foreground">{c.cd}</span>
                  <span className="font-mono tabular-nums text-muted-foreground">{c.pct}%</span>
                </div>
                <div className="h-1.5 w-full bg-white/5 rounded-full overflow-hidden">
                  <div
                    className={
                      "h-full " +
                      (c.status === "crÃ­tico"
                        ? "bg-danger"
                        : c.status === "atenÃ§Ã£o"
                          ? "bg-yellow-400"
                          : "bg-primary")
                    }
                    style={{ width: `${c.pct}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="grid grid-cols-3 gap-6 mb-10">
        <div className="col-span-2 bg-surface border border-border rounded-xl p-6">
          <div className="flex justify-between items-baseline mb-4">
            <div>
              <h3 className="font-display text-lg">Custo por Rota</h3>
              <p className="text-xs text-muted-foreground">R$/km transportado Â· semana atual</p>
            </div>
            <span className="text-[10px] font-mono text-yellow-400 uppercase tracking-widest">
              2 rotas em alerta
            </span>
          </div>
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={routeCost}>
              <CartesianGrid stroke="#ffffff10" vertical={false} />
              <XAxis dataKey="r" tick={{ fill: "#888", fontSize: 11 }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fill: "#888", fontSize: 11 }} axisLine={false} tickLine={false} />
              <Tooltip
                contentStyle={{
                  background: "#0d0d0d",
                  border: "1px solid #ffffff20",
                  borderRadius: 8,
                  fontSize: 12,
                }}
              />
              <Bar dataKey="custo" fill="#22ee5b" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>

        <div className="bg-surface border border-border rounded-xl p-6">
          <h3 className="font-display text-lg mb-1">Hoje</h3>
          <p className="text-xs text-muted-foreground mb-5">Resumo operacional</p>
          <div className="space-y-4">
            {[
              { icon: Package, l: "Pedidos despachados", v: "1.284" },
              { icon: MapPin, l: "Rotas ativas", v: "23" },
              { icon: Truck, l: "VeÃ­culos em rota", v: "71" },
              { icon: AlertTriangle, l: "OcorrÃªncias", v: "4", warn: true },
            ].map((i) => (
              <div key={i.l} className="flex justify-between items-center pb-3 border-b border-border last:border-0">
                <div className="flex items-center gap-3">
                  <i.icon className={"size-4 " + (i.warn ? "text-yellow-400" : "text-muted-foreground")} />
                  <span className="text-sm text-muted-foreground">{i.l}</span>
                </div>
                <span className={"font-display text-xl tabular-nums " + (i.warn ? "text-yellow-400" : "text-foreground")}>
                  {i.v}
                </span>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="mb-10">
        <h2 className="text-xs font-mono uppercase tracking-[0.3em] text-muted-foreground mb-6">
          Frota em OperaÃ§Ã£o Â· Tempo Real
        </h2>
        <div className="bg-surface border border-border rounded-xl overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-white/[0.02] border-b border-border">
              <tr className="text-left text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
                <th className="px-6 py-3">VeÃ­culo</th>
                <th className="px-6 py-3">Tipo</th>
                <th className="px-6 py-3">Rota</th>
                <th className="px-6 py-3">Carga</th>
                <th className="px-6 py-3">ETA</th>
                <th className="px-6 py-3">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {fleet.map((f) => (
                <tr key={f.id} className="hover:bg-white/[0.02]">
                  <td className="px-6 py-4 font-mono text-xs">{f.id}</td>
                  <td className="px-6 py-4 text-muted-foreground">{f.tipo}</td>
                  <td className="px-6 py-4">{f.rota}</td>
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-2">
                      <div className="h-1 w-16 bg-white/5 rounded-full overflow-hidden">
                        <div
                          className={
                            "h-full " +
                            (f.carga > 85 ? "bg-yellow-400" : f.carga > 0 ? "bg-primary" : "bg-white/10")
                          }
                          style={{ width: `${f.carga}%` }}
                        />
                      </div>
                      <span className="font-mono text-xs tabular-nums text-muted-foreground">
                        {f.carga}%
                      </span>
                    </div>
                  </td>
                  <td className="px-6 py-4 font-mono tabular-nums text-xs">{f.eta}</td>
                  <td className="px-6 py-4">
                    <span
                      className={
                        "text-[10px] font-mono uppercase px-2 py-0.5 rounded " +
                        (f.status === "atraso"
                          ? "bg-danger/10 text-danger"
                          : f.status === "manutenÃ§Ã£o"
                            ? "bg-white/5 text-muted-foreground"
                            : f.status === "carregando"
                              ? "bg-yellow-500/10 text-yellow-400"
                              : "bg-primary/10 text-primary")
                      }
                    >
                      {f.status}
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
              Frota a 87% e CD Central no limite. Comporta um pico?
            </h3>
            <p className="text-sm text-muted-foreground text-pretty max-w-[60ch]">
              Com 2 rotas em alerta de SLA e o CD Central a 94%, qualquer aumento de
              demanda forÃ§a terceirizaÃ§Ã£o. Rode a simulaÃ§Ã£o para dimensionar.
            </p>
          </div>
        </div>
        <Link
          to="/simulacao"
          className="shrink-0 inline-flex items-center gap-2 bg-primary text-primary-foreground px-6 py-3 rounded-lg font-bold uppercase text-xs tracking-widest hover:brightness-110 transition-all"
        >
          Simular Impacto <ArrowRight className="size-4" />
        </Link>
      </section>
    </div>
  );
}
