import type { RouteOptimizationDetail, RouteOptimizationSummary } from "@/lib/importer-api";

export type SuggestedVehicle = RouteOptimizationDetail["vehicles"][number];

export type FleetMovement = {
  releasedVehicleCount: number;
  releasedCapacityKg: number;
  introducedVehicleCount: number;
  introducedCapacityKg: number;
  netVehicleCount: number;
  netCapacityKg: number;
  releasedGroups: FleetVehicleGroup[];
  introducedGroups: FleetVehicleGroup[];
};

export type FleetVehicleGroup = {
  vehicleType: string;
  count: number;
};

export function suggestedRouteName(
  vehicle: SuggestedVehicle,
  vehicles: readonly SuggestedVehicle[],
): string {
  if (vehicle.sourceRouteName?.trim()) return vehicle.sourceRouteName.trim();
  if (vehicle.isAdditional) {
    const additionalSequence = vehicles.filter(
      (candidate) => candidate.isAdditional && candidate.sequence <= vehicle.sequence,
    ).length;
    return `Rota adicional ${additionalSequence}`;
  }
  return `Rota sugerida ${vehicle.sequence + 1}`;
}

export function dailyRouteComparison(summary: RouteOptimizationSummary) {
  return {
    distance: {
      current: summary.currentDistanceMeters,
      proposed: summary.proposedDistanceMeters,
      changePercent: calculatePercentageChange(
        summary.currentDistanceMeters,
        summary.proposedDistanceMeters,
      ),
    },
    duration: {
      current: summary.currentDurationSeconds,
      proposed: summary.proposedDurationSeconds,
      changePercent: calculatePercentageChange(
        summary.currentDurationSeconds,
        summary.proposedDurationSeconds,
      ),
    },
    vehicles: {
      current: summary.currentVehicleCount,
      proposed: summary.proposedVehicleCount,
      change: summary.proposedVehicleCount - summary.currentVehicleCount,
    },
    additionalVehicleCount: summary.additionalVehicleCount,
    additionalCapacityKg: summary.additionalCapacityKg,
    totalWeightKg: summary.totalWeightKg,
  };
}

export function calculatePercentageChange(current: number, proposed: number): number | null {
  if (current === 0) return proposed === 0 ? 0 : null;
  return ((proposed - current) / Math.abs(current)) * 100;
}

export function formatSignedPercentage(value: number | null): string {
  if (value === null || !Number.isFinite(value)) return "Sem base";
  const prefix = value > 0 ? "+" : "";
  return `${prefix}${value.toLocaleString("pt-BR", { maximumFractionDigits: 1 })}%`;
}

export function dailyFleetMovement(vehicles: readonly SuggestedVehicle[]): FleetMovement {
  const releasedVehicles = vehicles.filter(
    (vehicle) => !vehicle.isAdditional && vehicle.isIdle,
  );
  const introducedVehicles = vehicles.filter(
    (vehicle) => vehicle.isAdditional && !vehicle.isIdle,
  );
  const releasedCapacityKg = releasedVehicles.reduce(
    (total, vehicle) => total + vehicle.capacityKg,
    0,
  );
  const introducedCapacityKg = introducedVehicles.reduce(
    (total, vehicle) => total + vehicle.capacityKg,
    0,
  );

  return {
    releasedVehicleCount: releasedVehicles.length,
    releasedCapacityKg,
    introducedVehicleCount: introducedVehicles.length,
    introducedCapacityKg,
    netVehicleCount: introducedVehicles.length - releasedVehicles.length,
    netCapacityKg: introducedCapacityKg - releasedCapacityKg,
    releasedGroups: groupVehiclesByType(releasedVehicles),
    introducedGroups: groupVehiclesByType(introducedVehicles),
  };
}

function groupVehiclesByType(vehicles: readonly SuggestedVehicle[]): FleetVehicleGroup[] {
  const countByType = new Map<string, number>();
  for (const vehicle of vehicles) {
    const vehicleType = vehicle.vehicleType.trim() || "Veículo";
    countByType.set(vehicleType, (countByType.get(vehicleType) ?? 0) + 1);
  }
  return [...countByType.entries()]
    .map(([vehicleType, count]) => ({ vehicleType, count }))
    .sort((left, right) => left.vehicleType.localeCompare(right.vehicleType, "pt-BR"));
}
