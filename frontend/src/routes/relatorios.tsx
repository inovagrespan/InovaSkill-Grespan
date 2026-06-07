import { useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { BarChart3, FileText, Printer } from "lucide-react";
import { Button } from "@/components/ui/button";

export const Route = createFileRoute("/relatorios")({
  component: RelatoriosPage,
});

const reportAreas = [
  {
    id: "vendas",
    label: "Vendas",
    description: "Faturamento, médias semanal/mensal e ranking comercial.",
    metrics: ["Faturamento total", "Média mensal", "Média semanal", "Quantidade vendida"],
  },
  {
    id: "financas",
    label: "Finanças",
    description: "Indicadores financeiros por cliente e período.",
    metrics: ["Faturamento total", "Ticket médio", "Peso / quantidade", "Cliente filtrado"],
  },
  {
    id: "controle-estoque",
    label: "Controle e Estoque",
    description: "Risco operacional de rotas e disponibilidade de produtos.",
    metrics: ["Ocupação de caminhão por rota", "Ruptura de estoque", "SKUs críticos", "Rotas acima de 90%"],
  },
  {
    id: "clientes",
    label: "Clientes",
    description: "Carteira, saúde comercial e alertas de recompra.",
    metrics: ["Clientes ativos", "Ticket por cliente", "Risco comercial", "Produtos mais comprados"],
  },
];

const DEFAULT_SELECTED_METRICS = ["Faturamento total", "Ticket médio", "Peso / quantidade"];

function RelatoriosPage() {
  const [selectedAreaId, setSelectedAreaId] = useState(reportAreas[0].id);
  const [selectedMetrics, setSelectedMetrics] = useState<string[]>(DEFAULT_SELECTED_METRICS);
  const selectedArea = reportAreas.find((area) => area.id === selectedAreaId) ?? reportAreas[0];
  const printableMetrics = useMemo(
    () => selectedArea.metrics.filter((metric) => selectedMetrics.includes(metric)),
    [selectedArea.metrics, selectedMetrics],
  );

  function toggleMetric(metric: string) {
    setSelectedMetrics((current) => (
      current.includes(metric)
        ? current.filter((item) => item !== metric)
        : [...current, metric]
    ));
  }

  function changeArea(areaId: string) {
    const area = reportAreas.find((item) => item.id === areaId) ?? reportAreas[0];
    setSelectedAreaId(area.id);
    setSelectedMetrics(area.metrics.slice(0, 3));
  }

  function printSelectedReport() {
    window.print();
  }

  return (
    <div className="page-shell space-y-6">
      <header className="animate-soft-enter flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <span className="page-header-kicker">Smart Core / Relatórios</span>
          <h1 className="mt-2 text-4xl font-display tracking-tight">Emissão de relatórios</h1>
          <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
            Selecione a área, escolha as métricas filtradas e imprima um resumo pronto para reuniões.
          </p>
        </div>
        <Button type="button" onClick={printSelectedReport}>
          <Printer className="size-4" />
          Imprimir métricas
        </Button>
      </header>

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <div className="rounded-xl border border-border bg-surface p-5">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="font-display text-xl">Áreas disponíveis</h2>
            <FileText className="size-5 text-primary" />
          </div>
          <div className="space-y-3">
            {reportAreas.map((area) => (
              <button
                key={area.id}
                type="button"
                onClick={() => changeArea(area.id)}
                className={`w-full rounded-lg border p-4 text-left transition-colors ${
                  selectedArea.id === area.id
                    ? "border-primary/40 bg-primary/10"
                    : "border-border bg-background hover:bg-muted/50"
                }`}
              >
                <span className="text-sm font-semibold">{area.label}</span>
                <p className="mt-1 text-xs text-muted-foreground">{area.description}</p>
              </button>
            ))}
          </div>
        </div>

        <div className="rounded-xl border border-border bg-surface p-5">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h2 className="font-display text-xl">Métricas para impressão</h2>
              <p className="text-sm text-muted-foreground">Marque somente os indicadores necessários para a reunião.</p>
            </div>
            <BarChart3 className="size-5 text-primary" />
          </div>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            {selectedArea.metrics.map((metric) => (
              <label key={metric} className="flex items-center gap-3 rounded-lg border border-border bg-background p-3 text-sm">
                <input
                  type="checkbox"
                  checked={selectedMetrics.includes(metric)}
                  onChange={() => toggleMetric(metric)}
                  className="size-4 accent-[var(--primary)]"
                />
                <span>{metric}</span>
              </label>
            ))}
          </div>

          <div className="mt-5 rounded-lg border border-dashed border-border bg-muted/30 p-4">
            <p className="text-xs uppercase tracking-wide text-muted-foreground">Prévia do relatório</p>
            <h3 className="mt-2 text-lg font-display">{selectedArea.label}</h3>
            <ul className="mt-3 space-y-2 text-sm">
              {printableMetrics.map((metric) => (
                <li key={metric} className="flex items-center justify-between rounded-md bg-background px-3 py-2">
                  <span>{metric}</span>
                  <span className="text-xs text-muted-foreground">Dado fictício filtrado</span>
                </li>
              ))}
              {printableMetrics.length === 0 ? (
                <li className="rounded-md bg-background px-3 py-2 text-muted-foreground">Nenhuma métrica selecionada.</li>
              ) : null}
            </ul>
          </div>
        </div>
      </section>
    </div>
  );
}
