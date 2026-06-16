import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

describe("nginx upload config", () => {
  it("mantem o limite de upload alinhado com os 500 MB aceitos pela API", () => {
    const nginxConfigPath = resolve(import.meta.dirname, "../../nginx.conf");
    const nginxConfig = readFileSync(nginxConfigPath, "utf-8");

    expect(nginxConfig).toContain("client_max_body_size 500m;");
  });
});
