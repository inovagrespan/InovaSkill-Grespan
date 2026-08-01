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
    expect(component).toContain("CONECTA360");
    expect(component).toContain("Pergunte aos seus dados");
    expect(component).toContain("IA orientada por dados reais");
    expect(component).toContain("ASSISTANT_TRANSPARENCY_NOTICE");
    expect(component).toContain("informar o período consultado");
    expect(component).toContain("separar dados reais de interpretações");
    expect(component).toContain("pedir esclarecimento");
    expect(component).toContain("suggestions.map");
    expect(component).toContain("messages.map");
    expect(component).toContain("AssistantResponseText");
    expect(component).toContain("parseRouteLine");
    expect(component).toContain("parseEntityLine");
    expect(component).toContain("route-list");
    expect(component).toContain("entity-list");
    expect(component).toContain("bullet-list");
    expect(component).toContain("\\[CLIENTE\\]");
    expect(component).toContain("cleanListMarker");
    expect(component).toContain("backdrop-blur");
    expect(component).toContain("message.sources.map");
    expect(component).toContain('aria-label="Fontes externas"');
    expect(component).toContain("normalizeExternalSourceUrl");
    expect(component).toContain('rel="noreferrer noopener"');
    expect(component).toContain('aria-label="Histórico de conversas"');
    expect(component).toContain("loadConversationHistory");
    expect(component).toContain("Carregar conversas anteriores");
    expect(component).toContain("hasMoreConversations");
    expect(component).toContain("listAssistantConversations(loadPrevious ? conversations.length : 0)");
    expect(component).toContain("selectConversation");
    expect(component).toContain('aria-label="Conversas anteriores"');
    expect(component).toContain('aria-current={sessionId === conversation.sessionId ? "page" : undefined}');
    expect(component).toContain("formatConversationDate");
    expect(component).toContain("historyOpen");
    expect(component).toContain("absolute inset-y-0 right-0");
    expect(component).toContain('aria-expanded={historyOpen}');
    expect(component).toContain('aria-label={historyOpen ? "Fechar histórico" : "Abrir histórico"}');
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
    expect(component).toContain("function startNewConversation()");
    expect(component).toContain('aria-label="Nova conversa"');
    expect(component).toContain("<MessageSquarePlus");
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
    expect(client).toContain('buildGatewayUrl(`assistant/sessions?offset=${offset}`)');
    expect(client).toContain("getAssistantConversation");
  });
});
