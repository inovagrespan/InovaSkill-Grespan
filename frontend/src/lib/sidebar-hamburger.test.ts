import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("controle do menu lateral", () => {
  it("usa uma seta encaixada na borda que indica menu aberto ou fechado", () => {
    const source = fs.readFileSync(
      path.resolve(process.cwd(), "src/components/AppSidebar.tsx"),
      "utf8",
    );

    expect(source).toContain("function SidebarToggleArrow");
    expect(source).toContain("const Icon = open ? ChevronLeft : ChevronRight");
    expect(source).toContain('data-sidebar-toggle-arrow={open ? "open" : "closed"}');
    expect(source).toContain("absolute right-0 top-10 z-50");
    expect(source).toContain("translate-x-1/2");
    expect(source).toContain("title={collapsed ? \"Expandir menu\" : \"Recolher menu\"}");
    expect(source).toContain("transition-transform duration-300 ease-out");
    expect(source).toContain("motion-reduce:transition-none");
    expect(source).not.toContain("data-menu-line");
  });
});
