import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  Link,
  Outlet,
  createRootRouteWithContext,
  redirect,
  useRouter,
  useRouterState,
} from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { AppSidebar } from "../components/AppSidebar";
import { isAuthenticated, redirectToLogin } from "../lib/auth";
import { cn } from "../lib/utils";

const KPI_CARD_BASE_WIDTH_PX = 248;
const KPI_CARD_MIN_ZOOM_COMPENSATION = 1;
const KPI_CARD_MAX_ZOOM_COMPENSATION = 3;
const KPI_CARD_ZOOM_PRECISION = 4;

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
  beforeLoad: ({ location }) => {
    if (location.pathname === "/login") return;

    if (!isAuthenticated()) {
      throw redirect({
        to: "/login",
        search: { redirect: `${location.pathname}${location.searchStr}` },
      });
    }
  },
  component: RootComponent,
  notFoundComponent: NotFoundComponent,
  errorComponent: ErrorComponent,
});

function RootComponent() {
  const { queryClient } = Route.useRouteContext();
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [theme, setTheme] = useState<"light" | "dark">("light");
  const isLoginRoute = pathname === "/login";
  const [authenticated, setAuthenticated] = useState<boolean>(() => isAuthenticated());
  const canRenderPrivateApp = useMemo(
    () => !isLoginRoute && authenticated,
    [authenticated, isLoginRoute],
  );

  useEffect(() => {
    document.title = "AAI Seguri - ERP Corporativo";
    document.documentElement.lang = "pt-BR";
  }, []);

  useEffect(() => {
    const baselineDevicePixelRatio = window.devicePixelRatio || KPI_CARD_MIN_ZOOM_COMPENSATION;
    const rootStyle = document.documentElement.style;

    function updateMetricCardZoomCompensation() {
      const currentDevicePixelRatio = window.devicePixelRatio || baselineDevicePixelRatio;
      const zoomFactor = Math.min(
        KPI_CARD_MAX_ZOOM_COMPENSATION,
        Math.max(KPI_CARD_MIN_ZOOM_COMPENSATION, currentDevicePixelRatio / baselineDevicePixelRatio),
      );
      const metricCardZoom = KPI_CARD_MIN_ZOOM_COMPENSATION / zoomFactor;
      const metricCardColumnWidth = KPI_CARD_BASE_WIDTH_PX / zoomFactor;

      rootStyle.setProperty("--metric-card-zoom", metricCardZoom.toFixed(KPI_CARD_ZOOM_PRECISION));
      rootStyle.setProperty("--metric-card-column-width", `${metricCardColumnWidth.toFixed(2)}px`);
    }

    updateMetricCardZoomCompensation();
    window.addEventListener("resize", updateMetricCardZoomCompensation);
    window.visualViewport?.addEventListener("resize", updateMetricCardZoomCompensation);

    return () => {
      window.removeEventListener("resize", updateMetricCardZoomCompensation);
      window.visualViewport?.removeEventListener("resize", updateMetricCardZoomCompensation);
      rootStyle.removeProperty("--metric-card-zoom");
      rootStyle.removeProperty("--metric-card-column-width");
    };
  }, []);

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
      <div className="app-background min-h-screen font-body text-foreground">
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
