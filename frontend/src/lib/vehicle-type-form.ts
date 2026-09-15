import type { VehicleTypeInput } from "./importer-api";

export type VehicleTypeFormValues = {
  name: string;
  capacityKg: string;
  axleCount: string;
  minimumFuelEfficiencyKmPerLiter: string;
  maximumFuelEfficiencyKmPerLiter: string;
};

export type VehicleTypeFormValidation =
  | { success: true; input: VehicleTypeInput }
  | { success: false; message: string };

function optionalDecimal(value: string): number | null {
  return value.trim() === "" ? null : Number(value.replace(",", "."));
}

export function validateVehicleTypeForm(values: VehicleTypeFormValues, requireCostConfiguration = false): VehicleTypeFormValidation {
  const name = values.name.trim();
  if (!name) return { success: false, message: "O nome do tipo de veículo é obrigatório." };

  const capacityKg = Number(values.capacityKg.replace(",", "."));
  if (!Number.isFinite(capacityKg) || capacityKg < 0) {
    return { success: false, message: "Capacidade deve ser um número maior ou igual a zero." };
  }

  const axleCount = values.axleCount.trim() === "" ? null : Number(values.axleCount);
  if (axleCount !== null && (!Number.isInteger(axleCount) || axleCount < 2 || axleCount > 9)) {
    return { success: false, message: "A quantidade de eixos deve ser um número inteiro entre 2 e 9." };
  }

  const minimumFuelEfficiencyKmPerLiter = optionalDecimal(values.minimumFuelEfficiencyKmPerLiter);
  const maximumFuelEfficiencyKmPerLiter = optionalDecimal(values.maximumFuelEfficiencyKmPerLiter);
  const hasAnyCostConfiguration = axleCount !== null || minimumFuelEfficiencyKmPerLiter !== null || maximumFuelEfficiencyKmPerLiter !== null;
  const hasCompleteCostConfiguration = axleCount !== null && minimumFuelEfficiencyKmPerLiter !== null && maximumFuelEfficiencyKmPerLiter !== null;
  if ((requireCostConfiguration || hasAnyCostConfiguration) && !hasCompleteCostConfiguration) {
    return { success: false, message: "Informe eixos e a faixa completa de consumo do tipo de veículo." };
  }
  if (minimumFuelEfficiencyKmPerLiter !== null && (!Number.isFinite(minimumFuelEfficiencyKmPerLiter) || minimumFuelEfficiencyKmPerLiter <= 0)) {
    return { success: false, message: "O consumo mínimo deve ser maior que zero." };
  }
  if (maximumFuelEfficiencyKmPerLiter !== null && (!Number.isFinite(maximumFuelEfficiencyKmPerLiter) || maximumFuelEfficiencyKmPerLiter <= 0)) {
    return { success: false, message: "O consumo máximo deve ser maior que zero." };
  }
  if (minimumFuelEfficiencyKmPerLiter !== null && maximumFuelEfficiencyKmPerLiter !== null
    && minimumFuelEfficiencyKmPerLiter > maximumFuelEfficiencyKmPerLiter) {
    return { success: false, message: "O consumo mínimo não pode ser maior que o consumo máximo." };
  }

  return {
    success: true,
    input: {
      name,
      capacityKg,
      axleCount,
      minimumFuelEfficiencyKmPerLiter,
      maximumFuelEfficiencyKmPerLiter,
    },
  };
}
