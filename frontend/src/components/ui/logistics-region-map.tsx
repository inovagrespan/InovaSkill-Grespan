import { useEffect, useMemo, useRef, useState } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { LocateFixed, TrafficCone, Users } from "lucide-react";
import { Button } from "./button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "./select";
import {
  GRESPAN_HEADQUARTERS,
  type LogisticsMapRoute,
  type LogisticsTrafficPeriodDays,
  type LogisticsCustomerStatus,
  type LogisticsCustomerType,
  type LogisticsMapCustomer,
} from "@/lib/logistics-map-data";

type LogisticsRegionMapProps = { customers: LogisticsMapCustomer[]; routes: LogisticsMapRoute[]; periodDays: LogisticsTrafficPeriodDays; compact?: boolean };

const REGIONAL_ZOOM = 9;
const CUSTOMER_ZOOM = 11;
const ALL_FILTER = "Todos";

function escapeHtml(value: string): string {
  return value.replace(/[&<>'"]/g, (character) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" })[character] ?? character);
}

function customerPopup(customer: LogisticsMapCustomer): string {
  return `<div class="logistics-map-popup"><strong>${escapeHtml(customer.name)}</strong><span>${escapeHtml(customer.city)} · ${escapeHtml(customer.type)}</span><hr/><span><b>Status:</b> ${escapeHtml(customer.status)}</span><span><b>Última entrega:</b> ${escapeHtml(customer.lastDelivery)}</span><span><b>Próxima entrega:</b> ${escapeHtml(customer.nextDelivery)}</span><span><b>Situação:</b> ${escapeHtml(customer.situation)}</span><span><b>Rota:</b> ${escapeHtml(customer.route)}</span><span><b>Prioridade:</b> ${escapeHtml(customer.priority)}</span></div>`;
}

function createHeadquartersIcon(): L.DivIcon {
  return L.divIcon({ className: "logistics-map-headquarters", html: "<span>G</span>", iconSize: [34, 34], iconAnchor: [17, 17] });
}

function createCustomerPinIcon(status: LogisticsCustomerStatus): L.DivIcon {
  const statusClass = status === "Crítico" ? "critical" : status === "Atenção" ? "attention" : "normal";
  return L.divIcon({
    className: "logistics-map-customer-icon",
    html: `<span class="logistics-map-pin logistics-map-pin--${statusClass}"><i></i></span>`,
    iconSize: [28, 36],
    iconAnchor: [14, 34],
    popupAnchor: [0, -32],
    tooltipAnchor: [0, -28],
  });
}

function createCongestionIcon(severity: string): L.DivIcon {
  const severityClass = severity === "Crítico" ? "critical" : severity === "Intenso" ? "intense" : "moderate";
  return L.divIcon({
    className: "logistics-map-congestion-icon",
    html: `<span class="logistics-map-congestion logistics-map-congestion--${severityClass}"><b>!</b></span>`,
    iconSize: [28, 28],
    iconAnchor: [14, 14],
    popupAnchor: [0, -12],
  });
}

function routePopup(route: LogisticsMapRoute): string {
  return `<div class="logistics-map-popup"><strong>${escapeHtml(route.name)}</strong><span>${escapeHtml(route.cities.join(" → "))}</span><hr/><span><b>Trajeto estimado</b></span><span>Baseado nas cidades atendidas e pontos de congestionamento monitorados.</span></div>`;
}

export function LogisticsRegionMap({ customers, routes, periodDays, compact = false }: LogisticsRegionMapProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<L.Map | null>(null);
  const customerLayerRef = useRef<L.LayerGroup | null>(null);
  const [status, setStatus] = useState(ALL_FILTER);
  const [city, setCity] = useState(ALL_FILTER);
  const [type, setType] = useState(ALL_FILTER);

  const cities = useMemo(() => [...new Set(customers.map((customer) => customer.city))].sort(), [customers]);
  const types = useMemo(() => [...new Set(customers.map((customer) => customer.type))].sort(), [customers]);
  const filtered = useMemo(() => customers.filter((customer) =>
    (status === ALL_FILTER || customer.status === status)
    && (city === ALL_FILTER || customer.city === city)
    && (type === ALL_FILTER || customer.type === type)), [customers, status, city, type]);

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;
    const map = L.map(containerRef.current, { center: [GRESPAN_HEADQUARTERS.lat, GRESPAN_HEADQUARTERS.lng], zoom: REGIONAL_ZOOM, zoomControl: true, scrollWheelZoom: true });
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", { attribution: "&copy; OpenStreetMap contributors", maxZoom: 18 }).addTo(map);
    L.marker([GRESPAN_HEADQUARTERS.lat, GRESPAN_HEADQUARTERS.lng], { icon: createHeadquartersIcon(), zIndexOffset: 1000 }).bindPopup(`<div class="logistics-map-popup"><strong>${GRESPAN_HEADQUARTERS.name}</strong><span>${GRESPAN_HEADQUARTERS.city}</span><span><b>Base principal da operação logística</b></span></div>`).addTo(map);
    customerLayerRef.current = L.layerGroup().addTo(map);
    mapRef.current = map;
    window.setTimeout(() => map.invalidateSize(), 0);
    return () => { map.remove(); mapRef.current = null; customerLayerRef.current = null; };
  }, []);

  useEffect(() => {
    const layer = customerLayerRef.current;
    const map = mapRef.current;
    if (!layer || !map) return;
    layer.clearLayers();

    const visibleRoutes = routes.filter((route) => city === ALL_FILTER || route.cities.includes(city));
    for (const route of visibleRoutes) {
      const path = route.path.map((point) => [point.lat, point.lng] as [number, number]);
      const line = L.polyline(path, {
        color: route.color,
        weight: 2.5,
        opacity: 0.72,
        dashArray: "8 8",
        lineCap: "round",
        lineJoin: "round",
      });
      line.bindTooltip(route.name, { sticky: true });
      line.bindPopup(routePopup(route), { maxWidth: 310 });
      layer.addLayer(line);

      for (const point of route.congestionPoints) {
        const delay = point.delayMinutesByPeriod[periodDays];
        const occurrences = point.occurrencesByPeriod[periodDays];
        const marker = L.marker([point.lat, point.lng], { icon: createCongestionIcon(point.severity), zIndexOffset: 800 });
        marker.bindTooltip(`${escapeHtml(point.name)} · +${delay} min`, { direction: "top" });
        marker.bindPopup(`<div class="logistics-map-popup"><strong>${escapeHtml(point.name)}</strong><span>${escapeHtml(route.name)} · ${escapeHtml(point.severity)}</span><hr/><span><b>Causa:</b> ${escapeHtml(point.reason)}</span><span><b>Atraso no período:</b> ${delay} min</span><span><b>Registros:</b> ${occurrences}</span></div>`);
        layer.addLayer(marker);
      }
    }

    for (const customer of filtered) {
      const marker = L.marker([customer.lat, customer.lng], { icon: createCustomerPinIcon(customer.status), zIndexOffset: 500 });
      marker.bindPopup(customerPopup(customer), { maxWidth: 310 });
      marker.bindTooltip(customer.name, { direction: "top", offset: [0, -7] });
      layer.addLayer(marker);
    }
  }, [city, filtered, periodDays, routes]);

  function recenterHeadquarters() {
    mapRef.current?.setView([GRESPAN_HEADQUARTERS.lat, GRESPAN_HEADQUARTERS.lng], REGIONAL_ZOOM);
  }

  function showAllCustomers() {
    if (!mapRef.current || filtered.length === 0) return;
    const bounds = L.latLngBounds(filtered.map((customer) => [customer.lat, customer.lng] as [number, number]));
    mapRef.current.fitBounds(bounds, { padding: [35, 35], maxZoom: CUSTOMER_ZOOM });
  }

  return (
    <div className="space-y-4">
      <div className={compact ? "grid gap-2 sm:grid-cols-2" : "grid gap-2 sm:grid-cols-3 xl:grid-cols-[180px_220px_220px_auto]"}>
        <Select value={status} onValueChange={setStatus}><SelectTrigger aria-label="Filtrar por status"><SelectValue /></SelectTrigger><SelectContent><SelectItem value={ALL_FILTER}>Todos os status</SelectItem><SelectItem value="Normal">Normal</SelectItem><SelectItem value="Atenção">Atenção</SelectItem><SelectItem value="Crítico">Crítico</SelectItem></SelectContent></Select>
        <Select value={city} onValueChange={setCity}><SelectTrigger aria-label="Filtrar por cidade"><SelectValue /></SelectTrigger><SelectContent><SelectItem value={ALL_FILTER}>Todas as cidades</SelectItem>{cities.map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select>
        <Select value={type} onValueChange={setType}><SelectTrigger aria-label="Filtrar por tipo"><SelectValue /></SelectTrigger><SelectContent><SelectItem value={ALL_FILTER}>Todos os tipos</SelectItem>{types.map((item) => <SelectItem key={item} value={item}>{item}</SelectItem>)}</SelectContent></Select>
        <div className={compact ? "flex flex-wrap gap-2 sm:col-span-2" : "flex flex-wrap gap-2 xl:justify-end"}><Button variant="outline" onClick={recenterHeadquarters}><LocateFixed className="mr-2 size-4" />Centralizar matriz</Button><Button variant="outline" onClick={showAllCustomers}><Users className="mr-2 size-4" />Mostrar clientes</Button></div>
      </div>
      <div className="relative overflow-hidden rounded-xl border border-border shadow-sm"><div ref={containerRef} className={compact ? "h-[390px] min-h-[360px] w-full md:h-[430px]" : "h-[500px] min-h-[420px] w-full md:h-[560px]"} /><div className="pointer-events-none absolute left-3 top-3 z-[500] rounded-md border bg-background/95 px-3 py-2 text-xs font-medium shadow-sm backdrop-blur">{filtered.length} clientes visíveis</div></div>
      <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-xs text-muted-foreground"><span className="font-medium text-foreground">Legenda:</span><span className="inline-flex items-center gap-1.5"><i className="size-6 border-t-2 border-dashed border-primary" />Trajeto estimado</span><span className="inline-flex items-center gap-1.5"><i className="size-2.5 rounded-full bg-emerald-600" />Cliente normal</span><span className="inline-flex items-center gap-1.5"><i className="size-2.5 rounded-full bg-amber-500" />Cliente em atenção</span><span className="inline-flex items-center gap-1.5"><i className="size-2.5 rounded-full bg-red-600" />Cliente crítico</span><span className="inline-flex items-center gap-1.5"><TrafficCone className="size-3.5 text-red-600" />Congestionamento</span></div>
    </div>
  );
}
