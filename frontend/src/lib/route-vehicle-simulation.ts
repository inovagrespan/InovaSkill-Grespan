export type RouteVehicleSimulation = {
  occupancy: number | null;
  occupancyChange: number | null;
  capacityChangeKg: number | null;
};

export function simulateRouteVehicle(
  totalWeightKg: number,
  currentOccupancy: number | null,
  currentCapacityKg: number | null,
  simulatedCapacityKg: number | null,
): RouteVehicleSimulation {
  if (
    !Number.isFinite(totalWeightKg) ||
    totalWeightKg < 0 ||
    simulatedCapacityKg === null ||
    !Number.isFinite(simulatedCapacityKg) ||
    simulatedCapacityKg <= 0
  ) {
    return {
      occupancy: null,
      occupancyChange: null,
      capacityChangeKg: null,
    };
  }

  const occupancy = totalWeightKg / simulatedCapacityKg;
  const hasCurrentOccupancy = currentOccupancy !== null && Number.isFinite(currentOccupancy);
  const hasCurrentCapacity = currentCapacityKg !== null && Number.isFinite(currentCapacityKg);

  return {
    occupancy,
    occupancyChange: hasCurrentOccupancy ? occupancy - currentOccupancy : null,
    capacityChangeKg: hasCurrentCapacity ? simulatedCapacityKg - currentCapacityKg : null,
  };
}
