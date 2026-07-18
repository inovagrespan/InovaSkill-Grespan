import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("role based dashboard", () => {
  it("entrega a torre comercial para vendas e a visão logística aos demais perfis autorizados", () => {
    const source = fs.readFileSync(
      path.resolve(process.cwd(), "src/routes/dashboard.tsx"),
      "utf8",
    );

    expect(source).toContain('getCurrentUserRole() === "vendas"');
    expect(source).toContain("return <SalesControlTower />");
    expect(source).toContain("return <LogisticsDashboardMetrics />");
  });

  it("mantém tipos de veículo em modo consulta para o diretor", () => {
    const source = fs.readFileSync(
      path.resolve(process.cwd(), "src/routes/veiculos.tipos.tsx"),
      "utf8",
    );

    expect(source).toContain('currentRole === "logistica"');
    expect(source).toContain('currentRole === "admin"');
    expect(source).toContain('currentRole === "admin_system"');
    expect(source).toContain("{canManage && (");
  });
});
