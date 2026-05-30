import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function read(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("design system tokens", () => {
  it("define os tokens semânticos principais no styles.css", () => {
    const css = read("src/styles.css");

    expect(css).toContain("--primary-red:");
    expect(css).toContain("--primary-red-hover:");
    expect(css).toContain("--primary-red-active:");
    expect(css).toContain("--success:");
    expect(css).toContain("--warning:");
    expect(css).toContain("--error:");
    expect(css).toContain("--info:");
    expect(css).toContain("--text-primary:");
    expect(css).toContain("--text-secondary:");
    expect(css).toContain("--text-muted:");
  });
});

describe("component states", () => {
  it("mantém estado default de botão com hover e active", () => {
    const button = read("src/components/ui/button.tsx");

    expect(button).toContain("bg-primary");
    expect(button).toContain("primary-red-hover");
    expect(button).toContain("primary-red-active");
  });

  it("mantém variante outline de badge com borda neutra", () => {
    const badge = read("src/components/ui/badge.tsx");

    expect(badge).toContain('outline: "border-border bg-surface text-foreground"');
  });
});
