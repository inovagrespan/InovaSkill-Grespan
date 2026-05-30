import { createFileRoute } from "@tanstack/react-router";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export const Route = createFileRoute("/relatorios")({
  component: RelatoriosPage,
});

function RelatoriosPage() {
  return (
    <div className="page-shell">
      <header className="animate-soft-enter">
        <span className="page-header-kicker">Smart Core / Relatorios</span>
        <h1 className="mt-2 text-4xl font-display tracking-tight">Relatorios</h1>
      </header>

      <Card className="animate-soft-enter">
        <CardHeader>
          <CardTitle>Em construcao</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">Esta area foi criada para os proximos relatorios analiticos.</p>
        </CardContent>
      </Card>
    </div>
  );
}
