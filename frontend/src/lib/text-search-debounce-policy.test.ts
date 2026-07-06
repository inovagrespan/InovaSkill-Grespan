import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import {
  COMPLEX_TEXT_SEARCH_DEBOUNCE_MS,
  TEXT_SEARCH_DEBOUNCE_MS,
} from "./use-debounced-value";

function readSource(file: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), file), "utf8");
}

describe("política de debounce para buscas textuais", () => {
  it("define intervalos compartilhados para buscas comuns e complexas", () => {
    expect(TEXT_SEARCH_DEBOUNCE_MS).toBe(400);
    expect(COMPLEX_TEXT_SEARCH_DEBOUNCE_MS).toBeGreaterThan(TEXT_SEARCH_DEBOUNCE_MS);
  });

  it("usa o padrão compartilhado nas buscas textuais reativas", () => {
    for (const file of [
      "src/routes/clientes.tsx",
      "src/routes/rotas.tsx",
      "src/routes/logistica.rotas.tsx",
      "src/routes/financas.tsx",
      "src/routes/vendas.tsx",
      "src/components/ui/customer-map.tsx",
    ]) {
      const source = readSource(file);
      expect(source, file).toContain("useDebouncedValue");
      expect(source, file).toContain("TEXT_SEARCH_DEBOUNCE_MS");
    }
  });

  it("não mantém timeouts textuais isolados nas telas de rotas", () => {
    expect(readSource("src/routes/rotas.tsx")).not.toContain("setTimeout(");
    expect(readSource("src/routes/logistica.rotas.tsx")).not.toContain("setTimeout(");
  });
});
