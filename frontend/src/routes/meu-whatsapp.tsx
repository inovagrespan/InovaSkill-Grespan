import { createFileRoute } from "@tanstack/react-router";
import { CheckCircle2, MessageCircle, ShieldCheck } from "lucide-react";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  confirmWhatsAppVerification, getWhatsAppUserLink, requestWhatsAppVerification,
  revokeWhatsAppUserLink, type WhatsAppUserLink,
} from "@/lib/whatsapp-api";

export const Route = createFileRoute("/meu-whatsapp")({ component: MyWhatsAppPage });

function MyWhatsAppPage() {
  const [link, setLink] = useState<WhatsAppUserLink | null>(null);
  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => { void getWhatsAppUserLink().then(setLink).catch((reason) => setError(reason.message)); }, []);
  async function run(action: () => Promise<WhatsAppUserLink | void>) {
    setBusy(true); setError(null);
    try { const result = await action(); setLink(result ?? await getWhatsAppUserLink()); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "Falha inesperada."); }
    finally { setBusy(false); }
  }

  return <main className="mx-auto w-full max-w-3xl space-y-6 p-4 md:p-8">
    <header><p className="text-sm font-medium text-primary">Integração pessoal</p><h1 className="font-display text-3xl">Meu WhatsApp</h1>
      <p className="mt-2 text-muted-foreground">Autorize seu número para conversar com o CONECTA360 pelo WhatsApp corporativo.</p></header>
    <section className="rounded-2xl border border-border bg-surface p-6 shadow-sm">
      <div className="flex items-start gap-3"><span className="rounded-xl bg-primary/10 p-3 text-primary"><MessageCircle className="size-5" /></span>
        <div><h2 className="font-semibold">Número autorizado</h2><p className="text-sm text-muted-foreground">O histórico fica separado do chat web, mas suas permissões e memórias são compartilhadas.</p></div></div>
      {error ? <div role="alert" className="mt-4 rounded-lg border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">{error}</div> : null}
      {link?.status === "active" ? <div className="mt-6 space-y-4"><div className="flex items-center gap-2 text-sm"><CheckCircle2 className="size-5 text-emerald-600" /><strong>{link.maskedPhone}</strong> está confirmado.</div>
        <Button variant="destructive" disabled={busy} onClick={() => void run(revokeWhatsAppUserLink)}>Revogar acesso</Button></div> : <div className="mt-6 space-y-5">
        <label className="block text-sm font-medium">Telefone com DDD<Input className="mt-2" value={phone} onChange={(event) => setPhone(event.target.value)} placeholder="+55 11 99999-9999" /></label>
        <Button disabled={busy || phone.trim().length < 10} onClick={() => void run(() => requestWhatsAppVerification(phone))}>Enviar código no WhatsApp</Button>
        {link?.status === "pending" ? <div className="rounded-xl border border-border p-4"><label className="block text-sm font-medium">Código de 6 dígitos<Input className="mt-2 max-w-52" inputMode="numeric" maxLength={6} value={code} onChange={(event) => setCode(event.target.value.replace(/\D/g, ""))} /></label>
          <Button className="mt-3" disabled={busy || code.length !== 6} onClick={() => void run(() => confirmWhatsAppVerification(code))}>Confirmar número</Button></div> : null}
      </div>}
    </section>
    <div className="flex gap-3 rounded-xl border border-border bg-muted/30 p-4 text-sm text-muted-foreground"><ShieldCheck className="size-5 shrink-0 text-primary" /><p>O bot ignora números não confirmados e não responde em grupos. No primeiro MVP, são aceitos texto e áudio em conversas privadas.</p></div>
  </main>;
}
