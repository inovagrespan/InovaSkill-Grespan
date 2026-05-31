import type {
  CustomerComparisonItem,
  CustomerDetailSummary,
  CustomerInsightsResponse,
  CustomerTopProductItem,
} from "./importer-api";

export type CommercialIntelligenceTone = "success" | "warning" | "danger" | "neutral";

export type CommercialIntelligenceCard = {
  status: string;
  summary: string;
  detail: string;
  tone: CommercialIntelligenceTone;
  metricValue?: number;
  metricSuffix?: string;
};

export type ProductRelevanceInsight = {
  name: string;
  code: string;
  sharePercent: number;
  revenue: number;
  quantity: number;
  summary: string;
};

export type CustomerCommercialIntelligence = {
  health: CommercialIntelligenceCard;
  trend: CommercialIntelligenceCard;
  potential: CommercialIntelligenceCard;
  recommendation: CommercialIntelligenceCard;
  stability: CommercialIntelligenceCard;
  productsSummary: string;
  productsTone: CommercialIntelligenceTone;
  relevantProducts: ProductRelevanceInsight[];
};

type BuildCustomerCommercialIntelligenceInput = {
  details: CustomerDetailSummary | null;
  insights: CustomerInsightsResponse | null;
  comparison: CustomerComparisonItem[];
  topProducts: CustomerTopProductItem[];
};

function formatPercent(value: number | null): string {
  if (value == null || Number.isNaN(value)) return "sem base";
  return `${value > 0 ? "+" : ""}${value.toFixed(1)}%`;
}

function normalizeText(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();
}

function resolveMonthlyComparison(
  comparison: CustomerComparisonItem[],
): CustomerComparisonItem | null {
  return (
    comparison.find((item) => normalizeText(item.label).includes("mes")) ?? comparison[0] ?? null
  );
}

function resolveTrendFromComparison(
  monthlyComparison: CustomerComparisonItem | null,
): "Crescimento" | "Estabilidade" | "Queda" | null {
  const variation = monthlyComparison?.variationPercent;
  if (variation == null || Number.isNaN(variation)) return null;
  if (variation > 5) return "Crescimento";
  if (variation < -5) return "Queda";
  return "Estabilidade";
}

function buildHealthCard(
  details: CustomerDetailSummary | null,
  insights: CustomerInsightsResponse | null,
): CommercialIntelligenceCard {
  const riskLevel = insights?.riskLevel;

  if (riskLevel === "Crítico") {
    return {
      status: "Crítico",
      summary: "Cliente exige ação imediata de retenção.",
      detail: `${insights.daysWithoutPurchase} dias sem comprar; investigar preço, ruptura, atendimento ou troca de fornecedor.`,
      tone: "danger",
    };
  }

  if (riskLevel === "Em risco") {
    return {
      status: "Em risco",
      summary: "Ciclo de recompra já passou do padrão histórico.",
      detail: `${insights.daysWithoutPurchase} dias sem comprar; priorizar contato comercial antes de perder recorrência.`,
      tone: "danger",
    };
  }

  if (riskLevel === "Atenção" || details?.status === "Em queda") {
    return {
      status: "Atenção",
      summary: "Cliente ainda ativo, mas com sinais que pedem acompanhamento.",
      detail:
        insights?.riskReason ??
        "Monitorar próxima compra e validar se a queda é pontual ou recorrente.",
      tone: "warning",
    };
  }

  if (
    riskLevel === "Sem risco" ||
    details?.status === "Em crescimento" ||
    details?.status === "Ativo"
  ) {
    return {
      status: "Saudável",
      summary: "Cliente dentro do comportamento esperado de compra.",
      detail: "Manter relacionamento ativo e buscar expansão de mix sem abordagem emergencial.",
      tone: "success",
    };
  }

  if (details?.status === "Inativo") {
    return {
      status: "Em risco",
      summary: "Cliente inativo no período selecionado.",
      detail: "Reativar relacionamento e entender barreiras antes de projetar novo potencial.",
      tone: "danger",
    };
  }

  return {
    status: "Atenção",
    summary: "Histórico insuficiente para classificar saúde com segurança.",
    detail:
      insights?.riskReason ?? "Acompanhar novas compras antes de definir risco ou oportunidade.",
    tone: "neutral",
  };
}

function buildTrendCard(
  insights: CustomerInsightsResponse | null,
  monthlyComparison: CustomerComparisonItem | null,
): CommercialIntelligenceCard {
  const trend =
    insights?.consumptionTrend === "Crescimento" ||
    insights?.consumptionTrend === "Queda" ||
    insights?.consumptionTrend === "Estabilidade"
      ? insights.consumptionTrend
      : resolveTrendFromComparison(monthlyComparison);

  const variationDetail =
    monthlyComparison?.variationPercent == null
      ? "Sem comparação mensal suficiente para confirmar a direção."
      : `Mês atual versus anterior: ${formatPercent(monthlyComparison.variationPercent)}.`;

  if (trend === "Crescimento") {
    return {
      status: "Crescimento",
      summary: "Consumo aumentando; há espaço para ampliar volume ou mix.",
      detail: variationDetail,
      tone: "success",
    };
  }

  if (trend === "Queda") {
    return {
      status: "Queda",
      summary: "Consumo recuando; vale investigar perda de demanda ou share.",
      detail: variationDetail,
      tone: "danger",
    };
  }

  if (trend === "Estabilidade") {
    return {
      status: "Estabilidade",
      summary: "Compra em ritmo previsível, sem aceleração relevante.",
      detail: variationDetail,
      tone: "neutral",
    };
  }

  return {
    status: "Estabilidade",
    summary: "Ainda não há sinal forte de crescimento ou queda.",
    detail: variationDetail,
    tone: "neutral",
  };
}

function buildPotentialCard(
  details: CustomerDetailSummary | null,
  insights: CustomerInsightsResponse | null,
): CommercialIntelligenceCard {
  const expectedRevenue = insights?.predictedRevenue ?? details?.averageRevenueMonthly ?? null;

  if (expectedRevenue != null && expectedRevenue > 0) {
    return {
      status: "Potencial mapeado",
      summary: "Receita esperada para o próximo ciclo com base no histórico do cliente.",
      detail:
        insights?.predictedRevenue == null
          ? "Usando média mensal do período filtrado como referência conservadora."
          : `Histórico mensal disponível: ${insights.monthlyHistoryPeriods} períodos.`,
      tone: "success",
      metricValue: expectedRevenue,
    };
  }

  return {
    status: "Sem base suficiente",
    summary: "Ainda não há histórico confiável para estimar receita esperada.",
    detail:
      insights?.revenuePredictionReason ?? "Aguardar mais compras ou ampliar o período analisado.",
    tone: "neutral",
  };
}

function buildStabilityCard(
  monthlyComparison: CustomerComparisonItem | null,
): CommercialIntelligenceCard {
  const variation = monthlyComparison?.variationPercent;

  if (variation == null || Number.isNaN(variation)) {
    return {
      status: "Sem base mensal",
      summary: "Não há mês anterior comparável para medir estabilidade.",
      detail: "Use um período maior para avaliar variação entre meses.",
      tone: "neutral",
    };
  }

  const absoluteVariation = Math.abs(variation);
  if (absoluteVariation <= 10) {
    return {
      status: "Estável",
      summary: "Consumo com baixa oscilação entre meses.",
      detail: `Variação mensal de ${formatPercent(variation)}; boa previsibilidade para planejamento.`,
      tone: "success",
      metricValue: variation,
      metricSuffix: "%",
    };
  }

  if (absoluteVariation <= 25) {
    return {
      status: "Oscilação moderada",
      summary: "Existe variação, mas ainda dentro de um intervalo administrável.",
      detail: `Variação mensal de ${formatPercent(variation)}; acompanhar antes de assumir perda ou ganho permanente.`,
      tone: "warning",
      metricValue: variation,
      metricSuffix: "%",
    };
  }

  return {
    status: "Instável",
    summary: "Consumo variou muito entre meses.",
    detail: `Variação mensal de ${formatPercent(variation)}; validar se houve compra pontual, ruptura ou migração.`,
    tone: "danger",
    metricValue: variation,
    metricSuffix: "%",
  };
}

function buildProductInsights(topProducts: CustomerTopProductItem[]): {
  productsSummary: string;
  productsTone: CommercialIntelligenceTone;
  relevantProducts: ProductRelevanceInsight[];
} {
  const relevantProducts = topProducts.slice(0, 3).map((product) => ({
    name: product.productDescription || product.productCode,
    code: product.productCode,
    sharePercent: product.sharePercent,
    revenue: product.revenue,
    quantity: product.quantity,
    summary: `${product.sharePercent.toFixed(1)}% do faturamento do cliente no período.`,
  }));

  if (relevantProducts.length === 0) {
    return {
      productsSummary: "Sem produtos suficientes para identificar sustentação do cliente.",
      productsTone: "neutral",
      relevantProducts,
    };
  }

  const topProduct = relevantProducts[0];
  if (topProduct.sharePercent >= 50) {
    return {
      productsSummary: `${topProduct.name} concentra a relação comercial; proteger disponibilidade e margem.`,
      productsTone: "warning",
      relevantProducts,
    };
  }

  const combinedShare = relevantProducts.reduce((sum, product) => sum + product.sharePercent, 0);
  return {
    productsSummary: `Top ${relevantProducts.length} produtos somam ${combinedShare.toFixed(1)}% e mostram o mix que sustenta o cliente.`,
    productsTone: "success",
    relevantProducts,
  };
}

function buildRecommendation(
  health: CommercialIntelligenceCard,
  trend: CommercialIntelligenceCard,
  stability: CommercialIntelligenceCard,
  products: ProductRelevanceInsight[],
): CommercialIntelligenceCard {
  const mainProduct = products[0]?.name;
  const productContext = mainProduct ? ` Começar por ${mainProduct}.` : "";

  if (health.status === "Crítico" || health.status === "Em risco") {
    return {
      status: "Ação de retenção",
      summary: `Acionar o cliente para recuperar recorrência e remover barreiras de compra.${productContext}`,
      detail:
        "Prioridade comercial alta: entender motivo da pausa e propor recomposição de pedido.",
      tone: "danger",
    };
  }

  if (trend.status === "Queda") {
    return {
      status: "Recuperar consumo",
      summary: `Investigar queda e oferecer condição ou mix para recompor volume.${productContext}`,
      detail: "Comparar compras recentes com o padrão anterior antes de tratar como sazonalidade.",
      tone: "warning",
    };
  }

  if (health.status === "Saudável" && trend.status === "Crescimento") {
    return {
      status: "Expandir relacionamento",
      summary: `Cliente saudável e em crescimento; vale proposta de volume, contrato ou itens complementares.${productContext}`,
      detail: "Boa oportunidade para aumentar participação sem abordagem de emergência.",
      tone: "success",
    };
  }

  if (stability.status === "Instável") {
    return {
      status: "Qualificar demanda",
      summary:
        "Antes de vender mais, entender se a oscilação vem de compra pontual ou abastecimento irregular.",
      detail: "Ação recomendada: alinhar calendário de compra e evitar projeção otimista demais.",
      tone: "warning",
    };
  }

  return {
    status: "Manter e desenvolver",
    summary: `Cliente sem alerta forte; manter contato consultivo e buscar oportunidades de mix.${productContext}`,
    detail: "Acompanhar próximo ciclo para decidir entre expansão ou retenção.",
    tone: "neutral",
  };
}

export function buildCustomerCommercialIntelligence(
  input: BuildCustomerCommercialIntelligenceInput,
): CustomerCommercialIntelligence {
  const monthlyComparison = resolveMonthlyComparison(input.comparison);
  const health = buildHealthCard(input.details, input.insights);
  const trend = buildTrendCard(input.insights, monthlyComparison);
  const potential = buildPotentialCard(input.details, input.insights);
  const stability = buildStabilityCard(monthlyComparison);
  const products = buildProductInsights(input.topProducts);
  const recommendation = buildRecommendation(health, trend, stability, products.relevantProducts);

  return {
    health,
    trend,
    potential,
    recommendation,
    stability,
    productsSummary: products.productsSummary,
    productsTone: products.productsTone,
    relevantProducts: products.relevantProducts,
  };
}
