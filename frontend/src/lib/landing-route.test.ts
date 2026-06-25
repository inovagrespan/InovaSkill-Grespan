import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readSource(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("landing route", () => {
  it("cria landing publica do Conecta360 com hero visual e CTA para login", () => {
    const source = readSource("src/routes/landing.tsx");
    const root = readSource("src/routes/__root.tsx");
    const login = readSource("src/routes/login.tsx");

    expect(source).toContain('createFileRoute("/landing")');
    expect(source).toContain("/assets/conecta360-hero.png");
    expect(source).toContain("Conecta360");
    expect(source).toContain("Acessar o sistema");
    expect(source).toContain('to="/login"');
    expect(source).toContain("Da reunião reativa para uma operação");
    expect(root).toContain('const PUBLIC_ROUTES = new Set(["/login", "/landing"])');
    expect(login).toContain('to="/landing"');
    expect(login).toContain("Conhecer o Conecta360");
  });
});
