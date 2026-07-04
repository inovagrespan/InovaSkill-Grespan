import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getAuthToken, login } from "./auth";

function createToken(exp: number, role = "diretor"): string {
  const payload = btoa(JSON.stringify({ sub: "1", exp, role })).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  return `header.${payload}.signature`;
}

describe("director login token", () => {
  const localStorageMap = new Map<string, string>();
  const sessionStorageMap = new Map<string, string>();

  beforeEach(() => {
    localStorageMap.clear();
    sessionStorageMap.clear();
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("window", {
      fetch: fetchMock,
      localStorage: {
        getItem: (key: string) => localStorageMap.get(key) ?? null,
        setItem: (key: string, value: string) => localStorageMap.set(key, value),
        removeItem: (key: string) => localStorageMap.delete(key),
      },
      sessionStorage: {
        getItem: (key: string) => sessionStorageMap.get(key) ?? null,
        setItem: (key: string, value: string) => sessionStorageMap.set(key, value),
        removeItem: (key: string) => sessionStorageMap.delete(key),
      },
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("usa token real da API para diretor quando backend esta disponivel", async () => {
    const apiToken = createToken(Math.floor(Date.now() / 1000) + 60);
    vi.mocked(window.fetch).mockResolvedValueOnce(new Response(JSON.stringify({ token: apiToken }), { status: 200 }));

    const token = await login({ userOrEmail: "diretor", password: "diretor" });

    expect(token).toBe(apiToken);
    expect(getAuthToken()).toBe(apiToken);
    expect(window.fetch).toHaveBeenCalledWith(
      "http://localhost:5279/login",
      expect.objectContaining({ method: "POST" }),
    );
  });
});
