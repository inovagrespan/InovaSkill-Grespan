import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readLoginRoute(): string {
  return fs.readFileSync(path.resolve(process.cwd(), "src/routes/login.tsx"), "utf8");
}

describe("login route", () => {
  it("preenche o login local com o administrador padrão rh", () => {
    const source = readLoginRoute();

    expect(source).toContain('const DEFAULT_LOGIN_USER = "diretor"');
    expect(source).toContain('const DEFAULT_LOGIN_PASSWORD = "diretor"');
    expect(source).toContain("useState(DEFAULT_LOGIN_USER)");
    expect(source).toContain("useState(DEFAULT_LOGIN_PASSWORD)");
    expect(source).toContain("placeholder={DEFAULT_LOGIN_USER}");
    expect(source).toContain("placeholder={DEFAULT_LOGIN_PASSWORD}");
    expect(source).not.toContain("admin123*#");
  });

  it("exibe Conecta360 como marca do sistema desde o login", () => {
    const source = readLoginRoute();

    expect(source).toContain('import { BrandLogo } from "@/components/BrandLogo";');
    expect(source).toContain("Acesso seguro");
    expect(source).not.toContain("Acesso seguro ao GRESPAN");
  });

  it("usa modo escuro como tema padrão quando não há preferência salva", () => {
    const source = readLoginRoute();

    expect(source).toContain('useState<"light" | "dark">("dark")');
    expect(source).toContain('setTheme("dark")');
    expect(source).not.toContain("prefers-color-scheme");
  });

  it("mantém superfícies e controles coerentes no tema escuro", () => {
    const source = readLoginRoute();

    expect(source).toContain("dark:placeholder:text-slate-400");
    expect(source).toContain("dark:border-[#d01825]/25");
    expect(source).toContain("dark:bg-[#d01825]/10");
  });

  it("informa as credenciais do acesso de demonstração", () => {
    const source = readLoginRoute();

    expect(source).toContain(">diretor</strong>");
    expect(source.split(">diretor</strong>").length - 1).toBe(2);
  });
});
