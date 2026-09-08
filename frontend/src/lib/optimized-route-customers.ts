import type { DailyOptimizationStop, RouteRoadPath } from "./importer-api";

export type OptimizedMunicipalityCustomers = {
  municipalityId: string;
  municipalityName: string;
  customers: Array<{ id: string; label: string }>;
};

export function groupOptimizedRouteCustomers(
  routeStops: DailyOptimizationStop[],
  roadPath: RouteRoadPath | null,
): OptimizedMunicipalityCustomers[] {
  const municipalities = new Map<string, string>();
  for (const stop of routeStops) {
    if (!municipalities.has(stop.municipalityId)) {
      municipalities.set(stop.municipalityId, stop.municipalityName);
    }
  }

  return [...municipalities].map(([municipalityId, municipalityName]) => ({
    municipalityId,
    municipalityName,
    customers: (roadPath?.stops ?? [])
      .filter(stop => !stop.isDepot && stop.municipalityId === municipalityId)
      .map(stop => ({ id: stop.id, label: stop.label })),
  }));
}
