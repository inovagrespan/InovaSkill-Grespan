import { createFileRoute } from "@tanstack/react-router";
import { CheckCheck, MoreVertical, Phone, RotateCcw, Send, Sparkles, Video } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { AssistantResponseText } from "@/components/BusinessAssistant";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { getAssistantSessionUsage, simulateWhatsAppMessage, type AssistantSessionUsage } from "@/lib/whatsapp-api";

export const Route = createFileRoute("/simulador-whatsapp")({ component: WhatsAppSimulatorPage });

type SimulatedMessage = { id: string; role: "user" | "assistant"; content: string; time: string };
const INITIAL_MESSAGE: SimulatedMessage = {
  id: "welcome", role: "assistant",
  content: "Olá! Sou o CONECTA360. Esta é uma simulação do atendimento pelo WhatsApp. Como posso ajudar?",
  time: "agora",
};
const EMPTY_USAGE: AssistantSessionUsage = {
  inputTokens: 0, outputTokens: 0, totalTokens: 0,
  inputCostUsd: 0, outputCostUsd: 0, totalCostUsd: 0,
};

export function formatTokenCount(value: number) {
  return new Intl.NumberFormat("pt-BR", { notation: value >= 1_000 ? "compact" : "standard", maximumFractionDigits: 1 }).format(value);
}

export function formatUsd(value: number) {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "USD", minimumFractionDigits: 4, maximumFractionDigits: 6 }).format(value);
}

function WhatsAppSimulatorPage() {
  const [messages, setMessages] = useState<SimulatedMessage[]>([INITIAL_MESSAGE]);
  const [sessionId, setSessionId] = useState<string>();
  const [text, setText] = useState("");
  const [sending, setSending] = useState(false);
  const [usage, setUsage] = useState<AssistantSessionUsage>(EMPTY_USAGE);
  const [error, setError] = useState<string | null>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  useEffect(() => { bottomRef.current?.scrollIntoView({ behavior: "smooth" }); }, [messages, sending]);

  async function send(event: FormEvent) {
    event.preventDefault();
    const content = text.trim();
    if (!content || sending) return;
    const now = new Date().toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
    setMessages((current) => [...current, { id: crypto.randomUUID(), role: "user", content, time: now }]);
    setText(""); setSending(true); setError(null);
    try {
      const response = await simulateWhatsAppMessage(content, sessionId);
      setSessionId(response.sessionId);
      setMessages((current) => [...current, {
        id: crypto.randomUUID(), role: "assistant", content: response.answer,
        time: new Date().toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" }),
      }]);
      try {
        const currentUsage = await getAssistantSessionUsage(response.sessionId);
        setUsage(currentUsage);
      } catch {
        setError("A resposta foi recebida, mas o consumo da conversa não pôde ser atualizado.");
      }
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Não foi possível enviar a mensagem."); }
    finally { setSending(false); }
  }

  function reset() { setMessages([INITIAL_MESSAGE]); setSessionId(undefined); setText(""); setError(null); setUsage(EMPTY_USAGE); }

  return <main className="mx-auto flex h-[calc(100dvh-1rem)] w-full max-w-5xl flex-col gap-3 p-2 md:h-[calc(100dvh-2rem)] md:p-4">
    <div className="flex items-center justify-between"><div><p className="text-sm font-medium text-primary">Ambiente de demonstração</p><h1 className="font-display text-2xl">Simulador do WhatsApp</h1></div>
      <Button variant="outline" size="sm" onClick={reset}><RotateCcw className="size-4" />Nova conversa</Button></div>
    <section className="mx-auto flex min-h-0 w-full max-w-md flex-1 flex-col overflow-hidden rounded-[1.75rem] border-[6px] border-slate-900 bg-[#efeae2] shadow-2xl dark:border-slate-700">
      <header className="flex h-16 shrink-0 items-center gap-3 bg-[#075e54] px-4 text-white">
        <div className="flex size-10 items-center justify-center rounded-full bg-white/20 text-sm font-bold">C360</div>
        <div className="min-w-0 flex-1"><p className="truncate font-semibold">CONECTA360</p><p className="text-xs text-white/75">online · simulação</p></div>
        <Popover>
          <PopoverTrigger asChild>
            <button type="button" className="flex items-center gap-1 rounded-full bg-white/15 px-2 py-1 text-xs font-semibold hover:bg-white/25" aria-label={`Consumo da conversa: ${usage.totalTokens} tokens`}>
              <Sparkles className="size-3.5" aria-hidden="true" />{formatTokenCount(usage.totalTokens)}
            </button>
          </PopoverTrigger>
          <PopoverContent align="end" className="w-64 text-sm">
            <p className="font-semibold">Consumo desta conversa</p>
            <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-2">
              <dt className="text-muted-foreground">Entrada</dt><dd className="text-right font-medium">{usage.inputTokens.toLocaleString("pt-BR")}</dd>
              <dt className="text-muted-foreground">Saída</dt><dd className="text-right font-medium">{usage.outputTokens.toLocaleString("pt-BR")}</dd>
              <dt className="text-muted-foreground">Total</dt><dd className="text-right font-semibold">{usage.totalTokens.toLocaleString("pt-BR")}</dd>
              <dt className="text-muted-foreground">Custo estimado</dt><dd className="text-right font-semibold">{formatUsd(usage.totalCostUsd)}</dd>
            </dl>
          </PopoverContent>
        </Popover>
        <Video className="size-5" aria-hidden="true" /><Phone className="size-5" aria-hidden="true" /><MoreVertical className="size-5" aria-hidden="true" />
      </header>
      <div className="flex-1 space-y-2 overflow-y-auto p-3" style={{ backgroundImage: "radial-gradient(rgb(120 113 108 / 0.10) 1px, transparent 1px)", backgroundSize: "18px 18px" }} aria-live="polite">
        <div className="mx-auto w-fit rounded-md bg-[#fff3c4] px-3 py-1.5 text-center text-[11px] text-slate-600 shadow-sm">As mensagens desta tela não são enviadas ao WhatsApp real.</div>
        {messages.map((message) => <div key={message.id} className={`flex ${message.role === "user" ? "justify-end" : "justify-start"}`}>
          <div className={`rounded-lg px-3 py-2 text-sm text-slate-900 shadow-sm ${message.role === "user" ? "max-w-[86%] rounded-tr-none bg-[#d9fdd3]" : "max-w-[94%] rounded-tl-none bg-white"}`}>
            {message.role === "assistant"
              ? <div className="leading-relaxed [&_.text-muted-foreground]:text-slate-600 [&_.text-foreground]:text-slate-900"><AssistantResponseText text={message.content} presentation="whatsapp" /></div>
              : <p className="whitespace-pre-wrap leading-relaxed">{message.content}</p>}
            <span className="mt-1 flex items-center justify-end gap-1 text-[10px] text-slate-500">{message.time}{message.role === "user" ? <CheckCheck className="size-3.5 text-sky-600" /> : null}</span>
          </div></div>)}
        {sending ? <div className="flex justify-start"><div className="rounded-lg rounded-tl-none bg-white px-4 py-2 text-sm text-slate-500 shadow-sm">digitando<span className="animate-pulse">...</span></div></div> : null}
        <div ref={bottomRef} />
      </div>
      {error ? <div role="alert" className="shrink-0 bg-red-50 px-3 py-2 text-xs text-red-700">{error}</div> : null}
      <form onSubmit={(event) => void send(event)} className="flex shrink-0 items-center gap-2 bg-[#f0f2f5] p-2">
        <Input aria-label="Mensagem" value={text} maxLength={800} onChange={(event) => setText(event.target.value)} placeholder="Digite uma mensagem" className="h-11 rounded-full border-0 bg-white text-slate-900" />
        <Button type="submit" size="icon" disabled={sending || !text.trim()} aria-label="Enviar mensagem" className="size-11 shrink-0 rounded-full bg-[#00a884] hover:bg-[#008f70]"><Send className="size-5" /></Button>
      </form>
    </section>
  </main>;
}
