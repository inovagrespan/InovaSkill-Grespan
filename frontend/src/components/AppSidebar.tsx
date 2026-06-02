import { Link, useRouterState } from "@tanstack/react-router";
import { Activity, BarChart3, ChevronLeft, ChevronRight, FileUp, LayoutDashboard, LogOut, Menu, Moon, SlidersHorizontal, Sun, TrendingUp, Truck, Users } from "lucide-react";
import { useState } from "react";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { logout } from "@/lib/auth";
import { cn } from "@/lib/utils";

type AppSidebarProps = {
  collapsed: boolean;
  onToggleCollapsed: () => void;
  theme: "light" | "dark";
  onToggleTheme: () => void;
};

const items = [
  { to: "/", label: "Dashboard", icon: LayoutDashboard },
  { to: "/vendas", label: "Vendas", icon: TrendingUp },
  { to: "/clientes", label: "Clientes", icon: Users },
  { to: "/relatorios", label: "Relatorios", icon: BarChart3 },
  { to: "/logistica", label: "Logística", icon: Truck },
  { to: "/rh", label: "RH Atual", icon: Users },
  {
    to: "/importacoes",
    label: "Importações",
    icon: FileUp,
    children: [
      { to: "/importacoes/files", label: "Files", icon: FileUp },
      { to: "/importacoes/templates", label: "Templates", icon: SlidersHorizontal },
    ],
  },
  { to: "/simulacao", label: "Simulação", icon: Activity },
];

export function AppSidebar({ collapsed, onToggleCollapsed, theme, onToggleTheme }: AppSidebarProps) {
  const pathname = useRouterState({ select: (r) => r.location.pathname });
  const [mobileOpen, setMobileOpen] = useState(false);

  function isItemActive(to: string): boolean {
    if (to === "/importacoes") return pathname === "/importacoes" || pathname.startsWith("/importacoes/");
    return pathname === to || (to !== "/" && pathname.startsWith(`${to}/`));
  }

  function renderNav(showCollapsed: boolean, onNavigate?: () => void) {
    return (
      <nav className="flex-1 space-y-2 px-3">
        {items.map((item) => {
          const active = isItemActive(item.to);
          const Icon = item.icon;
          const topLevelLink = (
            <Link
              to={item.to}
              aria-label={item.label}
              onClick={onNavigate}
              className={cn(
                "group flex items-center rounded-lg border px-3 py-2.5 text-sm transition-all duration-200 motion-reduce:transition-none",
                "outline-none ring-primary/40 focus-visible:ring-2",
                showCollapsed ? "justify-center" : "gap-3",
                active
                  ? "border-primary/25 bg-[linear-gradient(90deg,rgba(180,35,47,0.14),rgba(180,35,47,0.05))] text-foreground shadow-[inset_0_0_0_1px_rgba(180,35,47,0.2)]"
                  : "border-transparent text-muted-foreground hover:border-border hover:bg-muted/60 hover:text-foreground",
              )}
            >
              <span className={cn("inline-flex size-7 items-center justify-center rounded-md transition-colors", active ? "bg-[var(--soft-red-background)] text-primary" : "bg-muted/50 text-muted-foreground group-hover:text-foreground")}>
                <Icon className="size-4 shrink-0" />
              </span>
              <span
                className={cn(
                  "whitespace-nowrap text-sm font-medium transition-all duration-200 motion-reduce:transition-none",
                  showCollapsed ? "pointer-events-none w-0 -translate-x-1 opacity-0" : "w-auto translate-x-0 opacity-100",
                )}
                aria-hidden={showCollapsed}
              >
                {item.label}
              </span>
            </Link>
          );

          return (
            <div key={item.to} className="space-y-1">
              {showCollapsed ? (
                <Tooltip>
                  <TooltipTrigger asChild>{topLevelLink}</TooltipTrigger>
                  <TooltipContent side="right">{item.label}</TooltipContent>
                </Tooltip>
              ) : (
                topLevelLink
              )}

              {!showCollapsed && item.children?.map((child) => {
                const childActive = pathname === child.to;
                const ChildIcon = child.icon;
                return (
                  <Link
                    key={child.to}
                    to={child.to}
                    aria-label={child.label}
                    onClick={onNavigate}
                    className={cn(
                      "ml-6 flex items-center gap-2 rounded-md px-2.5 py-2 text-xs transition-colors",
                      "outline-none ring-primary/40 focus-visible:ring-2",
                      childActive
                        ? "border border-primary/20 bg-primary/10 text-foreground"
                        : "text-muted-foreground hover:bg-muted/50 hover:text-foreground",
                    )}
                  >
                    <ChildIcon className="size-3.5 shrink-0" />
                    <span className="font-medium">{child.label}</span>
                  </Link>
                );
              })}
            </div>
          );
        })}
      </nav>
    );
  }

  return (
    <TooltipProvider delayDuration={120}>
      <div className="fixed left-3 top-3 z-30 md:hidden">
        <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
          <SheetTrigger asChild>
            <button
              type="button"
              aria-label="Abrir menu lateral"
              className="inline-flex size-10 items-center justify-center rounded-md border border-border bg-surface text-foreground shadow-sm outline-none ring-primary/40 focus-visible:ring-2"
            >
              <Menu className="size-5" />
            </button>
          </SheetTrigger>
          <SheetContent side="left" className="w-[280px] border-border bg-surface p-0">
            <SheetHeader className="border-b border-border px-4 py-4 text-left">
              <SheetTitle>Menu</SheetTitle>
            </SheetHeader>
            <div className="flex h-full flex-col py-3">
              {renderNav(false, () => setMobileOpen(false))}
              <div className="mt-auto px-3 pb-3">
                <button
                  type="button"
                  onClick={onToggleTheme}
                  className="inline-flex w-full items-center justify-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted/60 hover:text-foreground"
                >
                  {theme === "dark" ? <Sun className="size-4" /> : <Moon className="size-4" />}
                  <span>{theme === "dark" ? "Modo claro" : "Modo escuro"}</span>
                </button>
                <button
                  type="button"
                  onClick={logout}
                  className="mt-2 inline-flex w-full items-center justify-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted/60 hover:text-foreground"
                >
                  <LogOut className="size-4" />
                  <span>Sair</span>
                </button>
              </div>
            </div>
          </SheetContent>
        </Sheet>
      </div>

      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-20 hidden border-r border-border bg-[linear-gradient(180deg,rgba(255,255,255,0.98),rgba(249,250,252,0.98))] shadow-sm dark:bg-[linear-gradient(180deg,rgba(21,27,36,0.98),rgba(16,21,30,0.98))] md:flex md:flex-col",
          "transition-[width] duration-200 ease-out motion-reduce:transition-none",
          collapsed ? "md:w-[72px]" : "md:w-[264px]",
        )}
        aria-label="Navegação principal"
      >
        <div className={cn("mb-5 flex items-center border-b border-border px-3 py-4", collapsed ? "justify-center" : "justify-between")}>
          <Link to="/" className={cn("flex items-center gap-2 rounded-md outline-none ring-primary/40 focus-visible:ring-2", collapsed && "justify-center")}>
            <div className="flex size-8 items-center justify-center rounded-sm bg-primary font-display font-bold text-primary-foreground">
              N
            </div>
            <span
              className={cn(
                "font-display text-xl tracking-tight transition-all duration-200 motion-reduce:transition-none",
                collapsed ? "pointer-events-none w-0 -translate-x-1 opacity-0" : "w-auto translate-x-0 opacity-100",
              )}
              aria-hidden={collapsed}
            >
              GRESPAN
            </span>
          </Link>

          <button
            type="button"
            onClick={onToggleCollapsed}
            aria-label={collapsed ? "Expandir menu lateral" : "Recolher menu lateral"}
            className={cn(
              "inline-flex size-8 items-center justify-center rounded-md border border-border bg-surface text-muted-foreground transition-colors hover:bg-muted/60 hover:text-foreground",
              "outline-none ring-primary/40 focus-visible:ring-2",
              collapsed && "absolute -right-3 top-5 bg-background",
            )}
          >
            {collapsed ? <ChevronRight className="size-4" /> : <ChevronLeft className="size-4" />}
          </button>
        </div>

        {renderNav(collapsed)}
        <div className="mt-auto space-y-2 p-3">
          <button
            type="button"
            onClick={onToggleTheme}
            aria-label={theme === "dark" ? "Ativar modo claro" : "Ativar modo escuro"}
            className={cn(
              "inline-flex w-full items-center rounded-lg border border-border bg-surface px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted/60 hover:text-foreground",
              collapsed ? "justify-center" : "justify-start gap-2",
            )}
          >
            {theme === "dark" ? <Sun className="size-4" /> : <Moon className="size-4" />}
            {!collapsed ? <span>{theme === "dark" ? "Modo claro" : "Modo escuro"}</span> : null}
          </button>
          <button
            type="button"
            onClick={logout}
            aria-label="Sair"
            className={cn(
              "inline-flex w-full items-center rounded-lg border border-border bg-surface px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted/60 hover:text-foreground",
              collapsed ? "justify-center" : "justify-start gap-2",
            )}
          >
            <LogOut className="size-4" />
            {!collapsed ? <span>Sair</span> : null}
          </button>
        </div>
      </aside>
    </TooltipProvider>
  );
}
