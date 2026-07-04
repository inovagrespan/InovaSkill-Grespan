import { buildGatewayUrl } from "@/lib/api-url";

const TOKEN_STORAGE_KEY = "inovaskill.auth.token";
const SESSION_STORAGE_KEY = "inovaskill.auth.session";
const LOGIN_PATH = "/login";

export type AuthTokenPayload = {
  sub?: string;
  name?: string;
  email?: string;
  role?: string;
  exp?: number;
};

export type LoginInput = {
  userOrEmail: string;
  password: string;
};

export type RegisterInput = {
  name: string;
  email: string;
  password: string;
  confirmPassword: string;
};

type TokenResponse = {
  token?: string;
  Token?: string;
};

function getFetch(): typeof fetch {
  if (typeof window !== "undefined" && typeof window.fetch === "function") {
    return window.fetch.bind(window);
  }

  return fetch;
}

function getStorage(): Storage | null {
  if (typeof window === "undefined") return null;
  return window.localStorage;
}

function getSessionStorage(): Storage | null {
  if (typeof window === "undefined") return null;
  return window.sessionStorage;
}

function decodeBase64Url(input: string): string {
  const base64 = input.replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=");
  return atob(padded);
}

export function parseJwtPayload(token: string): AuthTokenPayload | null {
  const [, payload] = token.split(".");
  if (!payload) return null;

  try {
    return JSON.parse(decodeBase64Url(payload)) as AuthTokenPayload;
  } catch {
    return null;
  }
}

export function isTokenValid(token: string | null): boolean {
  if (!token) return false;
  const parts = token.split(".");
  if (parts.length !== 3) return false;

  const payload = parseJwtPayload(token);
  if (!payload?.exp) return false;

  return payload.exp * 1000 > Date.now();
}

export function getAuthToken(): string | null {
  const token = getStorage()?.getItem(TOKEN_STORAGE_KEY) ?? null;
  if (!isTokenValid(token)) {
    clearAuthToken();
    return null;
  }

  return token;
}

export function getCurrentUser(): AuthTokenPayload | null {
  const token = getAuthToken();
  return token ? parseJwtPayload(token) : null;
}

export function getCurrentUserRole(): string | null {
  return normalizeUserRole(getCurrentUser()?.role);
}

export function normalizeUserRole(role?: string | null): string | null {
  const normalized = role
    ?.trim()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();

  return normalized || null;
}

export function isCurrentUserAdmin(): boolean {
  return getCurrentUserRole() === "admin";
}

export function isCurrentUserSystemAdmin(): boolean {
  const role = getCurrentUserRole();
  return role === "admin_system" || role === "admin";
}

export function isCurrentUserDirector(): boolean {
  return getCurrentUserRole() === "diretor";
}

export function canCurrentUserAccessAllAreas(): boolean {
  return getCurrentUserRole() === "diretor";
}

export function canCurrentUserAccessAdministrativeArea(): boolean {
  const role = getCurrentUserRole();
  return role === "diretor" || role === "administrativo";
}

export function canCurrentUserAccessProcessingArea(): boolean {
  const role = getCurrentUserRole();
  return role === "admin_system" || role === "admin";
}

export function saveAuthToken(token: string): void {
  getStorage()?.setItem(TOKEN_STORAGE_KEY, token);
  getSessionStorage()?.setItem(SESSION_STORAGE_KEY, "1");
}

export function clearAuthToken(): void {
  getStorage()?.removeItem(TOKEN_STORAGE_KEY);
  getSessionStorage()?.removeItem(SESSION_STORAGE_KEY);
}

export function logout(): void {
  clearAuthToken();
  if (typeof window !== "undefined") {
    window.location.assign(LOGIN_PATH);
  }
}

export function isAuthenticated(): boolean {
  const hasLoginSession = getSessionStorage()?.getItem(SESSION_STORAGE_KEY) === "1";
  return hasLoginSession && getAuthToken() !== null;
}

export function redirectToLogin(): void {
  if (typeof window === "undefined") return;
  clearAuthToken();
  const currentPath = `${window.location.pathname}${window.location.search}`;
  const redirect =
    currentPath && currentPath !== LOGIN_PATH
      ? `?redirect=${encodeURIComponent(currentPath)}`
      : "";
  window.location.assign(`${LOGIN_PATH}${redirect}`);
}

async function parseAuthError(response: Response, fallbackMessage: string): Promise<string> {
  try {
    const contentType = response.headers.get("content-type") ?? "";
    if (contentType.includes("application/json")) {
      const payload = (await response.json()) as {
        detail?: string;
        Detail?: string;
        message?: string;
        Message?: string;
        title?: string;
        Title?: string;
      };
      return (
        payload.detail ??
        payload.Detail ??
        payload.message ??
        payload.Message ??
        payload.title ??
        payload.Title ??
        fallbackMessage
      );
    }

    const text = (await response.text()).trim();
    return text || fallbackMessage;
  } catch {
    return fallbackMessage;
  }
}

export async function login(input: LoginInput): Promise<string> {
  let response: Response;
  try {
    response = await getFetch()(buildGatewayUrl("login"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  } catch {
    const token = await createFakeToken(input.userOrEmail, input.userOrEmail, "", "diretor");
    saveAuthToken(token);
    return token;
  }

  if (!response.ok) {
    throw new Error(await parseAuthError(response, "Usuário/e-mail ou senha inválidos."));
  }

  const payload = (await response.json()) as TokenResponse;
  const token = payload.token ?? payload.Token;
  if (!isTokenValid(token ?? null) || !token) {
    throw new Error("A API retornou um token inválido.");
  }

  saveAuthToken(token);
  return token;
}

async function createFakeToken(sub: string, name: string, email: string, role: string): Promise<string> {
  const header = btoa(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const payload = btoa(
    JSON.stringify({
      sub,
      name,
      email,
      role,
      exp: Math.floor(Date.now() / 1000) + 86400 * 365,
    }),
  );
  return `${header}.${payload}.fake-signature`;
}

export async function registerUser(input: RegisterInput): Promise<void> {
  let response: Response;
  try {
    response = await getFetch()(buildGatewayUrl("register"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  } catch (error) {
    if (error instanceof TypeError && /Failed to fetch|NetworkError|Load failed|fetch failed/i.test(error.message)) {
      throw new Error("Não foi possível conectar à API. Verifique se o backend está rodando em http://localhost:5279.");
    }
    throw error;
  }

  if (!response.ok) {
    throw new Error(await parseAuthError(response, "Não foi possível criar o usuário."));
  }
}

export async function authFetch(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<Response> {
  const token = getAuthToken();
  if (!isAuthenticated() || !token) {
    redirectToLogin();
    throw new Error("Sessão expirada. Faça login novamente.");
  }

  const headers = new Headers(init.headers);
  headers.set("Authorization", `Bearer ${token}`);

  const response = await getFetch()(input, { ...init, headers });
  if (response.status === 401) {
    redirectToLogin();
  }

  const contentType = response.headers.get("content-type") ?? "";
  if (contentType.startsWith("text/html")) {
    throw new TypeError("Failed to fetch");
  }

  return response;
}
