import { Link, useRouterState } from "@tanstack/react-router";
import {
  BarChart3,
  BellRing,
  Building2,
  CalendarCheck,
  ChevronLeft,
  ChevronRight,
  Factory,
  LayoutDashboard,
  LogOut,
  Menu,
  Moon,
  Settings,
  Sun,
  Truck,
  UserRound,
} from "lucide-react";
import { useState } from "react";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { getCurrentUser, getCurrentUserRole, logout, normalizeUserRole } from "@/lib/auth";
import { cn } from "@/lib/utils";

type AppSidebarProps = {
  collapsed: boolean;
  onToggleCollapsed: () => void;
  theme: string;
  onToggleTheme: () => void;
};

type SidebarAccessRole = "vendas" | "logistica" | "producao" | "administrativo" | "reuniao" | "diretor" | "admin" | "admin_system";

const roleLabels: Record<string, string> = {
  admin: "Sistema",
  admin_system: "Sistema",
  administrativo: "Administrativo da Empresa",
  diretor: "Diretor",
  logistica: "Logística",
  producao: "Produção",
  reuniao: "Reuniões",
  vendas: "Vendas",
};

const items = [
  { to: "/", label: "Dashboard", icon: LayoutDashboard },
  { to: "/alertas", label: "Alertas", icon: BellRing, accessRoles: ["vendas", "logistica", "producao", "administrativo", "reuniao", "admin", "admin_system"] },
  { to: "/vendas", label: "Vendas", icon: BarChart3, accessRoles: ["vendas"] },
  { to: "/logistica", label: "Logística", icon: Truck, accessRoles: ["logistica"] },
  { to: "/produtos", label: "Produção", icon: Factory, accessRoles: ["producao"] },
  { to: "/processamentos", label: "Processamento", icon: Settings, accessRoles: ["admin_system", "admin"] },
  { to: "/administrativo", label: "Administrativo", icon: Building2, accessRoles: ["administrativo"] },
  { to: "/reunioes", label: "Reuniões", icon: CalendarCheck, accessRoles: ["diretor", "reuniao"] },
] as const;

export function getVisibleSidebarItemsForRole(role: string | null) {
  const normalizedRole = normalizeUserRole(role);
  const canSeeAll = normalizedRole === "admin_system";

  return items.filter((item) => {
    if (!("accessRoles" in item)) return true;
    if (normalizedRole === "diretor") return item.to !== "/processamentos";
    return canSeeAll || item.accessRoles.includes(normalizedRole as SidebarAccessRole);
  });
}

function formatUserRole(role: string | null): string {
  return roleLabels[role ?? ""] ?? "Usuário";
}

export function AppSidebar({ collapsed, onToggleCollapsed, theme, onToggleTheme }: AppSidebarProps) {
  const pathname = useRouterState({ select: (r) => r.location.pathname });
  const [mobileOpen, setMobileOpen] = useState(false);
  const currentUser = getCurrentUser();
  const currentRole = getCurrentUserRole();
  const visibleItems = getVisibleSidebarItemsForRole(currentRole);
  const userName = currentUser?.name?.trim() || currentUser?.email?.trim() || "User";
  const userRoleLabel = formatUserRole(currentRole);

  function isItemActive(to: string): boolean {
    if (to === "/") return pathname === "/";
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
              <span
                className={cn(
                  "inline-flex size-7 shrink-0 items-center justify-center rounded-md transition-colors",
                  active ? "bg-primary/10 text-primary" : "bg-muted/50 text-muted-foreground group-hover:text-foreground",
                )}
              >
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

  function renderUserHeader(compact = collapsed) {
    if (compact) {
      return (
        <Link
          to="/"
          aria-label={userName}
          className="flex size-10 shrink-0 items-center justify-center rounded-full border border-border bg-primary/10 text-primary outline-none ring-primary/40 focus-visible:ring-2"
        >
          <UserRound className="size-4" />
        </Link>
      );
    }

    return (
      <Link
        to="/"
        className="flex min-w-0 flex-1 items-center gap-2 rounded-md outline-none ring-primary/40 focus-visible:ring-2"
      >
        <span className="flex size-9 shrink-0 items-center justify-center rounded-full border border-border bg-primary/10 text-primary">
          <UserRound className="size-4" />
        </span>
        <span className="min-w-0">
          <span className="block truncate text-sm font-semibold text-foreground">{userName}</span>
          <span className="block truncate text-xs text-muted-foreground">{userRoleLabel}</span>
        </span>
      </Link>
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
              <div className="mb-3 border-b border-border px-3 pb-3">{renderUserHeader(false)}</div>
              {renderNav(false, () => setMobileOpen(false))}
              <div className="shrink-0 space-y-2 border-t border-border bg-surface px-3 pb-3 pt-3">
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

      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-20 hidden border-r border-border bg-surface md:flex md:flex-col",
          "transition-[width] duration-200 ease-out motion-reduce:transition-none",
          collapsed ? "md:w-[76px]" : "md:w-[264px]",
        )}
        aria-label="Navegação principal"
      >
        <div className={cn("mb-2 flex border-b border-border px-3 py-3.5", collapsed ? "flex-col items-center gap-2" : "items-center gap-2")}>
          {renderUserHeader()}
          <div className={cn("flex shrink-0 items-center", collapsed ? "flex-col gap-2" : "gap-1")}>
            <button
              onClick={onToggleCollapsed}
              className="inline-flex size-9 items-center justify-center rounded-md border border-transparent text-muted-foreground outline-none ring-primary/40 transition-colors hover:border-border hover:bg-muted/70 hover:text-foreground focus-visible:ring-2"
              aria-label={collapsed ? "Expandir sidebar" : "Recolher sidebar"}
            >
              {collapsed ? <ChevronRight className="size-4" /> : <ChevronLeft className="size-4" />}
            </button>
          </div>
        </div>

        {renderNav(collapsed)}

        <div className={cn("shrink-0 border-t border-border bg-surface px-3 pb-3 pt-3", collapsed ? "flex flex-col items-center gap-2" : "space-y-2")}>
          <button
            onClick={onToggleTheme}
            className={cn(
              "inline-flex items-center justify-center gap-2 rounded-md border border-border bg-surface text-sm text-muted-foreground transition-colors hover:bg-muted/60 hover:text-foreground",
              collapsed ? "size-10 p-0" : "h-10 w-full px-3",
            )}
            aria-label={theme === "dark" ? "Modo claro" : "Modo escuro"}
          >
            {theme === "dark" ? <Sun className="size-4" /> : <Moon className="size-4" />}
            {!collapsed && <span>{theme === "dark" ? "Modo claro" : "Modo escuro"}</span>}
          </button>
          <button
            onClick={logout}
            className={cn(
              "inline-flex items-center justify-center gap-2 rounded-md border border-border bg-surface text-sm text-muted-foreground transition-colors hover:bg-muted/60 hover:text-foreground",
              collapsed ? "size-10 p-0" : "h-10 w-full px-3",
            )}
            aria-label="Sair"
          >
            <LogOut className="size-4" />
            {!collapsed && <span>Sair</span>}
          </button>
        </div>
      </aside>
    </TooltipProvider>
  );
}
