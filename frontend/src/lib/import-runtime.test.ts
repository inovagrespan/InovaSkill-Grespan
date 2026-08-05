import { describe, expect, it } from "vitest";
import {
  IMPORT_STATUS_POLL_INTERVAL_MS,
  isImportActive,
  resolveImportProgressPercent,
} from "./import-runtime";

describe("acompanhamento de importações", () => {
  it("consulta execuções ativas a cada dez segundos", () => {
    expect(IMPORT_STATUS_POLL_INTERVAL_MS).toBe(10_000);
    expect(isImportActive("Queued")).toBe(true);
    expect(isImportActive("Processing")).toBe(true);
    expect(isImportActive("Completed")).toBe(false);
    expect(isImportActive("Failed")).toBe(false);
  });

  it("calcula e limita o progresso entre zero e cem", () => {
    expect(resolveImportProgressPercent(200, 50)).toBe(25);
    expect(resolveImportProgressPercent(100, 120)).toBe(100);
    expect(resolveImportProgressPercent(100, -1)).toBe(0);
    expect(resolveImportProgressPercent(0, 0)).toBeNull();
  });
});
