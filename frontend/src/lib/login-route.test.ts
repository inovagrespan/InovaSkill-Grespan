import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readLoginRoute(): string {
  return fs.readFileSync(path.resolve(process.cwd(), "src/routes/login.tsx"), "utf8");
}

describe("login route", () => {
  it("preenche o login local com o administrador padrão rh", () => {
    const source = readLoginRoute();

    expect(source).toContain('const DEFAULT_LOGIN_USER = "rh"');
    expect(source).toContain('const DEFAULT_LOGIN_PASSWORD = "rh"');
    expect(source).toContain("useState(DEFAULT_LOGIN_USER)");
    expect(source).toContain("useState(DEFAULT_LOGIN_PASSWORD)");
    expect(source).toContain("placeholder={DEFAULT_LOGIN_USER}");
    expect(source).toContain("placeholder={DEFAULT_LOGIN_PASSWORD}");
    expect(source).not.toContain("admin123*#");
  });
});
