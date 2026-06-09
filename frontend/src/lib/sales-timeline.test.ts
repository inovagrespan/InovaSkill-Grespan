import { describe, expect, it } from "vitest";
import { formatSalesTimelineLabel, resolveSalesTimelineGranularity } from "./sales-timeline";

describe("sales timeline", () => {
  it("usa granularidade automatica conforme o periodo guiado pelo seletor superior", () => {
    expect(resolveSalesTimelineGranularity({
      periodPreset: "today",
      dateFrom: "2026-06-09",
      dateTo: "2026-06-09",
    })).toBe("hour");
    expect(resolveSalesTimelineGranularity({
      periodPreset: "month",
      dateFrom: "2026-06-01",
      dateTo: "2026-06-30",
    })).toBe("day");
    expect(resolveSalesTimelineGranularity({
      periodPreset: "week",
      dateFrom: "2026-06-01",
      dateTo: "2026-06-07",
    })).toBe("day");
  });

  it("usa granularidade semanal para trimestre e personalizada intermediaria", () => {
    expect(resolveSalesTimelineGranularity({
      periodPreset: "quarter",
      dateFrom: "2026-04-01",
      dateTo: "2026-06-30",
    })).toBe("week");
    expect(resolveSalesTimelineGranularity({
      periodPreset: "custom",
      dateFrom: "2026-01-01",
      dateTo: "2026-03-15",
    })).toBe("week");
  });

  it("usa granularidade mensal para ano e trimestral para intervalos personalizados muito longos", () => {
    expect(resolveSalesTimelineGranularity({
      periodPreset: "year",
      dateFrom: "2026-01-01",
      dateTo: "2026-12-31",
    })).toBe("month");
    expect(resolveSalesTimelineGranularity({
      periodPreset: "custom",
      dateFrom: "2026-01-01",
      dateTo: "2026-12-31",
    })).toBe("month");
    expect(resolveSalesTimelineGranularity({
      periodPreset: "custom",
      dateFrom: "2024-01-01",
      dateTo: "2026-12-31",
    })).toBe("quarter");
  });

  it("mantem leitura dos rotulos coerente com a granularidade", () => {
    expect(formatSalesTimelineLabel("2026-06-08T08:00:00Z", "hour")).toBe("08h");
    expect(formatSalesTimelineLabel("2026-04-13", "day")).toBe("13 de abr");
    expect(formatSalesTimelineLabel("2026-04-06", "week")).toBe("06 de abr - 12 de abr");
    expect(formatSalesTimelineLabel("2026-01-01", "month")).toBe("Jan");
    expect(formatSalesTimelineLabel("2026-04-01", "quarter")).toBe("T2 2026");
  });

  it("cai para mensal quando o intervalo personalizado e invalido", () => {
    expect(resolveSalesTimelineGranularity({
      periodPreset: "custom",
      dateFrom: "2026-06-10",
      dateTo: "2026-06-01",
    })).toBe("month");
  });
});
