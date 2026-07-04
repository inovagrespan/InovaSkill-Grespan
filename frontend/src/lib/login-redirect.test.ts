import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readRootRoute(): string {
  return fs.readFileSync(path.resolve(process.cwd(), "src/routes/__root.tsx"), "utf8");
}

describe("login redirect", () => {
  it("deriva autenticacao do storage ao mudar de rota para evitar voltar ao login apos entrar", () => {
    const source = readRootRoute();

    expect(source).toContain("const authenticated = useMemo(() => isAuthenticated(), [pathname]);");
    expect(source).not.toContain("setAuthenticated(isAuthenticated())");
    expect(source).not.toContain("useState<boolean>(() => isAuthenticated())");
  });
});
