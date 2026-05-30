import { describe, expect, it } from "vitest";
import { buildSparklinePoints, resolveTrendDirection } from "./kpi-card.utils";

describe("kpi-card helpers", () => {
  it("resolveTrendDirection usa direção explícita quando enviada", () => {
    expect(resolveTrendDirection(12.4, "down")).toBe("down");
  });

  it("resolveTrendDirection infere por variação percentual", () => {
    expect(resolveTrendDirection(2.5)).toBe("up");
    expect(resolveTrendDirection(-0.1)).toBe("down");
    expect(resolveTrendDirection(0)).toBe("stable");
    expect(resolveTrendDirection(null)).toBe("stable");
  });

  it("buildSparklinePoints gera sequência válida", () => {
    const points = buildSparklinePoints([10, 20, 15]);
    expect(points.split(" ").length).toBe(3);
    expect(points).toContain(",");
  });
});
