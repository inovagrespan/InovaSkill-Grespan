import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readSource(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("landing route", () => {
  it("cria landing pública alinhada ao sistema visual e com CTA para login", () => {
    const source = readSource("src/routes/index.tsx");
    const dashboard = readSource("src/routes/dashboard.tsx");
    const root = readSource("src/routes/__root.tsx");
    const login = readSource("src/routes/login.tsx");

    expect(source).toContain('createFileRoute("/")');
    expect(source).toContain("Conecta360");
    expect(source).toContain("Acessar o sistema");
    expect(source).toContain('to="/login"');
    expect(source).toContain("Uma visão clara para decisões que movem a operação.");
    expect(source).toContain("DashboardPreview");
    expect(source).toContain("bg-background");
    expect(source).toContain("border-border");
    expect(source).not.toContain("conecta360-hero.png");
    expect(root).toContain('const PUBLIC_ROUTES = new Set(["/", "/login"])');
    expect(login).toContain('to="/"');
    expect(login).toContain('search.redirect || "/dashboard"');
    expect(dashboard).toContain('createFileRoute("/dashboard")');
    expect(login).toContain("Conhecer o Conecta360");
  });
});
