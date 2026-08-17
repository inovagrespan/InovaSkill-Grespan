import { useEffect, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { CheckCircle2, Loader2, MapPin } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { getCurrentUserRole } from "@/lib/auth";
import { checkOsrmHealth, fetchLogisticsDepot, updateLogisticsDepot } from "@/lib/importer-api";
import { validateLogisticsDepotForm, type LogisticsDepotForm } from "@/lib/logistics-depot";

export const Route = createFileRoute("/configuracoes/deposito")({ component: LogisticsDepotPage });

const EMPTY_FORM: LogisticsDepotForm = { name: "", address: "", latitude: "", longitude: "" };

function LogisticsDepotPage() {
  const role = getCurrentUserRole();
  const canManage = role === "logistica" || role === "admin" || role === "admin_system";
  const [form, setForm] = useState(EMPTY_FORM);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [message, setMessage] = useState("");
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    fetchLogisticsDepot()
      .then((depot) => {
        if (depot) setForm({ name: depot.name, address: depot.address,
          latitude: String(depot.latitude), longitude: String(depot.longitude) });
      })
      .catch((error) => setMessage((error as Error).message))
      .finally(() => setLoading(false));
  }, []);

  function field(name: keyof LogisticsDepotForm, value: string) {
    setForm((current) => ({ ...current, [name]: value }));
    setSuccess(false);
  }

  async function save() {
    const validated = validateLogisticsDepotForm(form);
    if (!validated.value) { setMessage(validated.error ?? "Dados inválidos."); return; }
    setSaving(true); setMessage(""); setSuccess(false);
    try {
      const depot = await updateLogisticsDepot(validated.value);
      setForm({ name: depot.name, address: depot.address,
        latitude: String(depot.latitude), longitude: String(depot.longitude) });
      setSuccess(true);
    } catch (error) { setMessage((error as Error).message); }
    finally { setSaving(false); }
  }

  async function testOsrm() {
    setTesting(true); setMessage(""); setSuccess(false);
    try { await checkOsrmHealth(); setMessage("OSRM disponível e depósito localizado no mapa."); }
    catch (error) { setMessage((error as Error).message); }
    finally { setTesting(false); }
  }

  return (
    <div className="page-shell">
      <header>
        <span className="page-header-kicker">Configurações</span>
        <h1 className="mt-2 text-4xl font-display tracking-tight">Depósito logístico</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Configure o ponto único de saída e retorno das futuras rotas diárias.
        </p>
      </header>
      {message && <Alert><AlertDescription>{message}</AlertDescription></Alert>}
      {success && <Alert><CheckCircle2 className="size-4"/><AlertDescription>Depósito salvo com sucesso.</AlertDescription></Alert>}
      <Card className="border-border bg-surface">
        <CardHeader><CardTitle className="flex items-center gap-2"><MapPin className="size-5"/>Origem e retorno</CardTitle></CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <Field label="Nome" value={form.name} onChange={(value) => field("name", value)} disabled={!canManage || loading} />
          <Field label="Endereço" value={form.address} onChange={(value) => field("address", value)} disabled={!canManage || loading} />
          <Field label="Latitude" value={form.latitude} onChange={(value) => field("latitude", value)} disabled={!canManage || loading} />
          <Field label="Longitude" value={form.longitude} onChange={(value) => field("longitude", value)} disabled={!canManage || loading} />
          <div className="flex gap-2 md:col-span-2">
            {canManage && <Button onClick={save} disabled={loading || saving}>
              {saving && <Loader2 className="mr-2 size-4 animate-spin"/>}Salvar depósito
            </Button>}
            <Button variant="outline" onClick={testOsrm} disabled={loading || testing || !form.latitude || !form.longitude}>
              {testing && <Loader2 className="mr-2 size-4 animate-spin"/>}Testar OSRM
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function Field({ label, value, onChange, disabled }: { label: string; value: string; onChange: (value: string) => void; disabled: boolean }) {
  return <label className="space-y-2 text-sm font-medium">{label}
    <input value={value} onChange={(event) => onChange(event.target.value)} disabled={disabled}
      className="flex h-10 w-full rounded-md border border-input bg-background px-3 text-sm disabled:opacity-60" />
  </label>;
}
