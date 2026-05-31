import { describe, expect, it } from "vitest";
import { buildFallbackStages, clampProgressPercent, stageStatusLabel } from "./importer-progress";

describe("importer progress helpers", () => {
  it("clamps progress percent to the stage range", () => {
    expect(clampProgressPercent(-12)).toBe(0);
    expect(clampProgressPercent(42.6)).toBe(43);
    expect(clampProgressPercent(180)).toBe(100);
    expect(clampProgressPercent(Number.NaN)).toBe(0);
  });

  it("marks only validation as running while validation is active", () => {
    const stages = buildFallbackStages({ status: "Validating", progressPercent: 55, errorCount: 0 });

    expect(stages).toEqual([
      expect.objectContaining({ code: "PRE_PROCESSING", status: "completed", progressPercent: 100 }),
      expect.objectContaining({ code: "VALIDATION", status: "running", progressPercent: 55 }),
      expect.objectContaining({ code: "IMPORT", status: "pending", progressPercent: 0 }),
    ]);
  });

  it("marks validation as failed when legacy jobs have validation errors", () => {
    const stages = buildFallbackStages({ status: "ValidationFailed", progressPercent: 100, errorCount: 3 });

    expect(stages.find((stage) => stage.code === "VALIDATION")).toEqual(
      expect.objectContaining({ status: "failed", progressPercent: 100, errorCount: 3 }),
    );
  });

  it("returns PT-BR labels for stage statuses", () => {
    expect(stageStatusLabel("pending")).toBe("Pendente");
    expect(stageStatusLabel("running")).toBe("Em andamento");
    expect(stageStatusLabel("completed")).toBe("Concluída");
    expect(stageStatusLabel("failed")).toBe("Com erro");
  });
});
