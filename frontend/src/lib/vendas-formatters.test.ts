import { describe, expect, it } from "vitest";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "./vendas-formatters";

describe("vendas KPI formatter", () => {
  it("abrevia milhares com mil e milhões com M", () => {
    expect(formatKpiCompactNumber(1_000)).toBe("1 mil");
    expect(formatKpiCompactNumber(20_000)).toBe("20 mil");
    expect(formatKpiCompactNumber(1_000_000)).toBe("1 M");
    expect(formatKpiCompactNumber(19_000_000)).toBe("19 M");
  });

  it("mantém números abaixo de mil sem abreviação", () => {
    expect(formatKpiCompactNumber(999.99)).toBe("999,99");
    expect(formatKpiCompactNumber(987)).toBe("987");
  });

  it("preserva sinal e arredonda a parte compactada para uma casa decimal", () => {
    expect(formatKpiCompactNumber(-1_250)).toBe("-1,3 mil");
    expect(formatKpiCompactNumber(-2_750_000)).toBe("-2,8 M");
  });

  it("mantém bilhões distintos de milhões", () => {
    expect(formatKpiCompactNumber(1_500_000_000)).toBe("1,5 bi");
  });

  it("trata entradas numéricas inválidas como zero", () => {
    expect(formatKpiCompactNumber(Number.NaN)).toBe("0");
    expect(formatKpiCompactNumber(Number.POSITIVE_INFINITY)).toBe("0");
  });

  it("formata moeda compacta com o mesmo padrão", () => {
    expect(formatKpiCompactCurrency(20_000)).toBe("R$ 20 mil");
    expect(formatKpiCompactCurrency(19_500_000)).toBe("R$ 19,5 M");
  });
});
