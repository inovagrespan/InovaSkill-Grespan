import { FormEvent, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { UserRoundPlus } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { createAdminUser, type AssignableUserRole } from "@/lib/admin-users-api";

export const Route = createFileRoute("/administracao/usuarios")({ component: UserAdministrationPage });

const ROLE_OPTIONS: ReadonlyArray<{ value: AssignableUserRole; label: string }> = [
  { value: "diretor", label: "Diretor" },
  { value: "vendas", label: "Vendas" },
  { value: "logistica", label: "Logística" },
  { value: "admin", label: "Administrador" },
  { value: "admin_system", label: "Administrador do sistema" },
];

const EMPTY_FORM = { name: "", email: "", role: "vendas" as AssignableUserRole, password: "", confirmPassword: "" };

function UserAdministrationPage() {
  const [form, setForm] = useState(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    if (!form.name.trim() || !form.email.trim() || !form.password || !form.confirmPassword) {
      setMessage({ type: "error", text: "Preencha todos os campos obrigatórios." });
      return;
    }
    if (form.password.length < 6) {
      setMessage({ type: "error", text: "A senha deve ter pelo menos 6 caracteres." });
      return;
    }
    if (form.password !== form.confirmPassword) {
      setMessage({ type: "error", text: "A confirmação de senha não confere." });
      return;
    }

    setSaving(true);
    try {
      await createAdminUser({ ...form, name: form.name.trim(), email: form.email.trim() });
      setForm(EMPTY_FORM);
      setMessage({ type: "success", text: "Usuário cadastrado com sucesso." });
    } catch (reason) {
      setMessage({ type: "error", text: reason instanceof Error ? reason.message : "Não foi possível cadastrar o usuário." });
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="page-shell">
      <div className="mx-auto w-full max-w-3xl space-y-6">
        <header>
          <span className="page-header-kicker">Administração do sistema</span>
          <div className="mt-3 flex items-center gap-3">
            <span className="grid size-11 place-items-center rounded-xl border border-primary/25 bg-primary/10 text-primary"><UserRoundPlus className="size-5" /></span>
            <div><h1 className="font-display text-3xl tracking-tight">Novo usuário</h1><p className="mt-1 text-sm text-muted-foreground">Cadastre o acesso e defina o perfil funcional do usuário.</p></div>
          </div>
        </header>

        <form className="space-y-5 rounded-xl border border-border bg-surface p-5 md:p-6" onSubmit={submit} noValidate>
          <div className="grid gap-5 md:grid-cols-2">
            <div className="space-y-2"><Label htmlFor="user-name">Nome de usuário</Label><Input id="user-name" autoComplete="username" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required /></div>
            <div className="space-y-2"><Label htmlFor="user-email">E-mail</Label><Input id="user-email" type="email" autoComplete="email" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} required /></div>
            <div className="space-y-2"><Label htmlFor="user-role">Perfil de acesso</Label><Select value={form.role} onValueChange={(role: AssignableUserRole) => setForm({ ...form, role })}><SelectTrigger id="user-role"><SelectValue /></SelectTrigger><SelectContent>{ROLE_OPTIONS.map((role) => <SelectItem key={role.value} value={role.value}>{role.label}</SelectItem>)}</SelectContent></Select></div>
            <div className="hidden md:block" />
            <div className="space-y-2"><Label htmlFor="user-password">Senha</Label><Input id="user-password" type="password" autoComplete="new-password" value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} required /></div>
            <div className="space-y-2"><Label htmlFor="user-confirm-password">Confirmar senha</Label><Input id="user-confirm-password" type="password" autoComplete="new-password" value={form.confirmPassword} onChange={(event) => setForm({ ...form, confirmPassword: event.target.value })} required /></div>
          </div>
          {message ? <Alert variant={message.type === "error" ? "destructive" : "default"}><AlertTitle>{message.type === "error" ? "Atenção" : "Tudo certo"}</AlertTitle><AlertDescription>{message.text}</AlertDescription></Alert> : null}
          <div className="flex justify-end"><Button type="submit" disabled={saving}><UserRoundPlus className="size-4" />{saving ? "Cadastrando..." : "Cadastrar usuário"}</Button></div>
        </form>
      </div>
    </div>
  );
}
