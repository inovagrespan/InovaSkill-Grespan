import type { RouteCostItem } from "./importer-api";

export type RouteCostGrouping = "route" | "vehicle";

export type RouteCostViewRow = RouteCostItem & { routeCount: number; unavailableItemCount: number };

function sumAvailable(items: RouteCostItem[], field: keyof RouteCostItem): number {
  return items.filter((item) => item.isAvailable).reduce((total, item) => total + Number(item[field] ?? 0), 0);
}

function sumNullable(items: RouteCostItem[], field: keyof RouteCostItem): number | null {
  return items.some((item) => item.isAvailable) ? sumAvailable(items, field) : null;
}

export function groupRouteCostItems(items: RouteCostItem[], grouping: RouteCostGrouping): RouteCostViewRow[] {
  if (grouping === "route") return items.map((item) => ({ ...item, routeCount: 1, unavailableItemCount: item.isAvailable ? 0 : 1 }));

  const groups = new Map<string, RouteCostItem[]>();
  for (const item of items) {
    const key = item.vehicleType?.trim() || "Tipo não informado";
    groups.set(key, [...(groups.get(key) ?? []), item]);
  }

  return [...groups.entries()].map(([vehicleType, groupedItems]) => {
    const first = groupedItems[0];
    const availableCount = groupedItems.filter((item) => item.isAvailable).length;
    return {
      ...first,
      id: `vehicle:${vehicleType}`,
      routeId: null,
      optimizationVehicleId: null,
      label: vehicleType,
      vehicleType,
      isAvailable: availableCount > 0,
      unavailableReason: availableCount > 0 ? null : "Nenhum custo disponível para este tipo de veículo.",
      distanceMeters: sumNullable(groupedItems, "distanceMeters"),
      durationSeconds: sumNullable(groupedItems, "durationSeconds"),
      minimumFuelLiters: sumNullable(groupedItems, "minimumFuelLiters"),
      maximumFuelLiters: sumNullable(groupedItems, "maximumFuelLiters"),
      minimumFuelCost: sumNullable(groupedItems, "minimumFuelCost"),
      maximumFuelCost: sumNullable(groupedItems, "maximumFuelCost"),
      tollCost: sumAvailable(groupedItems, "tollCost"),
      tollPassages: sumAvailable(groupedItems, "tollPassages"),
      minimumTotalCost: sumNullable(groupedItems, "minimumTotalCost"),
      maximumTotalCost: sumNullable(groupedItems, "maximumTotalCost"),
      tolls: groupedItems.flatMap((item) => item.tolls),
      routeCount: groupedItems.length,
      unavailableItemCount: groupedItems.length - availableCount,
    };
  }).sort((left, right) => left.label.localeCompare(right.label, "pt-BR"));
}
