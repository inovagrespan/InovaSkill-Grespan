import { createFileRoute } from "@tanstack/react-router";
import { LogisticsRoutesBoard } from "@/components/logistics-routes-board";
import { Badge } from "@/components/ui/badge";

export const Route = createFileRoute("/rotas")({ component: RotasPage });

function RotasPage() {
  return (
    <div className="page-shell app-background space-y-6">
      <header className="animate-fade-in">
        <div className="flex flex-wrap items-center gap-2">
          <span className="page-header-kicker">Rotas</span>
          <Badge variant="outline">Base demonstrativa</Badge>
        </div>
        <h1 className="mt-1 text-3xl font-display font-semibold tracking-tight">Rotas</h1>
        <p className="mt-1 text-sm text-muted-foreground">Acompanhe ocupação, clientes atendidos e atrasos por rota.</p>
      </header>

      <LogisticsRoutesBoard />
    </div>
  );
}
