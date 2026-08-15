import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("cadastro de clientes", () => {
  const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");
  const detail = fs.readFileSync(path.resolve(process.cwd(), "src/components/CustomerConsumptionDialog.tsx"), "utf8");
  const api = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");

  it("consulta a listagem atual com busca paginada no backend", () => {
    expect(route).toContain('createFileRoute("/clientes")');
    expect(route).toContain("useDebouncedValue");
    expect(route).toContain("nome, documento ou cidade");
    expect(api).toContain("/api/customers?");
    expect(api).toContain('params.set("search"');
  });

  it("representa loading, vazio, erro e paginação", () => {
    expect(route).toContain("SkeletonTable");
    expect(route).toContain("Nenhum cliente encontrado");
    expect(route).toContain('variant="destructive"');
    expect(route).toContain("Página {page} de {totalPages}");
  });

  it("mantém tabela e paginação dentro da largura disponível", () => {
    expect(route).toContain("min-w-0 max-w-full overflow-x-hidden");
    expect(route).toContain('className="min-w-[1240px] table-fixed"');
    expect(route).toContain('<TableHead className="w-24">Código</TableHead>');
    expect(route).toContain("flex flex-col gap-3");
    expect(route).toContain("truncate");
  });

  it("permite iniciar o enriquecimento sem apresentar o endereço cadastral na listagem", () => {
    expect(route).toContain("CUSTOMER_REGISTRATION_ADDRESS_ENRICHMENT");
    expect(route).toContain("canCurrentUserAccessProcessingArea");
    expect(route).toContain("Enriquecer endereços");
    expect(route).toContain(">UF</TableHead>");
    expect(route).toContain(">Município</TableHead>");
    expect(route).not.toContain("Endereço cadastral");
    expect(route).not.toContain("RegistrationAddressCell");
    expect(api).toContain("registrationAddress:");
  });

  it("apresenta o endereço cadastral completo no detalhe do cliente", () => {
    expect(detail).toContain("Endereço cadastral");
    expect(detail).toContain("Não consultado");
    expect(detail).toContain("registrationAddress.street");
    expect(detail).toContain("registrationAddress.number");
    expect(detail).toContain("registrationAddress.neighborhood");
    expect(detail).toContain("registrationAddress.complement");
    expect(detail).toContain("registrationAddress.postalCode");
  });
});
