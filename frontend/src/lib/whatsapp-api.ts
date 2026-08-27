import { authFetch } from "@/lib/auth";
import { buildGatewayUrl } from "@/lib/api-url";
import type { AssistantAnswer } from "@/lib/assistant-api";

export type WhatsAppUserLink = {
  id: string | null;
  status: "not_configured" | "pending" | "active" | "revoked";
  maskedPhone: string | null;
  confirmedAt: string | null;
};

export type WhatsAppConnection = {
  status: string;
  maskedPhone: string | null;
  detail: string | null;
  providerAvailable: boolean;
};

export type AssistantSessionUsage = {
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  inputCostUsd: number;
  outputCostUsd: number;
  totalCostUsd: number;
};

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await authFetch(buildGatewayUrl(path), init);
  if (!response.ok) {
    const contentType = response.headers.get("content-type") ?? "";
    const body = contentType.includes("application/json")
      ? await response.json().catch(() => null) as { detail?: string; title?: string } | null
      : null;
    const fallback = response.status === 503
      ? "O serviço necessário está indisponível. Verifique a configuração e tente novamente."
      : response.status === 401
        ? "Sua sessão expirou. Entre novamente para continuar."
        : response.status === 403
          ? "Seu perfil não tem permissão para realizar esta operação."
          : `A operação falhou (HTTP ${response.status}).`;
    throw new Error(body?.detail ?? body?.title ?? fallback);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

const json = (body: unknown): RequestInit => ({
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});

export const getWhatsAppUserLink = () => api<WhatsAppUserLink>("whatsapp/user-link");
export const requestWhatsAppVerification = (phone: string) => api<WhatsAppUserLink>("whatsapp/user-link/verification", json({ phone }));
export const confirmWhatsAppVerification = (code: string) => api<WhatsAppUserLink>("whatsapp/user-link/confirmation", json({ code }));
export const revokeWhatsAppUserLink = () => api<void>("whatsapp/user-link", { method: "DELETE" });
export const getWhatsAppConnection = () => api<WhatsAppConnection>("admin/whatsapp/connection");
export const startWhatsAppConnection = () => api<WhatsAppConnection>("admin/whatsapp/connection", { method: "POST" });
export const getWhatsAppQrCode = () => api<{ dataUrl: string }>("admin/whatsapp/connection/qr-code");
export const disconnectWhatsApp = () => api<void>("admin/whatsapp/connection", { method: "DELETE" });
export const simulateWhatsAppMessage = (message: string, sessionId?: string) =>
  api<AssistantAnswer>("assistant/whatsapp-simulator", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ message, sessionId }),
  });
export const getAssistantSessionUsage = (sessionId: string) =>
  api<AssistantSessionUsage>(`assistant/sessions/${sessionId}/usage`);
