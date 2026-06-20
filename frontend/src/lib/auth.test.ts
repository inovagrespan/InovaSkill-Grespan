import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { authFetch, clearAuthToken, getAuthToken, getCurrentUserRole, isAuthenticated, isTokenValid, saveAuthToken } from "./auth";

function createToken(exp: number, role = "gestor"): string {
  const payload = btoa(JSON.stringify({ sub: "1", exp, role })).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  return `header.${payload}.signature`;
}

describe("auth", () => {
  const localStorageMap = new Map<string, string>();
  const sessionStorageMap = new Map<string, string>();
  const assignMock = vi.fn();

  beforeEach(() => {
    localStorageMap.clear();
    sessionStorageMap.clear();
    assignMock.mockClear();
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
      location: {
        pathname: "/clientes",
        search: "",
        assign: assignMock,
      },
    });
  });

  afterEach(() => {
    clearAuthToken();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("aceita token JWT com expiração futura", () => {
    const token = createToken(Math.floor(Date.now() / 1000) + 60);

    expect(isTokenValid(token)).toBe(true);
  });

  it("não autentica apenas com token salvo, sem sessão de login", () => {
    localStorageMap.set("inovaskill.auth.token", createToken(Math.floor(Date.now() / 1000) + 60));

    expect(getAuthToken()).not.toBeNull();
    expect(isAuthenticated()).toBe(false);
  });

  it("remove token expirado ao consultar armazenamento", () => {
    saveAuthToken(createToken(Math.floor(Date.now() / 1000) - 60));

    expect(getAuthToken()).toBeNull();
    expect(localStorageMap.get("inovaskill.auth.token")).toBeUndefined();
  });

  it("envia Authorization Bearer nas requisições autenticadas", async () => {
    const token = createToken(Math.floor(Date.now() / 1000) + 60);
    saveAuthToken(token);
    const fetchMock = vi.mocked(fetch).mockResolvedValue(new Response("{}", { status: 200 }));

    await authFetch("http://localhost/api/files/jobs");

    const [, init] = fetchMock.mock.calls[0];
    expect(new Headers(init?.headers).get("Authorization")).toBe(`Bearer ${token}`);
  });

  it("expõe a role do usuário autenticado", () => {
    saveAuthToken(createToken(Math.floor(Date.now() / 1000) + 60, "admin"));

    expect(getCurrentUserRole()).toBe("admin");
  });

  it("bloqueia requisições sem sessão de login", async () => {
    localStorageMap.set("inovaskill.auth.token", createToken(Math.floor(Date.now() / 1000) + 60));

    await expect(authFetch("http://localhost/api/files/jobs")).rejects.toThrow("Sessão expirada");
    expect(assignMock).toHaveBeenCalledWith("/login?redirect=%2Fclientes");
  });
});
