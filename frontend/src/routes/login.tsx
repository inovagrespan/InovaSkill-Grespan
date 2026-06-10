import { createFileRoute } from "@tanstack/react-router";
import { LogIn, Moon, Sun, UserPlus } from "lucide-react";
import { FormEvent, useEffect, useState } from "react";
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
  const [theme, setTheme] = useState<"light" | "dark">("light");
  const [activeTab, setActiveTab] = useState<"login" | "cadastro">("login");
  const [loginUser, setLoginUser] = useState("");
  const [loginPassword, setLoginPassword] = useState("");
  const [registerName, setRegisterName] = useState("");
  const [registerEmail, setRegisterEmail] = useState("");
  const [registerPassword, setRegisterPassword] = useState("");
  const [registerConfirmPassword, setRegisterConfirmPassword] = useState("");
  const [message, setMessage] = useState<Message>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const savedTheme = window.localStorage.getItem("app.theme");
    if (savedTheme === "dark" || savedTheme === "light") {
      setTheme(savedTheme);
      return;
    }

    const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
    setTheme(prefersDark ? "dark" : "light");
  }, []);

  useEffect(() => {
    document.documentElement.classList.toggle("dark", theme === "dark");
    window.localStorage.setItem("app.theme", theme);
  }, [theme]);

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
      setActiveTab("login");
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
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-[#eef1f6] px-4 py-8 font-body text-foreground dark:bg-[#0d1117]">
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute inset-0 bg-[linear-gradient(180deg,#f4f6fa_0%,#edf1f6_100%)] dark:bg-[linear-gradient(180deg,#0f141c_0%,#121923_100%)]" />
        <div className="absolute left-[8%] top-[10%] h-80 w-80 rounded-full bg-primary/10 blur-3xl dark:bg-primary/18" />
        <div className="absolute right-[10%] top-[14%] h-72 w-72 rounded-full bg-rose-200/35 blur-3xl dark:bg-rose-400/10" />
        <div className="absolute bottom-[8%] left-1/2 h-56 w-[42rem] -translate-x-1/2 rounded-full bg-slate-900/7 blur-3xl dark:bg-black/30" />
        <div className="absolute inset-0 bg-[linear-gradient(135deg,rgba(255,255,255,0.30),transparent_44%,rgba(148,163,184,0.05)_100%)] dark:bg-[linear-gradient(135deg,rgba(255,255,255,0.03),transparent_44%,rgba(148,163,184,0.03)_100%)]" />
        <div className="absolute inset-0 hidden opacity-[0.18] md:block [mask-image:radial-gradient(circle_at_center,black,transparent_78%)] dark:opacity-[0.14]">
          <div className="absolute inset-0 bg-[linear-gradient(rgba(148,163,184,0.10)_1px,transparent_1px),linear-gradient(90deg,rgba(148,163,184,0.10)_1px,transparent_1px)] dark:bg-[linear-gradient(rgba(148,163,184,0.08)_1px,transparent_1px),linear-gradient(90deg,rgba(148,163,184,0.08)_1px,transparent_1px)] bg-[size:32px_32px]" />
        </div>
      </div>

      <button
        type="button"
        onClick={() => setTheme((current) => (current === "dark" ? "light" : "dark"))}
        className="absolute right-4 top-4 z-20 inline-flex size-10 items-center justify-center rounded-xl border border-white/40 bg-white/70 text-slate-700 shadow-sm backdrop-blur dark:border-white/10 dark:bg-white/5 dark:text-slate-200"
        aria-label={theme === "dark" ? "Ativar modo claro" : "Ativar modo escuro"}
      >
        {theme === "dark" ? <Sun className="size-4" /> : <Moon className="size-4" />}
      </button>

      <main className="relative z-10 grid w-full max-w-5xl overflow-hidden rounded-[24px] border border-white/70 bg-surface shadow-[0_24px_48px_rgba(16,24,40,0.08),0_8px_18px_rgba(16,24,40,0.06)] ring-1 ring-black/3 dark:border-white/10 dark:bg-[#121923] dark:shadow-[0_24px_48px_rgba(0,0,0,0.34),0_8px_18px_rgba(0,0,0,0.22)] dark:ring-white/5 md:grid-cols-[0.94fr_1.06fr]">
        <div className="pointer-events-none absolute inset-x-0 top-0 h-px bg-[linear-gradient(90deg,transparent,rgba(255,255,255,0.8),transparent)]" />

        <section className="relative hidden bg-[linear-gradient(165deg,rgba(180,35,47,0.95),rgba(96,28,42,0.94)_58%,rgba(30,36,48,0.96))] p-10 text-white md:flex md:flex-col md:justify-between lg:p-11">
          <div className="pointer-events-none absolute inset-0 opacity-25">
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(255,255,255,0.16),transparent_34%),linear-gradient(rgba(255,255,255,0.08)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,0.06)_1px,transparent_1px)] bg-[size:auto,28px_28px,28px_28px]" />
            <div className="absolute bottom-0 left-0 h-36 w-full bg-[linear-gradient(180deg,transparent,rgba(17,24,39,0.18))]" />
          </div>

          <div>
            <div className="flex size-11 items-center justify-center rounded-xl bg-white/14 font-display text-lg font-bold shadow-sm ring-1 ring-white/10 backdrop-blur">
              G
            </div>
            <h1 className="mt-9 max-w-sm font-display text-[2.1rem] font-bold leading-tight tracking-tight">
              Acesso seguro ao GRESPAN
            </h1>
            <p className="mt-5 max-w-sm text-[15px] leading-7 text-white/82">
              A tela de login é obrigatória. Entre com nome de usuário ou e-mail para liberar o acesso ao sistema.
            </p>
          </div>

          <div className="h-px w-full bg-white/12" />
        </section>

        <section className="bg-white/80 p-6 backdrop-blur-[2px] dark:bg-transparent sm:p-8 lg:p-10">
          <div className="mb-7 md:hidden">
            <div className="flex size-10 items-center justify-center rounded-xl bg-primary font-display text-lg font-bold text-primary-foreground shadow-sm">
              G
            </div>
            <h1 className="mt-4 font-display text-2xl font-bold tracking-tight">Acesso seguro ao GRESPAN</h1>
            <p className="mt-2 text-sm text-muted-foreground">
              Entre com seu usuário ou e-mail para liberar o acesso ao sistema.
            </p>
          </div>

          <Tabs
            value={activeTab}
            onValueChange={(value) => setActiveTab(value as "login" | "cadastro")}
            className="w-full"
          >
            <TabsList className="grid h-11 w-full grid-cols-2 rounded-xl border border-border/50 bg-[linear-gradient(180deg,rgba(241,243,247,0.92),rgba(236,239,244,0.84))] p-1 shadow-[inset_0_1px_0_rgba(255,255,255,0.7)] dark:border-white/10 dark:bg-white/5 dark:shadow-none">
              <TabsTrigger
                value="login"
                className="rounded-lg text-sm font-medium text-muted-foreground data-[state=active]:bg-white data-[state=active]:font-semibold data-[state=active]:text-foreground data-[state=active]:shadow-[0_2px_8px_rgba(15,23,42,0.06)] data-[state=active]:ring-1 data-[state=active]:ring-primary/12 dark:data-[state=active]:bg-white/10 dark:data-[state=active]:text-white dark:data-[state=active]:shadow-none"
              >
                Login
              </TabsTrigger>
              <TabsTrigger
                value="cadastro"
                className="rounded-lg text-sm font-medium text-muted-foreground data-[state=active]:bg-white data-[state=active]:font-semibold data-[state=active]:text-foreground data-[state=active]:shadow-[0_2px_8px_rgba(15,23,42,0.06)] data-[state=active]:ring-1 data-[state=active]:ring-primary/12 dark:data-[state=active]:bg-white/10 dark:data-[state=active]:text-white dark:data-[state=active]:shadow-none"
              >
                Cadastro
              </TabsTrigger>
            </TabsList>

            {message ? (
              <Alert className="mt-5 rounded-xl" variant={message.type === "error" ? "destructive" : "default"}>
                <AlertTitle>{message.type === "error" ? "Atenção" : "Tudo certo"}</AlertTitle>
                <AlertDescription>{message.text}</AlertDescription>
              </Alert>
            ) : null}

            <TabsContent value="login" className="mt-6">
              <div className="mb-5">
                <p className="text-[10px] font-mono uppercase tracking-widest text-primary">Acesso</p>
                <h3 className="mt-1 text-lg font-semibold tracking-tight">Entrar na sua conta</h3>
              </div>
              <form className="space-y-4.5" onSubmit={handleLogin} noValidate>
                <div className="space-y-2.5">
                  <Label htmlFor="login-user" className="text-sm font-medium">
                    Usuário/e-mail
                  </Label>
                  <Input
                    id="login-user"
                    value={loginUser}
                    onChange={(event) => setLoginUser(event.target.value)}
                    autoComplete="username"
                    placeholder="admin ou admin@local.test"
                    className="h-11 rounded-xl border-border/50 bg-white px-4 shadow-none transition-colors placeholder:text-muted-foreground/75 hover:border-border/70 focus-visible:border-primary/35 focus-visible:ring-2 focus-visible:ring-primary/8 dark:border-white/10 dark:bg-white/4 dark:hover:border-white/20 dark:placeholder:text-slate-400"
                    required
                  />
                </div>
                <div className="space-y-2.5">
                  <Label htmlFor="login-password" className="text-sm font-medium">
                    Senha
                  </Label>
                  <Input
                    id="login-password"
                    type="password"
                    value={loginPassword}
                    onChange={(event) => setLoginPassword(event.target.value)}
                    autoComplete="current-password"
                    placeholder="admin"
                    className="h-11 rounded-xl border-border/50 bg-white px-4 shadow-none transition-colors placeholder:text-muted-foreground/75 hover:border-border/70 focus-visible:border-primary/35 focus-visible:ring-2 focus-visible:ring-primary/8 dark:border-white/10 dark:bg-white/4 dark:hover:border-white/20 dark:placeholder:text-slate-400"
                    required
                  />
                </div>

                <Button type="submit" className="mt-3 h-11 w-full rounded-xl bg-[linear-gradient(180deg,var(--primary-red),var(--primary-red-hover))] font-semibold shadow-[0_8px_16px_rgba(180,35,47,0.18)] hover:brightness-[1.02]" disabled={loading}>
                  <LogIn className="size-4" />
                  {loading ? "Entrando..." : "Entrar"}
                </Button>
              </form>
            </TabsContent>

            <TabsContent value="cadastro" className="mt-6">
              <div className="mb-5">
                <p className="text-[10px] font-mono uppercase tracking-widest text-primary">Novo acesso</p>
                <h3 className="mt-1 text-lg font-semibold tracking-tight">Criar cadastro local</h3>
              </div>
              <form className="space-y-4.5" onSubmit={handleRegister} noValidate>
                <div className="space-y-2.5">
                  <Label htmlFor="register-name" className="text-sm font-medium">
                    Nome de usuário
                  </Label>
                  <Input
                    id="register-name"
                    value={registerName}
                    onChange={(event) => setRegisterName(event.target.value)}
                    autoComplete="username"
                    className="h-11 rounded-xl border-border/50 bg-white px-4 shadow-none transition-colors placeholder:text-muted-foreground/75 hover:border-border/70 focus-visible:border-primary/35 focus-visible:ring-2 focus-visible:ring-primary/8 dark:border-white/10 dark:bg-white/4 dark:hover:border-white/20 dark:placeholder:text-slate-400"
                    required
                  />
                </div>
                <div className="space-y-2.5">
                  <Label htmlFor="register-email" className="text-sm font-medium">
                    E-mail
                  </Label>
                  <Input
                    id="register-email"
                    type="email"
                    value={registerEmail}
                    onChange={(event) => setRegisterEmail(event.target.value)}
                    autoComplete="email"
                    className="h-11 rounded-xl border-border/50 bg-white px-4 shadow-none transition-colors placeholder:text-muted-foreground/75 hover:border-border/70 focus-visible:border-primary/35 focus-visible:ring-2 focus-visible:ring-primary/8 dark:border-white/10 dark:bg-white/4 dark:hover:border-white/20 dark:placeholder:text-slate-400"
                    required
                  />
                </div>
                <div className="grid gap-4 sm:grid-cols-2">
                  <div className="space-y-2.5">
                    <Label htmlFor="register-password" className="text-sm font-medium">
                      Senha
                    </Label>
                    <Input
                      id="register-password"
                      type="password"
                      value={registerPassword}
                      onChange={(event) => setRegisterPassword(event.target.value)}
                      autoComplete="new-password"
                      className="h-11 rounded-xl border-border/50 bg-white px-4 shadow-none transition-colors placeholder:text-muted-foreground/75 hover:border-border/70 focus-visible:border-primary/35 focus-visible:ring-2 focus-visible:ring-primary/8 dark:border-white/10 dark:bg-white/4 dark:hover:border-white/20 dark:placeholder:text-slate-400"
                      required
                    />
                  </div>
                  <div className="space-y-2.5">
                    <Label htmlFor="register-confirm-password" className="text-sm font-medium">
                      Confirmar senha
                    </Label>
                    <Input
                      id="register-confirm-password"
                      type="password"
                      value={registerConfirmPassword}
                      onChange={(event) => setRegisterConfirmPassword(event.target.value)}
                      autoComplete="new-password"
                      className="h-11 rounded-xl border-border/50 bg-white px-4 shadow-none transition-colors placeholder:text-muted-foreground/75 hover:border-border/70 focus-visible:border-primary/35 focus-visible:ring-2 focus-visible:ring-primary/8 dark:border-white/10 dark:bg-white/4 dark:hover:border-white/20 dark:placeholder:text-slate-400"
                      required
                    />
                  </div>
                </div>
                <p className="text-xs text-muted-foreground">
                  A senha deve ter pelo menos {MIN_PASSWORD_LENGTH} caracteres.
                </p>
                <Button type="submit" className="mt-3 h-11 w-full rounded-xl bg-[linear-gradient(180deg,var(--primary-red),var(--primary-red-hover))] font-semibold shadow-[0_8px_16px_rgba(180,35,47,0.18)] hover:brightness-[1.02]" disabled={loading}>
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
