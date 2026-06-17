import { useEffect, useMemo, useRef, useState } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { cn } from "@/lib/utils";
import { CITY_COORDS, REGIONS, findCity } from "@/lib/brazil-cities-coords";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { MapPin, Search, X } from "lucide-react";

delete (L.Icon.Default.prototype as any)._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png",
  iconUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png",
  shadowUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png",
});

interface CustomerPoint {
  name: string;
  city: string;
  revenue: number;
  lat?: number;
  lng?: number;
}

interface CustomerMapProps {
  customers: CustomerPoint[];
  className?: string;
}

export function CustomerMap({ customers, className }: CustomerMapProps) {
  const mapRef = useRef<HTMLDivElement>(null);
  const mapInstance = useRef<L.Map | null>(null);
  const markersLayer = useRef<L.LayerGroup | null>(null);
  const [selectedRegion, setSelectedRegion] = useState("Todas");
  const [searchCity, setSearchCity] = useState("");

  const enriched = useMemo(() => {
    const cityGroups: Record<string, number> = {};
    customers.forEach(c => {
      cityGroups[c.city] = (cityGroups[c.city] ?? 0) + 1;
    });
    const cityOffsets: Record<string, number> = {};
    return customers.map(c => {
      const coords = CITY_COORDS[c.city] ?? findCity(c.city);
      const baseLat = coords?.lat ?? -23.55;
      const baseLng = coords?.lng ?? -46.63;
      const total = cityGroups[c.city] ?? 1;
      if (total === 1) {
        return { ...c, lat: baseLat, lng: baseLng };
      }
      const idx = cityOffsets[c.city] ?? 0;
      cityOffsets[c.city] = idx + 1;
      const angle = (idx / total) * Math.PI * 2;
      const spread = 0.015;
      return {
        ...c,
        lat: baseLat + Math.cos(angle) * spread,
        lng: baseLng + Math.sin(angle) * spread,
      };
    });
  }, [customers]);

  const filtered = useMemo(() => {
    let list = enriched;
    if (selectedRegion !== "Todas") {
      list = list.filter(c => {
        const coords = findCity(c.city) ?? CITY_COORDS[c.city];
        return coords?.region === selectedRegion;
      });
    }
    if (searchCity.trim()) {
      const q = searchCity.trim().toLowerCase();
      list = list.filter(c => c.city.toLowerCase().includes(q));
    }
    return list;
  }, [enriched, selectedRegion, searchCity]);

  useEffect(() => {
    if (!mapRef.current || mapInstance.current) return;
    const map = L.map(mapRef.current, {
      center: [-15.5, -52],
      zoom: 4.5,
      zoomControl: true,
      scrollWheelZoom: true,
    });
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      attribution: "&copy; OpenStreetMap contributors",
      maxZoom: 18,
    }).addTo(map);
    mapInstance.current = map;
    markersLayer.current = L.layerGroup().addTo(map);
    return () => { map.remove(); mapInstance.current = null; };
  }, []);

  useEffect(() => {
    if (!markersLayer.current) return;
    markersLayer.current.clearLayers();
    if (filtered.length === 0) return;
    const bounds = L.latLngBounds([]);
    let validCount = 0;
    filtered.forEach(c => {
      const color = c.revenue > 100000 ? "#B4232F" : c.revenue > 50000 ? "#D97706" : "#3B82F6";
      const size = c.revenue > 100000 ? 14 : c.revenue > 50000 ? 11 : 8;
      const marker = L.circleMarker([c.lat, c.lng], {
        radius: size,
        fillColor: color,
        color: "#fff",
        weight: 2,
        opacity: 1,
        fillOpacity: 0.85,
      });
      marker.bindTooltip(`<b>${c.name}</b><br/>${c.city}<br/>R$ ${c.revenue.toLocaleString("pt-BR")}`, {
        direction: "top",
        offset: [0, -8],
      });
      markersLayer.current?.addLayer(marker);
      try { bounds.extend([c.lat, c.lng]); validCount++; } catch { /* skip invalid coords */ }
    });
    if (validCount === 1) {
      mapInstance.current?.setView([filtered[0].lat, filtered[0].lng], 5);
    } else if (validCount > 1) {
      mapInstance.current?.fitBounds(bounds, { padding: [40, 40] });
    }
  }, [filtered]);

  const cityCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    enriched.forEach(c => { counts[c.city] = (counts[c.city] || 0) + 1; });
    return Object.entries(counts).sort((a, b) => b[1] - a[1]);
  }, [enriched]);

  return (
    <div className={cn("space-y-4", className)}>
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-[200px] max-w-xs">
          <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input
            placeholder="Filtrar por cidade..."
            value={searchCity}
            onChange={e => setSearchCity(e.target.value)}
            className="pl-8 h-9 text-sm"
          />
          {searchCity && (
            <button onClick={() => setSearchCity("")} className="absolute right-2 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground">
              <X className="w-3.5 h-3.5" />
            </button>
          )}
        </div>
        <div className="flex flex-wrap gap-1.5">
          {REGIONS.map(r => (
            <button
              key={r}
              onClick={() => setSelectedRegion(r)}
              className={cn(
                "px-2.5 py-1 rounded-full text-xs font-medium border transition-colors",
                selectedRegion === r
                  ? "bg-primary text-primary-foreground border-primary"
                  : "bg-surface text-muted-foreground border-border hover:border-primary/40"
              )}
            >
              {r}
            </button>
          ))}
        </div>
      </div>
      <div className="flex gap-4 flex-col lg:flex-row">
        <div ref={mapRef} className="h-[420px] rounded-xl border border-border overflow-hidden flex-1 z-0" />
        <div className="w-full lg:w-56 space-y-2">
          <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Clientes por cidade</p>
          <div className="space-y-1 max-h-[380px] overflow-y-auto custom-scrollbar pr-1">
            {cityCounts.map(([city, count]) => (
              <button
                key={city}
                onClick={() => { setSearchCity(city); setSelectedRegion("Todas"); }}
                className="flex w-full items-center justify-between rounded-lg px-2.5 py-1.5 text-sm hover:bg-muted/60 transition-colors text-left"
              >
                <span className="flex items-center gap-1.5">
                  <MapPin className="w-3 h-3 text-primary shrink-0" />
                  <span className="truncate">{city}</span>
                </span>
                <Badge variant="outline" className="text-[10px] ml-2 shrink-0">{count}</Badge>
              </button>
            ))}
            {cityCounts.length === 0 && (
              <p className="text-xs text-muted-foreground">Nenhuma cidade encontrada.</p>
            )}
          </div>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
        <span className="flex items-center gap-1"><span className="w-2.5 h-2.5 rounded-full bg-[#B4232F]" /> Alto faturamento</span>
        <span className="flex items-center gap-1"><span className="w-2.5 h-2.5 rounded-full bg-[#D97706]" /> Médio faturamento</span>
        <span className="flex items-center gap-1"><span className="w-2.5 h-2.5 rounded-full bg-[#3B82F6]" /> Baixo faturamento</span>
      </div>
    </div>
  );
}
