import { afterEach, describe, expect, it, vi } from "vitest";

describe("api url", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  it("evita duplicar /api nos endpoints da API quando o gateway ja termina com /api", async () => {
    vi.stubEnv("VITE_API_URL", "/api");
    const { getApiServiceBaseUrl } = await import("./api-url");

    expect(getApiServiceBaseUrl()).toBe("");
  });

  it("mantem o gateway com /api para login e cadastro", async () => {
    vi.stubEnv("VITE_API_URL", "/api");
    const { buildGatewayUrl } = await import("./api-url");

    expect(buildGatewayUrl("login")).toBe("/api/login");
  });

  it("mantem os hubs fora do /api duplicado em producao", async () => {
    vi.stubEnv("VITE_API_URL", "/api");
    const { buildServiceUrl } = await import("./api-url");

    expect(buildServiceUrl("hubs/file-jobs")).toBe("/hubs/file-jobs");
    expect(buildServiceUrl("api/products?page=1")).toBe("/api/products?page=1");
  });
});
