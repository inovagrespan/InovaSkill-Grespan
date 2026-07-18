import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("business assistant UI", () => {
  const component = fs.readFileSync(
    path.resolve(process.cwd(), "src/components/BusinessAssistant.tsx"),
    "utf8",
  );
  const root = fs.readFileSync(path.resolve(process.cwd(), "src/routes/__root.tsx"), "utf8");

  it("fica disponível em todas as telas privadas", () => {
    expect(root).toContain("BusinessAssistant");
    expect(root).toContain("canRenderPrivateApp && !isAssistantPage");
  });

  it("oferece painel moderno, sugestões e histórico visual", () => {
    expect(component).toContain("Conecta IA");
    expect(component).toContain("Pergunte aos seus dados");
    expect(component).toContain("dados reais de rotas");
    expect(component).toContain("consulta somente informações de rotas");
    expect(component).toContain("suggestions.map");
    expect(component).toContain("messages.map");
    expect(component).toContain("AssistantResponseText");
    expect(component).toContain("parseRouteLine");
    expect(component).toContain("route-list");
    expect(component).toContain("bullet-list");
    expect(component).toContain("cleanListMarker");
    expect(component).toContain("backdrop-blur");
    expect(component).not.toContain("message.sources.map");
  });

  it("mantém o acionador recolhido e expande no hover ou foco", () => {
    expect(component).toContain("h-14 w-14");
    expect(component).toContain("hover:w-[218px]");
    expect(component).toContain("focus-visible:w-[218px]");
    expect(component).toContain("group-hover:opacity-100");
  });

  it("permite limpar a conversa nos modos flutuante e página", () => {
    expect(component).toContain("function clearConversation()");
    expect(component).toContain("setMessages([WELCOME_MESSAGE])");
    expect(component).toContain("setSuggestions(DEFAULT_SUGGESTIONS)");
    expect(component).toContain("setSessionId(undefined)");
    expect(component).toContain('aria-label="Limpar conversa"');
    expect(component).toContain("<RotateCcw");
    expect(component).toContain("Deseja limpar a conversa?");
    expect(component).toContain("ClearConversationDialog");
    expect(component).toContain("setClearConfirmationOpen(true)");
    expect(component).toContain('role="alertdialog"');
    expect(component).toContain("!isPage && clearConfirmationOpen");
  });

  it("limita a pergunta e envia com autenticação pelo cliente dedicado", () => {
    const client = fs.readFileSync(
      path.resolve(process.cwd(), "src/lib/assistant-api.ts"),
      "utf8",
    );
    expect(component).toContain("slice(0, 800)");
    expect(client).toContain("authFetch");
    expect(client).toContain('buildGatewayUrl("assistant/ask")');
    expect(client).toContain("sessionId");
  });
});
