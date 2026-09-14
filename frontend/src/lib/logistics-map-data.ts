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
  locationPrecision?: "ADDRESS_EXACT" | "ADDRESS_INTERPOLATED" | "ADDRESS_APPROXIMATE" | "MUNICIPALITY";
  coordinateAccuracy?: "EXACT" | "APPROXIMATE";
  coordinatePrecision?: "EXACT" | "INTERPOLATED" | "STREET" | "POSTAL_CODE" | "MUNICIPALITY";
  address?: string | null;
  lat: number;
  lng: number;
};

export type LogisticsMapPrecisionFilter = "ALL" | "EXACT" | "APPROXIMATE";

export const DEFAULT_LOGISTICS_MAP_PRECISION_FILTER: LogisticsMapPrecisionFilter = "ALL";

const APPROXIMATE_COORDINATE_PRECISIONS = new Set<NonNullable<LogisticsMapCustomer["coordinatePrecision"]>>([
  "INTERPOLATED",
  "STREET",
  "POSTAL_CODE",
  "MUNICIPALITY",
]);

function getCustomerCoordinateAccuracy(customer: LogisticsMapCustomer): "EXACT" | "APPROXIMATE" | null {
  if (customer.coordinatePrecision === "EXACT") return "EXACT";
  if (customer.coordinatePrecision && APPROXIMATE_COORDINATE_PRECISIONS.has(customer.coordinatePrecision)) {
    return "APPROXIMATE";
  }
  if (customer.coordinateAccuracy) return customer.coordinateAccuracy;
  if (customer.locationPrecision === "ADDRESS_EXACT") return "EXACT";
  if (customer.locationPrecision) return "APPROXIMATE";
  return null;
}

export function filterLogisticsMapCustomersByPrecision(
  customers: LogisticsMapCustomer[],
  precision: LogisticsMapPrecisionFilter,
): LogisticsMapCustomer[] {
  if (precision === DEFAULT_LOGISTICS_MAP_PRECISION_FILTER) return customers;
  return customers.filter((customer) => getCustomerCoordinateAccuracy(customer) === precision);
}

export function listLogisticsMapCities(customers: LogisticsMapCustomer[]): string[] {
  return [...new Set(customers.map((customer) => customer.city))].sort((left, right) => left.localeCompare(right, "pt-BR"));
}

export type LogisticsTrafficPeriodDays = 1 | 7 | 30 | 90;
export type LogisticsTrafficSeverity = "Moderado" | "Intenso" | "Crítico";
export type LogisticsMapCoordinate = { lat: number; lng: number };
export type TollOperator = "EIXO SP" | "ViaRondon" | "Rodovias do Tietê" | "Triunfo Transbrasiliana" | "CART" | "Entrevias" | "Arteris ViaPaulista";
export type TollPlaza = LogisticsMapCoordinate & {
  id: string;
  name: string;
  highway: string;
  kilometer: number;
  municipality: string;
  passengerManual: number;
  commercialManualByAxle: Record<number, number>;
  operator?: TollOperator;
  automaticDiscountRate?: number;
};
export type EixoTollPlaza = TollPlaza;

/**
 * Praças da concessão EIXO SP, com tarifas manuais vigentes desde 04/06/2026
 * (fonte: https://eixosp.com.br/tarifas/).
 * A tarifa automática é calculada com o desconto contratual de 5%.
 * Coordenadas são pontos de referência da praça para visualização regional.
 */
export const EIXO_AUTOMATIC_DISCOUNT_RATE = 0.05;
export const DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE = 0.05;
const CURRENCY_ROUNDING_EPSILON = 1e-9;
const commercial = (values: readonly number[]): Record<number, number> =>
  Object.fromEntries(values.map((value, index) => [index + 2, value]));

export const EIXO_TOLL_PLAZAS: readonly TollPlaza[] = [
  { id: "P01", name: "Rio Claro", highway: "SP-310", kilometer: 181.5, municipality: "Rio Claro", lat: -22.3740609, lng: -47.620447, passengerManual: 11.6, commercialManualByAxle: commercial([23.3, 35, 46.6, 58.3, 69.9, 81.5, 93.2, 104.9]) },
  { id: "P02", name: "Itirapina", highway: "SP-310", kilometer: 217, municipality: "Itirapina", lat: -22.1289932, lng: -47.8080853, passengerManual: 7, commercialManualByAxle: commercial([14.1, 21.1, 28.2, 35.2, 42.3, 49.4, 56.4, 63.4]) },
  { id: "P03", name: "Brotas", highway: "SP-225", kilometer: 106.9, municipality: "Brotas", lat: -22.263813, lng: -47.9025024, passengerManual: 10, commercialManualByAxle: commercial([20.1, 30.1, 40.1, 50.1, 60.2, 70.2, 80.2, 90.3]) },
  { id: "P04", name: "Dois Córregos", highway: "SP-225", kilometer: 143.8, municipality: "Dois Córregos", lat: -22.2559811, lng: -48.2438122, passengerManual: 11.4, commercialManualByAxle: commercial([22.8, 34.1, 45.5, 56.9, 68.3, 79.7, 91.1, 102.4]) },
  { id: "P05", name: "Jaú", highway: "SP-225", kilometer: 99.4, municipality: "Jaú", lat: -22.3182804, lng: -48.7254956, passengerManual: 14.6, commercialManualByAxle: commercial([29.3, 43.9, 58.6, 73.2, 87.9, 102.5, 117.2, 131.8]) },
  { id: "P06", name: "Piracicaba", highway: "SP-308", kilometer: 182.25, municipality: "Piracicaba", lat: -22.606497, lng: -47.7148423, passengerManual: 7.4, commercialManualByAxle: commercial([14.7, 22.1, 29.5, 36.9, 44.2, 51.6, 59, 66.4]) },
  { id: "P07", name: "São Pedro I", highway: "SP-304", kilometer: 183.4, municipality: "São Pedro", lat: -22.6482408, lng: -47.8071778, passengerManual: 8.3, commercialManualByAxle: commercial([16.5, 24.8, 33.1, 41.4, 49.6, 57.9, 66.2, 74.4]) },
  { id: "P08", name: "São Pedro II", highway: "SP-304", kilometer: 215.1, municipality: "São Pedro", lat: -22.5648998, lng: -48.0620089, passengerManual: 8.6, commercialManualByAxle: commercial([17.1, 25.7, 34.3, 42.9, 51.4, 60, 68.6, 77.2]) },
  { id: "P09", name: "Torrinha", highway: "SP-304", kilometer: 155.8, municipality: "Torrinha", lat: -22.4050711, lng: -48.2616579, passengerManual: 7.6, commercialManualByAxle: commercial([15.3, 22.9, 30.5, 38.2, 45.8, 53.5, 61.1, 68.7]) },
  { id: "P10", name: "Piratininga", highway: "SP-294", kilometer: 370, municipality: "Piratininga", lat: -22.3456145, lng: -49.2862524, passengerManual: 13, commercialManualByAxle: commercial([26, 39, 52, 65.1, 78.1, 91.1, 104.1, 117.1]) },
  { id: "P11", name: "Garça", highway: "SP-294", kilometer: 425.7, municipality: "Garça", lat: -22.2138223, lng: -49.7424902, passengerManual: 11.7, commercialManualByAxle: commercial([23.5, 35.2, 46.9, 58.7, 70.4, 82.2, 93.9, 105.6]) },
  { id: "P12", name: "Oriente", highway: "SP-294", kilometer: 474.8, municipality: "Oriente", lat: -22.1388848, lng: -50.1180007, passengerManual: 11.9, commercialManualByAxle: commercial([23.8, 35.8, 47.7, 59.6, 71.6, 83.5, 95.4, 107.3]) },
  { id: "P13", name: "Parapuã", highway: "SP-294", kilometer: 551.5, municipality: "Parapuã", lat: -21.8420125, lng: -50.721259, passengerManual: 11.4, commercialManualByAxle: commercial([22.9, 34.3, 45.7, 57.2, 68.6, 80, 91.5, 102.9]) },
  { id: "P14", name: "Inúbia Paulista", highway: "SP-294", kilometer: 581.7, municipality: "Inúbia Paulista", lat: -21.7337511, lng: -50.9821736, passengerManual: 8, commercialManualByAxle: commercial([15.9, 23.9, 31.8, 39.8, 47.8, 55.7, 63.7, 71.7]) },
  { id: "P15", name: "Pacaembu", highway: "SP-294", kilometer: 623.1, municipality: "Pacaembu", lat: -21.5458248, lng: -51.3117108, passengerManual: 8.9, commercialManualByAxle: commercial([17.7, 26.6, 35.5, 44.3, 53.2, 62.1, 70.9, 79.8]) },
  { id: "P16", name: "Indiana", highway: "SP-425", kilometer: 436, municipality: "Indiana", lat: -22.143444, lng: -51.2370804, passengerManual: 7.2, commercialManualByAxle: commercial([14.5, 21.7, 28.9, 36.2, 43.4, 50.6, 57.9, 65.1]) },
  { id: "P17", name: "Paraguaçu Paulista", highway: "SP-284", kilometer: 458.3, municipality: "Paraguaçu Paulista", lat: -22.5723235, lng: -50.5172302, passengerManual: 8, commercialManualByAxle: commercial([15.9, 23.9, 31.8, 39.8, 47.8, 55.7, 63.7, 71.7]) },
  { id: "P18", name: "Rancharia", highway: "SP-284", kilometer: 531.2, municipality: "Rancharia", lat: -22.2186759, lng: -51.0208485, passengerManual: 8.3, commercialManualByAxle: commercial([16.7, 25, 33.3, 41.6, 50, 58.3, 66.6, 75]) },
  { id: "P19", name: "Martinópolis", highway: "SP-425", kilometer: 400.1, municipality: "Martinópolis", lat: -21.9592878, lng: -50.9552327, passengerManual: 5.9, commercialManualByAxle: commercial([11.9, 17.8, 23.7, 29.7, 35.6, 41.5, 47.5, 53.4]) },
  { id: "P20", name: "Santa Mercedes", highway: "SP-294", kilometer: 670.8, municipality: "Santa Mercedes", lat: -21.3613234, lng: -51.7129677, passengerManual: 6.8, commercialManualByAxle: commercial([13.6, 20.4, 27.2, 34, 40.8, 47.6, 54.4, 61.2]) },
  { id: "P21", name: "Cabrália Paulista", highway: "SP-293", kilometer: 2, municipality: "Cabrália Paulista", lat: -22.4953675, lng: -49.3126802, passengerManual: 3.9, commercialManualByAxle: commercial([7.7, 11.6, 15.5, 19.3, 23.2, 27.1, 30.9, 34.8]) },
].map((plaza) => ({ ...plaza, operator: "EIXO SP" as const, automaticDiscountRate: EIXO_AUTOMATIC_DISCOUNT_RATE }));

function commercialByAxle(baseTariff: number): Record<number, number> {
  return commercial(Array.from({ length: 8 }, (_, index) => Number((baseTariff * (index + 2)).toFixed(2))));
}

/** Praças não-EIXO identificadas nas geometrias das rotas atuais. */
export const OTHER_TOLL_PLAZAS: readonly TollPlaza[] = [
  { id: "VR-AVAI", name: "Avaí", highway: "SP-300", kilometer: 367.7, municipality: "Avaí", lat: -22.16743, lng: -49.23975, passengerManual: 8.5, commercialManualByAxle: commercialByAxle(8.5), operator: "ViaRondon", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
  { id: "VR-PIRAJUI", name: "Pirajuí", highway: "SP-300", kilometer: 400.8, municipality: "Pirajuí", lat: -21.95899, lng: -49.4663, passengerManual: 8, commercialManualByAxle: commercialByAxle(8), operator: "ViaRondon", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
  { id: "VR-PROMISSAO", name: "Promissão", highway: "SP-300", kilometer: 455.7, municipality: "Promissão", lat: -21.62194, lng: -49.8529, passengerManual: 9.6, commercialManualByAxle: commercialByAxle(9.6), operator: "ViaRondon", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
  { id: "TIE-AREIOPOLIS", name: "Areiópolis", highway: "SP-300", kilometer: 285, municipality: "Areiópolis", lat: -22.67581, lng: -48.68876, passengerManual: 9, commercialManualByAxle: commercialByAxle(9), operator: "Rodovias do Tietê", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
  { id: "TIE-AGUDOS", name: "Agudos", highway: "SP-300", kilometer: 314, municipality: "Agudos", lat: -22.51336, lng: -48.9046, passengerManual: 8.8, commercialManualByAxle: commercialByAxle(8.8), operator: "Rodovias do Tietê", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
  { id: "TBR-LINS", name: "Lins", highway: "BR-153", kilometer: 183.8, municipality: "Lins", lat: -21.71103, lng: -49.8084, passengerManual: 10.1, commercialManualByAxle: commercialByAxle(10.1), operator: "Triunfo Transbrasiliana", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
  { id: "TBR-VERA-CRUZ", name: "Vera Cruz", highway: "BR-153", kilometer: 268.1, municipality: "Vera Cruz", lat: -22.3363, lng: -49.89896, passengerManual: 10.1, commercialManualByAxle: commercialByAxle(10.1), operator: "Triunfo Transbrasiliana", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
  { id: "CART-PIRATININGA", name: "Piratininga", highway: "SP-225", kilometer: 251.9, municipality: "Piratininga", lat: -22.45084, lng: -49.1706, passengerManual: 10.7, commercialManualByAxle: commercialByAxle(10.7), operator: "CART", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
  { id: "CART-SANTA-CRUZ", name: "Santa Cruz do Rio Pardo", highway: "SP-225", kilometer: 300.9, municipality: "Santa Cruz do Rio Pardo", lat: -22.74894, lng: -49.4784, passengerManual: 10.4, commercialManualByAxle: commercialByAxle(10.4), operator: "CART", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
  { id: "ENT-MARILIA", name: "Marília", highway: "SP-333", kilometer: 315.1, municipality: "Marília", lat: -22.0908, lng: -49.9095, passengerManual: 13.6, commercialManualByAxle: commercialByAxle(13.6), operator: "Entrevias", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
  { id: "ENT-ECHAPORA", name: "Echaporã", highway: "SP-333", kilometer: 354.7, municipality: "Echaporã", lat: -22.3308, lng: -50.1199, passengerManual: 11.8, commercialManualByAxle: commercialByAxle(11.8), operator: "Entrevias", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
  { id: "VP-JAU", name: "Jaú", highway: "SP-255", kilometer: 165.6, municipality: "Jaú", lat: -22.4083, lng: -48.5642, passengerManual: 7, commercialManualByAxle: commercialByAxle(7), operator: "Arteris ViaPaulista", automaticDiscountRate: DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE },
];

export const ALL_TOLL_PLAZAS: readonly TollPlaza[] = [...EIXO_TOLL_PLAZAS, ...OTHER_TOLL_PLAZAS];

export function automaticTollTariff(plaza: TollPlaza, manualTariff: number): number {
  const discountedCents = manualTariff * (1 - (plaza.automaticDiscountRate ?? DEFAULT_TOLL_AUTOMATIC_DISCOUNT_RATE)) * 100;
  return Math.floor(discountedCents + CURRENCY_ROUNDING_EPSILON) / 100;
}

export function eixoAutomaticTariff(manualTariff: number): number {
  const discountedCents = manualTariff * (1 - EIXO_AUTOMATIC_DISCOUNT_RATE) * 100;
  return Math.floor(discountedCents + CURRENCY_ROUNDING_EPSILON) / 100;
}
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

export const GRESPAN_HEADQUARTERS = {
  name: "Grespan Matriz",
  city: "Marília-SP",
  address: "Avenida República, 7000 - Distrito Industrial Santo Barion - Marília/SP - CEP 17512-035",
  lat: -22.21389,
  lng: -49.94583,
} as const;

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
