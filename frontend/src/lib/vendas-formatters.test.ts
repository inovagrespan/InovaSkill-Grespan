import { describe, expect, it } from "vitest";
import { formatKpiCompactCurrency, formatKpiCompactNumber } from "./vendas-formatters";

describe("vendas KPI formatter", () => {
  it("abrevia milhares e milhoes", () => {
    expect(formatKpiCompactNumber(20_000)).toBe("20 mil");
    expect(formatKpiCompactNumber(19_000_000)).toBe("19 mi");
  });

  it("mantem numero base para valores pequenos", () => {
    expect(formatKpiCompactNumber(987)).toBe("987");
  });

  it("formata moeda compacta", () => {
    expect(formatKpiCompactCurrency(19_500_000)).toBe("R$ 19,5 mi");
  });
});
