import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readLoginRoute(): string {
  return fs.readFileSync(path.resolve(process.cwd(), "src/routes/login.tsx"), "utf8");
}

describe("login route", () => {
  it("orienta o login local com a senha seedada do administrador", () => {
    const source = readLoginRoute();

    expect(source).toContain('placeholder="admin ou admin@local.test"');
    expect(source).toContain('placeholder="admin"');
    expect(source).not.toContain('placeholder="admin123*#"');
  });
});
