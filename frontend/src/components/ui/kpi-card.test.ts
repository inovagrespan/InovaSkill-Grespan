import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { buildSparklinePoints, resolveKpiValueSizeClass, resolveTrendDirection } from "./kpi-card.utils";

describe("kpi-card helpers", () => {
  it("resolveTrendDirection usa direcao explicita quando enviada", () => {
    expect(resolveTrendDirection(12.4, "down")).toBe("down");
  });

  it("resolveTrendDirection infere por variacao percentual", () => {
    expect(resolveTrendDirection(2.5)).toBe("up");
    expect(resolveTrendDirection(-0.1)).toBe("down");
    expect(resolveTrendDirection(0)).toBe("stable");
    expect(resolveTrendDirection(null)).toBe("stable");
  });

  it("buildSparklinePoints gera sequencia valida", () => {
    const points = buildSparklinePoints([10, 20, 15]);
    expect(points.split(" ").length).toBe(3);
    expect(points).toContain(",");
  });

  it("reduz a fonte proporcionalmente sem ultrapassar o mínimo legível", () => {
    expect(resolveKpiValueSizeClass("3,5 mil")).toBe("text-3xl");
    expect(resolveKpiValueSizeClass("R$ 38,7 M/mês")).toBe("text-2xl");
    expect(resolveKpiValueSizeClass("R$ 123.456.789,99/mês")).toBe("text-lg");
  });
});

describe("kpi-card component markup", () => {
  it("nao renderiza sparkline inferior no card", () => {
    const component = fs.readFileSync(path.resolve(process.cwd(), "src/components/ui/kpi-card.tsx"), "utf8");

    expect(component).not.toContain('viewBox="0 0 100 26"');
    expect(component).not.toContain("h-7 w-full");
  });

  it("mantem valor sem quebra de linha e com tooltip", () => {
    const component = fs.readFileSync(path.resolve(process.cwd(), "src/components/ui/kpi-card.tsx"), "utf8");

    expect(component).toContain("text-ellipsis");
    expect(component).toContain("whitespace-nowrap");
    expect(component).toContain("allowWrapValue");
    expect(component).toContain("valueClassName");
    expect(component).toContain("whitespace-normal break-words");
    expect(component).toContain("title={valueTooltip ?? value}");
  });

  it("oferece tons semânticos para crescimento, queda e informação", () => {
    const component = fs.readFileSync(path.resolve(process.cwd(), "src/components/ui/kpi-card.tsx"), "utf8");

    expect(component).toContain('tone?: "neutral" | "success" | "danger" | "info"');
    expect(component).toContain('success: "text-[var(--success)]"');
    expect(component).toContain('danger: "text-[var(--error)]"');
    expect(component).toContain('info: "text-[var(--info)]"');
    expect(component).toContain("periodLabelClassName");
  });
});
