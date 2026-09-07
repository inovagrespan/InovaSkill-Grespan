import { buildGatewayUrl } from "@/lib/api-url";
import { authFetch } from "@/lib/auth";

export const ASSIGNABLE_USER_ROLES = ["diretor", "vendas", "logistica", "admin", "admin_system"] as const;
export type AssignableUserRole = (typeof ASSIGNABLE_USER_ROLES)[number];

export type CreateAdminUserInput = {
  name: string;
  email: string;
  role: AssignableUserRole;
  password: string;
  confirmPassword: string;
};

export async function createAdminUser(input: CreateAdminUserInput): Promise<void> {
  const response = await authFetch(buildGatewayUrl("admin/users"), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });

  if (response.ok) return;

  let message = "Não foi possível cadastrar o usuário.";
  try {
    const payload = (await response.json()) as { detail?: string; Detail?: string };
    message = payload.detail ?? payload.Detail ?? message;
  } catch {
    // Mantém a mensagem segura quando a API não retorna JSON.
  }
  throw new Error(message);
}
