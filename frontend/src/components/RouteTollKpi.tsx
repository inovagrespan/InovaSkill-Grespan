import { useState } from "react";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { routeTollPolicy, type RouteTollEstimate } from "@/lib/route-toll-cost";

const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const distanceFormatter = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 });

function formatCurrency(value: number) {
  return currencyFormatter.format(value);
}

export function RouteTollKpi({ estimate }: { estimate: RouteTollEstimate }) {
  const [detailsOpen, setDetailsOpen] = useState(false);
  const axleCount = routeTollPolicy.defaultCommercialAxles;

  return (
    <>
      <button
        type="button"
        onClick={() => setDetailsOpen(true)}
        aria-label="Ver detalhes dos pedágios da rota"
        className="route-kpi-card rounded-xl border border-primary/30 bg-primary/5 p-4 text-left shadow-sm transition-colors hover:border-primary/60 hover:bg-primary/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <div className="flex items-center gap-2">
          <p className="text-xs font-semibold uppercase tracking-wider text-primary">Gasto estimado com pedágio</p>
          <span className="h-px flex-1 bg-primary/25" aria-hidden="true" />
        </div>
        <p className="mt-2 text-xl font-display font-semibold tracking-tight text-foreground">{formatCurrency(estimate.totalAutomaticCost)}</p>
        <p className="mt-2 text-xs leading-relaxed text-muted-foreground">
          {estimate.totalPassages} passagem(ns) · Clique para ver os pedágios
        </p>
      </button>

      <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto border-border bg-surface">
          <DialogHeader>
            <DialogTitle>Pedágios da rota</DialogTitle>
            <DialogDescription>
              Praças identificadas no percurso rodoviário e tarifas estimadas para {axleCount} eixos.
            </DialogDescription>
          </DialogHeader>

          {estimate.tolls.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nenhum pedágio identificado no percurso.</p>
          ) : (
            <div className="space-y-3">
              <div className="rounded-lg border border-primary/30 bg-primary/5 p-3">
                <p className="text-xs text-muted-foreground">Total estimado</p>
                <p className="mt-1 text-lg font-semibold">{formatCurrency(estimate.totalAutomaticCost)}</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  {estimate.totalPassages} passagem(ns) · tarifa automática estimada
                </p>
              </div>
              <div className="divide-y divide-border rounded-lg border border-border">
                {estimate.tolls.map((toll) => {
                  const manualTariff = toll.plaza.commercialManualByAxle[axleCount];
                  const operator = toll.plaza.operator ?? "EIXO SP";
                  return (
                    <div key={toll.plaza.id} className="flex flex-col gap-2 px-3 py-3 text-sm sm:flex-row sm:items-center sm:justify-between">
                      <div>
                        <p className="font-medium">{toll.plaza.name}</p>
                        <p className="text-xs text-muted-foreground">
                          {operator} · {toll.plaza.highway} km {distanceFormatter.format(toll.plaza.kilometer)}
                        </p>
                      </div>
                      <div className="text-left text-xs text-muted-foreground sm:text-right">
                        <p>{toll.passages} passagem(ns)</p>
                        <p>
                          {axleCount} eixos: manual {manualTariff === undefined ? "não cadastrada" : formatCurrency(manualTariff)} · automático {formatCurrency(toll.automaticCost)}
                        </p>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
