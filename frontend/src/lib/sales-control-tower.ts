export type SalesControlPeriodDays = 1 | 7 | 30 | 90;
export type SalesKpiId = "sales-volume" | "bonus-volume" | "active-consumption" | "product-consumption" | "conversion" | "conversion-time" | "seasonality" | "retention" | "mape" | "average-price" | "average-ticket" | "ltv";
export type SalesMetricStatus = "Normal" | "Atenção" | "Crítico";
export type SalesHistoryPoint = { label: string; value: number };
export type SalesRootCause = "sales_growth" | "bonus_efficiency" | "active_consumption_growth" | "product_growth" | "product_drop" | "conversion_gap" | "slow_pipeline" | "seasonality" | "retention_risk" | "forecast_error" | "price_gap" | "ticket_growth" | "ltv_growth";

export type SalesKpiSnapshot = {
  revenue: number;
  weightKg: number;
  bonusAmount: number;
  bonusRatePercent: number;
  activeCustomerConsumptionChangePercent: number;
  productConsumptionChangePercent: number;
  conversionRatePercent: number;
  conversionDays: number;
  seasonalityIndex: number;
  retentionRatePercent: number;
  mapePercent: number;
  averagePricePerKg: number;
  averageTicket: number;
  lifetimeValue: number;
  prospects: number;
  closedDeals: number;
};

const REFERENCE_DATE = "2026-06-24";
const MILLISECONDS_PER_DAY = 86_400_000;

function round(value: number, places = 1): number {
  const factor = 10 ** places;
  return Math.round((value + Number.EPSILON) * factor) / factor;
}

function percent(numerator: number, denominator: number): number {
  return denominator > 0 ? round((numerator / denominator) * 100) : 0;
}

export function calculateSalesControlSnapshot(periodDays: SalesControlPeriodDays): SalesKpiSnapshot {
  const periodScale = periodDays / 30;
  const revenue = 1_284_600 * periodScale;
  const weightKg = 184_320 * periodScale;
  const bonusAmount = 42_600 * periodScale;
  const prospects = Math.max(1, Math.round(86 * periodScale));
  const closedDeals = Math.round(prospects * (24 / 86));
  const actual = [102, 118, 96, 131];
  const forecast = [106, 112, 101, 126];
  const mapePercent = round(actual.reduce((total, value, index) => total + Math.abs((value - forecast[index]) / value), 0) / actual.length * 100);

  return {
    revenue: round(revenue, 2),
    weightKg: round(weightKg, 1),
    bonusAmount: round(bonusAmount, 2),
    bonusRatePercent: percent(bonusAmount, revenue),
    activeCustomerConsumptionChangePercent: percent(735_000 - 676_200, 676_200),
    productConsumptionChangePercent: percent(210_400 - 187_200, 187_200),
    conversionRatePercent: percent(closedDeals, prospects),
    conversionDays: 18.4,
    seasonalityIndex: 1.18,
    retentionRatePercent: percent(229, 250),
    mapePercent,
    averagePricePerKg: round(921_030 / 49_200, 2),
    averageTicket: round(revenue / Math.max(1, Math.round(264 * periodScale)), 2),
    lifetimeValue: round((1_284_600 / 264) * 2.2 * 12, 2),
    prospects,
    closedDeals,
  };
}

const HISTORY_VALUES: Record<SalesKpiId, number[]> = {
  "sales-volume": [945_000, 1_012_000, 1_086_000, 1_041_000, 1_158_000, 1_221_000, 1_284_600],
  "bonus-volume": [31_800, 35_400, 39_200, 36_700, 41_900, 44_100, 42_600],
  "active-consumption": [2.1, 3.8, 4.6, 3.9, 5.7, 7.2, 8.7],
  "product-consumption": [4.2, 6.1, 5.4, 7.8, 9.2, 10.6, 12.4],
  conversion: [19.8, 21.4, 22.1, 23.6, 24.8, 26.1, 27.9],
  "conversion-time": [26.2, 24.8, 23.5, 22.9, 21.1, 19.7, 18.4],
  seasonality: [0.91, 0.96, 1.04, 1.01, 1.09, 1.14, 1.18],
  retention: [87.2, 88.4, 89.1, 89.8, 90.5, 91.0, 91.6],
  mape: [13.8, 12.6, 11.4, 10.9, 9.6, 8.7, 7.8],
  "average-price": [17.42, 17.68, 17.91, 18.08, 18.31, 18.55, 18.72],
  "average-ticket": [4_120, 4_260, 4_390, 4_510, 4_630, 4_740, 4_866],
  ltv: [108_400, 112_700, 116_900, 119_800, 122_600, 125_300, 128_436],
};

function timelineLabels(periodDays: SalesControlPeriodDays): string[] {
  if (periodDays === 1) return ["06h", "09h", "12h", "15h", "18h", "21h", "24h"];
  const referenceTime = Date.parse(`${REFERENCE_DATE}T00:00:00Z`);
  const startTime = referenceTime - (periodDays - 1) * MILLISECONDS_PER_DAY;
  return Array.from({ length: 7 }, (_, index) => {
    const offset = Math.round((periodDays - 1) * (index / 6));
    const [, month, day] = new Date(startTime + offset * MILLISECONDS_PER_DAY).toISOString().slice(0, 10).split("-");
    return `${day}/${month}`;
  });
}

export function buildSalesKpiHistory(kpi: SalesKpiId, periodDays: SalesControlPeriodDays): SalesHistoryPoint[] {
  const labels = timelineLabels(periodDays);
  const scale = ["sales-volume", "bonus-volume"].includes(kpi) ? periodDays / 30 : 1;
  return HISTORY_VALUES[kpi].map((value, index) => ({ label: labels[index], value: round(value * scale, 2) }));
}

export function buildContextualSalesRecommendation(cause: SalesRootCause, subject: string): string {
  if (cause === "sales_growth") return `Reservar capacidade de estoque e ampliar a cobertura comercial de ${subject}, priorizando pães congelados de maior margem e oportunidades qualificadas de locação de fornos.`;
  if (cause === "bonus_efficiency") return `Condicionar novas bonificações de ${subject} a uma meta mensurável de volume, conversão ou margem incremental e revisar o retorno da ação em 30 dias.`;
  if (cause === "active_consumption_growth") return `Replicar em clientes semelhantes a combinação de mix, frequência de reposição e equipamento usado por ${subject}, preservando disponibilidade e margem.`;
  if (cause === "product_growth") return `Garantir estoque e ampliar a exposição comercial de ${subject} nas regiões com maior aderência, sem reduzir preço onde a demanda já cresce.`;
  if (cause === "product_drop") return `Revisar preço, disponibilidade e argumentação de venda de ${subject}, comparando a queda por vendedor e região.`;
  if (cause === "conversion_gap") return `Priorizar a oportunidade de ${subject}, registrar o bloqueio do fechamento e definir responsável e próximo passo com prazo comercial.`;
  if (cause === "slow_pipeline") return `Reduzir o tempo de resposta de ${subject}, definindo prazo máximo para proposta, teste de produto e aprovação comercial.`;
  if (cause === "seasonality") return `Antecipar campanha e estoque de ${subject} para a próxima janela sazonal identificada no consumo.`;
  if (cause === "retention_risk") return `Acionar o vendedor responsável por ${subject} com oferta de recomposição de mix e revisão da frequência de entrega.`;
  if (cause === "forecast_error") return `Recalibrar a previsão de ${subject} usando os últimos ciclos de venda e separar demanda de pães congelados da demanda de equipamentos alugados.`;
  if (cause === "price_gap") return `Revisar a política de preço de ${subject}, preservando margem e justificando diferenças por frete, região e volume.`;
  if (cause === "ticket_growth") return `Elevar o ticket de ${subject} com venda combinada de itens complementares, consumíveis de maior margem e locação do forno adequado ao volume.`;
  return `Proteger e ampliar o LTV de ${subject} com calendário de recompra, renovação da locação, manutenção preventiva do equipamento e expansão gradual do mix.`;
}
