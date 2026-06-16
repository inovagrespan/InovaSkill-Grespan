import { createFileRoute, Link } from "@tanstack/react-router";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { Activity, AlertTriangle, ArrowRight, Clock, MapPin, Package, Route as RouteIcon, Truck } from "lucide-react";
import { useMemo, useState } from "react";
import { CircleMarker, MapContainer, Marker, Popup, TileLayer } from "react-leaflet";
import { getModuleHighlightFromLocation } from "@/lib/control-tower-dashboard";

export const Route = createFileRoute("/logistica")({ component: Logistica });

type DeliveryStatus = "on-time" | "attention" | "delayed";
type DeliveryPeriodFilter = "today" | "next-7-days" | "next-30-days" | "delayed";

type DeliveryMapPoint = {
  id: string;
  customerName: string;
  city: string;
  regionId: string;
  status: DeliveryStatus;
  statusLabel: string;
  expectedDelivery: string;
  orderValue: number;
  daysAhead: number;
  position: [number, number];
};

const MARILIA_MAP_CENTER: [number, number] = [-22.2171, -49.9501];
const MARILIA_MAP_ZOOM = 8;
const NEXT_7_DAYS_LIMIT = 7;
const NEXT_30_DAYS_LIMIT = 30;
const CRITICAL_ROUTE_OCCUPANCY_PERCENT = 90;
const HIGH_RUPTURE_RISK_PERCENT = 45;

const deliveryFilterOptions: Array<{ value: DeliveryPeriodFilter; label: string }> = [
  { value: "today", label: "Hoje" },
  { value: "next-7-days", label: "Próximos 7 dias" },
  { value: "next-30-days", label: "Próximos 30 dias" },
  { value: "delayed", label: "Entregas atrasadas" },
];

const routeOccupancy = [
  { route: "Marília -> Bauru", regionId: "bauru", truck: "Truck 3/4", occupancy: 92, loadKg: "7.820 kg", stops: 18, status: "No limite" },
  { route: "Marília -> Assis", regionId: "assis", truck: "Toco", occupancy: 84, loadKg: "6.140 kg", stops: 14, status: "Saudável" },
  { route: "Marília -> Ourinhos", regionId: "ourinhos", truck: "Carreta", occupancy: 76, loadKg: "18.900 kg", stops: 22, status: "Folga" },
  { route: "Marília -> Tupã", regionId: "tupa", truck: "Truck", occupancy: 95, loadKg: "10.450 kg", stops: 16, status: "Crítico" },
];

const stockBreaks = [
  { sku: "PAN-104", product: "Pão Francês Congelado 60g", warehouse: "CD Marília", stock: 320, demand: 580, ruptureRisk: 45 },
  { sku: "PAN-221", product: "Pão de Queijo Congelado 1kg", warehouse: "CD Bauru", stock: 140, demand: 260, ruptureRisk: 46 },
  { sku: "PAN-318", product: "Croissant Congelado 80g", warehouse: "CD Ourinhos", stock: 90, demand: 180, ruptureRisk: 50 },
];

const deliveryMapPoints: DeliveryMapPoint[] = [
  {
    id: "cliente-marilia-01",
    customerName: "Padaria Avenida Marília",
    city: "Marília",
    regionId: "marilia",
    status: "on-time",
    statusLabel: "Entrega no prazo",
    expectedDelivery: "Hoje, 15:30",
    orderValue: 18420,
    daysAhead: 0,
    position: [-22.2171, -49.9501],
  },
  {
    id: "cliente-bauru-01",
    customerName: "Supermercado Confiança Bauru",
    city: "Bauru",
    regionId: "bauru",
    status: "attention",
    statusLabel: "Atenção",
    expectedDelivery: "Amanhã, 10:00",
    orderValue: 42600,
    daysAhead: 1,
    position: [-22.3145, -49.0606],
  },
  {
    id: "cliente-tupa-01",
    customerName: "Rede Bom Pão Tupã",
    city: "Tupã",
    regionId: "tupa",
    status: "delayed",
    statusLabel: "Entrega atrasada",
    expectedDelivery: "Ontem, 17:00",
    orderValue: 27150,
    daysAhead: -1,
    position: [-21.9347, -50.5136],
  },
  {
    id: "cliente-assis-01",
    customerName: "Mercado Avenida Assis",
    city: "Assis",
    regionId: "assis",
    status: "on-time",
    statusLabel: "Entrega no prazo",
    expectedDelivery: "Em 5 dias, 09:30",
    orderValue: 35680,
    daysAhead: 5,
    position: [-22.6617, -50.4122],
  },
  {
    id: "cliente-ourinhos-01",
    customerName: "Atacado União Ourinhos",
    city: "Ourinhos",
    regionId: "ourinhos",
    status: "attention",
    statusLabel: "Atenção",
    expectedDelivery: "Em 12 dias, 14:00",
    orderValue: 51290,
    daysAhead: 12,
    position: [-22.9797, -49.8697],
  },
  {
    id: "cliente-lins-01",
    customerName: "Padaria Central Lins",
    city: "Lins",
    regionId: "lins",
    status: "delayed",
    statusLabel: "Entrega atrasada",
    expectedDelivery: "Hoje, 08:30",
    orderValue: 16340,
    daysAhead: 0,
    position: [-21.6736, -49.7475],
  },
];

const deliveryVolumeRegions = [
  { id: "marilia", name: "Marília", center: [-22.2171, -49.9501] as [number, number], deliveries: 38, tone: "on-time" as DeliveryStatus },
  { id: "bauru", name: "Bauru", center: [-22.3145, -49.0606] as [number, number], deliveries: 52, tone: "attention" as DeliveryStatus },
  { id: "tupa", name: "Tupã", center: [-21.9347, -50.5136] as [number, number], deliveries: 29, tone: "delayed" as DeliveryStatus },
  { id: "ourinhos", name: "Ourinhos", center: [-22.9797, -49.8697] as [number, number], deliveries: 34, tone: "attention" as DeliveryStatus },
];

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(value);
}

function markerColor(status: DeliveryStatus): string {
  if (status === "delayed") return "#dc2626";
  if (status === "attention") return "#f59e0b";
  return "#16a34a";
}

function markerLabel(status: DeliveryStatus): string {
  if (status === "delayed") return "Atrasada";
  if (status === "attention") return "Atenção";
  return "No prazo";
}

function routeStatusClassName(status: string): string {
  const normalizedStatus = status.normalize("NFD").replace(/\p{Diacritic}/gu, "").toLowerCase();
  if (normalizedStatus === "critico") return "text-sm font-semibold text-red-600";
  if (normalizedStatus === "no limite") return "text-sm font-semibold text-orange-600";
  if (normalizedStatus === "saudavel") return "text-sm font-semibold text-blue-600";
  if (normalizedStatus === "folga") return "text-sm font-semibold text-green-600";
  return "text-sm font-semibold text-muted-foreground";
}

function createDeliveryIcon(status: DeliveryStatus): L.DivIcon {
  const color = markerColor(status);
  return L.divIcon({
    className: "delivery-map-marker",
    html: `<span style="background:${color}; box-shadow: 0 0 0 6px ${color}24;"></span>`,
    iconSize: [22, 22],
    iconAnchor: [11, 11],
    popupAnchor: [0, -12],
  });
}

function filterDeliveries(deliveries: DeliveryMapPoint[], filter: DeliveryPeriodFilter): DeliveryMapPoint[] {
  if (filter === "delayed") {
    return deliveries.filter((delivery) => delivery.status === "delayed");
  }

  if (filter === "today") {
    return deliveries.filter((delivery) => delivery.daysAhead === 0);
  }

  const limit = filter === "next-7-days" ? NEXT_7_DAYS_LIMIT : NEXT_30_DAYS_LIMIT;
  return deliveries.filter((delivery) => delivery.daysAhead >= 0 && delivery.daysAhead <= limit);
}

function Logistica() {
  const moduleHighlight = getModuleHighlightFromLocation();
  const [selectedDeliveryFilter, setSelectedDeliveryFilter] = useState<DeliveryPeriodFilter>("today");
  const filteredDeliveries = useMemo(
    () => filterDeliveries(deliveryMapPoints, selectedDeliveryFilter),
    [selectedDeliveryFilter],
  );
  const activeRegionIds = new Set(filteredDeliveries.map((delivery) => delivery.regionId));
  const filteredRoutes = routeOccupancy.filter((item) => selectedDeliveryFilter !== "delayed" || item.status === "Crítico" || activeRegionIds.has(item.regionId));
  const criticalRoutes = filteredRoutes.filter((item) => item.occupancy >= CRITICAL_ROUTE_OCCUPANCY_PERCENT).length;
  const averageOccupancy = Math.round(filteredRoutes.reduce((total, item) => total + item.occupancy, 0) / Math.max(1, filteredRoutes.length));
  const ruptureItems = stockBreaks.filter((item) => item.ruptureRisk >= HIGH_RUPTURE_RISK_PERCENT).length;
  const delayedDeliveries = filteredDeliveries.filter((delivery) => delivery.status === "delayed").length;
  const attentionDeliveries = filteredDeliveries.filter((delivery) => delivery.status === "attention").length;

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

      {moduleHighlight && (
        <section className="rounded-xl border border-primary/30 bg-surface p-4 text-sm text-muted-foreground ring-4 ring-primary/5">
          Indicador destacado pela Torre de Controle: {moduleHighlight}
        </section>
      )}

      <section className="metric-row">
        {[
          { label: "Frota ativa", value: "86", sub: "de 92 veículos", icon: Truck, tone: "text-foreground" },
          { label: "Ocupação por rota", value: `${averageOccupancy}%`, sub: `${criticalRoutes} rotas acima de 90%`, icon: Activity, tone: "text-primary" },
          { label: "Ruptura de estoque", value: `${ruptureItems}`, sub: "SKUs com risco alto", icon: Package, tone: "text-amber-600" },
          { label: "SLA entregas", value: "93,1%", sub: `${delayedDeliveries} atrasadas no filtro`, icon: Clock, tone: "text-foreground" },
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

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[1.45fr_0.55fr]">
        <div className="rounded-xl border border-border bg-surface p-5">
          <div className="mb-4 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div>
              <h2 className="font-display text-xl">Mapa interativo de entregas</h2>
              <p className="text-sm text-muted-foreground">Clientes, entregas, rotas planejadas e regiões com maior volume na região de Marília.</p>
            </div>
            <div className="flex flex-wrap gap-2">
              {deliveryFilterOptions.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  onClick={() => setSelectedDeliveryFilter(option.value)}
                  className={`rounded-lg border px-3 py-2 text-xs font-semibold transition-colors ${selectedDeliveryFilter === option.value ? "border-primary bg-primary text-primary-foreground" : "border-border bg-background text-muted-foreground hover:border-primary/40 hover:text-foreground"}`}
                >
                  {option.label}
                </button>
              ))}
            </div>
          </div>

          <div className="overflow-hidden rounded-xl border border-border bg-background">
            <MapContainer center={MARILIA_MAP_CENTER} zoom={MARILIA_MAP_ZOOM} scrollWheelZoom className="h-[430px] w-full">
              <TileLayer
                attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              />
              {deliveryVolumeRegions.map((region) => (
                <CircleMarker
                  key={region.id}
                  center={region.center}
                  radius={Math.max(14, region.deliveries / 2)}
                  pathOptions={{
                    color: markerColor(region.tone),
                    fillColor: markerColor(region.tone),
                    fillOpacity: activeRegionIds.has(region.id) ? 0.18 : 0.08,
                    opacity: activeRegionIds.has(region.id) ? 0.55 : 0.25,
                  }}
                >
                  <Popup>
                    <strong>{region.name}</strong>
                    <br />
                    {region.deliveries} entregas planejadas
                  </Popup>
                </CircleMarker>
              ))}
              {filteredDeliveries.map((delivery) => (
                <Marker key={delivery.id} position={delivery.position} icon={createDeliveryIcon(delivery.status)}>
                  <Popup>
                    <div className="min-w-[210px] space-y-1 text-sm">
                      <p className="font-semibold">{delivery.customerName}</p>
                      <p>Cidade: {delivery.city}</p>
                      <p>Status: {delivery.statusLabel}</p>
                      <p>Previsão: {delivery.expectedDelivery}</p>
                      <p>Valor: {formatCurrency(delivery.orderValue)}</p>
                    </div>
                  </Popup>
                </Marker>
              ))}
            </MapContainer>
          </div>
        </div>

        <div className="rounded-xl border border-border bg-surface p-5">
          <div className="mb-4 flex items-center justify-between gap-3">
            <div>
              <h2 className="font-display text-xl">Resumo do mapa</h2>
              <p className="text-sm text-muted-foreground">Filtro ativo e situação das entregas.</p>
            </div>
            <MapPin className="size-5 text-primary" />
          </div>
          <div className="space-y-3">
            <div className="rounded-lg border border-border bg-background p-4">
              <p className="text-xs uppercase tracking-widest text-muted-foreground">Filtro ativo</p>
              <p className="mt-2 text-2xl font-display">{deliveryFilterOptions.find((option) => option.value === selectedDeliveryFilter)?.label}</p>
              <p className="mt-2 text-sm text-muted-foreground">
                Base operacional com dados fictícios coerentes para clientes e cidades no entorno de Marília.
              </p>
            </div>
            <div className="grid grid-cols-2 gap-2 text-xs">
              <span className="rounded-md bg-muted px-3 py-2">Entregas: {filteredDeliveries.length}</span>
              <span className="rounded-md bg-muted px-3 py-2">Atenção: {attentionDeliveries}</span>
              <span className="rounded-md bg-muted px-3 py-2">Atrasadas: {delayedDeliveries}</span>
              <span className="rounded-md bg-muted px-3 py-2">Regiões: {activeRegionIds.size}</span>
            </div>
            <div className="space-y-2 pt-2">
              {(["on-time", "attention", "delayed"] as DeliveryStatus[]).map((status) => (
                <div key={status} className="flex items-center gap-2 text-sm text-muted-foreground">
                  <span className="size-3 rounded-full" style={{ backgroundColor: markerColor(status) }} />
                  <span>{markerLabel(status)}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
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
            {filteredRoutes.map((item) => (
              <div key={item.route} className="rounded-lg border border-border bg-background p-4">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <p className="text-sm font-semibold">{item.route}</p>
                    <p className="text-xs text-muted-foreground">{item.truck} - {item.loadKg} - {item.stops} paradas</p>
                  </div>
                  <span className={routeStatusClassName(item.status)}>{item.status}</span>
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
              <h3 className="mb-1 text-lg font-display">Tupã tem entrega atrasada e rota com 95% de ocupação.</h3>
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
