export type LogisticsCustomerType = string;
export type LogisticsCustomerStatus = "Normal" | "Atenção" | "Crítico";

export type LogisticsMapCustomer = {
  id: string;
  name: string;
  isActive: boolean;
  city: string;
  type: LogisticsCustomerType;
  status: LogisticsCustomerStatus;
  lastDelivery: string;
  nextDelivery: string;
  situation: "Atraso" | "Devolução" | "Ruptura" | "Ocorrência" | "Entrega normal";
  route: string;
  priority: "Baixa" | "Média" | "Alta";
  lat: number;
  lng: number;
};

export type LogisticsTrafficPeriodDays = 1 | 7 | 30 | 90;
export type LogisticsTrafficSeverity = "Moderado" | "Intenso" | "Crítico";
export type LogisticsMapCoordinate = { lat: number; lng: number };
export type LogisticsCongestionPoint = LogisticsMapCoordinate & {
  id: string;
  name: string;
  reason: string;
  severity: LogisticsTrafficSeverity;
  delayMinutesByPeriod: Record<LogisticsTrafficPeriodDays, number>;
  occurrencesByPeriod: Record<LogisticsTrafficPeriodDays, number>;
};
export type LogisticsMapRoute = {
  id: string;
  name: string;
  cities: string[];
  color: string;
  path: LogisticsMapCoordinate[];
  congestionPoints: LogisticsCongestionPoint[];
};
export type LogisticsTrafficDelayRanking = {
  routeId: string;
  routeName: string;
  cities: string;
  delayMinutes: number;
  congestionCount: number;
  severity: LogisticsTrafficSeverity;
};

type CityProfile = { city: string; lat: number; lng: number; customers: string[] };

const cityProfiles: CityProfile[] = [
  { city: "Marília", lat: -22.2171, lng: -49.9501, customers: ["Padaria Santa Clara", "Mercado Pão Quente", "Panificadora São Bento", "Empório do Trigo Marília", "Supermercado Avenida", "Padaria Nova Marília", "Casa dos Congelados", "Mercado Jardim Europa"] },
  { city: "Tupã", lat: -21.9347, lng: -50.5136, customers: ["Mercado Bom Pão", "Padaria Tupã Center", "Panificadora Primavera", "Supermercado Avenida Tupã", "Empório Pão de Ouro", "Mini Mercado São João", "Padaria Vila Industrial"] },
  { city: "Bauru", lat: -22.3145, lng: -49.0587, customers: ["Panificadora Avenida", "Mercado Bauru Pães", "Supermercado Vitória", "Empório Central Bauru", "Padaria Bela Vista", "Atacado Pão & Massa", "Restaurante Forno Bauru", "Conveniência Rodovia"] },
  { city: "Garça", lat: -22.2125, lng: -49.6546, customers: ["Empório do Trigo", "Padaria Garça Norte", "Mercado São Lucas", "Panificadora Vitória"] },
  { city: "Pompeia", lat: -22.1086, lng: -50.1712, customers: ["Padaria Pompeia", "Mercado Pompeia Center", "Panificadora União", "Supermercado Pão Nosso"] },
  { city: "Vera Cruz", lat: -22.2183, lng: -49.8207, customers: ["Supermercado Vera Cruz", "Padaria Estrela Vera Cruz", "Mercado Bom Preço", "Panificadora São José"] },
  { city: "Oriente", lat: -22.1549, lng: -50.0971, customers: ["Pão & Cia Oriente", "Mercado Oriente", "Padaria Central Oriente"] },
  { city: "Herculândia", lat: -22.0038, lng: -50.3907, customers: ["Mercado Herculândia", "Padaria Herculândia Pães", "Mini Mercado Esperança"] },
  { city: "Quintana", lat: -22.0691, lng: -50.307, customers: ["Panificadora Quintana", "Mercado Quintana Center", "Padaria São Pedro"] },
  { city: "Duartina", lat: -22.4149, lng: -49.4037, customers: ["Padaria Duartina", "Mercado Duartina Pães", "Panificadora Família"] },
  { city: "Lins", lat: -21.6787, lng: -49.7425, customers: ["Atacado Lins", "Padaria Lins Center", "Mercado Pão de Mel", "Supermercado Lins Norte"] },
  { city: "Cafelândia", lat: -21.8031, lng: -49.6092, customers: ["Padaria Central Cafelândia"] },
  { city: "Gália", lat: -22.2918, lng: -49.5504, customers: ["Mercado Gália Pães"] },
  { city: "Álvaro de Carvalho", lat: -22.0841, lng: -49.719, customers: ["Padaria Carvalho"] },
  { city: "Ocauçu", lat: -22.438, lng: -49.922, customers: ["Mercado Bom Trigo Ocauçu"] },
  { city: "Júlio Mesquita", lat: -22.0112, lng: -49.7873, customers: ["Panificadora Mesquita"] },
  { city: "Getulina", lat: -21.7961, lng: -49.9312, customers: ["Supermercado Getulina"] },
  { city: "Guarantã", lat: -21.8942, lng: -49.5914, customers: ["Padaria Guarantã"] },
  { city: "Pirajuí", lat: -21.9988, lng: -49.4574, customers: ["Atacado Pirajuí"] },
  { city: "Agudos", lat: -22.4694, lng: -48.9863, customers: ["Mercado Agudos Pães"] },
  { city: "Lençóis Paulista", lat: -22.5986, lng: -48.8003, customers: ["Conveniência Lençóis"] },
];

const customerTypes: LogisticsCustomerType[] = ["Padaria", "Mercado", "Supermercado", "Restaurante", "Conveniência", "Atacado"];
const situations: LogisticsMapCustomer["situation"][] = ["Entrega normal", "Entrega normal", "Atraso", "Devolução", "Ruptura", "Ocorrência"];

export const GRESPAN_HEADQUARTERS = { name: "Grespan Matriz", city: "Marília-SP", lat: -22.2139, lng: -49.9458 } as const;

const ROUTE_COLORS = ["#dc2626", "#2563eb", "#7c3aed", "#ea580c", "#0891b2", "#16a34a", "#c026d3", "#ca8a04"] as const;
const ROUTE_CITY_GROUPS = [
  ["Marília", "Vera Cruz", "Garça", "Gália"],
  ["Pompeia", "Oriente", "Quintana", "Tupã"],
  ["Herculândia"],
  ["Bauru", "Agudos", "Lençóis Paulista"],
  ["Duartina"],
  ["Lins", "Cafelândia", "Getulina"],
  ["Álvaro de Carvalho", "Guarantã", "Pirajuí"],
  ["Ocauçu", "Júlio Mesquita"],
] as const;

const CONGESTION_DEFINITIONS = [
  { name: "Acesso urbano de Marília", reason: "Fluxo intenso no horário de abastecimento", severity: "Intenso", lat: -22.226, lng: -49.932, delay: [18, 96, 318, 884], occurrences: [1, 5, 14, 39] },
  { name: "Trevo de Pompeia", reason: "Obras e redução temporária de faixa", severity: "Crítico", lat: -22.126, lng: -50.128, delay: [31, 142, 486, 1_240], occurrences: [2, 7, 21, 54] },
  { name: "Acesso de Herculândia", reason: "Pico urbano e faixa simples no acesso municipal", severity: "Intenso", lat: -22.035, lng: -50.352, delay: [24, 118, 402, 1_058], occurrences: [1, 6, 18, 46] },
  { name: "Eixo Bauru–Agudos", reason: "Retenção por alto volume de veículos pesados", severity: "Crítico", lat: -22.394, lng: -49.018, delay: [42, 196, 642, 1_680], occurrences: [2, 9, 27, 71] },
  { name: "Acesso de Duartina", reason: "Congestionamento recorrente no início da manhã", severity: "Intenso", lat: -22.39, lng: -49.443, delay: [27, 153, 521, 1_364], occurrences: [1, 8, 23, 59] },
  { name: "Trevo de Lins", reason: "Interdição parcial e filas no acesso municipal", severity: "Moderado", lat: -21.724, lng: -49.768, delay: [12, 71, 244, 690], occurrences: [1, 4, 11, 31] },
  { name: "Corredor de Pirajuí", reason: "Lentidão no entroncamento rodoviário", severity: "Moderado", lat: -22.022, lng: -49.532, delay: [9, 58, 198, 548], occurrences: [1, 3, 9, 24] },
  { name: "Acesso sul de Ocauçu", reason: "Tráfego local e faixa simples", severity: "Moderado", lat: -22.404, lng: -49.9, delay: [7, 44, 152, 416], occurrences: [1, 3, 7, 19] },
] as const satisfies ReadonlyArray<{ name: string; reason: string; severity: LogisticsTrafficSeverity; lat: number; lng: number; delay: readonly [number, number, number, number]; occurrences: readonly [number, number, number, number] }>;

const TRAFFIC_PERIODS: LogisticsTrafficPeriodDays[] = [1, 7, 30, 90];

function valuesByPeriod(values: readonly [number, number, number, number]): Record<LogisticsTrafficPeriodDays, number> {
  return Object.fromEntries(TRAFFIC_PERIODS.map((period, index) => [period, values[index]])) as Record<LogisticsTrafficPeriodDays, number>;
}

function findCityProfile(city: string): CityProfile {
  const profile = cityProfiles.find((item) => item.city === city);
  if (!profile) throw new Error(`Cidade sem coordenadas logísticas: ${city}`);
  return profile;
}

export function buildDemoLogisticsMapRoutes(): LogisticsMapRoute[] {
  return ROUTE_CITY_GROUPS.map((cities, index) => {
    const congestion = CONGESTION_DEFINITIONS[index];
    return {
      id: `ROT-${String(index + 1).padStart(2, "0")}`,
      name: `Rota ${String(index + 1).padStart(2, "0")}`,
      cities: [...cities],
      color: ROUTE_COLORS[index],
      path: [{ lat: GRESPAN_HEADQUARTERS.lat, lng: GRESPAN_HEADQUARTERS.lng }, ...cities.map((city) => { const profile = findCityProfile(city); return { lat: profile.lat, lng: profile.lng }; })],
      congestionPoints: [{
        id: `CONG-${String(index + 1).padStart(2, "0")}`,
        name: congestion.name,
        reason: congestion.reason,
        severity: congestion.severity,
        lat: congestion.lat,
        lng: congestion.lng,
        delayMinutesByPeriod: valuesByPeriod(congestion.delay),
        occurrencesByPeriod: valuesByPeriod(congestion.occurrences),
      }],
    };
  });
}

export const demoLogisticsMapRoutes = buildDemoLogisticsMapRoutes();

export function buildTrafficDelayRanking(
  routes: LogisticsMapRoute[],
  periodDays: LogisticsTrafficPeriodDays,
): LogisticsTrafficDelayRanking[] {
  return routes.map((route) => {
    const delayMinutes = route.congestionPoints.reduce((total, point) => total + Math.max(0, point.delayMinutesByPeriod[periodDays] ?? 0), 0);
    const congestionCount = route.congestionPoints.reduce((total, point) => total + Math.max(0, point.occurrencesByPeriod[periodDays] ?? 0), 0);
    const severity = route.congestionPoints.reduce<LogisticsTrafficSeverity>((current, point) => {
      const rank: Record<LogisticsTrafficSeverity, number> = { Moderado: 0, Intenso: 1, Crítico: 2 };
      return rank[point.severity] > rank[current] ? point.severity : current;
    }, "Moderado");
    return { routeId: route.id, routeName: route.name, cities: route.cities.join(" → "), delayMinutes, congestionCount, severity };
  }).filter((route) => route.delayMinutes > 0 || route.congestionCount > 0)
    .sort((left, right) => right.delayMinutes - left.delayMinutes || right.congestionCount - left.congestionCount || left.routeName.localeCompare(right.routeName));
}

export function buildDemoLogisticsMapCustomers(): LogisticsMapCustomer[] {
  let globalIndex = 0;
  return cityProfiles.flatMap((profile, cityIndex) => profile.customers.map((name, customerIndex) => {
    const angle = (customerIndex / Math.max(1, profile.customers.length)) * Math.PI * 2;
    const spread = 0.008 + (customerIndex % 3) * 0.002;
    const status: LogisticsCustomerStatus = globalIndex % 11 === 0 ? "Crítico" : globalIndex % 4 === 0 ? "Atenção" : "Normal";
    const type = customerTypes[(globalIndex + cityIndex) % customerTypes.length];
    const situation = status === "Normal" ? "Entrega normal" : situations[(globalIndex + 2) % situations.length];
    const customer: LogisticsMapCustomer = {
      id: `CLI-${String(globalIndex + 1).padStart(3, "0")}`,
      name,
      isActive: status !== "Crítico",
      city: profile.city,
      type,
      status,
      lastDelivery: `${String(18 + (globalIndex % 6)).padStart(2, "0")}/06/2026`,
      nextDelivery: `${String(24 + (globalIndex % 5)).padStart(2, "0")}/06/2026`,
      situation,
      route: `${demoLogisticsMapRoutes.find((route) => route.cities.includes(profile.city))?.name ?? "Rota regional"} · ${profile.city}`,
      priority: status === "Crítico" ? "Alta" : status === "Atenção" ? "Média" : "Baixa",
      lat: profile.lat + Math.cos(angle) * spread,
      lng: profile.lng + Math.sin(angle) * spread,
    };
    globalIndex += 1;
    return customer;
  }));
}

export const demoLogisticsMapCustomers = buildDemoLogisticsMapCustomers();

export type LogisticsCityCustomerCount = { city: string; customerCount: number };

const DEFAULT_CITY_CHART_LIMIT = 10;

export function buildCustomerCountByCity(
  customers: LogisticsMapCustomer[],
  limit = DEFAULT_CITY_CHART_LIMIT,
): LogisticsCityCustomerCount[] {
  const counts = new Map<string, number>();
  for (const customer of customers) counts.set(customer.city, (counts.get(customer.city) ?? 0) + 1);
  const ordered = [...counts.entries()]
    .map(([city, customerCount]) => ({ city, customerCount }))
    .sort((left, right) => right.customerCount - left.customerCount || left.city.localeCompare(right.city));
  const safeLimit = Math.max(1, Math.floor(limit));
  if (ordered.length <= safeLimit) return ordered;
  const visible = ordered.slice(0, safeLimit);
  const remaining = ordered.slice(safeLimit).reduce((total, item) => total + item.customerCount, 0);
  return [...visible, { city: "Outras cidades", customerCount: remaining }];
}
