import { describe, expect, it } from "vitest";
import { formatSalesTimelineLabel, resolveSalesTimelineGranularity } from "./sales-timeline";

describe("sales timeline", () => {
  it("usa granularidade diária para períodos curtos guiados pelo seletor superior", () => {
    expect(resolveSalesTimelineGranularity({
      periodPreset: "month",
      dateFrom: "2026-06-01",
      dateTo: "2026-06-30",
    })).toBe("daily");
    expect(resolveSalesTimelineGranularity({
      periodPreset: "week",
      dateFrom: "2026-06-01",
      dateTo: "2026-06-07",
    })).toBe("daily");
  });

  it("usa granularidade semanal para trimestre e personalizada intermediária", () => {
    expect(resolveSalesTimelineGranularity({
      periodPreset: "quarter",
      dateFrom: "2026-04-01",
      dateTo: "2026-06-30",
    })).toBe("weekly");
    expect(resolveSalesTimelineGranularity({
      periodPreset: "custom",
      dateFrom: "2026-01-01",
      dateTo: "2026-03-15",
    })).toBe("weekly");
  });

  it("usa granularidade mensal para ano e períodos personalizados longos", () => {
    expect(resolveSalesTimelineGranularity({
      periodPreset: "year",
      dateFrom: "2026-01-01",
      dateTo: "2026-12-31",
    })).toBe("monthly");
    expect(resolveSalesTimelineGranularity({
      periodPreset: "custom",
      dateFrom: "2026-01-01",
      dateTo: "2026-12-31",
    })).toBe("monthly");
  });

  it("mantém leitura dos rótulos coerente com a granularidade", () => {
    expect(formatSalesTimelineLabel("2026-06-08", "daily")).toBe("08/06");
    expect(formatSalesTimelineLabel("2026-06-08", "weekly")).toBe("Sem 08/06");
    expect(formatSalesTimelineLabel("2026-06-01", "monthly")).toBe("jun/26");
  });

  it("cai para mensal quando o intervalo personalizado é inválido", () => {
    expect(resolveSalesTimelineGranularity({
      periodPreset: "custom",
      dateFrom: "2026-06-10",
      dateTo: "2026-06-01",
    })).toBe("monthly");
  });
});
