import { Link, createFileRoute } from "@tanstack/react-router";
import { BarChart3, Route as RouteIcon } from "lucide-react";
import { LogisticsRoutesBoard } from "@/components/logistics-routes-board";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

export const Route = createFileRoute("/logistica/rotas")({ component: LogisticaRotasPage });

function LogisticaRotasPage() {
  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <span className="page-header-kicker">Logística / Rotas</span>
            <Badge variant="outline">Base demonstrativa</Badge>
          </div>
          <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">Rotas</h1>
          <p className="mt-1 text-sm text-muted-foreground">Visualize trajetos, clientes atendidos e atrasos por congestionamento.</p>
        </div>
        <nav className="flex flex-wrap gap-2" aria-label="Abas de logística">
          <Button variant="outline" asChild>
            <Link to="/logistica">
              <BarChart3 className="mr-2 size-4" />
              Métricas
            </Link>
          </Button>
          <Button asChild>
            <Link to="/logistica/rotas">
              <RouteIcon className="mr-2 size-4" />
              Rotas
            </Link>
          </Button>
        </nav>
      </header>

      <LogisticsRoutesBoard />
    </div>
  );
}
