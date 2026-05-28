import { createFileRoute } from "@tanstack/react-router";
import { TrendingUp, Users, UserPlus, DollarSign, ArrowUpRight, Activity } from "lucide-react";
import {
  AreaChart,
  Area,
  BarChart,
  Bar,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Legend,
} from "recharts";

export const Route = createFileRoute("/vendas")({
  component: VendasPage,
});

// Histórico real (últimos 12 meses) + Previsão IA (próximos 6 meses)
const salesHistory = [
  { mes: "Jun/25", real: 1820, previsao: null, novos: 42, ativos: 318 },
  { mes: "Jul/25", real: 1910, previsao: null, novos: 38, ativos: 332 },
  { mes: "Ago/25", real: 2040, previsao: null, novos: 45, ativos: 348 },
  { mes: "Set/25", real: 2110, previsao: null, novos: 51, ativos: 361 },
  { mes: "Out/25", real: 2280, previsao: null, novos: 47, ativos: 378 },
  { mes: "Nov/25", real: 2510, previsao: null, novos: 62, ativos: 401 },
  { mes: "Dez/25", real: 2890, previsao: null, novos: 78, ativos: 432 },
  { mes: "Jan/26", real: 2640, previsao: null, novos: 54, ativos: 451 },
  { mes: "Fev/26", real: 2720, previsao: null, novos: 49, ativos: 463 },
  { mes: "Mar/26", real: 2880, previsao: null, novos: 58, ativos: 478 },
  { mes: "Abr/26", real: 3020, previsao: null, novos: 64, ativos: 492 },
  { mes: "Mai/26", real: 3180, previsao: 3180, novos: 71, ativos: 508 },
  { mes: "Jun/26", real: null, previsao: 3360, novos: 76, ativos: 524 },
  { mes: "Jul/26", real: null, previsao: 3540, novos: 82, ativos: 541 },
  { mes: "Ago/26", real: null, previsao: 3720, novos: 88, ativos: 559 },
  { mes: "Set/26", real: null, previsao: 3890, novos: 91, ativos: 576 },
  { mes: "Out/26", real: null, previsao: 4080, novos: 96, ativos: 594 },
  { mes: "Nov/26", real: null, previsao: 4380, novos: 108, ativos: 615 },
];

const topClientes = [
  { nome: "Distribuidora Vega S.A.", consumoAtual: 142, projetado: 178, var: 25.4 },
  { nome: "Mercantil Norte LTDA", consumoAtual: 118, projetado: 146, var: 23.7 },
  { nome: "Rede Atacado Sul", consumoAtual: 96, projetado: 121, var: 26.0 },
  { nome: "Comercial Aurora", consumoAtual: 84, projetado: 102, var: 21.4 },
  { nome: "Grupo Pampa Foods", consumoAtual: 71, projetado: 89, var: 25.3 },
];

const tooltipStyle = {
  backgroundColor: "#12151c",
  border: "1px solid #1e293b",
  borderRadius: "8px",
  fontSize: "12px",
  fontFamily: "JetBrains Mono, monospace",
};

function VendasPage() {
  return (
    <div className="p-12">
      <header className="flex justify-between items-end mb-12 animate-fade-in">
        <div>
          <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-muted-foreground">
            Smart Core / Inteligência de Vendas
          </span>
          <h1 className="text-4xl font-display tracking-tight mt-2 mb-2">
            Vendas & Previsão de Demanda
          </h1>
          <p className="text-muted-foreground max-w-[64ch] text-pretty">
            Histórico consolidado dos últimos 12 meses e projeção da IA para os próximos
            6 meses, segmentando consumo de clientes ativos e captação de novos clientes.
          </p>
        </div>
        <span className="text-[10px] font-mono text-muted-foreground uppercase tracking-[0.2em]">
          Modelo v3.1 · Confiança 94%
        </span>
      </header>

      {/* KPIs */}
      <section className="grid grid-cols-4 gap-4 mb-10 animate-fade-in [animation-delay:80ms]">
        {[
          { label: "Receita 12M", value: "R$ 30.0M", delta: "+18.4%", icon: DollarSign },
          { label: "Previsão 6M", value: "R$ 23.9M", delta: "+22.7%", icon: TrendingUp, accent: true },
          { label: "Clientes Ativos", value: "508", delta: "+59 vs Q4", icon: Users },
          { label: "Novos Clientes / Mês", value: "71", delta: "+34% YoY", icon: UserPlus },
        ].map((s) => (
          <div key={s.label} className="border border-border bg-surface p-5 rounded-xl">
            <div className="flex justify-between items-start mb-3">
              <p className="text-[10px] text-muted-foreground font-mono uppercase tracking-wider">
                {s.label}
              </p>
              <s.icon className="size-3.5 text-muted-foreground" />
            </div>
            <p className={"text-2xl font-display tabular-nums " + (s.accent ? "text-primary" : "text-foreground")}>
              {s.value}
            </p>
            <p className="text-[11px] font-mono text-primary mt-1 flex items-center gap-1">
              <ArrowUpRight className="size-3" /> {s.delta}
            </p>
          </div>
        ))}
      </section>

      {/* Histórico + Previsão (chart) */}
      <section className="bg-surface border border-border rounded-2xl p-6 mb-10 animate-fade-in [animation-delay:160ms]">
        <div className="flex justify-between items-end mb-6">
          <div>
            <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
              Série Temporal · Receita (R$ mil)
            </span>
            <h2 className="text-xl font-display mt-1">Histórico Real vs Previsão IA</h2>
          </div>
          <div className="flex gap-4 text-[11px] font-mono">
            <span className="flex items-center gap-2 text-muted-foreground">
              <span className="size-2 rounded-full bg-foreground/60" /> Realizado
            </span>
            <span className="flex items-center gap-2 text-primary">
              <span className="size-2 rounded-full bg-primary" /> Previsto
            </span>
          </div>
        </div>
        <div className="h-80">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={salesHistory}>
              <defs>
                <linearGradient id="gReal" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#94a3b8" stopOpacity={0.4} />
                  <stop offset="100%" stopColor="#94a3b8" stopOpacity={0} />
                </linearGradient>
                <linearGradient id="gPrev" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#22ee5b" stopOpacity={0.5} />
                  <stop offset="100%" stopColor="#22ee5b" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid stroke="#1e293b" vertical={false} />
              <XAxis dataKey="mes" tick={{ fill: "#94a3b8", fontSize: 11, fontFamily: "JetBrains Mono" }} />
              <YAxis tick={{ fill: "#94a3b8", fontSize: 11, fontFamily: "JetBrains Mono" }} />
              <Tooltip contentStyle={tooltipStyle} />
              <Area type="monotone" dataKey="real" stroke="#f8fafc" strokeWidth={2} fill="url(#gReal)" name="Realizado" />
              <Area type="monotone" dataKey="previsao" stroke="#22ee5b" strokeWidth={2} strokeDasharray="5 5" fill="url(#gPrev)" name="Previsto" />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </section>

      {/* Dois gráficos lado a lado */}
      <section className="grid grid-cols-2 gap-6 mb-10 animate-fade-in [animation-delay:240ms]">
        <div className="bg-surface border border-border rounded-2xl p-6">
          <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
            Consumo · Clientes Ativos
          </span>
          <h3 className="text-lg font-display mt-1 mb-1">Base ativa em expansão</h3>
          <p className="text-xs text-muted-foreground mb-4">
            IA prevê +21% no consumo médio dos clientes existentes nos próximos 6 meses.
          </p>
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={salesHistory}>
                <CartesianGrid stroke="#1e293b" vertical={false} />
                <XAxis dataKey="mes" tick={{ fill: "#94a3b8", fontSize: 10, fontFamily: "JetBrains Mono" }} />
                <YAxis tick={{ fill: "#94a3b8", fontSize: 10, fontFamily: "JetBrains Mono" }} />
                <Tooltip contentStyle={tooltipStyle} />
                <Line type="monotone" dataKey="ativos" stroke="#22ee5b" strokeWidth={2} dot={{ r: 3, fill: "#22ee5b" }} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="bg-surface border border-border rounded-2xl p-6">
          <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
            Aquisição · Novos Clientes / Mês
          </span>
          <h3 className="text-lg font-display mt-1 mb-1">Captação acelerando</h3>
          <p className="text-xs text-muted-foreground mb-4">
            Tendência de crescimento de 34% YoY, puxada por campanhas no Sudeste.
          </p>
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={salesHistory}>
                <CartesianGrid stroke="#1e293b" vertical={false} />
                <XAxis dataKey="mes" tick={{ fill: "#94a3b8", fontSize: 10, fontFamily: "JetBrains Mono" }} />
                <YAxis tick={{ fill: "#94a3b8", fontSize: 10, fontFamily: "JetBrains Mono" }} />
                <Tooltip contentStyle={tooltipStyle} />
                <Bar dataKey="novos" fill="#22ee5b" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      </section>

      {/* Tabela top clientes */}
      <section className="bg-surface border border-border rounded-2xl p-6 mb-10 animate-fade-in [animation-delay:320ms]">
        <div className="flex justify-between items-end mb-6">
          <div>
            <span className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">
              Top Clientes Ativos · Projeção 6M
            </span>
            <h3 className="text-lg font-display mt-1">Previsão de aumento de consumo</h3>
          </div>
          <span className="text-[10px] font-mono text-muted-foreground">
            Baseado em 24 meses de histórico transacional
          </span>
        </div>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground border-b border-border">
              <th className="text-left py-3">Cliente</th>
              <th className="text-right py-3">Consumo Atual (un/mês)</th>
              <th className="text-right py-3">Projetado IA</th>
              <th className="text-right py-3">Variação</th>
            </tr>
          </thead>
          <tbody>
            {topClientes.map((c) => (
              <tr key={c.nome} className="border-b border-border/50 last:border-0">
                <td className="py-4 font-medium">{c.nome}</td>
                <td className="py-4 text-right font-mono tabular-nums text-muted-foreground">{c.consumoAtual}</td>
                <td className="py-4 text-right font-mono tabular-nums text-foreground">{c.projetado}</td>
                <td className="py-4 text-right">
                  <span className="inline-flex items-center gap-1 font-mono text-primary text-xs">
                    <ArrowUpRight className="size-3" /> +{c.var}%
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* Insight */}
      <section className="bg-primary/5 border border-primary/30 rounded-2xl p-6 animate-fade-in [animation-delay:400ms]">
        <div className="flex items-start gap-4">
          <div className="size-10 rounded-lg bg-primary/15 flex items-center justify-center shrink-0">
            <Activity className="size-5 text-primary" />
          </div>
          <div>
            <span className="text-[10px] font-mono uppercase tracking-widest text-primary">
              Insight Nexus AI
            </span>
            <h3 className="text-lg font-display mt-1 mb-2">
              Demanda projetada exigirá +20% de capacidade operacional até Set/26
            </h3>
            <p className="text-sm text-muted-foreground max-w-[80ch] text-pretty">
              A combinação do crescimento orgânico dos 508 clientes ativos (+21% de consumo médio) com
              a captação de novos clientes (76/mês) deve elevar a receita mensal para R$ 4.38M até
              Nov/26. Recomendamos abrir o Simulador para projetar o impacto em Logística e RH.
            </p>
          </div>
        </div>
      </section>
    </div>
  );
}
