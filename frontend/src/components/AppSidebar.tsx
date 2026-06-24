import { Link, useRouterState } from "@tanstack/react-router";
import { Activity, BarChart3, ChevronLeft, ChevronRight, FileUp, LayoutDashboard, LogOut, Menu, Moon, ServerCog, Sun, TrendingUp, Truck, Users } from "lucide-react";
import { useState } from "react";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { isCurrentUserAdmin, logout } from "@/lib/auth";
import { cn } from "@/lib/utils";

type AppSidebarProps = {
  collapsed: boolean;
  onToggleCollapsed: () => void;
  theme: string;
  onToggleTheme: () => void;
};

const items = [
  { to: "/", label: "Dashboard", icon: LayoutDashboard },
  { to: "/vendas", label: "Vendas", icon: TrendingUp },
  { to: "/clientes", label: "Finanças", icon: Users },
  { to: "/processamentos", label: "Processamentos", icon: ServerCog, adminOnly: true },
  { to: "/relatorios", label: "Relatórios", icon: BarChart3 },
  { to: "/logistica", label: "Logística", icon: Truck },
  { to: "/importacoes", label: "Importações", icon: FileUp },
  { to: "/simulacao", label: "Simulação", icon: Activity },
];

export function AppSidebar({ collapsed, onToggleCollapsed, theme, onToggleTheme }: AppSidebarProps) {
  const pathname = useRouterState({ select: (r) => r.location.pathname });
  const [mobileOpen, setMobileOpen] = useState(false);
  const visibleItems = items.filter((item) => !item.adminOnly || isCurrentUserAdmin());

  function isItemActive(to: string): boolean {
    if (to === "/") return pathname === "/";
    if (to === "/importacoes") return pathname.startsWith("/importacoes");
    return pathname === to || pathname.startsWith(`${to}/`);
  }

  function renderNav(showCollapsed: boolean, onNavigate?: () => void) {
    return (
      <nav className="custom-scrollbar min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-3 pb-2 space-y-1">
        {visibleItems.map((item) => {
          const active = isItemActive(item.to);
          const Icon = item.icon;
          const link = (
            <Link
              to={item.to}
              aria-label={item.label}
              onClick={onNavigate}
              className={cn(
                "group flex items-center rounded-lg border px-3 py-2.5 text-sm transition-all duration-200",
                "outline-none ring-primary/40 focus-visible:ring-2",
                showCollapsed ? "justify-center" : "gap-3",
                active
                  ? "border-primary/20 bg-primary/5 text-foreground"
                  : "border-transparent text-muted-foreground hover:border-border hover:bg-muted/60 hover:text-foreground",
              )}
            >
              <span className={cn(
                "inline-flex size-7 items-center justify-center rounded-md transition-colors shrink-0",
                active ? "bg-primary/10 text-primary" : "bg-muted/50 text-muted-foreground group-hover:text-foreground"
              )}>
                <Icon className="size-4" />
              </span>
              <span
                className={cn(
                  "whitespace-nowrap text-sm font-medium transition-all duration-200",
                  showCollapsed ? "pointer-events-none w-0 -translate-x-1 opacity-0" : "w-auto translate-x-0 opacity-100",
                )}
                aria-hidden={showCollapsed}
              >
                <span>{item.label}</span>
              </span>
            </Link>
          );

          return (
            <div key={item.to}>
              {showCollapsed ? (
                <Tooltip>
                  <TooltipTrigger asChild>{link}</TooltipTrigger>
                  <TooltipContent side="right" className="text-xs">{item.label}</TooltipContent>
                </Tooltip>
              ) : link}
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
            <button type="button" aria-label="Abrir menu" className="inline-flex size-10 items-center justify-center rounded-md border border-border bg-surface text-foreground shadow-sm outline-none ring-primary/40 focus-visible:ring-2">
              <Menu className="size-5" />
            </button>
          </SheetTrigger>
          <SheetContent side="left" className="w-[280px] border-border bg-surface p-0">
            <SheetHeader className="border-b border-border px-4 py-4 text-left">
              <SheetTitle>Navegação</SheetTitle>
            </SheetHeader>
            <div className="flex h-full min-h-0 flex-col py-3">
              {renderNav(false, () => setMobileOpen(false))}
              <div className="shrink-0 border-t border-border bg-surface px-3 pt-3 pb-3 space-y-2">
                <button onClick={onToggleTheme} className="inline-flex w-full items-center justify-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted/60">
                  {theme === "dark" ? <Sun className="size-4" /> : <Moon className="size-4" />}
                  <span>{theme === "dark" ? "Modo claro" : "Modo escuro"}</span>
                </button>
                <button onClick={logout} className="inline-flex w-full items-center justify-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted/60">
                  <LogOut className="size-4" />
                  <span>Sair</span>
                </button>
              </div>
            </div>
          </SheetContent>
        </Sheet>
      </div>

      <aside className={cn(
        "fixed inset-y-0 left-0 z-20 hidden border-r border-border bg-surface md:flex md:flex-col",
        "transition-[width] duration-200 ease-out motion-reduce:transition-none",
        collapsed ? "md:w-[72px]" : "md:w-[264px]",
      )} aria-label="Navegação principal">
        <div className={cn("mb-3 flex items-center border-b border-border px-3 py-4", collapsed ? "justify-center" : "justify-between")}>
          <Link to="/" className={cn("flex items-center gap-2 rounded-md outline-none ring-primary/40 focus-visible:ring-2", collapsed && "justify-center")}>
            <div className="flex size-8 items-center justify-center rounded-sm bg-primary font-display font-bold text-primary-foreground">N</div>
            <span className={cn("font-display text-xl tracking-tight transition-all duration-200", collapsed ? "pointer-events-none w-0 -translate-x-1 opacity-0" : "w-auto translate-x-0 opacity-100")} aria-hidden={collapsed}>GRESPAN</span>
          </Link>
          <button onClick={onToggleCollapsed} className={cn("rounded-md p-1 text-muted-foreground hover:bg-muted/80", collapsed && "hidden")} aria-label="Recolher sidebar">
            <ChevronLeft className="size-4" />
          </button>
        </div>

        {renderNav(collapsed)}

        <div className={cn("shrink-0 border-t border-border bg-surface px-3 pt-3 pb-3 space-y-2", collapsed && "flex flex-col items-center")}>
          <button onClick={onToggleTheme} className={cn(
            "inline-flex items-center justify-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted/60",
            collapsed ? "w-10 h-10" : "w-full"
          )}>
            {theme === "dark" ? <Sun className="size-4" /> : <Moon className="size-4" />}
            {!collapsed && <span>{theme === "dark" ? "Modo claro" : "Modo escuro"}</span>}
          </button>
          <button onClick={logout} className={cn(
            "inline-flex items-center justify-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-muted/60",
            collapsed ? "w-10 h-10" : "w-full"
          )}>
            <LogOut className="size-4" />
            {!collapsed && <span>Sair</span>}
          </button>
        </div>
      </aside>
    </TooltipProvider>
  );
}
