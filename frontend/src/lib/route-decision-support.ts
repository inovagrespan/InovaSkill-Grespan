import type { ImportedRouteDetail, VehicleTypeItem } from "@/lib/importer-api";

const TARGET_OCCUPANCY = 0.9;
const HEALTHY_MINIMUM_OCCUPANCY = 0.85;
const CRITICAL_MINIMUM_EXCLUSIVE = 0.95;
const MAXIMUM_OCCUPANCY = 1;
const MAXIMUM_RECOMMENDATIONS = 3;
const MAXIMUM_AI_QUESTION_CHARACTERS = 800;
const MAXIMUM_AI_ROUTE_NAME_CHARACTERS = 80;
const MAXIMUM_AI_VEHICLE_NAME_CHARACTERS = 50;

export type RouteVehicleRecommendation = {
  vehicleTypeId: string;
  vehicleName: string;
  capacityKg: number;
  occupancy: number;
  occupancyChange: number | null;
  capacityChangeKg: number | null;
  status: "Recomendado" | "Alternativa" | "Atenção";
  rationale: string;
  risk: string;
};

export type RouteDecisionSupport = {
  recommendation: RouteVehicleRecommendation | null;
  alternatives: RouteVehicleRecommendation[];
  summary: string;
};

function clampOccupancy(value: number): number {
  return Math.min(MAXIMUM_OCCUPANCY, Math.max(0, value));
}

function round(value: number): number {
  return Math.round((value + Number.EPSILON) * 10_000) / 10_000;
}

function describeRisk(occupancy: number): string {
  if (occupancy > CRITICAL_MINIMUM_EXCLUSIVE) return "Baixa margem para aumento de carga; exige acompanhamento antes da expedição.";
  if (occupancy >= HEALTHY_MINIMUM_OCCUPANCY) return "Faixa equilibrada, com bom aproveitamento e margem operacional.";
  return "Há capacidade ociosa; considere consolidar pedidos ou usar um veículo menor.";
}

export function buildRouteDecisionSupport(
  route: ImportedRouteDetail,
  vehicleTypes: VehicleTypeItem[],
): RouteDecisionSupport {
  if (!Number.isFinite(route.totalWeightKg) || route.totalWeightKg <= 0) {
    return {
      recommendation: null,
      alternatives: [],
      summary: "A carga da rota é inválida ou está zerada; revise a importação antes de comparar veículos.",
    };
  }

  const currentOccupancy = route.overallOccupancy;
  const candidates = vehicleTypes
    .filter((vehicle) => vehicle.capacityKg !== null && vehicle.capacityKg > 0)
    .filter((vehicle) => vehicle.capacityKg! >= route.totalWeightKg)
    .map((vehicle) => {
      const capacityKg = vehicle.capacityKg!;
      const occupancy = clampOccupancy(route.totalWeightKg / capacityKg);
      return {
        vehicle,
        capacityKg,
        occupancy,
        distanceFromTarget: Math.abs(occupancy - TARGET_OCCUPANCY),
      };
    })
    .sort((left, right) =>
      left.distanceFromTarget - right.distanceFromTarget
      || left.capacityKg - right.capacityKg
      || left.vehicle.name.localeCompare(right.vehicle.name),
    )
    .slice(0, MAXIMUM_RECOMMENDATIONS);

  const alternatives = candidates.map((candidate, index): RouteVehicleRecommendation => ({
    vehicleTypeId: candidate.vehicle.id,
    vehicleName: candidate.vehicle.name,
    capacityKg: candidate.capacityKg,
    occupancy: round(candidate.occupancy),
    occupancyChange: currentOccupancy === null ? null : round(candidate.occupancy - currentOccupancy),
    capacityChangeKg: route.vehicleCapacityKg === null ? null : candidate.capacityKg - route.vehicleCapacityKg,
    status: index === 0
      ? "Recomendado"
      : candidate.occupancy > CRITICAL_MINIMUM_EXCLUSIVE ? "Atenção" : "Alternativa",
    rationale: candidate.occupancy >= HEALTHY_MINIMUM_OCCUPANCY && candidate.occupancy <= CRITICAL_MINIMUM_EXCLUSIVE
      ? "A carga permanece na faixa saudável de ocupação."
      : candidate.occupancy > CRITICAL_MINIMUM_EXCLUSIVE
        ? "A carga cabe, mas fica próxima do limite operacional."
        : "A carga cabe com folga, porém mantém capacidade ociosa.",
    risk: describeRisk(candidate.occupancy),
  }));

  const recommendation = alternatives[0] ?? null;
  return {
    recommendation,
    alternatives,
    summary: recommendation
      ? `${recommendation.vehicleName} oferece o melhor equilíbrio entre ocupação e margem operacional para a carga atual.`
      : "Nenhum veículo cadastrado comporta a carga atual; divida a carga ou cadastre um veículo com maior capacidade.",
  };
}

export function buildRouteAiAnalysisPrompt(
  route: ImportedRouteDetail,
  support: RouteDecisionSupport,
): string {
  const compact = (value: string, maximumCharacters: number): string =>
    value.length <= maximumCharacters ? value : `${value.slice(0, maximumCharacters - 1)}…`;
  const alternatives = support.alternatives.map((item) =>
    `${compact(item.vehicleName, MAXIMUM_AI_VEHICLE_NAME_CHARACTERS)}: ${item.capacityKg} kg e ${(item.occupancy * 100).toFixed(1)}%`,
  ).join("; ");

  const prompt = [
    "Responda em PT-BR, com até 80 palavras e exatamente quatro linhas: Recomendação, Motivo, Risco e Próximo passo.",
    "Não invente custos, distâncias ou tempos e não aplique alterações.",
    `Rota: ${compact(route.name, MAXIMUM_AI_ROUTE_NAME_CHARACTERS)}; carga: ${route.totalWeightKg} kg; veículo atual: ${compact(route.vehicleType, MAXIMUM_AI_VEHICLE_NAME_CHARACTERS)}; cidades: ${route.entries.length}.`,
    `Cenários calculados: ${alternatives || "nenhum veículo compatível"}.`,
    "Compare benefício, risco e margem operacional sem introdução, conclusão, tabela ou repetição dos dados.",
  ].join(" ");

  return prompt.length <= MAXIMUM_AI_QUESTION_CHARACTERS
    ? prompt
    : `${prompt.slice(0, MAXIMUM_AI_QUESTION_CHARACTERS - 1)}…`;
}
