export type FuelConsumptionEstimate = {
  minimumLiters: number;
  maximumLiters: number;
  minimumEfficiencyKmPerLiter: number;
  maximumEfficiencyKmPerLiter: number;
};

type VehicleFuelPolicy = {
  matches: string[];
  minimumEfficiencyKmPerLiter: number;
  maximumEfficiencyKmPerLiter: number;
};

const METERS_PER_KILOMETER = 1_000;
export const ROUTE_DEPARTURE_HOUR = 8;
export const ROUTE_PREFERRED_RETURN_HOUR = 18;
export const ROUTE_LATEST_RETURN_HOUR = 19;

const VEHICLE_FUEL_POLICIES: VehicleFuelPolicy[] = [
  { matches: ["MERCEDES ACCELO", "ACCELO", "MERCEDES ACELO", "ACELO"], minimumEfficiencyKmPerLiter: 5.5, maximumEfficiencyKmPerLiter: 7 },
  { matches: ["TOCO 4X2", "TOCO"], minimumEfficiencyKmPerLiter: 3.8, maximumEfficiencyKmPerLiter: 4.5 },
  { matches: ["TRUCK 6X2", "TRUCK"], minimumEfficiencyKmPerLiter: 3.2, maximumEfficiencyKmPerLiter: 4 },
];

function normalizeVehicleType(value: string): string {
  return value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toUpperCase().replace(/[^A-Z0-9]+/g, " ").trim();
}

export function estimateRouteFuelConsumption(
  vehicleType: string,
  distanceMeters: number,
): FuelConsumptionEstimate | null {
  if (!Number.isFinite(distanceMeters) || distanceMeters < 0) return null;
  const normalizedType = normalizeVehicleType(vehicleType);
  const policy = VEHICLE_FUEL_POLICIES.find(item => item.matches.some(match => normalizedType.includes(match)));
  if (!policy) return null;

  const distanceKilometers = distanceMeters / METERS_PER_KILOMETER;
  return {
    minimumLiters: distanceKilometers / policy.maximumEfficiencyKmPerLiter,
    maximumLiters: distanceKilometers / policy.minimumEfficiencyKmPerLiter,
    minimumEfficiencyKmPerLiter: policy.minimumEfficiencyKmPerLiter,
    maximumEfficiencyKmPerLiter: policy.maximumEfficiencyKmPerLiter,
  };
}

export function totalRouteFuelConsumption(
  routes: Array<{ vehicleType: string; distanceMeters: number }>,
): { minimumLiters: number; maximumLiters: number; unavailableRoutes: number } {
  return routes.reduce((total, route) => {
    const estimate = estimateRouteFuelConsumption(route.vehicleType, route.distanceMeters);
    if (!estimate) return { ...total, unavailableRoutes: total.unavailableRoutes + 1 };
    return {
      minimumLiters: total.minimumLiters + estimate.minimumLiters,
      maximumLiters: total.maximumLiters + estimate.maximumLiters,
      unavailableRoutes: total.unavailableRoutes,
    };
  }, { minimumLiters: 0, maximumLiters: 0, unavailableRoutes: 0 });
}

export function formatFuelConsumptionRange(estimate: FuelConsumptionEstimate): string {
  const format = (value: number) => value.toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  return `${format(estimate.minimumLiters)} a ${format(estimate.maximumLiters)} L`;
}

export function formatRouteDuration(durationSeconds: number): string {
  if (!Number.isFinite(durationSeconds) || durationSeconds < 0) return "Indisponível";
  const totalMinutes = Math.round(durationSeconds / 60);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (hours === 0) return `${minutes} min`;
  if (minutes === 0) return `${hours} h`;
  return `${hours} h ${minutes} min`;
}

export function formatRouteReturnTime(durationSeconds: number): string {
  if (!Number.isFinite(durationSeconds) || durationSeconds < 0) return "Indisponível";
  const departureMinutes = ROUTE_DEPARTURE_HOUR * 60;
  const returnMinutes = departureMinutes + Math.round(durationSeconds / 60);
  const hours = Math.floor(returnMinutes / 60);
  const minutes = returnMinutes % 60;
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
}

export function getRouteReturnWindowStatus(durationSeconds: number): "preferred" | "tolerance" | "late" | "unavailable" {
  if (!Number.isFinite(durationSeconds) || durationSeconds < 0) return "unavailable";
  const returnHour = ROUTE_DEPARTURE_HOUR + durationSeconds / 3_600;
  if (returnHour <= ROUTE_PREFERRED_RETURN_HOUR) return "preferred";
  if (returnHour <= ROUTE_LATEST_RETURN_HOUR) return "tolerance";
  return "late";
}

export function estimateFuelCost(
  fuel: FuelConsumptionEstimate | null,
  dieselPricePerLiter: number | null,
): { minimumFuelCost: number; maximumFuelCost: number } | null {
  if (!fuel || dieselPricePerLiter === null || !Number.isFinite(dieselPricePerLiter) || dieselPricePerLiter < 0)
    return null;
  return {
    minimumFuelCost: fuel.minimumLiters * dieselPricePerLiter,
    maximumFuelCost: fuel.maximumLiters * dieselPricePerLiter,
  };
}
