import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { canRoleAccessPath } from "./access-control";

describe("integração com WhatsApp", () => {
  it.each(["diretor", "vendas", "logistica", "admin", "admin_system"])("permite autoatendimento para %s", (role) => {
    expect(canRoleAccessPath(role, "/meu-whatsapp")).toBe(true);
    expect(canRoleAccessPath(role, "/simulador-whatsapp")).toBe(true);
  });

  it("oferece simulador visual sem enviar mensagens ao WhatsApp real", () => {
    const simulator = readFileSync("src/routes/simulador-whatsapp.tsx", "utf8");
    expect(simulator).toContain("simulateWhatsAppMessage");
    expect(simulator).toContain("AssistantResponseText");
    expect(simulator).toContain('presentation="whatsapp"');
    expect(simulator).toContain('message.role === "assistant"');
    expect(simulator).toContain("max-w-[94%]");
    expect(simulator).toContain("getAssistantSessionUsage");
    expect(simulator).toContain("Consumo desta conversa");
    expect(simulator).toContain("usage.totalTokens");
    expect(simulator).toContain("setUsage(EMPTY_USAGE)");
    expect(simulator).toContain("As mensagens desta tela não são enviadas ao WhatsApp real");
    expect(simulator).not.toContain("getWhatsAppConnection");
  });

  it("restringe a conexão corporativa ao admin_system", () => {
    expect(canRoleAccessPath("admin_system", "/administracao/whatsapp")).toBe(true);
    expect(canRoleAccessPath("admin", "/administracao/whatsapp")).toBe(false);
    expect(canRoleAccessPath("vendas", "/administracao/whatsapp")).toBe(false);
  });

  it("expõe confirmação, revogação, QR e polling por constante", () => {
    const userPage = readFileSync("src/routes/meu-whatsapp.tsx", "utf8");
    const adminPage = readFileSync("src/routes/administracao.whatsapp.tsx", "utf8");
    expect(userPage).toContain("confirmWhatsAppVerification");
    expect(userPage).toContain("revokeWhatsAppUserLink");
    expect(adminPage).toContain("CONNECTION_POLLING_MS");
    expect(adminPage).toContain("QR_CODE_MAXIMUM_ATTEMPTS");
    expect(adminPage).toContain("getWhatsAppQrCode");
    expect(adminPage).toContain("Conector local indisponível");
    expect(adminPage).toContain("Nenhuma chave do WhatsApp ou da Meta é necessária");
    expect(adminPage).toContain('connection?.status !== "connecting"');
  });

  it("exibe o QR Code no tamanho nativo, com fundo e área de respiro", () => {
    const adminPage = readFileSync("src/routes/administracao.whatsapp.tsx", "utf8");
    expect(adminPage).toContain('className="h-auto w-[360px] max-w-full"');
    expect(adminPage).toContain('className="w-fit max-w-full rounded-lg bg-white p-2 shadow-sm"');
    expect(adminPage).not.toContain("max-h-60 max-w-60");
  });
});
