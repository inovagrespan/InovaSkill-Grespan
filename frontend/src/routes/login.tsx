import { createFileRoute, Link } from "@tanstack/react-router";
import { LogIn, Moon, Sun } from "lucide-react";
import { FormEvent, useEffect, useState } from "react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { BrandLogo } from "@/components/BrandLogo";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { login } from "@/lib/auth";

export const Route = createFileRoute("/login")({
  validateSearch: (search: Record<string, unknown>) => ({
    redirect: typeof search.redirect === "string" ? search.redirect : undefined,
  }),
  component: LoginPage,
});

type Message = { type: "success" | "error"; text: string } | null;

const DEFAULT_LOGIN_USER = "diretor";
const DEFAULT_LOGIN_PASSWORD = "diretor";

function LoginPage() {
  const search = Route.useSearch();
  const [theme, setTheme] = useState<"light" | "dark">("dark");
  const [loginUser, setLoginUser] = useState(DEFAULT_LOGIN_USER);
  const [loginPassword, setLoginPassword] = useState(DEFAULT_LOGIN_PASSWORD);
  const [message, setMessage] = useState<Message>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const savedTheme = window.localStorage.getItem("app.theme");
    if (savedTheme === "dark" || savedTheme === "light") {
      setTheme(savedTheme);
      return;
    }

    setTheme("dark");
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
      window.location.assign(search.redirect || "/dashboard");
    } catch (error) {
      setMessage({ type: "error", text: error instanceof Error ? error.message : "Não foi possível entrar." });
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="relative flex min-h-dvh items-center justify-center bg-[#eef1f6] px-4 py-6 font-body text-foreground dark:bg-[#0d1117]">
      <button
        type="button"
        onClick={() => setTheme((current) => (current === "dark" ? "light" : "dark"))}
        className="absolute right-4 top-4 z-20 inline-flex size-9 items-center justify-center rounded-lg border border-border/50 bg-surface text-muted-foreground shadow-sm transition-colors hover:bg-muted/60 hover:text-foreground"
        aria-label={theme === "dark" ? "Ativar modo claro" : "Ativar modo escuro"}
      >
        {theme === "dark" ? <Sun className="size-4" /> : <Moon className="size-4" />}
      </button>

      <main className="relative w-full max-w-md rounded-2xl border border-border/50 bg-surface p-6 shadow-lg sm:p-8">
        <div className="mb-6 text-center">
          <BrandLogo markClassName="size-10" textClassName="text-lg" taglineClassName="hidden" />
          <h1 className="mt-4 font-display text-xl font-bold tracking-tight">Acesso seguro</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Entre para acompanhar indicadores, reuniões e ações.
          </p>
        </div>

        <form className="space-y-4" onSubmit={handleLogin} noValidate>
          <div className="space-y-1.5">
            <Label htmlFor="login-user" className="text-sm font-medium">
              Usuário/e-mail
            </Label>
            <Input
              id="login-user"
              value={loginUser}
              onChange={(event) => setLoginUser(event.target.value)}
              autoComplete="username"
              placeholder={DEFAULT_LOGIN_USER}
              className="h-10 rounded-lg border-border/50 bg-white px-3 shadow-none transition-colors placeholder:text-muted-foreground/60 hover:border-border focus-visible:border-[#d01825]/40 focus-visible:ring-2 focus-visible:ring-[#d01825]/10 dark:border-white/10 dark:bg-white/4 dark:hover:border-white/20 dark:placeholder:text-slate-400"
              required
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="login-password" className="text-sm font-medium">
              Senha
            </Label>
            <Input
              id="login-password"
              type="password"
              value={loginPassword}
              onChange={(event) => setLoginPassword(event.target.value)}
              autoComplete="current-password"
              placeholder={DEFAULT_LOGIN_PASSWORD}
              className="h-10 rounded-lg border-border/50 bg-white px-3 shadow-none transition-colors placeholder:text-muted-foreground/60 hover:border-border focus-visible:border-[#d01825]/40 focus-visible:ring-2 focus-visible:ring-[#d01825]/10 dark:border-white/10 dark:bg-white/4 dark:hover:border-white/20 dark:placeholder:text-slate-400"
              required
            />
          </div>

          <div className="flex items-center justify-center gap-1.5 rounded-lg border border-[#d01825]/15 bg-[#d01825]/5 px-3 py-2 text-xs dark:border-[#d01825]/25 dark:bg-[#d01825]/10">
            <span className="size-1.5 rounded-full bg-emerald-500" />
            <span className="text-muted-foreground">
              <strong className="font-semibold text-foreground">diretor</strong>
              {" / "}
              <strong className="font-semibold text-foreground">diretor</strong>
            </span>
          </div>

          <Button type="submit" className="h-10 w-full rounded-lg bg-[#d01825] font-semibold text-white shadow-sm transition-all duration-200 hover:bg-[#b91420] active:scale-[0.98] disabled:opacity-60" disabled={loading}>
            <LogIn className="size-4" />
            {loading ? "Entrando..." : "Entrar"}
          </Button>

          {message ? (
            <Alert className="rounded-lg py-2" variant={message.type === "error" ? "destructive" : "default"}>
              <AlertTitle className="text-sm">{message.type === "error" ? "Atenção" : "Tudo certo"}</AlertTitle>
              <AlertDescription className="text-xs">{message.text}</AlertDescription>
            </Alert>
          ) : null}
        </form>

        <p className="mt-6 text-center text-xs text-muted-foreground">
          <Link to="/" className="text-[#d01825] hover:underline">Conhecer o Conecta360</Link>
        </p>
      </main>
    </div>
  );
}
