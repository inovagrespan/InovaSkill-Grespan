import { Link, useRouterState } from "@tanstack/react-router";
import {
  ChevronLeft,
  ChevronRight,
  Factory,
  FileUp,
  FileText,
  LayoutDashboard,
  LogOut,
  Map,
  MessageCircle,
  Moon,
  Package,
  PackageCheck,
  Route,
  Settings,
  Sun,
  Truck,
  UserRound,
  Users,
} from "lucide-react";
import { useState } from "react";
import { BrandLogo } from "@/components/BrandLogo";
import { Sheet, SheetContent, SheetTrigger } from "@/components/ui/sheet";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { getCurrentUser, getCurrentUserRole, logout } from "@/lib/auth";
import { canRoleAccessPath } from "@/lib/access-control";
import { cn } from "@/lib/utils";

type AppSidebarProps = {
  collapsed: boolean;
  onToggleCollapsed: () => void;
  theme: string;
  onToggleTheme: () => void;
};

const roleLabels: Record<string, string> = {
  admin: "Sistema",
  admin_system: "Sistema",
  diretor: "Diretor",
  logistica: "Logística",
  vendas: "Vendas",
};

const items = [
  { to: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { to: "/assistente", label: "Chat IA", icon: MessageCircle },
  { to: "/logistica/rotas", label: "Rotas", icon: Route },
  { to: "/veiculos/tipos", label: "Tipos de Veículo", icon: Truck },
  { to: "/mapa", label: "Mapa", icon: Map },
  { to: "/clientes", label: "Clientes", icon: Users },
  { to: "/notas-fiscais", label: "Notas Fiscais", icon: FileText },
  { to: "/produtos", label: "Produtos", icon: Package },
  { to: "/estoque", label: "Estoque", icon: PackageCheck },
  { to: "/producao", label: "Produção", icon: Factory },
  { to: "/importacoes/files", label: "Importações", icon: FileUp },
  { to: "/processamentos", label: "Processamento", icon: Settings },
] as const;

export function getVisibleSidebarItemsForRole(role: string | null) {
  return items.filter((item) => canRoleAccessPath(role, item.to));
}

function formatUserRole(role: string | null): string {
  return roleLabels[role ?? ""] ?? "Usuário";
}

function SidebarToggleArrow({ open }: { open: boolean }) {
  const Icon = open ? ChevronLeft : ChevronRight;

  return (
    <span
      aria-hidden="true"
      data-sidebar-toggle-arrow={open ? "open" : "closed"}
      className="inline-flex transition-transform duration-300 ease-out motion-reduce:transition-none"
    >
      <Icon className="size-5" strokeWidth={2.4} />
    </span>
  );
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
          to="/dashboard"
          aria-label={userName}
          className="flex size-10 shrink-0 items-center justify-center rounded-full border border-border bg-primary/10 text-primary outline-none ring-primary/40 focus-visible:ring-2"
        >
          <UserRound className="size-4" />
        </Link>
      );
    }

    return (
      <Link
        to="/dashboard"
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

  function renderBrandHeader(compact = collapsed) {
    return (
      <Link
        to="/dashboard"
        aria-label="Conecta360"
        className={cn(
          "flex min-w-0 items-center rounded-md outline-none ring-primary/40 focus-visible:ring-2",
          compact ? "justify-center" : "flex-1",
        )}
      >
        <BrandLogo
          compact={compact}
          markClassName={compact ? "size-10" : "size-9"}
          textClassName="text-lg"
          taglineClassName="hidden"
        />
      </Link>
    );
  }

  return (
    <TooltipProvider delayDuration={120}>
      <div className="fixed left-3 top-3 z-30 md:hidden">
        <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
          <SheetTrigger asChild>
            <button
              type="button"
              aria-label={mobileOpen ? "Fechar menu" : "Abrir menu"}
              className="inline-flex size-10 items-center justify-center rounded-xl bg-surface/60 text-foreground shadow-lg shadow-black/10 backdrop-blur-lg outline-none ring-primary/40 transition-all duration-200 hover:bg-surface/85 focus-visible:ring-2"
            >
              <SidebarToggleArrow open={mobileOpen} />
            </button>
          </SheetTrigger>
          <SheetContent side="left" hideClose className="w-[280px] border-r border-border bg-surface p-0">
            <div className="flex h-dvh flex-col">
              <div className="shrink-0 border-b border-border px-4 py-4">
                <BrandLogo markClassName="size-9" textClassName="text-lg" taglineClassName="hidden" />
              </div>
              <div className="shrink-0 border-b border-border px-3 py-3">
                {renderUserHeader(false)}
              </div>
              {renderNav(false, () => setMobileOpen(false))}
              <div className="shrink-0 space-y-2 border-t border-border px-3 pb-4 pt-3">
                <button onClick={onToggleTheme} className="inline-flex w-full items-center justify-center gap-2 rounded-lg border border-border bg-surface px-3 py-2.5 text-sm text-muted-foreground transition-colors hover:bg-muted/60">
                  {theme === "dark" ? <Sun className="size-4" /> : <Moon className="size-4" />}
                  <span>{theme === "dark" ? "Modo claro" : "Modo escuro"}</span>
                </button>
                <button onClick={logout} className="inline-flex w-full items-center justify-center gap-2 rounded-lg border border-border bg-surface px-3 py-2.5 text-sm text-muted-foreground transition-colors hover:bg-muted/60">
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
          "fixed inset-y-0 left-0 z-40 hidden h-dvh border-r border-border bg-surface md:flex md:flex-col",
          "transition-[width] duration-300 ease-out motion-reduce:transition-none",
          collapsed ? "md:w-[76px]" : "md:w-[264px]",
        )}
        aria-label="Navegação principal"
      >
        <button
          onClick={onToggleCollapsed}
          className="absolute right-0 top-10 z-50 flex size-9 translate-x-1/2 items-center justify-center rounded-full border border-border bg-surface text-muted-foreground shadow-lg shadow-black/15 outline-none ring-primary/40 transition-[background-color,color,transform,box-shadow] duration-300 ease-out hover:bg-muted/80 hover:text-foreground hover:shadow-black/25 focus-visible:ring-2 motion-reduce:transition-none"
          aria-label={collapsed ? "Expandir sidebar" : "Recolher sidebar"}
          aria-expanded={!collapsed}
          title={collapsed ? "Expandir menu" : "Recolher menu"}
        >
          <SidebarToggleArrow open={!collapsed} />
        </button>

        <div
          className={cn(
            "mb-3 mt-4 flex shrink-0 items-center gap-3 px-3 pb-4",
            collapsed ? "justify-center" : "justify-between border-b border-border",
          )}
        >
          {renderUserHeader(collapsed)}
        </div>

        {renderNav(collapsed)}

        <div className={cn("sticky bottom-0 z-10 shrink-0 border-t border-border bg-surface px-3 pb-3 pt-3", collapsed ? "flex flex-col items-center gap-2" : "space-y-2")}>
          {renderBrandHeader()}
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
