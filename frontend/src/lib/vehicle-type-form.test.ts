import { describe, expect, it } from "vitest";
import { validateVehicleTypeForm, type VehicleTypeFormValues } from "./vehicle-type-form";

const validValues: VehicleTypeFormValues = {
  name: " Truck ",
  capacityKg: "10300",
  axleCount: "3",
  minimumFuelEfficiencyKmPerLiter: "3,2",
  maximumFuelEfficiencyKmPerLiter: "4,0",
};

describe("cadastro operacional de tipo de veículo", () => {
  it("normaliza os decimais em PT-BR e monta o contrato da API", () => {
    expect(validateVehicleTypeForm(validValues)).toEqual({
      success: true,
      input: {
        name: "Truck",
        capacityKg: 10_300,
        axleCount: 3,
        minimumFuelEfficiencyKmPerLiter: 3.2,
        maximumFuelEfficiencyKmPerLiter: 4,
      },
    });
  });

  it.each(["1", "10", "2.5"])("rejeita quantidade de eixos fora do domínio: %s", (axleCount) => {
    const result = validateVehicleTypeForm({ ...validValues, axleCount });
    expect(result).toEqual({ success: false, message: "A quantidade de eixos deve ser um número inteiro entre 2 e 9." });
  });

  it("permite tipo pendente de configuração de custo", () => {
    const result = validateVehicleTypeForm({
      ...validValues,
      axleCount: "",
      minimumFuelEfficiencyKmPerLiter: "",
      maximumFuelEfficiencyKmPerLiter: "",
    });
    expect(result).toMatchObject({ success: true, input: { axleCount: null, minimumFuelEfficiencyKmPerLiter: null, maximumFuelEfficiencyKmPerLiter: null } });
  });

  it("exige a configuração completa ao criar um novo tipo", () => {
    const result = validateVehicleTypeForm({
      ...validValues,
      axleCount: "",
      minimumFuelEfficiencyKmPerLiter: "",
      maximumFuelEfficiencyKmPerLiter: "",
    }, true);
    expect(result).toEqual({ success: false, message: "Informe eixos e a faixa completa de consumo do tipo de veículo." });
  });

  it("rejeita faixa parcial, não positiva ou invertida", () => {
    expect(validateVehicleTypeForm({ ...validValues, maximumFuelEfficiencyKmPerLiter: "" })).toMatchObject({ success: false });
    expect(validateVehicleTypeForm({ ...validValues, minimumFuelEfficiencyKmPerLiter: "", maximumFuelEfficiencyKmPerLiter: "" })).toMatchObject({ success: false });
    expect(validateVehicleTypeForm({ ...validValues, axleCount: "" })).toMatchObject({ success: false });
    expect(validateVehicleTypeForm({ ...validValues, minimumFuelEfficiencyKmPerLiter: "0" })).toMatchObject({ success: false });
    expect(validateVehicleTypeForm({ ...validValues, minimumFuelEfficiencyKmPerLiter: "5" })).toEqual({
      success: false,
      message: "O consumo mínimo não pode ser maior que o consumo máximo.",
    });
  });
});
