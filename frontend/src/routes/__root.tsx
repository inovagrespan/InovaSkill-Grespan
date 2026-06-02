import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  Outlet,
  Link,
  createRootRouteWithContext,
  useRouter,
  HeadContent,
  Scripts,
  redirect,
  useRouterState,
} from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";

import appCss from "../styles.css?url";
import { AppSidebar } from "../components/AppSidebar";
import { isAuthenticated, redirectToLogin } from "../lib/auth";
import { cn } from "../lib/utils";

function NotFoundComponent() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <div className="max-w-md text-center">
        <h1 className="text-7xl font-display font-bold text-primary">404</h1>
        <h2 className="mt-4 text-xl font-semibold">Página não encontrada</h2>
        <p className="mt-2 text-sm text-muted-foreground">
          O recurso solicitado não existe no AAI Seguri.
        </p>
        <div className="mt-6">
          <Link
            to="/"
            className="inline-flex items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-bold text-primary-foreground"
          >
            Voltar ao Dashboard
          </Link>
        </div>
      </div>
    </div>
  );
}

function ErrorComponent({ error, reset }: { error: Error; reset: () => void }) {
  console.error(error);
  const router = useRouter();
  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <div className="max-w-md text-center">
        <h1 className="text-xl font-semibold">Algo deu errado</h1>
        <p className="mt-2 text-sm text-muted-foreground">{error.message}</p>
        <button
          onClick={() => {
            router.invalidate();
            reset();
          }}
          className="mt-6 rounded-md bg-primary px-4 py-2 text-sm font-bold text-primary-foreground"
        >
          Tentar novamente
        </button>
      </div>
    </div>
  );
}

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  head: () => ({
    meta: [
      { charSet: "utf-8" },
      { name: "viewport", content: "width=device-width, initial-scale=1" },
      { title: "AAI Seguri - ERP Corporativo" },
      {
        name: "description",
        content:
          "Plataforma corporativa para gestão integrada de Vendas, Logística, RH e Importações.",
      },
    ],
    links: [
      { rel: "stylesheet", href: appCss },
      { rel: "preconnect", href: "https://fonts.googleapis.com" },
      { rel: "preconnect", href: "https://fonts.gstatic.com", crossOrigin: "anonymous" },
      {
        rel: "stylesheet",
        href: "https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@500;600;700&family=Inter:wght@400;500;600&family=JetBrains+Mono:wght@400;500&display=swap",
      },
    ],
  }),
  beforeLoad: ({ location }) => {
    if (typeof window === "undefined") return;

    if (location.pathname === "/login") return;

    if (!isAuthenticated()) {
      throw redirect({
        to: "/login",
        search: { redirect: `${location.pathname}${location.searchStr}` },
      });
    }
  },
  shellComponent: RootShell,
  component: RootComponent,
  notFoundComponent: NotFoundComponent,
  errorComponent: ErrorComponent,
});

function RootShell({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR">
      <head>
        <HeadContent />
      </head>
      <body>
        {children}
        <Scripts />
      </body>
    </html>
  );
}

function RootComponent() {
  const { queryClient } = Route.useRouteContext();
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [theme, setTheme] = useState<"light" | "dark">("light");
  const isLoginRoute = pathname === "/login";
  const [authenticated, setAuthenticated] = useState<boolean>(() => {
    if (typeof window === "undefined") return false;
    return isAuthenticated();
  });
  const canRenderPrivateApp = useMemo(
    () => !isLoginRoute && authenticated,
    [authenticated, isLoginRoute],
  );

  useEffect(() => {
    setAuthenticated(isAuthenticated());
  }, [pathname]);

  useEffect(() => {
    if (!isLoginRoute && !authenticated) {
      redirectToLogin();
    }
  }, [authenticated, isLoginRoute]);

  useEffect(() => {
    const saved = window.localStorage.getItem("app.sidebar.collapsed");
    if (saved === "1") setSidebarCollapsed(true);
    if (saved === "0") setSidebarCollapsed(false);

    const savedTheme = window.localStorage.getItem("app.theme");
    if (savedTheme === "dark" || savedTheme === "light") {
      setTheme(savedTheme);
      return;
    }

    const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
    setTheme(prefersDark ? "dark" : "light");
  }, []);

  useEffect(() => {
    window.localStorage.setItem("app.sidebar.collapsed", sidebarCollapsed ? "1" : "0");
  }, [sidebarCollapsed]);

  useEffect(() => {
    document.documentElement.classList.toggle("dark", theme === "dark");
    window.localStorage.setItem("app.theme", theme);
  }, [theme]);

  return (
    <QueryClientProvider client={queryClient}>
      <div className="app-background min-h-screen text-foreground font-body">
        {canRenderPrivateApp ? (
          <AppSidebar
            collapsed={sidebarCollapsed}
            onToggleCollapsed={() => setSidebarCollapsed((prev) => !prev)}
            theme={theme}
            onToggleTheme={() => setTheme((prev) => (prev === "dark" ? "light" : "dark"))}
          />
        ) : null}
        <main
          className={cn(
            "min-h-screen transition-[margin] duration-200 ease-out motion-reduce:transition-none",
            canRenderPrivateApp && (sidebarCollapsed ? "md:ml-[72px]" : "md:ml-[264px]"),
          )}
        >
          {isLoginRoute || authenticated ? <Outlet /> : null}
        </main>
      </div>
    </QueryClientProvider>
  );
}
