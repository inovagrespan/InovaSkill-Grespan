import { createFileRoute, Link } from "@tanstack/react-router";
import { Activity, AlertTriangle, ArrowRight, Clock, MapPin, Package, Route as RouteIcon, Truck } from "lucide-react";

export const Route = createFileRoute("/logistica")({ component: Logistica });

const routeOccupancy = [
  { route: "Campinas -> Interior SP", truck: "Truck 3/4", occupancy: 92, loadKg: "7.820 kg", stops: 18, status: "No limite" },
  { route: "São Paulo -> ABC", truck: "Toco", occupancy: 84, loadKg: "6.140 kg", stops: 14, status: "Saudável" },
  { route: "Ribeirão Preto -> Norte", truck: "Carreta", occupancy: 76, loadKg: "18.900 kg", stops: 22, status: "Folga" },
  { route: "Sorocaba -> Oeste", truck: "Truck", occupancy: 95, loadKg: "10.450 kg", stops: 16, status: "Crítico" },
];

const stockBreaks = [
  { sku: "PAN-104", product: "Pão Francês Congelado 60g", warehouse: "CD Central", stock: 320, demand: 580, ruptureRisk: 45 },
  { sku: "PAN-221", product: "Pão de Queijo Congelado 1kg", warehouse: "CD Campinas", stock: 140, demand: 260, ruptureRisk: 46 },
  { sku: "PAN-318", product: "Croissant Congelado 80g", warehouse: "CD Ribeirão", stock: 90, demand: 180, ruptureRisk: 50 },
];

const CRITICAL_ROUTE_OCCUPANCY_PERCENT = 90;
const HIGH_RUPTURE_RISK_PERCENT = 45;

function Logistica() {
  const criticalRoutes = routeOccupancy.filter((item) => item.occupancy >= CRITICAL_ROUTE_OCCUPANCY_PERCENT).length;
  const averageOccupancy = Math.round(routeOccupancy.reduce((total, item) => total + item.occupancy, 0) / routeOccupancy.length);
  const ruptureItems = stockBreaks.filter((item) => item.ruptureRisk >= HIGH_RUPTURE_RISK_PERCENT).length;

  return (
    <div className="page-shell space-y-6">
      <header className="animate-soft-enter flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <span className="page-header-kicker">Smart Core / Controle e Estoque</span>
          <h1 className="mt-2 text-4xl font-display tracking-tight text-balance">Controle logístico e estoque</h1>
          <p className="mt-2 max-w-[64ch] text-sm text-muted-foreground text-pretty">
            Linha de base da frota, ocupação por rota e risco de ruptura para priorizar reposição antes do pico de venda.
          </p>
        </div>
        <span className="text-[10px] font-mono uppercase tracking-[0.2em] text-muted-foreground">Sync - há 4 min</span>
      </header>

      <section className="metric-row">
        {[
          { label: "Frota ativa", value: "86", sub: "de 92 veículos", icon: Truck, tone: "text-foreground" },
          { label: "Ocupação por rota", value: `${averageOccupancy}%`, sub: `${criticalRoutes} rotas acima de 90%`, icon: Activity, tone: "text-primary" },
          { label: "Ruptura de estoque", value: `${ruptureItems}`, sub: "SKUs com risco alto", icon: Package, tone: "text-amber-600" },
          { label: "SLA entregas", value: "93,1%", sub: "alerta em 2 rotas", icon: Clock, tone: "text-foreground" },
        ].map((item) => (
          <div key={item.label} className="metric-card-item rounded-xl border border-border bg-surface p-5 shadow-xs">
            <div className="mb-3 flex items-start justify-between">
              <p className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">{item.label}</p>
              <item.icon className="size-3.5 text-muted-foreground" />
            </div>
            <p className={`text-2xl font-display tabular-nums ${item.tone}`}>{item.value}</p>
            <p className="mt-1 text-[10px] font-mono uppercase tracking-wider text-muted-foreground">{item.sub}</p>
          </div>
        ))}
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <div className="rounded-xl border border-border bg-surface p-5">
          <div className="mb-4 flex items-center justify-between gap-3">
            <div>
              <h2 className="font-display text-xl">Ocupação de caminhão por rota</h2>
              <p className="text-sm text-muted-foreground">Capacidade usada por rota planejada, carga e quantidade de paradas.</p>
            </div>
            <RouteIcon className="size-5 text-primary" />
          </div>
          <div className="space-y-3">
            {routeOccupancy.map((item) => (
              <div key={item.route} className="rounded-lg border border-border bg-background p-4">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <p className="text-sm font-semibold">{item.route}</p>
                    <p className="text-xs text-muted-foreground">{item.truck} - {item.loadKg} - {item.stops} paradas</p>
                  </div>
                  <span className={item.occupancy >= CRITICAL_ROUTE_OCCUPANCY_PERCENT ? "text-sm font-semibold text-amber-600" : "text-sm font-semibold text-muted-foreground"}>{item.status}</span>
                </div>
                <div className="mt-3 h-2 rounded-full bg-muted">
                  <div className="h-2 rounded-full bg-primary" style={{ width: `${item.occupancy}%` }} />
                </div>
                <p className="mt-2 text-xs text-muted-foreground">{item.occupancy}% de ocupação</p>
              </div>
            ))}
          </div>
        </div>

        <div className="rounded-xl border border-border bg-surface p-5">
          <div className="mb-4 flex items-center justify-between gap-3">
            <div>
              <h2 className="font-display text-xl">Ruptura de estoque</h2>
              <p className="text-sm text-muted-foreground">Itens com demanda prevista acima do saldo disponível.</p>
            </div>
            <AlertTriangle className="size-5 text-amber-600" />
          </div>
          <div className="space-y-3">
            {stockBreaks.map((item) => (
              <div key={item.sku} className="rounded-lg border border-border bg-background p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-sm font-semibold">{item.product}</p>
                    <p className="text-xs text-muted-foreground">{item.sku} - {item.warehouse}</p>
                  </div>
                  <span className="text-sm font-semibold text-amber-600">{item.ruptureRisk}%</span>
                </div>
                <div className="mt-3 grid grid-cols-2 gap-2 text-xs">
                  <span className="rounded-md bg-muted px-2 py-1">Estoque: {item.stock}</span>
                  <span className="rounded-md bg-muted px-2 py-1">Demanda: {item.demand}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="rounded-xl border border-primary/30 bg-surface p-6 ring-4 ring-primary/5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex items-start gap-4">
            <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10"><MapPin className="size-5 text-primary" /></div>
            <div>
              <h3 className="mb-1 text-lg font-display">Sorocaba para Oeste está em 95% e exige revisão de carga.</h3>
              <p className="max-w-[64ch] text-sm text-muted-foreground">Redistribua pedidos de alto peso ou antecipe reposição dos SKUs em ruptura para proteger o SLA.</p>
            </div>
          </div>
          <Link to="/simulacao" className="inline-flex shrink-0 items-center justify-center gap-2 rounded-lg bg-primary px-5 py-3 text-xs font-bold uppercase tracking-widest text-primary-foreground transition-all hover:brightness-110">
            Simular impacto <ArrowRight className="size-4" />
          </Link>
        </div>
      </section>
    </div>
  );
}
