import { estimateFuelCost, estimateRouteFuelConsumption } from "./route-fuel-consumption";

export type RouteCostReportGrouping = "route" | "vehicle";

export type RouteCostReportInput = {
  routeId: string;
  routeName: string;
  weekday: string;
  vehicleType: string;
  distanceMeters: number | null;
  tollCost: number;
  tollPassages: number;
};

export type RouteCostReportRow = {
  key: string;
  label: string;
  vehicleType: string;
  routeCount: number;
  distanceMeters: number;
  minimumFuelCost: number | null;
  maximumFuelCost: number | null;
  tollCost: number;
  tollPassages: number;
  minimumTotalCost: number | null;
  maximumTotalCost: number | null;
  unavailableRoutes: number;
};

export type RouteCostReport = {
  rows: RouteCostReportRow[];
  routeCount: number;
  totalDistanceMeters: number;
  minimumFuelCost: number;
  maximumFuelCost: number;
  tollCost: number;
  tollPassages: number;
  minimumTotalCost: number;
  maximumTotalCost: number;
  unavailableRoutes: number;
};

const EMPTY_TOTAL = 0;

function nonNegative(value: number | null): number {
  return value !== null && Number.isFinite(value) && value >= 0 ? value : EMPTY_TOTAL;
}

export function buildRouteCostReport(
  inputs: RouteCostReportInput[],
  dieselPricePerLiter: number | null,
  grouping: RouteCostReportGrouping,
): RouteCostReport {
  const groups = new Map<string, RouteCostReportRow>();

  for (const input of inputs) {
    const key = grouping === "vehicle" ? input.vehicleType : input.routeId;
    const label = grouping === "vehicle" ? input.vehicleType : input.routeName;
    const current = groups.get(key) ?? {
      key,
      label,
      vehicleType: input.vehicleType,
      routeCount: 0,
      distanceMeters: EMPTY_TOTAL,
      minimumFuelCost: 0,
      maximumFuelCost: 0,
      tollCost: 0,
      tollPassages: 0,
      minimumTotalCost: 0,
      maximumTotalCost: 0,
      unavailableRoutes: 0,
    };
    const fuel = input.distanceMeters === null
      ? null
      : estimateFuelCost(estimateRouteFuelConsumption(input.vehicleType, input.distanceMeters), dieselPricePerLiter);
    const routeAvailable = input.distanceMeters !== null && fuel !== null;

    current.routeCount += 1;
    current.distanceMeters += nonNegative(input.distanceMeters);
    current.tollCost += nonNegative(input.tollCost);
    current.tollPassages += nonNegative(input.tollPassages);
    if (!routeAvailable) {
      current.unavailableRoutes += 1;
    } else {
      current.minimumFuelCost = (current.minimumFuelCost ?? EMPTY_TOTAL) + fuel.minimumFuelCost;
      current.maximumFuelCost = (current.maximumFuelCost ?? EMPTY_TOTAL) + fuel.maximumFuelCost;
      current.minimumTotalCost = (current.minimumTotalCost ?? EMPTY_TOTAL) + fuel.minimumFuelCost + nonNegative(input.tollCost);
      current.maximumTotalCost = (current.maximumTotalCost ?? EMPTY_TOTAL) + fuel.maximumFuelCost + nonNegative(input.tollCost);
    }
    groups.set(key, current);
  }

  const rows = [...groups.values()].map((row) => row.unavailableRoutes === row.routeCount
    ? { ...row, minimumFuelCost: null, maximumFuelCost: null, minimumTotalCost: null, maximumTotalCost: null }
    : row);

  return {
    rows,
    routeCount: inputs.length,
    totalDistanceMeters: inputs.reduce((total, input) => total + nonNegative(input.distanceMeters), EMPTY_TOTAL),
    minimumFuelCost: rows.reduce((total, row) => total + (row.minimumFuelCost ?? EMPTY_TOTAL), EMPTY_TOTAL),
    maximumFuelCost: rows.reduce((total, row) => total + (row.maximumFuelCost ?? EMPTY_TOTAL), EMPTY_TOTAL),
    tollCost: rows.reduce((total, row) => total + row.tollCost, EMPTY_TOTAL),
    tollPassages: rows.reduce((total, row) => total + row.tollPassages, EMPTY_TOTAL),
    minimumTotalCost: rows.reduce((total, row) => total + (row.minimumTotalCost ?? EMPTY_TOTAL), EMPTY_TOTAL),
    maximumTotalCost: rows.reduce((total, row) => total + (row.maximumTotalCost ?? EMPTY_TOTAL), EMPTY_TOTAL),
    unavailableRoutes: rows.reduce((total, row) => total + row.unavailableRoutes, EMPTY_TOTAL),
  };
}
