import { createFileRoute, Link } from "@tanstack/react-router";
import { Activity, AlertTriangle, ArrowRight, Clock, MapPin, Package, Route as RouteIcon, Truck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

export const Route = createFileRoute("/logistica/")({ component: LogisticaPage });

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

const CRITICAL_OCCUPANCY = 90;
const HIGH_RUPTURE = 45;

function LogisticaPage() {
  const criticalRoutes = routeOccupancy.filter(r => r.occupancy >= CRITICAL_OCCUPANCY).length;
  const avgOccupancy = Math.round(routeOccupancy.reduce((s, r) => s + r.occupancy, 0) / routeOccupancy.length);
  const ruptureItems = stockBreaks.filter(s => s.ruptureRisk >= HIGH_RUPTURE).length;

  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <span className="page-header-kicker">Logística</span>
          <h1 className="text-3xl font-display font-semibold tracking-tight mt-1">Rotas e estoque</h1>
          <p className="text-sm text-muted-foreground mt-1">Ocupação de frota, risco de ruptura e mapa de clientes.</p>
        </div>
        <Button variant="outline" asChild>
          <Link to="/logistica/mapa"><MapPin className="w-4 h-4 mr-2" />Mapa de clientes</Link>
        </Button>
      </header>

      <section className="metric-row">
        {[
          { label: "Frota ativa", value: "86", sub: "de 92 veículos", icon: Truck },
          { label: "Ocupação média", value: `${avgOccupancy}%`, sub: `${criticalRoutes} rotas críticas`, icon: Activity },
          { label: "Ruptura", value: `${ruptureItems}`, sub: "SKUs em risco", icon: Package },
          { label: "SLA entregas", value: "93,1%", sub: "alerta em 2 rotas", icon: Clock },
        ].map(item => (
          <div key={item.label} className="rounded-xl border bg-card p-5 flex-1 min-w-[180px]">
            <div className="flex items-start justify-between mb-3">
              <p className="text-xs font-medium text-muted-foreground">{item.label}</p>
              <item.icon className="size-4 text-muted-foreground" />
            </div>
            <p className="text-2xl font-semibold tracking-tight">{item.value}</p>
            <p className="mt-1 text-xs text-muted-foreground">{item.sub}</p>
          </div>
        ))}
      </section>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <div className="rounded-xl border bg-card p-5">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="text-lg font-semibold">Ocupação por rota</h2>
              <p className="text-sm text-muted-foreground">Capacidade usada por rota.</p>
            </div>
            <RouteIcon className="size-5 text-primary" />
          </div>
          <div className="space-y-3">
            {routeOccupancy.map(item => (
              <div key={item.route} className="rounded-lg border bg-background p-4">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <p className="text-sm font-semibold">{item.route}</p>
                    <p className="text-xs text-muted-foreground">{item.truck} — {item.loadKg} — {item.stops} paradas</p>
                  </div>
                  <Badge variant={item.occupancy >= CRITICAL_OCCUPANCY ? "destructive" : "outline"}>{item.status}</Badge>
                </div>
                <div className="mt-3 h-2 rounded-full bg-muted">
                  <div className="h-2 rounded-full bg-primary" style={{ width: `${item.occupancy}%` }} />
                </div>
                <p className="mt-2 text-xs text-muted-foreground">{item.occupancy}% de ocupação</p>
              </div>
            ))}
          </div>
        </div>

        <div className="rounded-xl border bg-card p-5">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="text-lg font-semibold">Ruptura de estoque</h2>
              <p className="text-sm text-muted-foreground">Demanda acima do saldo.</p>
            </div>
            <AlertTriangle className="size-5 text-amber-600" />
          </div>
          <div className="space-y-3">
            {stockBreaks.map(item => (
              <div key={item.sku} className="rounded-lg border bg-background p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-sm font-semibold">{item.product}</p>
                    <p className="text-xs text-muted-foreground">{item.sku} — {item.warehouse}</p>
                  </div>
                  <Badge variant="destructive">{item.ruptureRisk}%</Badge>
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

    </div>
  );
}