import { describe, expect, it } from "vitest";
import { isJobExecutionActive, JOB_STATUS_POLL_INTERVAL_MS } from "./job-runtime";

describe("monitoramento compartilhado de jobs", () => {
  it("consulta jobs ativos a cada cinco segundos", () => {
    expect(JOB_STATUS_POLL_INTERVAL_MS).toBe(5_000);
    expect(["Queued", "Processing", "Retrying"].every(isJobExecutionActive)).toBe(true);
  });

  it("interrompe polling em estados terminais ou ausentes", () => {
    expect(["Completed", "Failed", "Cancelled", null, undefined].some(isJobExecutionActive)).toBe(
      false,
    );
  });
});
