import { authFetch } from "@/lib/auth";
import { buildGatewayUrl } from "@/lib/api-url";

export type AssistantSource = {
  label: string;
  value: string;
};

export type AssistantAnswer = {
  answer: string;
  sources: AssistantSource[];
  suggestions: string[];
  mode: string;
};

export async function askBusinessAssistant(question: string): Promise<AssistantAnswer> {
  const response = await authFetch(buildGatewayUrl("assistant/ask"), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ question }),
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { detail?: string } | null;
    throw new Error(payload?.detail ?? "Não foi possível consultar o assistente.");
  }

  return response.json() as Promise<AssistantAnswer>;
}
