import { createFileRoute } from "@tanstack/react-router";
import { CheckCircle2, LogIn, UserPlus } from "lucide-react";
import { FormEvent, useState } from "react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { login, registerUser } from "@/lib/auth";

export const Route = createFileRoute("/login")({
  validateSearch: (search: Record<string, unknown>) => ({
    redirect: typeof search.redirect === "string" ? search.redirect : undefined,
  }),
  component: LoginPage,
});

type Message = { type: "success" | "error"; text: string } | null;

const MIN_PASSWORD_LENGTH = 6;

function LoginPage() {
  const search = Route.useSearch();
  const [loginUser, setLoginUser] = useState("");
  const [loginPassword, setLoginPassword] = useState("");
  const [registerName, setRegisterName] = useState("");
  const [registerEmail, setRegisterEmail] = useState("");
  const [registerPassword, setRegisterPassword] = useState("");
  const [registerConfirmPassword, setRegisterConfirmPassword] = useState("");
  const [message, setMessage] = useState<Message>(null);
  const [loading, setLoading] = useState(false);

  async function handleLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);

    if (!loginUser.trim() || !loginPassword.trim()) {
      setMessage({ type: "error", text: "Informe usuário/e-mail e senha." });
      return;
    }

    setLoading(true);
    try {
      await login({ userOrEmail: loginUser.trim(), password: loginPassword });
      setMessage({ type: "success", text: "Login realizado com sucesso." });
      window.location.assign(search.redirect || "/");
    } catch (error) {
      setMessage({ type: "error", text: error instanceof Error ? error.message : "Não foi possível entrar." });
    } finally {
      setLoading(false);
    }
  }

  async function handleRegister(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);

    if (!registerName.trim() || !registerEmail.trim() || !registerPassword.trim() || !registerConfirmPassword.trim()) {
      setMessage({ type: "error", text: "Preencha todos os campos obrigatórios." });
      return;
    }

    if (registerPassword.length < MIN_PASSWORD_LENGTH) {
      setMessage({ type: "error", text: `A senha deve ter pelo menos ${MIN_PASSWORD_LENGTH} caracteres.` });
      return;
    }

    if (registerPassword !== registerConfirmPassword) {
      setMessage({ type: "error", text: "A confirmação de senha não confere." });
      return;
    }

    setLoading(true);
    try {
      await registerUser({
        name: registerName.trim(),
        email: registerEmail.trim(),
        password: registerPassword,
        confirmPassword: registerConfirmPassword,
      });
      setMessage({ type: "success", text: "Cadastro criado com sucesso. Entre com seu usuário/e-mail e senha." });
      setLoginUser(registerName.trim());
      setLoginPassword("");
      setRegisterName("");
      setRegisterEmail("");
      setRegisterPassword("");
      setRegisterConfirmPassword("");
    } catch (error) {
      setMessage({ type: "error", text: error instanceof Error ? error.message : "Não foi possível criar o usuário." });
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="app-background flex min-h-screen items-center justify-center px-4 py-8 font-body text-foreground">
      <main className="grid w-full max-w-5xl overflow-hidden rounded-xl border border-border bg-surface shadow-lg md:grid-cols-[0.9fr_1.1fr]">
        <section className="hidden bg-[linear-gradient(155deg,rgba(180,35,47,0.92),rgba(17,24,39,0.94))] p-10 text-white md:flex md:flex-col md:justify-between">
          <div>
            <div className="flex size-10 items-center justify-center rounded-md bg-white/15 font-display text-lg font-bold">
              G
            </div>
            <h1 className="mt-8 max-w-sm font-display text-3xl font-bold tracking-tight">
              Acesso seguro ao GRESPAN
            </h1>
            <p className="mt-4 max-w-sm text-sm leading-6 text-white/78">
              A tela de login é obrigatória. Entre com nome de usuário ou e-mail para liberar o acesso ao sistema.
            </p>
          </div>
          <div className="flex items-center gap-2 text-sm text-white/80">
            <CheckCircle2 className="size-4" />
            Usuário de teste: admin / admin.
          </div>
        </section>

        <section className="p-6 sm:p-8 lg:p-10">
          <div className="mb-8 md:hidden">
            <div className="flex size-10 items-center justify-center rounded-md bg-primary font-display text-lg font-bold text-primary-foreground">
              G
            </div>
            <h1 className="mt-4 font-display text-2xl font-bold">Acesso seguro ao GRESPAN</h1>
          </div>

          <Tabs defaultValue="login" className="w-full">
            <TabsList className="grid w-full grid-cols-2">
              <TabsTrigger value="login">Login</TabsTrigger>
              <TabsTrigger value="cadastro">Cadastro</TabsTrigger>
            </TabsList>

            {message ? (
              <Alert className="mt-6" variant={message.type === "error" ? "destructive" : "default"}>
                <AlertTitle>{message.type === "error" ? "Atenção" : "Tudo certo"}</AlertTitle>
                <AlertDescription>{message.text}</AlertDescription>
              </Alert>
            ) : null}

            <TabsContent value="login" className="mt-6">
              <form className="space-y-4" onSubmit={handleLogin} noValidate>
                <div className="space-y-2">
                  <Label htmlFor="login-user">Usuário/e-mail</Label>
                  <Input
                    id="login-user"
                    value={loginUser}
                    onChange={(event) => setLoginUser(event.target.value)}
                    autoComplete="username"
                    placeholder="admin ou admin@local.test"
                    required
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="login-password">Senha</Label>
                  <Input
                    id="login-password"
                    type="password"
                    value={loginPassword}
                    onChange={(event) => setLoginPassword(event.target.value)}
                    autoComplete="current-password"
                    placeholder="admin"
                    required
                  />
                </div>
                <Button type="submit" className="w-full" disabled={loading}>
                  <LogIn className="size-4" />
                  {loading ? "Entrando..." : "Entrar"}
                </Button>
              </form>
            </TabsContent>

            <TabsContent value="cadastro" className="mt-6">
              <form className="space-y-4" onSubmit={handleRegister} noValidate>
                <div className="space-y-2">
                  <Label htmlFor="register-name">Nome de usuário</Label>
                  <Input
                    id="register-name"
                    value={registerName}
                    onChange={(event) => setRegisterName(event.target.value)}
                    autoComplete="username"
                    required
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="register-email">E-mail</Label>
                  <Input
                    id="register-email"
                    type="email"
                    value={registerEmail}
                    onChange={(event) => setRegisterEmail(event.target.value)}
                    autoComplete="email"
                    required
                  />
                </div>
                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="space-y-2">
                    <Label htmlFor="register-password">Senha</Label>
                    <Input
                      id="register-password"
                      type="password"
                      value={registerPassword}
                      onChange={(event) => setRegisterPassword(event.target.value)}
                      autoComplete="new-password"
                      required
                    />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="register-confirm-password">Confirmar senha</Label>
                    <Input
                      id="register-confirm-password"
                      type="password"
                      value={registerConfirmPassword}
                      onChange={(event) => setRegisterConfirmPassword(event.target.value)}
                      autoComplete="new-password"
                      required
                    />
                  </div>
                </div>
                <Button type="submit" className="w-full" disabled={loading}>
                  <UserPlus className="size-4" />
                  {loading ? "Criando..." : "Criar cadastro"}
                </Button>
              </form>
            </TabsContent>
          </Tabs>
        </section>
      </main>
    </div>
  );
}
