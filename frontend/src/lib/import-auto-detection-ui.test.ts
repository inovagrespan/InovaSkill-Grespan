import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { MAX_UPLOAD_SIZE_BYTES, MAX_UPLOAD_SIZE_MEGABYTES } from "./importer-api";

describe("upload com identificação automática", () => {
  const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/importacoes.files.tsx"), "utf8");
  const api = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");

  it("remove escolha manual e explica a identificação pelo cabeçalho", () => {
    expect(route).not.toContain("Fonte de dados");
    expect(route).not.toContain("<select");
    expect(route).toContain("Identificação automática");
    expect(route).toContain("planilhas operacionais pelo cabeçalho");
    expect(api).not.toContain('form.append("sourceCode"');
  });

  it("mantém o limite de 100 MB compartilhado na validação do frontend", () => {
    expect(MAX_UPLOAD_SIZE_MEGABYTES).toBe(100);
    expect(MAX_UPLOAD_SIZE_BYTES).toBe(100 * 1024 * 1024);
    expect(route).toContain("file.size > MAX_UPLOAD_SIZE_BYTES");
  });

  it("aceita CSV de coordenadas HERE além das planilhas XLSX", () => {
    expect(route).toContain('accept=".xlsx,.csv"');
    expect(route).toContain('/\\.(xlsx|csv)$/i');
    expect(route).toContain("CSV de coordenadas HERE");
  });
});
