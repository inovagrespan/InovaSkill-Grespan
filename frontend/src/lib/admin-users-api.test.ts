import { beforeEach, describe, expect, it, vi } from "vitest";
import { createAdminUser } from "@/lib/admin-users-api";
import { saveAuthToken } from "@/lib/auth";

function token(): string {
  const payload = btoa(JSON.stringify({ sub: "1", role: "admin_system", exp: Math.floor(Date.now() / 1000) + 60 }));
  return `header.${payload}.signature`;
}

describe("cadastro administrativo de usuários", () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    const values = new Map<string, string>();
    fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("window", {
      fetch: fetchMock,
      localStorage: { getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => values.set(key, value), removeItem: (key: string) => values.delete(key) },
      sessionStorage: { getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => values.set(key, value), removeItem: (key: string) => values.delete(key) },
      location: { pathname: "/administracao/usuarios", search: "", assign: vi.fn() },
    });
    saveAuthToken(token());
  });

  it("envia perfil e credenciais ao endpoint protegido", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 201 }));
    await createAdminUser({ name: "Maria", email: "maria@example.com", role: "vendas", password: "segredo", confirmPassword: "segredo" });

    expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/api\/admin\/users$/), expect.objectContaining({ method: "POST" }));
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body))).toEqual({ name: "Maria", email: "maria@example.com", role: "vendas", password: "segredo", confirmPassword: "segredo" });
    expect(new Headers(fetchMock.mock.calls[0][1]?.headers).get("Authorization")).toMatch(/^Bearer /);
  });

  it("propaga a validação retornada pela API", async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ detail: "Usuário duplicado." }), { status: 409, headers: { "Content-Type": "application/json" } }));
    await expect(createAdminUser({ name: "Maria", email: "maria@example.com", role: "vendas", password: "segredo", confirmPassword: "segredo" })).rejects.toThrow("Usuário duplicado.");
  });
});
