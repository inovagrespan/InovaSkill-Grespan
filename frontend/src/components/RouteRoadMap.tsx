import { useEffect, useRef } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import type { RouteRoadPath } from "@/lib/importer-api";

type RouteRoadMapProps = { route: RouteRoadPath };

function escapeHtml(value: string): string {
  return value.replace(/[&<>'"]/g, (character) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" })[character] ?? character);
}

function stopIcon(sequence: number, isDepot: boolean): L.DivIcon {
  return L.divIcon({
    className: "route-road-map-marker",
    html: `<span class="${isDepot ? "route-road-map-marker--depot" : "route-road-map-marker--customer"}">${isDepot ? "G" : sequence}</span>`,
    iconSize: [30, 30],
    iconAnchor: [15, 15],
    popupAnchor: [0, -14],
  });
}

export function RouteRoadMap({ route }: RouteRoadMapProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!containerRef.current || route.geometry.coordinates.length < 2) return;
    const geometry = route.geometry.coordinates.map(([longitude, latitude]) => [latitude, longitude] as [number, number]);
    const map = L.map(containerRef.current, { zoomControl: true, scrollWheelZoom: true });
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      attribution: "&copy; OpenStreetMap contributors",
      maxZoom: 18,
    }).addTo(map);
    L.polyline(geometry, {
      color: "#2563eb",
      weight: 4,
      opacity: 0.9,
      dashArray: "10 8",
      lineCap: "round",
      lineJoin: "round",
    }).addTo(map).bindTooltip("Percurso calculado sobre a malha rodoviária", { sticky: true });

    const visibleStops = route.stops.filter((stop, index) => index < route.stops.length - 1);
    visibleStops.forEach((stop) => {
      L.marker([stop.latitude, stop.longitude], { icon: stopIcon(stop.sequence, stop.isDepot) })
        .bindPopup(`<div class="logistics-map-popup"><strong>${escapeHtml(stop.label)}</strong><span>${stop.isDepot ? "Origem e retorno" : `Parada ${stop.sequence}`}</span></div>`)
        .addTo(map);
    });
    map.fitBounds(L.latLngBounds(geometry), { padding: [24, 24] });
    window.setTimeout(() => map.invalidateSize(), 0);
    return () => map.remove();
  }, [route]);

  return <div className="relative z-0 overflow-hidden rounded-lg border border-border"><div ref={containerRef} className="h-[420px] min-h-[340px] w-full" /></div>;
}
