import { Bot, ChevronDown, RotateCcw, Send, Sparkles, UserRound, X } from "lucide-react";
import { FormEvent, useEffect, useRef, useState } from "react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { askBusinessAssistant, type AssistantSource } from "@/lib/assistant-api";
import { getCurrentUserRole } from "@/lib/auth";
import { cn } from "@/lib/utils";

type AssistantMessage = {
  id: string;
  author: "assistant" | "user";
  text: string;
  sources?: AssistantSource[];
  mode?: string;
};

type BusinessAssistantProps = {
  variant?: "floating" | "page";
};

const DEFAULT_SUGGESTIONS = [
  "Quais rotas estão críticas?",
  "Quais são as 3 rotas mais ociosas?",
  "Procure a rota Marília.",
  "Quais rotas estão acima de 140%?",
];

const WELCOME_MESSAGE: AssistantMessage = {
  id: "welcome",
  author: "assistant",
  text: "Olá! Eu consulto os dados reais de rotas disponíveis na aplicação. O que você gostaria de perguntar?",
  mode: "IA com dados reais de rotas",
};

export function BusinessAssistant({ variant = "floating" }: BusinessAssistantProps) {
  const isPage = variant === "page";
  const role = getCurrentUserRole();
  const [open, setOpen] = useState(isPage);
  const [question, setQuestion] = useState("");
  const [loading, setLoading] = useState(false);
  const [clearConfirmationOpen, setClearConfirmationOpen] = useState(false);
  const [sessionId, setSessionId] = useState<string | undefined>();
  const [suggestions, setSuggestions] = useState(DEFAULT_SUGGESTIONS);
  const [messages, setMessages] = useState<AssistantMessage[]>([WELCOME_MESSAGE]);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: "smooth" });
  }, [messages, loading]);

  async function sendQuestion(value: string) {
    const trimmed = value.trim();
    if (!trimmed || loading) return;

    setQuestion("");
    setMessages((current) => [
      ...current,
      { id: crypto.randomUUID(), author: "user", text: trimmed },
    ]);
    setLoading(true);
    try {
      const response = await askBusinessAssistant(trimmed, sessionId);
      setSessionId(response.sessionId);
      setMessages((current) => [
        ...current,
        {
          id: crypto.randomUUID(),
          author: "assistant",
          text: response.answer,
          sources: response.sources,
          mode: response.mode,
        },
      ]);
      setSuggestions(response.suggestions);
    } catch (error) {
      setMessages((current) => [
        ...current,
        {
          id: crypto.randomUUID(),
          author: "assistant",
          text: (error as Error).message,
          mode: "Não foi possível responder",
        },
      ]);
    } finally {
      setLoading(false);
    }
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    void sendQuestion(question);
  }

  function clearConversation() {
    setMessages([WELCOME_MESSAGE]);
    setSuggestions(DEFAULT_SUGGESTIONS);
    setQuestion("");
    setSessionId(undefined);
    setClearConfirmationOpen(false);
  }

  const assistantPanel = (
    <section
      onMouseDown={(event) => event.stopPropagation()}
      className={cn(
        "flex flex-col overflow-hidden border border-border/80 bg-surface shadow-2xl",
        isPage
          ? "h-full min-h-0 w-full rounded-2xl"
          : "absolute inset-x-0 bottom-0 h-[min(88dvh,760px)] sm:bottom-5 sm:left-auto sm:right-5 sm:w-[min(440px,calc(100vw-40px))] sm:rounded-3xl",
      )}
      aria-label="Assistente inteligente"
    >
      <header className="relative overflow-hidden border-b border-white/10 bg-[radial-gradient(circle_at_top_right,hsl(var(--primary)/0.55),transparent_42%),linear-gradient(135deg,#111827,#172554)] px-5 py-4 text-white">
        <div className="absolute -right-10 -top-16 size-40 rounded-full bg-primary/20 blur-3xl" />
        <div className="relative flex items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <span className="grid size-11 place-items-center rounded-2xl border border-white/15 bg-white/10 shadow-inner">
              <Sparkles className="size-5 text-cyan-300" />
            </span>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="font-display text-lg font-semibold">Conecta IA</h2>
                <span className="rounded-full bg-emerald-400/15 px-2 py-0.5 text-[10px] font-semibold text-emerald-300">ONLINE</span>
              </div>
              <p className="text-xs text-slate-300">Consulta segura aos dados reais de rotas</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setClearConfirmationOpen(true)}
              disabled={loading || messages.length <= 1}
              className="inline-flex h-9 items-center gap-2 rounded-full bg-white/10 px-3 text-xs font-medium text-slate-200 transition-colors hover:bg-white/20 disabled:cursor-not-allowed disabled:opacity-40"
              aria-label="Limpar conversa"
              title="Limpar conversa"
            >
              <RotateCcw className="size-3.5" />
              <span className="hidden sm:inline">Limpar</span>
            </button>
            {!isPage && (
              <button type="button" onClick={() => setOpen(false)} className="grid size-9 place-items-center rounded-full bg-white/10 text-slate-200 transition-colors hover:bg-white/20" aria-label="Fechar assistente">
                <X className="size-4" />
              </button>
            )}
          </div>
        </div>
      </header>

      <div ref={scrollRef} className="custom-scrollbar flex-1 space-y-5 overflow-y-auto bg-[radial-gradient(circle_at_top,hsl(var(--primary)/0.06),transparent_35%)] px-4 py-5 sm:px-6">
        {messages.map((message) => (
          <article key={message.id} className={cn("flex gap-2.5", message.author === "user" && "flex-row-reverse")}>
            <span className={cn(
              "grid size-8 shrink-0 place-items-center rounded-xl border",
              message.author === "assistant"
                ? "border-primary/20 bg-primary/10 text-primary"
                : "border-border bg-muted text-muted-foreground",
            )}>
              {message.author === "assistant" ? <Bot className="size-4" /> : <UserRound className="size-4" />}
            </span>
            <div className={cn(isPage ? "max-w-[min(78%,760px)]" : "max-w-[82%]", "space-y-2", message.author === "user" && "items-end")}>
              <div className={cn(
                "rounded-2xl px-4 py-3 text-sm leading-relaxed shadow-sm",
                message.author === "assistant"
                  ? "rounded-tl-md border border-border/70 bg-surface"
                  : "rounded-tr-md bg-primary text-primary-foreground",
              )}>
                {message.author === "assistant" ? (
                  <AssistantResponseText text={message.text} />
                ) : (
                  message.text
                )}
              </div>
              {message.author === "assistant" && message.mode && (
                <p className="flex items-center gap-1 text-[10px] text-muted-foreground">
                  <Sparkles className="size-3" />
                  {message.mode}
                </p>
              )}
            </div>
          </article>
        ))}

        {loading && (
          <div className="flex gap-2.5">
            <span className="grid size-8 place-items-center rounded-xl border border-primary/20 bg-primary/10 text-primary"><Bot className="size-4" /></span>
            <div className="flex items-center gap-1 rounded-2xl rounded-tl-md border border-border/70 bg-surface px-4 py-4">
              {[0, 1, 2].map((item) => <span key={item} className="size-1.5 animate-bounce rounded-full bg-primary" style={{ animationDelay: `${item * 120}ms` }} />)}
            </div>
          </div>
        )}
      </div>

      <footer className="border-t border-border bg-surface/95 p-3 backdrop-blur sm:px-5">
        <div className="custom-scrollbar mb-3 flex gap-2 overflow-x-auto pb-1">
          {suggestions.map((suggestion) => (
            <button key={suggestion} type="button" onClick={() => void sendQuestion(suggestion)} disabled={loading} className="shrink-0 rounded-full border border-primary/20 bg-primary/[0.04] px-3 py-1.5 text-xs text-foreground transition-colors hover:bg-primary/10 disabled:opacity-50">
              {suggestion}
            </button>
          ))}
        </div>
        <form onSubmit={handleSubmit} className={cn("mx-auto flex items-end gap-2 rounded-2xl border border-border bg-muted/30 p-2 ring-primary/30 focus-within:ring-2", isPage && "max-w-4xl")}>
          <textarea
            value={question}
            onChange={(event) => setQuestion(event.target.value.slice(0, 800))}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                void sendQuestion(question);
              }
            }}
            rows={1}
            placeholder="Pergunte sobre rotas..."
            className="max-h-28 min-h-10 flex-1 resize-none bg-transparent px-2 py-2 text-sm outline-none placeholder:text-muted-foreground"
          />
          <button type="submit" disabled={!question.trim() || loading} className="grid size-10 shrink-0 place-items-center rounded-xl bg-primary text-primary-foreground transition-all hover:scale-105 disabled:cursor-not-allowed disabled:opacity-40" aria-label="Enviar pergunta">
            <Send className="size-4" />
          </button>
        </form>
        <p className="mt-2 flex items-center justify-center gap-1 text-[10px] text-muted-foreground">
          <ChevronDown className="size-3 rotate-90" />
          Nesta versão, a IA consulta somente informações de rotas.
        </p>
      </footer>
      {!isPage && clearConfirmationOpen && (
        <div className="absolute inset-0 z-20 grid place-items-center bg-black/55 p-5 backdrop-blur-sm">
          <div
            role="alertdialog"
            aria-modal="true"
            aria-labelledby="floating-clear-title"
            aria-describedby="floating-clear-description"
            className="w-full max-w-sm rounded-2xl border border-border bg-surface p-5 shadow-2xl"
          >
            <div className="grid size-10 place-items-center rounded-xl bg-primary/10 text-primary">
              <RotateCcw className="size-4" />
            </div>
            <h3 id="floating-clear-title" className="mt-4 text-base font-semibold">
              Deseja limpar a conversa?
            </h3>
            <p id="floating-clear-description" className="mt-2 text-sm leading-relaxed text-muted-foreground">
              Todas as mensagens exibidas neste chat serão removidas. Esta ação não altera dados do sistema.
            </p>
            <div className="mt-5 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setClearConfirmationOpen(false)}
                className="rounded-lg border border-border px-3 py-2 text-sm font-medium transition-colors hover:bg-muted"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={clearConversation}
                className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-primary-foreground transition-colors hover:bg-primary/90"
              >
                Limpar conversa
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );

  if (isPage) {
    return (
      <>
        {assistantPanel}
        <ClearConversationDialog
          open={clearConfirmationOpen}
          onOpenChange={setClearConfirmationOpen}
          onConfirm={clearConversation}
        />
      </>
    );
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className={cn(
          "group fixed bottom-5 right-3 z-[70] flex h-14 w-14 items-center overflow-hidden rounded-full border border-primary/30",
          "bg-[linear-gradient(135deg,hsl(var(--primary)),hsl(var(--primary)/0.78))] p-2",
          "text-primary-foreground shadow-[0_18px_55px_-15px_hsl(var(--primary)/0.7)]",
          "outline-none ring-primary/40 transition-[width,transform,box-shadow] duration-300 ease-out",
          "hover:w-[218px] hover:-translate-y-0.5 hover:shadow-[0_22px_65px_-15px_hsl(var(--primary)/0.8)]",
          "focus-visible:w-[218px] focus-visible:ring-2",
        )}
        aria-label="Abrir assistente inteligente"
      >
        <span className="relative grid size-10 shrink-0 place-items-center rounded-full bg-white/15">
          <Sparkles className="size-5" />
          <span className="absolute -right-0.5 -top-0.5 size-2.5 rounded-full border-2 border-primary bg-emerald-400" />
        </span>
        <span className="ml-3 min-w-[146px] translate-x-2 pr-2 text-left opacity-0 transition-[opacity,transform] delay-75 duration-200 group-hover:translate-x-0 group-hover:opacity-100 group-focus-visible:translate-x-0 group-focus-visible:opacity-100">
          <span className="block text-[10px] font-semibold uppercase tracking-[0.16em] opacity-75">Conecta IA</span>
          <span className="block text-sm font-semibold">Pergunte aos seus dados</span>
        </span>
      </button>

      {open && (
        <div className="fixed inset-0 z-[2200] bg-black/45 backdrop-blur-sm sm:bg-black/20" onMouseDown={() => setOpen(false)}>
          {assistantPanel}
        </div>
      )}
    </>
  );
}

function AssistantResponseText({ text }: { text: string }) {
  const lines = text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
  const blocks = buildAssistantBlocks(lines);

  if (blocks.length === 0) {
    return <p className="whitespace-pre-line">{sanitizeAssistantText(text)}</p>;
  }

  return (
    <div className="space-y-3">
      {blocks.map((block, index) => {
        if (block.type === "heading") {
          return <h3 key={`${block.type}-${index}`} className="text-sm font-semibold">{block.text}</h3>;
        }

        if (block.type === "paragraph") {
          return <p key={`${block.type}-${index}`}>{block.text}</p>;
        }

        if (block.type === "bullet-list") {
          return (
            <ul key={`${block.type}-${index}`} className="space-y-1.5 pl-4">
              {block.items.map((item) => (
                <li key={item} className="list-disc pl-1 text-muted-foreground marker:text-primary">
                  <span className="text-foreground">{item}</span>
                </li>
              ))}
            </ul>
          );
        }

        if (block.type === "entity-list") {
          return (
            <div key={`${block.type}-${index}`} className="space-y-2">
              {block.items.map((item, itemIndex) => (
                <EntityDisplayCard key={`${item.kind}-${item.title}-${itemIndex}`} item={item} index={itemIndex} />
              ))}
            </div>
          );
        }

        return (
          <div key={`${block.type}-${index}`} className="space-y-2">
            {block.routes.map((route, routeIndex) => (
              <RouteDisplayCard key={`${route.name}-${routeIndex}`} route={route} index={routeIndex} />
            ))}
          </div>
        );
      })}
    </div>
  );
}

type AssistantBlock =
  | { type: "heading"; text: string }
  | { type: "paragraph"; text: string }
  | { type: "bullet-list"; items: string[] }
  | { type: "entity-list"; items: AssistantEntityDisplay[] }
  | { type: "route-list"; routes: AssistantRouteDisplay[] };

type AssistantRouteDisplay = {
  name: string;
  occupancy?: string;
  status?: string;
  detail?: string;
};

type AssistantEntityDisplay = {
  kind: "customer";
  title: string;
  badges: string[];
  detail?: string;
};

function buildAssistantBlocks(lines: string[]): AssistantBlock[] {
  const blocks: AssistantBlock[] = [];
  let pendingBullets: string[] = [];
  let pendingRoutes: AssistantRouteDisplay[] = [];
  let pendingEntities: AssistantEntityDisplay[] = [];

  function flushLists() {
    if (pendingRoutes.length > 0) {
      blocks.push({ type: "route-list", routes: pendingRoutes });
      pendingRoutes = [];
    }
    if (pendingEntities.length > 0) {
      blocks.push({ type: "entity-list", items: pendingEntities });
      pendingEntities = [];
    }
    if (pendingBullets.length > 0) {
      blocks.push({ type: "bullet-list", items: pendingBullets });
      pendingBullets = [];
    }
  }

  for (const rawLine of lines) {
    const line = sanitizeAssistantText(rawLine);
    const route = parseRouteLine(line);
    if (route) {
      if (pendingBullets.length > 0) {
        blocks.push({ type: "bullet-list", items: pendingBullets });
        pendingBullets = [];
      }
      pendingRoutes.push(route);
      continue;
    }

    const entity = parseEntityLine(line);
    if (entity) {
      if (pendingRoutes.length > 0) {
        blocks.push({ type: "route-list", routes: pendingRoutes });
        pendingRoutes = [];
      }
      if (pendingBullets.length > 0) {
        blocks.push({ type: "bullet-list", items: pendingBullets });
        pendingBullets = [];
      }
      pendingEntities.push(entity);
      continue;
    }

    if (isBulletLine(line)) {
      if (pendingRoutes.length > 0) {
        blocks.push({ type: "route-list", routes: pendingRoutes });
        pendingRoutes = [];
      }
      if (pendingEntities.length > 0) {
        blocks.push({ type: "entity-list", items: pendingEntities });
        pendingEntities = [];
      }
      pendingBullets.push(cleanListMarker(line));
      continue;
    }

    flushLists();
    if (line.startsWith("### ")) {
      blocks.push({ type: "heading", text: line.replace(/^###\s+/, "") });
    } else {
      blocks.push({ type: "paragraph", text: line });
    }
  }

  flushLists();
  return blocks;
}

function RouteDisplayCard({ route, index }: { route: AssistantRouteDisplay; index: number }) {
  return (
    <div className="rounded-lg border border-border/70 bg-muted/25 px-3 py-2">
      <div className="flex flex-wrap items-center gap-2">
        <span className="grid size-5 shrink-0 place-items-center rounded-full bg-primary/10 text-[10px] font-semibold text-primary">
          {index + 1}
        </span>
        <span className="min-w-0 flex-1 break-words font-semibold text-foreground">{route.name}</span>
        {route.occupancy && (
          <span className="rounded-full bg-primary/10 px-2 py-0.5 text-xs font-semibold text-primary">
            {route.occupancy}
          </span>
        )}
        {route.status && (
          <span className="rounded-full border border-border px-2 py-0.5 text-xs text-muted-foreground">
            {route.status}
          </span>
        )}
      </div>
      {route.detail && <p className="mt-1 text-xs text-muted-foreground">{route.detail}</p>}
    </div>
  );
}

function EntityDisplayCard({ item, index }: { item: AssistantEntityDisplay; index: number }) {
  return (
    <div className="rounded-lg border border-border/70 bg-muted/25 px-3 py-2">
      <div className="flex flex-wrap items-center gap-2">
        <span className="grid size-5 shrink-0 place-items-center rounded-full bg-primary/10 text-[10px] font-semibold text-primary">
          {index + 1}
        </span>
        <span className="min-w-0 flex-1 break-words font-semibold text-foreground">{item.title}</span>
        {item.badges.map((badge) => (
          <span key={badge} className="rounded-full border border-border px-2 py-0.5 text-xs text-muted-foreground">
            {badge}
          </span>
        ))}
      </div>
      {item.detail && <p className="mt-1 text-xs text-muted-foreground">{item.detail}</p>}
    </div>
  );
}

function parseRouteLine(line: string): AssistantRouteDisplay | null {
  const routeContractMatch = line.match(/^(?:[-*]\s*)?\[ROTA\]\s*(.+)$/i);
  if (routeContractMatch) {
    return parseRouteContract(routeContractMatch[1]);
  }

  const routeListMatch = cleanListMarker(line).match(/^(.+?)\s+[—-]\s+(\d+(?:,\d+)?%)\s+[—-]\s+(.+)$/);
  if (!routeListMatch) return null;

  return {
    name: routeListMatch[1].trim(),
    occupancy: routeListMatch[2].trim(),
    status: routeListMatch[3].trim(),
  };
}

function parseRouteContract(value: string): AssistantRouteDisplay {
  const parts = value.split("|").map((part) => part.trim()).filter(Boolean);
  const route: AssistantRouteDisplay = { name: parts[0] ?? "Rota" };

  for (const part of parts.slice(1)) {
    const [rawLabel, ...rawValueParts] = part.split(":");
    const label = rawLabel.trim().toLowerCase();
    const partValue = rawValueParts.join(":").trim();
    if (!partValue) continue;

    if (label === "ocupação" || label === "ocupacao") {
      route.occupancy = partValue;
    } else if (label === "status") {
      route.status = partValue;
    } else if (label === "motivo" || label === "observação" || label === "observacao") {
      route.detail = partValue;
    }
  }

  return route;
}

function parseEntityLine(line: string): AssistantEntityDisplay | null {
  const customerMatch = line.match(/^(?:[-*]\s*)?\[CLIENTE\]\s*(.+)$/i);
  if (!customerMatch) return null;

  const parts = customerMatch[1].split("|").map((part) => part.trim()).filter(Boolean);
  const item: AssistantEntityDisplay = {
    kind: "customer",
    title: parts[0] ?? "Cliente",
    badges: [],
  };

  for (const part of parts.slice(1)) {
    const [rawLabel, ...rawValueParts] = part.split(":");
    const label = rawLabel.trim().toLowerCase();
    const partValue = rawValueParts.join(":").trim();
    if (!partValue) continue;

    if (label === "código" || label === "codigo" || label === "cidade" || label === "tipo") {
      item.badges.push(partValue);
    } else if (label === "relação" || label === "relacao" || label === "observação" || label === "observacao") {
      item.detail = partValue;
    }
  }

  return item;
}

function isBulletLine(line: string) {
  return /^([-*]\s+|\d+[\).]\s+)/.test(line);
}

function cleanListMarker(line: string) {
  return line.replace(/^([-*]\s+|\d+[\).]\s+)/, "").trim();
}

function sanitizeAssistantText(value: string) {
  return value.replace(/\*\*/g, "").trim();
}

function ClearConversationDialog({
  open,
  onOpenChange,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
}) {
  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent className="border-border bg-surface">
        <AlertDialogHeader>
          <AlertDialogTitle>Deseja limpar a conversa?</AlertDialogTitle>
          <AlertDialogDescription>
            Todas as mensagens exibidas neste chat serão removidas. Esta ação não altera dados do sistema.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancelar</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm}>Limpar conversa</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
