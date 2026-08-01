import { authFetch } from "@/lib/auth";
import { buildGatewayUrl } from "@/lib/api-url";

export type AssistantSource = {
  label: string;
  value: string;
};

export type AssistantAnswer = {
  sessionId: string;
  answer: string;
  sources: AssistantSource[];
  suggestions: string[];
  mode: string;
};

export type AssistantConversationSummary = {
  sessionId: string;
  preview: string;
  updatedAt: string;
};

export type AssistantConversationPage = {
  items: AssistantConversationSummary[];
  hasMore: boolean;
  nextOffset: number;
};

export type AssistantConversationMessage = {
  id: string;
  role: "assistant" | "user";
  content: string;
  createdAt: string;
};

export type AssistantConversation = {
  sessionId: string;
  messages: AssistantConversationMessage[];
};

export async function askBusinessAssistant(message: string, sessionId?: string): Promise<AssistantAnswer> {
  const response = await authFetch(buildGatewayUrl("assistant/ask"), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ sessionId, message }),
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { detail?: string } | null;
    throw new Error(payload?.detail ?? "Não foi possível consultar o assistente.");
  }

  return response.json() as Promise<AssistantAnswer>;
}

export async function listAssistantConversations(offset = 0): Promise<AssistantConversationPage> {
  const response = await authFetch(buildGatewayUrl(`assistant/sessions?offset=${offset}`));
  if (!response.ok) throw new Error("Não foi possível carregar o histórico de conversas.");
  return response.json() as Promise<AssistantConversationPage>;
}

export async function getAssistantConversation(sessionId: string): Promise<AssistantConversation> {
  const response = await authFetch(buildGatewayUrl(`assistant/sessions/${sessionId}`));
  if (!response.ok) throw new Error("Não foi possível carregar esta conversa.");
  return response.json() as Promise<AssistantConversation>;
}
