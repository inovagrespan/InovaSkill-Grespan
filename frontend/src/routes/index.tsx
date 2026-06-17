import { useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { BarChart3, DollarSign, TrendingUp, Users, Target, TrendingDown, BarChart4, AlertTriangle } from "lucide-react";
import { Bar, BarChart, CartesianGrid, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { InsightCard } from "@/components/ui/insight-card";
import { KpiCard } from "@/components/ui/kpi-card";
import { ScoreBadge } from "@/components/ui/score-badge";
import { TrendBadge } from "@/components/ui/trend-badge";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { SkeletonMetricCard, SkeletonChart } from "@/components/ui/skeleton";
import { authFetch } from "@/lib/auth";
import { buildServiceUrl } from "@/lib/api-url";

export const Route = createFileRoute("/")({
  component: HomePage,
});

function HomePage() {
  const [loading, setLoading] = useState(true);
  const [resumo, setResumo] = useState<any>(null);
  const [topClientes, setTopClientes] = useState<any[]>([]);
  const [tendencias, setTendencias] = useState<any[]>([]);

  async function loadData() {
    try {
      const base = buildServiceUrl("api/analytics");
      const [res, top, ten] = await Promise.all([
        authFetch(`${base}/dashboard/resumo`).then(r => r.ok ? r.json() : null),
        authFetch(`${base}/dashboard/top-clientes?limite=20`).then(r => r.ok ? r.json() : null),
        authFetch(`${base}/tendencias`).then(r => r.ok ? r.json() : null),
      ]);
      if (res) setResumo(res);
      if (top) setTopClientes(top);
      if (ten) setTendencias(ten);
    } catch { /* */ } finally { setLoading(false); }
  }

  useEffect(() => { void loadData(); }, []);

  const growthTrend = useMemo(() => {
    const crescimento = tendencias.find((t: any) => t.tendencia === "Crescimento");
    const queda = tendencias.find((t: any) => t.tendencia === "Queda");
    const totalCrescimento = crescimento?.totalClientes ?? 0;
    const totalQueda = queda?.totalClientes ?? 0;
    if (totalCrescimento > totalQueda) return { direction: "up" as const, value: `${totalCrescimento - totalQueda}`, label: "clientes" };
    if (totalQueda > totalCrescimento) return { direction: "down" as const, value: `${totalQueda - totalCrescimento}`, label: "clientes" };
    return null;
  }, [tendencias]);

  const scoreTrend = useMemo(() => {
    if (!topClientes || topClientes.length < 2) return null;
    const media = topClientes.reduce((s: number, c: any) => s + c.scorePotencial, 0) / topClientes.length;
    return { direction: media >= 60 ? ("up" as const) : media >= 40 ? ("stable" as const) : ("down" as const), value: media.toFixed(0), label: "médio" };
  }, [topClientes]);

  const faturamentoTotal3M = useMemo(() => topClientes.reduce((s: number, c: any) => s + (c.faturamento3M ?? 0), 0), [topClientes]);
  const faturamentoTotal6M = useMemo(() => topClientes.reduce((s: number, c: any) => s + (c.faturamento6M ?? 0), 0), [topClientes]);
  const faturamentoTotal12M = useMemo(() => topClientes.reduce((s: number, c: any) => s + (c.faturamento12M ?? 0), 0), [topClientes]);

  const crescimentoPeriodo = useMemo(() => {
    if (faturamentoTotal6M === 0) return null;
    return ((faturamentoTotal12M - faturamentoTotal6M) / faturamentoTotal6M) * 100;
  }, [faturamentoTotal12M, faturamentoTotal6M]);

  const concentracaoTop3 = useMemo(() => {
    if (topClientes.length === 0 || faturamentoTotal12M === 0) return 0;
    const top3 = topClientes.slice(0, 3).reduce((s: number, c: any) => s + c.faturamento12M, 0);
    return (top3 / faturamentoTotal12M) * 100;
  }, [topClientes, faturamentoTotal12M]);

  const chartComparativo = useMemo(() => [
    { periodo: "Últimos 3M", valor: faturamentoTotal3M },
    { periodo: "Últimos 6M", valor: faturamentoTotal6M },
    { periodo: "Últimos 12M", valor: faturamentoTotal12M },
  ], [faturamentoTotal3M, faturamentoTotal6M, faturamentoTotal12M]);

  const insights = useMemo(() => {
    const list: { text: string; type: "insight" | "opportunity" | "alert" | "info" }[] = [];
    if (!resumo) return list;
    if (resumo.tendenciaCrescimento > 0)
      list.push({ text: `${resumo.tendenciaCrescimento} cliente(s) em crescimento nos últimos meses.`, type: "opportunity" });
    if (resumo.tendenciaQueda > 0)
      list.push({ text: `${resumo.tendenciaQueda} cliente(s) em queda — vale revisar o relacionamento.`, type: "alert" });
    if (resumo.classificacaoA > 0)
      list.push({ text: `${resumo.classificacaoA} cliente(s) são categoria A — alto potencial de expansão.`, type: "opportunity" });
    if (resumo.classificacaoD > 0)
      list.push({ text: `${resumo.classificacaoD} cliente(s) em categoria D — risco de perda.`, type: "alert" });
    if (resumo.totalClientes > 0)
      list.push({ text: `Score médio geral: ${resumo.scoreMedio}/100. ${resumo.totalClientes} clientes ativos.`, type: "insight" });
    return list;
  }, [resumo]);

  const chartCrescimento = useMemo(() =>
    topClientes.filter((c: any) => c.tendencia === "Crescimento").slice(0, 5).map((c: any) => ({
      name: (c.clienteNome ?? c.clienteId).length > 12 ? (c.clienteNome ?? c.clienteId).slice(0, 12) + "..." : (c.clienteNome ?? c.clienteId),
      taxa: c.crescimento12M ?? 0,
    })),
    [topClientes]
  );

  return (
    <div className="page-shell app-background">
      <header className="animate-fade-in space-y-2">
        <span className="page-header-kicker">Visão Geral</span>
        <h1 className="text-3xl font-display font-semibold tracking-tight">Dashboard</h1>
        <p className="text-muted-foreground max-w-2xl">Acompanhe o desempenho do negócio em tempo real.</p>
      </header>

      <section className="metric-row animate-soft-enter">
        <KpiCard
          title="Faturamento 12M"
          value={resumo ? formatCurrency(resumo.faturamentoTotal12M) : "—"}
          valueTooltip={resumo ? formatCurrency(resumo.faturamentoTotal12M) : "—"}
          icon={DollarSign}
          loading={loading}
          showPercentageChange={crescimentoPeriodo != null}
          percentageChange={crescimentoPeriodo}
          periodLabel="Receita acumulada · vs 6M"
        />
        <KpiCard
          title="Clientes Ativos"
          value={resumo ? formatNumber(resumo.totalClientes) : "—"}
          valueTooltip={resumo ? formatNumber(resumo.totalClientes) : "—"}
          icon={Users}
          loading={loading}
          showPercentageChange={false}
          periodLabel={`${resumo ? resumo.classificacaoA : 0} cat. A`}
        />
        <KpiCard
          title="Score Médio"
          value={resumo ? `${resumo.scoreMedio}/100` : "—"}
          valueTooltip={resumo ? `${resumo.scoreMedio}/100` : "—"}
          icon={Target}
          loading={loading}
          showPercentageChange={false}
          periodLabel="Média de potencial"
        />
        <KpiCard
          title="Concentração Top 3"
          value={resumo ? `${concentracaoTop3.toFixed(0)}%` : "—"}
          valueTooltip={resumo ? `${concentracaoTop3.toFixed(0)}%` : "—"}
          icon={AlertTriangle}
          loading={loading}
          showPercentageChange={false}
          periodLabel={concentracaoTop3 > 50 ? "Alta concentração — risco" : "Receita dos 3 maiores"}
        />
      </section>

      <section className="analytics-grid analytics-grid-2 animate-soft-enter" style={{ animationDelay: "0.1s" }}>
        <Card>
          <CardHeader>
            <CardTitle className="text-base font-semibold flex items-center gap-2">
              <BarChart3 className="w-4 h-4 text-primary" />
              Faturamento por período
            </CardTitle>
          </CardHeader>
          <CardContent>
            {loading ? <SkeletonChart /> : (
              <div className="space-y-4">
                <div className="overflow-x-auto custom-scrollbar -mx-1 px-1">
                  <div className="min-w-[350px]">
                    <ResponsiveContainer width="100%" height={200}>
                      <BarChart data={chartComparativo} margin={{ top: 8, right: 8, bottom: 4, left: 8 }}>
                        <CartesianGrid vertical={false} stroke="#E2E8F0" strokeDasharray="3 3" />
                        <XAxis dataKey="periodo" tick={{ fontSize: 12, fill: "#64748B" }} axisLine={false} tickLine={false} />
                        <YAxis tick={{ fontSize: 11, fill: "#64748B" }} axisLine={false} tickLine={false} width={60} tickFormatter={(v: number) => formatCompact(v)} />
                        <Tooltip contentStyle={{ background: "#FFFFFF", border: "1px solid #E2E8F0", borderRadius: "8px", fontSize: "13px", boxShadow: "0 4px 12px rgba(0,0,0,0.06)" }} formatter={(v: number) => formatCurrency(v)} />
                        <Bar dataKey="valor" radius={[6, 6, 0, 0]} maxBarSize={40}>
                          {chartComparativo.map((_, i) => (
                            <Cell key={i} fill={i === 2 ? "#B4232F" : i === 1 ? "#9F1F29" : "#D97706"} />
                          ))}
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </div>
                <div className="grid grid-cols-3 gap-3 text-sm">
                  <div className="rounded-lg bg-muted/50 p-3 text-center">
                    <p className="text-xs text-muted-foreground">3 meses</p>
                    <p className="font-semibold mt-0.5">{formatCurrency(faturamentoTotal3M)}</p>
                  </div>
                  <div className="rounded-lg bg-muted/50 p-3 text-center">
                    <p className="text-xs text-muted-foreground">6 meses</p>
                    <p className="font-semibold mt-0.5">{formatCurrency(faturamentoTotal6M)}</p>
                  </div>
                  <div className="rounded-lg bg-muted/50 p-3 text-center">
                    <p className="text-xs text-muted-foreground">12 meses</p>
                    <p className="font-semibold mt-0.5">{formatCurrency(faturamentoTotal12M)}</p>
                  </div>
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base font-semibold">Distribuição por Categoria</CardTitle>
            </CardHeader>
            <CardContent>
              {loading ? (
                <div className="space-y-3">{[1,2,3,4].map(i => <div key={i} className="skeleton-shimmer h-6 w-full rounded" />)}</div>
              ) : resumo ? (
                <div className="space-y-4">
                  {[
                    { label: "A — Excelente", count: resumo.classificacaoA, color: "bg-[#059669]", pct: resumo.totalClientes > 0 ? Math.round(resumo.classificacaoA / resumo.totalClientes * 100) : 0 },
                    { label: "B — Bom", count: resumo.classificacaoB, color: "bg-primary", pct: resumo.totalClientes > 0 ? Math.round(resumo.classificacaoB / resumo.totalClientes * 100) : 0 },
                    { label: "C — Atenção", count: resumo.classificacaoC, color: "bg-[#D97706]", pct: resumo.totalClientes > 0 ? Math.round(resumo.classificacaoC / resumo.totalClientes * 100) : 0 },
                    { label: "D — Risco", count: resumo.classificacaoD, color: "bg-[#B91C1C]", pct: resumo.totalClientes > 0 ? Math.round(resumo.classificacaoD / resumo.totalClientes * 100) : 0 },
                  ].map(item => (
                    <div key={item.label} className="space-y-1.5">
                      <div className="flex justify-between text-sm items-center">
                        <span className="flex items-center gap-1.5"><span className={`w-2 h-2 rounded-full ${item.color}`} />{item.label}</span>
                        <span className="text-muted-foreground tabular-nums">{item.count} ({item.pct}%)</span>
                      </div>
                      <div className="h-2 rounded-full bg-muted overflow-hidden">
                        <div className={`h-full rounded-full ${item.color} transition-all duration-700`} style={{ width: `${item.pct}%` }} />
                      </div>
                    </div>
                  ))}
                </div>
              ) : <p className="text-sm text-muted-foreground">Aguardando dados.</p>}
            </CardContent>
          </Card>

          {chartCrescimento.length > 0 && (
            <div className="rounded-lg border bg-card p-4">
              <p className="text-sm font-semibold mb-3">Top crescimento (%)</p>
              <div className="space-y-2">
                {chartCrescimento.map((c: any) => (
                  <div key={c.name} className="flex justify-between text-sm items-center">
                    <span className="truncate mr-2">{c.name}</span>
                    <span className="text-[#059669] font-medium tabular-nums">+{c.taxa.toFixed(1)}%</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </section>

      {insights.length > 0 && (
        <section className="space-y-3 animate-soft-enter" style={{ animationDelay: "0.2s" }}>
          <h2 className="text-lg font-semibold tracking-tight">Resumo Inteligente</h2>
          <div className="grid gap-2 sm:grid-cols-2">
            {insights.map((insight, i) => (
              <InsightCard key={i} type={insight.type}>{insight.text}</InsightCard>
            ))}
          </div>
        </section>
      )}

      {topClientes.length > 0 && (
        <section className="animate-soft-enter" style={{ animationDelay: "0.3s" }}>
          <Card>
            <CardHeader>
              <CardTitle className="text-base font-semibold flex items-center gap-2">
                <Users className="w-4 h-4 text-primary" />
                Top Clientes por Score
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {topClientes.slice(0, 9).map((cliente: any) => {
                  const isUp = (cliente.crescimento12M ?? 0) > 0;
                  const isDown = (cliente.crescimento12M ?? 0) < 0;
                  return (
                    <div key={cliente.clienteId} className="rounded-lg border p-4 space-y-3 hover:shadow-sm transition-shadow hover:border-primary/20">
                      <div className="flex items-center justify-between">
                        <span className="font-medium text-sm truncate">{cliente.clienteNome ?? cliente.clienteId}</span>
                        <TrendBadge trend={cliente.tendencia} size="sm" />
                      </div>
                      <ScoreBadge score={cliente.scorePotencial} size="sm" />
                      <div className="flex items-center justify-between text-xs">
                        <span className="text-muted-foreground">12M: {formatCurrency(cliente.faturamento12M)}</span>
                        {cliente.crescimento12M != null && (
                          <span className={`inline-flex items-center gap-0.5 font-semibold whitespace-nowrap ${isUp ? "text-[#059669]" : isDown ? "text-[#B91C1C]" : "text-[#2563EB]"}`}>
                            {isUp ? "↑" : isDown ? "↓" : "→"}{isUp ? "+" : ""}{cliente.crescimento12M.toFixed(1)}%
                          </span>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </CardContent>
          </Card>
        </section>
      )}
    </div>
  );
}

function formatCurrency(v: number) { return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL", maximumFractionDigits: 0 }).format(v ?? 0); }
function formatNumber(v: number) { return new Intl.NumberFormat("pt-BR").format(v ?? 0); }
function formatCompact(v: number) { return new Intl.NumberFormat("pt-BR", { notation: "compact" }).format(v ?? 0); }
