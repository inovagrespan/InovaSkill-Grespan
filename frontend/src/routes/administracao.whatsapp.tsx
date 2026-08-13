import { createFileRoute } from "@tanstack/react-router";
import { AlertTriangle, CheckCircle2, QrCode, RefreshCw, Unplug } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { disconnectWhatsApp, getWhatsAppConnection, getWhatsAppQrCode, startWhatsAppConnection, type WhatsAppConnection } from "@/lib/whatsapp-api";

export const Route = createFileRoute("/administracao/whatsapp")({ component: WhatsAppAdministrationPage });
const CONNECTION_POLLING_MS = 10_000;
const QR_CODE_RETRY_INTERVAL_MS = 750;
const QR_CODE_MAXIMUM_ATTEMPTS = 12;

const wait = (milliseconds: number) => new Promise((resolve) => window.setTimeout(resolve, milliseconds));

function WhatsAppAdministrationPage() {
  const [connection, setConnection] = useState<WhatsAppConnection | null>(null);
  const [qrCode, setQrCode] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const load = useCallback(async () => {
    try {
      const state = await getWhatsAppConnection();
      setConnection(state); setError(null);
      if (state.status === "connecting") {
        try { setQrCode((await getWhatsAppQrCode()).dataUrl); } catch { /* QR ainda está sendo gerado. */ }
      } else if (state.status === "connected") setQrCode(null);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Falha ao consultar conexão."); }
  }, []);
  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    if (connection?.status !== "connecting") return;
    const timer = window.setInterval(() => void load(), CONNECTION_POLLING_MS);
    return () => window.clearInterval(timer);
  }, [connection?.status, load]);
  async function connect() {
    try {
      setError(null); setQrCode(null);
      setConnection(await startWhatsAppConnection());
      for (let attempt = 1; attempt <= QR_CODE_MAXIMUM_ATTEMPTS; attempt += 1) {
        try { const qr = await getWhatsAppQrCode(); setQrCode(qr.dataUrl); return; }
        catch { if (attempt === QR_CODE_MAXIMUM_ATTEMPTS) throw new Error("O conector iniciou, mas o QR Code não ficou pronto. Clique em Verificar novamente e tente gerar o QR outra vez."); }
        await wait(QR_CODE_RETRY_INTERVAL_MS);
      }
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Falha ao iniciar conexão."); }
  }
  async function disconnect() { if (!window.confirm("Desconectar o WhatsApp corporativo?")) return; try { setError(null); await disconnectWhatsApp(); setQrCode(null); await load(); } catch (reason) { setError(reason instanceof Error ? reason.message : "Falha ao desconectar."); } }
  const unavailable = connection?.status === "unavailable";
  return <main className="mx-auto w-full max-w-5xl space-y-6 p-4 md:p-8"><header><p className="text-sm font-medium text-primary">Administração do sistema</p><h1 className="font-display text-3xl">WhatsApp corporativo</h1><p className="mt-2 text-muted-foreground">Vincule um WhatsApp real como dispositivo conectado, usando apenas o QR Code.</p></header>
    {error ? <div role="alert" className="rounded-lg border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive"><strong className="block">A operação não foi concluída</strong><span className="mt-1 block">{error}</span></div> : null}
    {unavailable ? <div role="status" className="flex gap-3 rounded-lg border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-700 dark:text-amber-300"><AlertTriangle className="size-5 shrink-0" /><div><strong>Conector local parado</strong><p className="mt-1">{connection.detail}</p><p className="mt-2">Nenhuma chave do WhatsApp ou da Meta é necessária. Enquanto isso, o <strong>Simulador WhatsApp</strong> continua disponível.</p></div></div> : null}
    <section className="grid gap-5 rounded-2xl border border-border bg-surface p-6 md:grid-cols-2"><div className="space-y-4"><div><span className="text-sm text-muted-foreground">Estado da integração</span><p className="flex items-center gap-2 font-semibold">{connection?.status === "connected" ? <><CheckCircle2 className="size-4 text-emerald-600" />Conectado</> : connection?.status === "connecting" ? "Aguardando leitura do QR Code" : unavailable ? "Conector local indisponível" : connection ? "Desconectado" : "Consultando..."}</p>{connection?.maskedPhone ? <p className="text-sm">Número conectado: {connection.maskedPhone}</p> : null}</div>
      <div className="flex flex-wrap gap-2"><Button disabled={unavailable} onClick={() => void connect()}><QrCode className="size-4" />Iniciar e gerar QR</Button><Button variant="outline" onClick={() => void load()}><RefreshCw className="size-4" />Verificar novamente</Button><Button variant="destructive" disabled={connection?.status !== "connected"} onClick={() => void disconnect()}><Unplug className="size-4" />Desconectar</Button></div><p className="text-xs text-muted-foreground">No celular: WhatsApp → Aparelhos conectados → Conectar um aparelho → leia o QR Code.</p></div>
      <div className="flex min-h-64 items-center justify-center rounded-xl border border-dashed border-border bg-muted/20 p-4">{qrCode ? <div className="w-fit max-w-full rounded-lg bg-white p-2 shadow-sm"><img src={qrCode} alt="QR Code para conectar o WhatsApp corporativo" className="h-auto w-[360px] max-w-full" /></div> : <p className="max-w-56 text-center text-sm text-muted-foreground">Inicie a conexão para visualizar o QR Code temporário.</p>}</div></section>
  </main>;
}
