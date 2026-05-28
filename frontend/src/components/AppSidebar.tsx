import { Link, useRouterState } from "@tanstack/react-router";
import { LayoutDashboard, Users, Truck, Activity, TrendingUp, FileUp } from "lucide-react";

const items = [
  { to: "/", label: "Dashboard", icon: LayoutDashboard },
  { to: "/vendas", label: "Vendas", icon: TrendingUp },
  { to: "/logistica", label: "Logística", icon: Truck },
  { to: "/rh", label: "RH Atual", icon: Users },
  { to: "/importacoes", label: "Importações", icon: FileUp },
  { to: "/simulacao", label: "Simulação", icon: Activity },
];

export function AppSidebar() {
  const pathname = useRouterState({ select: (r) => r.location.pathname });

  return (
    <aside className="w-64 border-r border-border bg-surface flex flex-col fixed inset-y-0 left-0 z-20">
      <div className="p-6 mb-8">
        <Link to="/" className="flex items-center gap-2">
          <div className="size-8 bg-primary rounded-sm flex items-center justify-center text-primary-foreground font-bold font-display">
            N
          </div>
          <span className="font-display text-xl tracking-tight">
            NEXUS <span className="text-primary">AI</span>
          </span>
        </Link>
      </div>

      <nav className="flex-1 px-4 space-y-2">
        {items.map((item) => {
          const active = pathname === item.to;
          const Icon = item.icon;
          return (
            <Link
              key={item.to}
              to={item.to}
              className={
                "flex items-center gap-3 px-4 py-3 rounded-lg transition-colors " +
                (active
                  ? "text-foreground bg-white/5 border border-white/10"
                  : "text-muted-foreground hover:text-foreground hover:bg-white/[0.02]")
              }
            >
              <Icon className="size-4" />
              <span className="text-sm font-medium">{item.label}</span>
            </Link>
          );
        })}
      </nav>

      <div className="p-6 border-t border-border">
        <div className="bg-white/5 rounded p-3">
          <p className="text-[10px] uppercase tracking-widest text-muted-foreground mb-1">
            Status do Sistema
          </p>
          <p className="text-xs font-mono text-foreground">
            <span className="inline-block size-1.5 rounded-full bg-primary mr-2 animate-pulse" />
            PROD_CORE: ACTIVE
          </p>
        </div>
      </div>
    </aside>
  );
}
