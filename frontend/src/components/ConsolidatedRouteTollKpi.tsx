import { useState } from "react";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import type { RouteCostItem } from "@/lib/importer-api";

const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

export function ConsolidatedRouteTollKpi({ cost }: { cost: RouteCostItem | null }) {
  const [detailsOpen, setDetailsOpen] = useState(false);
  const available = cost?.isAvailable === true;
  return <>
    <button type="button" disabled={!available} onClick={() => setDetailsOpen(true)} aria-label="Ver detalhes dos pedágios da rota" className="route-kpi-card rounded-xl border border-primary/30 bg-primary/5 p-4 text-left shadow-sm disabled:cursor-default">
      <p className="text-xs font-semibold uppercase tracking-wider text-primary">Gasto estimado com pedágio</p>
      <p className="mt-2 text-xl font-display font-semibold tracking-tight">{available ? currencyFormatter.format(cost.tollCost) : "Indisponível"}</p>
      <p className="mt-2 text-xs text-muted-foreground">{available ? `${cost.tollPassages} passagem(ns) · Clique para ver os pedágios` : cost?.unavailableReason ?? "Custo consolidado ainda não disponível"}</p>
    </button>
    <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}><DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto border-border bg-surface"><DialogHeader><DialogTitle>Pedágios da rota</DialogTitle><DialogDescription>Praças e tarifas automáticas usadas na consolidação oficial.</DialogDescription></DialogHeader>
      {cost?.tolls.length ? <div className="divide-y divide-border rounded-lg border border-border">{cost.tolls.map((toll) => <div key={`${toll.tollPlazaCode}-${toll.axleCount}`} className="flex justify-between gap-3 p-3 text-sm"><div><p className="font-medium">{toll.tollPlazaName}</p><p className="text-xs text-muted-foreground">{toll.operatorName} · {toll.highway} km {toll.kilometer.toLocaleString("pt-BR")}</p></div><div className="text-right text-xs text-muted-foreground"><p>{toll.passages} passagem(ns) · {toll.axleCount} eixos</p><p>{currencyFormatter.format(toll.automaticUnitTariff)} por passagem · {currencyFormatter.format(toll.totalCost)}</p></div></div>)}</div> : <p className="text-sm text-muted-foreground">Nenhum pedágio identificado no percurso.</p>}
    </DialogContent></Dialog>
  </>;
}
