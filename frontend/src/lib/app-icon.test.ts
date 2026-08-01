import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("app icon", () => {
  it("usa localmente o ícone do Conecta360", () => {
    const html = fs.readFileSync(path.resolve(process.cwd(), "index.html"), "utf8");
    const root = fs.readFileSync(path.resolve(process.cwd(), "src/routes/__root.tsx"), "utf8");
    const iconPath = path.resolve(process.cwd(), "public/assets/conecta360-icon.svg");

    expect(html).toContain('<link rel="icon" type="image/svg+xml" href="/assets/conecta360-icon.svg" />');
    expect(html).toContain('<link rel="apple-touch-icon" href="/assets/conecta360-icon.svg" />');
    expect(html).toContain("<title>Conecta360</title>");
    expect(root).toContain('document.title = "Conecta360"');
    expect(fs.existsSync(iconPath)).toBe(true);
    expect(fs.statSync(iconPath).size).toBeGreaterThan(0);
  });
});
