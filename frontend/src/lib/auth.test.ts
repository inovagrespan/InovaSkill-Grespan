import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  authFetch,
  canCurrentUserAccessAdministrativeArea,
  canCurrentUserAccessAllAreas,
  canCurrentUserAccessProcessingArea,
  clearAuthToken,
  getAuthToken,
  getCurrentUserRole,
  isAuthenticated,
  isCurrentUserSystemAdmin,
  isTokenValid,
  login,
  normalizeUserRole,
  registerUser,
  saveAuthToken,
} from "./auth";

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
    saveAuthToken(createToken(Math.floor(Date.now() / 1000) + 60, "Diretor"));

    expect(getCurrentUserRole()).toBe("diretor");
    expect(canCurrentUserAccessAllAreas()).toBe(true);
    expect(canCurrentUserAccessAdministrativeArea()).toBe(true);
    expect(canCurrentUserAccessProcessingArea()).toBe(false);
  });

  it("diferencia acesso administrativo da empresa vs processamento do sistema", () => {
    saveAuthToken(createToken(Math.floor(Date.now() / 1000) + 60, "administrativo"));

    expect(getCurrentUserRole()).toBe("administrativo");
    expect(canCurrentUserAccessAdministrativeArea()).toBe(true);
    expect(canCurrentUserAccessProcessingArea()).toBe(false);
    expect(isCurrentUserSystemAdmin()).toBe(false);
  });

  it("admin_system acessa processamento mas nao administrativo da empresa", () => {
    saveAuthToken(createToken(Math.floor(Date.now() / 1000) + 60, "admin_system"));

    expect(getCurrentUserRole()).toBe("admin_system");
    expect(canCurrentUserAccessAdministrativeArea()).toBe(false);
    expect(canCurrentUserAccessProcessingArea()).toBe(true);
    expect(isCurrentUserSystemAdmin()).toBe(true);
  });

  it("admin legado mantem acesso a processamento e e reconhecido como system admin", () => {
    saveAuthToken(createToken(Math.floor(Date.now() / 1000) + 60, "admin"));

    expect(getCurrentUserRole()).toBe("admin");
    expect(canCurrentUserAccessAdministrativeArea()).toBe(false);
    expect(canCurrentUserAccessProcessingArea()).toBe(true);
    expect(isCurrentUserSystemAdmin()).toBe(true);
  });

  it("normaliza perfis acentuados usados no menu", () => {
    expect(normalizeUserRole("Logística")).toBe("logistica");
    expect(normalizeUserRole("Produção")).toBe("producao");
    expect(normalizeUserRole("Reunião")).toBe("reuniao");
  });

  it("bloqueia requisições sem sessão de login", async () => {
    localStorageMap.set("inovaskill.auth.token", createToken(Math.floor(Date.now() / 1000) + 60));

    await expect(authFetch("http://localhost/api/files/jobs")).rejects.toThrow("Sessão expirada");
    expect(assignMock).toHaveBeenCalledWith("/login?redirect=%2Fclientes");
  });

  it("cria token fake quando a API de login está fora", async () => {
    vi.mocked(window.fetch).mockRejectedValueOnce(new TypeError("Failed to fetch"));

    const token = await login({ userOrEmail: "rh", password: "rh" });
    expect(token.split(".").length).toBe(3);
  });

  it("mostra mensagem clara quando a API de cadastro está fora", async () => {
    vi.mocked(window.fetch).mockRejectedValueOnce(new TypeError("Failed to fetch"));

    await expect(registerUser({
      name: "teste",
      email: "teste@local.test",
      password: "teste123",
      confirmPassword: "teste123",
    })).rejects.toThrow("Não foi possível conectar à API");
  });
});
