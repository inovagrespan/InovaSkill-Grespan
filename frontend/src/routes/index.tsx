import { createFileRoute } from "@tanstack/react-router";
import { AlertTriangle, ArrowRight, BarChart3, Boxes, CircleDollarSign, Truck } from "lucide-react";
import { useMemo, useState } from "react";
import {
  getControlTowerScenario,
  sortControlTowerCardsByRisk,
  type ControlTowerCard,
  type ControlTowerPeriod,
  type ControlTowerStatus,
} from "@/lib/control-tower-dashboard";

export const Route = createFileRoute("/")({ component: Dashboard });

const periodOptions: Array<{ value: ControlTowerPeriod; label: string }> = [
  { value: "today", label: "Hoje" },
  { value: "next7", label: "Próximos 7 dias" },
  { value: "next30", label: "Próximos 30 dias" },
];

const moduleIcons = {
  Vendas: BarChart3,
  Produtos: Boxes,
  Finanças: CircleDollarSign,
  Logística: Truck,
};

function statusClassName(status: ControlTowerStatus): string {
  if (status === "red") return "border-red-500/35 bg-red-50/80 text-red-700 dark:bg-red-950/20 dark:text-red-300";
  if (status === "yellow") return "border-amber-500/35 bg-amber-50/80 text-amber-700 dark:bg-amber-950/20 dark:text-amber-300";
  return "border-emerald-500/35 bg-emerald-50/80 text-emerald-700 dark:bg-emerald-950/20 dark:text-emerald-300";
}

function statusLabel(status: ControlTowerStatus): string {
  if (status === "red") return "Crítico";
  if (status === "yellow") return "Atenção";
  return "Saudável";
}

function Dashboard() {
  const [period, setPeriod] = useState<ControlTowerPeriod>("today");
  const scenario = getControlTowerScenario(period);
  const cards = useMemo(() => sortControlTowerCardsByRisk(scenario.cards), [scenario.cards]);

  return (
    <div className="page-shell">
      <header className="mb-8 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between animate-soft-enter">
        <div>
          <span className="page-header-kicker">Smart Core / Torre de Controle</span>
          <h1 className="mt-2 mb-2 text-4xl font-display tracking-tight text-balance">Torre de Controle Inteligente</h1>
          <p className="max-w-[68ch] text-muted-foreground text-pretty">
            Situação atual e previsões de vendas, estoque, finanças e logística para priorizar decisões.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {periodOptions.map((option) => (
            <button
              key={option.value}
              type="button"
              onClick={() => setPeriod(option.value)}
              className={`rounded-lg border px-3 py-2 text-xs font-semibold transition-colors ${
                period === option.value
                  ? "border-primary bg-primary text-primary-foreground"
                  : "border-border bg-surface text-muted-foreground hover:border-primary/40 hover:text-foreground"
              }`}
            >
              {option.label}
            </button>
          ))}
        </div>
      </header>

      <section className="mb-5 rounded-xl border border-border bg-surface p-5 animate-soft-enter">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <p className="text-[10px] font-mono uppercase tracking-widest text-primary">{scenario.label}</p>
            <h2 className="mt-1 text-xl font-display">Painel de decisão</h2>
            <p className="mt-1 max-w-[70ch] text-sm text-muted-foreground">{scenario.subtitle}</p>
          </div>
          <div className="inline-flex items-center gap-2 rounded-lg border border-border bg-background px-3 py-2 text-xs text-muted-foreground">
            <AlertTriangle className="size-4 text-primary" />
            Cards ordenados por risco
          </div>
        </div>
      </section>

      <section className="metric-row animate-soft-enter">
        {cards.map((card) => (
          <ControlTowerCardView key={card.id} card={card} />
        ))}
      </section>
    </div>
  );
}

function ControlTowerCardView({ card }: { card: ControlTowerCard }) {
  const Icon = moduleIcons[card.module];

  return (
    <a
      href={card.href}
      className="group flex min-h-[230px] flex-col justify-between rounded-xl border border-border bg-surface p-5 shadow-xs transition-all hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-sm"
    >
      <div className="space-y-4">
        <div className="flex items-start justify-between gap-3">
          <div className="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <Icon className="size-5" />
          </div>
          <span className={`rounded-full border px-2.5 py-1 text-[11px] font-semibold ${statusClassName(card.status)}`}>
            {statusLabel(card.status)}
          </span>
        </div>
        <div>
          <p className="text-[10px] font-mono uppercase tracking-widest text-muted-foreground">{card.module}</p>
          <h3 className="mt-2 text-base font-semibold text-foreground">{card.title}</h3>
          <p className="mt-2 text-3xl font-display tracking-tight text-foreground">{card.value}</p>
          <p className="mt-3 text-sm leading-6 text-muted-foreground">{card.description}</p>
        </div>
      </div>
      <span className="mt-5 inline-flex items-center gap-2 text-sm font-medium text-primary">
        Abrir módulo <ArrowRight className="size-4 transition-transform group-hover:translate-x-0.5" />
      </span>
    </a>
  );
}
