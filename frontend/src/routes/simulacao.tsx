import { createFileRoute, Link } from "@tanstack/react-router";
import { useState } from "react";
import {
  ArrowRight,
  ArrowLeft,
  CheckCircle2,
  TrendingUp,
  TrendingDown,
  AlertTriangle,
  Sparkles,
  Truck,
  Users,
  Clock,
  X,
  Brain,
  Activity,
} from "lucide-react";
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  ResponsiveContainer,
  Tooltip,
  ReferenceLine,
} from "recharts";

export const Route = createFileRoute("/simulacao")({
  component: Simulacao,
});

type Step = "slider" | "impacto" | "cenarios" | "detalhe" | "sucesso";

function Simulacao() {
  const [step, setStep] = useState<Step>("slider");
  const [demand, setDemand] = useState(20);
  const [selected, setSelected] = useState<"A" | "B">("B");

  return (
    <div className="p-12">
      <FlowProgress step={step} />

      {step === "slider" && (
        <SliderStep
          demand={demand}
          setDemand={setDemand}
          onNext={() => setStep("impacto")}
        />
      )}
      {step === "impacto" && (
        <ImpactoStep
          demand={demand}
          onBack={() => setStep("slider")}
          onNext={() => setStep("cenarios")}
        />
      )}
      {step === "cenarios" && (
        <CenariosStep
          selected={selected}
          setSelected={setSelected}
          onBack={() => setStep("impacto")}
          onNext={() => setStep("detalhe")}
        />
      )}
      {step === "detalhe" && (
        <DetalheStep
          scenario={selected}
          onBack={() => setStep("cenarios")}
          onNext={() => setStep("sucesso")}
        />
      )}
      {step === "sucesso" && <SuccessModal onClose={() => setStep("slider")} />}
    </div>
  );
}

function FlowProgress({ step }: { step: Step }) {
  const steps: { id: Step; label: string }[] = [
    { id: "slider", label: "Demanda" },
    { id: "impacto", label: "Impacto" },
    { id: "cenarios", label: "Cenários" },
    { id: "detalhe", label: "Plano" },
  ];
  const currentIdx = steps.findIndex((s) => s.id === step);
  return (
    <header className="flex justify-between items-end mb-12 animate-fade-in">
      <div>
        <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-muted-foreground">
          Smart Core / Simulador
        </span>
        <h1 className="text-4xl font-display tracking-tight text-balance mt-2">
          Simulador de Demanda Inteligente
        </h1>
      </div>
      <div className="flex items-center gap-2">
        {steps.map((s, i) => (
          <div key={s.id} className="flex items-center gap-2">
            <div
              className={
                "size-6 rounded-full flex items-center justify-center text-[10px] font-mono font-bold transition-all " +
                (i <= currentIdx
                  ? "bg-primary text-primary-foreground"
                  : "bg-white/5 text-muted-foreground")
              }
            >
              {i + 1}
            </div>
            <span
              className={
                "text-[10px] font-mono uppercase tracking-widest " +
                (i === currentIdx ? "text-foreground" : "text-muted-foreground")
              }
            >
              {s.label}
            </span>
            {i < steps.length - 1 && (
              <div className="w-8 h-px bg-border mx-2" />
            )}
          </div>
        ))}
      </div>
    </header>
  );
}

/* ------------------------- STEP 1: SLIDER ------------------------- */
function SliderStep({
  demand,
  setDemand,
  onNext,
}: {
  demand: number;
  setDemand: (n: number) => void;
  onNext: () => void;
}) {
  const pct = Math.min(100, (demand / 50) * 100);

  return (
    <section className="animate-fade-in">
      <p className="text-muted-foreground max-w-[60ch] text-pretty mb-10">
        Arraste o controle para projetar diferentes níveis de crescimento de vendas
        mensais. A IA recalcula em tempo real o impacto na operação.
      </p>

      <div className="bg-surface border border-border p-12 rounded-2xl relative overflow-hidden mb-8">
        <div className="flex justify-between items-center mb-10">
          <span className="font-mono text-xs text-muted-foreground uppercase tracking-widest">
            Ajuste de Vendas Mensais
          </span>
          <div className="text-primary font-display text-8xl tracking-tighter tabular-nums leading-none">
            +{demand}%
          </div>
        </div>

        <div className="relative h-16 flex items-center">
          <div className="absolute inset-x-0 bg-white/5 h-2 my-auto rounded-full" />
          <div
            className="absolute left-0 bg-primary h-2 my-auto rounded-full pointer-events-none transition-[width] duration-150"
            style={{ width: `${pct}%` }}
          />
          <div
            className="absolute size-10 bg-primary rounded-full border-[6px] border-background pointer-events-none animate-pulse-glow transition-[left] duration-150"
            style={{ left: `calc(${pct}% - 20px)` }}
          />
          <input
            type="range"
            min={0}
            max={50}
            step={1}
            value={demand}
            onChange={(e) => setDemand(Number(e.target.value))}
            className="absolute inset-0 w-full h-full opacity-0 cursor-grab active:cursor-grabbing"
            aria-label="Ajuste de demanda"
          />
        </div>

        <div className="flex justify-between mt-6 font-mono text-[10px] text-muted-foreground">
          <span>0% (BASE)</span>
          <span>+25%</span>
          <span>+50% (STRESS TEST)</span>
        </div>
      </div>

      <AiForecastPanel demand={demand} />

      <div className="grid grid-cols-3 gap-4 mb-10">

        {[
          { label: "Receita Atual", value: "R$ 3.5M", delta: null },
          {
            label: "Receita Projetada",
            value: `R$ ${(3.5 * (1 + demand / 100)).toFixed(2)}M`,
            delta: `+R$ ${(3.5 * (demand / 100) * 1000).toFixed(0)}K`,
          },
          {
            label: "Tendência S&OP",
            value: demand > 25 ? "Agressiva" : demand > 10 ? "Moderada" : "Estável",
            delta: null,
          },
        ].map((c) => (
          <div key={c.label} className="bg-surface border border-border p-5 rounded-xl">
            <p className="text-[10px] text-muted-foreground font-mono uppercase tracking-wider mb-2">
              {c.label}
            </p>
            <p className="text-2xl font-display tabular-nums">{c.value}</p>
            {c.delta && (
              <div className="inline-flex items-center px-2 py-0.5 mt-2 rounded bg-primary/10 text-primary text-[10px] font-bold">
                {c.delta} ADICIONAL
              </div>
            )}
          </div>
        ))}
      </div>

      <div className="flex justify-end">
        <button
          onClick={onNext}
          className="inline-flex items-center gap-2 bg-primary text-primary-foreground px-8 py-4 rounded-lg font-bold uppercase text-xs tracking-widest hover:brightness-110 transition-all"
        >
          Processar Impacto Operacional <ArrowRight className="size-4" />
        </button>
      </div>
    </section>
  );
}

/* ------------------------- STEP 2: IMPACTO ------------------------- */
function ImpactoStep({
  demand,
  onBack,
  onNext,
}: {
  demand: number;
  onBack: () => void;
  onNext: () => void;
}) {
  const costPct = (demand * 0.62).toFixed(1);
  const extraRevenue = (3.5 * (demand / 100) * 1000).toFixed(0);

  return (
    <section className="animate-fade-in">
      <h2 className="text-xs font-mono uppercase tracking-[0.3em] text-muted-foreground mb-2">
        Diagnóstico de Impacto Geral · Demanda +{demand}%
      </h2>
      <p className="text-muted-foreground max-w-[60ch] text-pretty mb-10">
        A IA cruzou o histórico de vendas com a capacidade atual e identificou os
        gargalos críticos abaixo.
      </p>

      <div className="grid grid-cols-3 gap-6 mb-10">
        <div className="bg-surface border border-border p-6 rounded-xl animate-fade-in [animation-delay:100ms]">
          <div className="flex items-center justify-between mb-4">
            <TrendingUp className="size-5 text-primary" />
            <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-primary/10 text-primary">
              GANHO
            </span>
          </div>
          <p className="text-xs text-muted-foreground mb-2">Vendas Adicionais</p>
          <p className="text-3xl font-display text-foreground tabular-nums">
            R$ {extraRevenue}K
          </p>
          <p className="text-[10px] text-muted-foreground font-mono mt-3">
            POR MÊS · PROJEÇÃO LINEAR
          </p>
        </div>

        <div className="bg-surface border border-danger/30 p-6 rounded-xl relative animate-fade-in [animation-delay:200ms]">
          <div className="absolute -left-3 top-1/2 -translate-y-1/2 text-danger text-xl">
            →
          </div>
          <div className="flex items-center justify-between mb-4">
            <Truck className="size-5 text-danger" />
            <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-danger/10 text-danger">
              GARGALO
            </span>
          </div>
          <p className="text-xs text-muted-foreground mb-2">Custo Geral de Rotas</p>
          <p className="text-3xl font-display text-danger tabular-nums">+{costPct}%</p>
          <div className="mt-4 p-2 bg-danger/10 border border-danger/20 rounded">
            <p className="text-[10px] font-bold text-danger font-mono">
              FALTA 1 CAMINHÃO PRÓPRIO
            </p>
          </div>
        </div>

        <div className="bg-surface border border-danger/30 p-6 rounded-xl relative animate-fade-in [animation-delay:300ms]">
          <div className="absolute -left-3 top-1/2 -translate-y-1/2 text-danger text-xl">
            →
          </div>
          <div className="flex items-center justify-between mb-4">
            <Users className="size-5 text-danger" />
            <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-danger/10 text-danger">
              GARGALO
            </span>
          </div>
          <p className="text-xs text-muted-foreground mb-2">Capacidade de Pessoal</p>
          <p className="text-3xl font-display text-danger tabular-nums">2 Vagas</p>
          <div className="mt-4 p-2 bg-danger/10 border border-danger/20 rounded">
            <p className="text-[10px] font-bold text-danger font-mono">
              1 MOTORISTA · 1 AJUDANTE
            </p>
          </div>
        </div>
      </div>

      <div className="bg-surface border border-border rounded-2xl p-8 mb-10">
        <div className="flex items-start gap-4">
          <div className="size-10 rounded-lg bg-danger/10 flex items-center justify-center shrink-0">
            <AlertTriangle className="size-5 text-danger" />
          </div>
          <div>
            <h3 className="text-lg font-display mb-2">Efeito Cascata Identificado</h3>
            <p className="text-sm text-muted-foreground text-pretty max-w-[72ch]">
              Sem ação, o crescimento de {demand}% nas vendas se traduz em atrasos de
              entrega, sobrecarga de equipes e perda estimada de R$ 312K em SLA. A IA já
              calculou dois caminhos viáveis para resolver a crise.
            </p>
          </div>
        </div>
      </div>

      <div className="flex justify-between">
        <button
          onClick={onBack}
          className="inline-flex items-center gap-2 text-muted-foreground hover:text-foreground px-4 py-3 text-xs font-mono uppercase tracking-widest"
        >
          <ArrowLeft className="size-4" /> Voltar
        </button>
        <button
          onClick={onNext}
          className="inline-flex items-center gap-2 bg-primary text-primary-foreground px-8 py-4 rounded-lg font-bold uppercase text-xs tracking-widest hover:brightness-110 transition-all"
        >
          <Sparkles className="size-4" /> Gerar Soluções da IA
        </button>
      </div>
    </section>
  );
}

/* ------------------------- STEP 3: CENÁRIOS ------------------------- */
function CenariosStep({
  selected,
  setSelected,
  onBack,
  onNext,
}: {
  selected: "A" | "B";
  setSelected: (s: "A" | "B") => void;
  onBack: () => void;
  onNext: () => void;
}) {
  return (
    <section className="animate-fade-in">
      <h2 className="text-xs font-mono uppercase tracking-[0.3em] text-muted-foreground mb-2">
        Soluções Recomendadas pela IA
      </h2>
      <p className="text-muted-foreground max-w-[60ch] text-pretty mb-10">
        A IA vasculhou o histórico do negócio e identificou dois caminhos cobrindo todo
        o leque de soluções: saídas externas e expansão interna.
      </p>

      <div className="grid grid-cols-2 gap-8 mb-10">
        <ScenarioCard
          id="A"
          selected={selected === "A"}
          onSelect={() => setSelected("A")}
          tag="Cenário A · Terceirização"
          title="Contratação Spot & Frete Externo"
          badge="Baixo Capex"
          items={[
            "Aluguel de 1 caminhão plataforma (diário)",
            "Contratação via agência (2 temporários)",
            { label: "Prazo de implementação:", value: "48 horas" },
          ]}
          roi="92%"
          payback="1.2 meses"
        />
        <ScenarioCard
          id="B"
          selected={selected === "B"}
          onSelect={() => setSelected("B")}
          tag="Cenário B · Expansão de Ativos"
          title="Otimização e Ativos Próprios"
          badge="Recomendado"
          recommended
          items={[
            "Aquisição de 1 caminhão Cargo 2024",
            "Contratação CLT: 1 Motorista + 1 Ajudante",
            { label: "Prazo de execução:", value: "30 dias", accent: true },
          ]}
          roi="156%"
          payback="3.5 meses"
        />
      </div>

      <div className="flex justify-between">
        <button
          onClick={onBack}
          className="inline-flex items-center gap-2 text-muted-foreground hover:text-foreground px-4 py-3 text-xs font-mono uppercase tracking-widest"
        >
          <ArrowLeft className="size-4" /> Voltar
        </button>
        <button
          onClick={onNext}
          className="inline-flex items-center gap-2 bg-primary text-primary-foreground px-8 py-4 rounded-lg font-bold uppercase text-xs tracking-widest hover:brightness-110 transition-all"
        >
          Detalhar Cenário {selected} <ArrowRight className="size-4" />
        </button>
      </div>
    </section>
  );
}

type Item = string | { label: string; value: string; accent?: boolean };

function ScenarioCard({
  id,
  selected,
  onSelect,
  tag,
  title,
  badge,
  recommended,
  items,
  roi,
  payback,
}: {
  id: "A" | "B";
  selected: boolean;
  onSelect: () => void;
  tag: string;
  title: string;
  badge: string;
  recommended?: boolean;
  items: Item[];
  roi: string;
  payback: string;
}) {
  return (
    <button
      onClick={onSelect}
      className={
        "text-left bg-surface p-8 rounded-2xl transition-all " +
        (selected
          ? "border-2 border-primary ring-4 ring-primary/10"
          : "border border-border hover:border-white/20")
      }
    >
      <div className="flex justify-between items-start mb-6">
        <span
          className={
            "text-[10px] font-mono uppercase " +
            (recommended ? "text-primary" : "text-muted-foreground")
          }
        >
          {tag}
        </span>
        <span
          className={
            "text-[10px] font-bold px-2 py-1 rounded uppercase " +
            (recommended
              ? "bg-primary text-primary-foreground"
              : "bg-white/5 text-foreground")
          }
        >
          {badge}
        </span>
      </div>
      <h3 className="text-2xl font-display mb-4">{title}</h3>
      <ul className="space-y-3 mb-8 text-sm text-muted-foreground">
        {items.map((it, i) => (
          <li key={i} className="flex gap-3">
            <span className="size-1.5 rounded-full bg-current shrink-0 mt-2" />
            {typeof it === "string" ? (
              <span>{it}</span>
            ) : (
              <span>
                {it.label}{" "}
                <span
                  className={it.accent ? "text-primary font-medium" : "text-foreground"}
                >
                  {it.value}
                </span>
              </span>
            )}
          </li>
        ))}
      </ul>
      <div className="grid grid-cols-2 gap-4 pt-6 border-t border-border">
        <div>
          <p className="text-[10px] text-muted-foreground uppercase font-mono">
            ROI Estimado
          </p>
          <p
            className={
              "text-lg font-display tabular-nums " +
              (recommended ? "text-primary" : "text-foreground")
            }
          >
            {roi}
          </p>
        </div>
        <div>
          <p className="text-[10px] text-muted-foreground uppercase font-mono">
            Payback
          </p>
          <p
            className={
              "text-lg font-display tabular-nums " +
              (recommended ? "text-primary" : "text-foreground")
            }
          >
            {payback}
          </p>
        </div>
      </div>
      {selected && (
        <div className="mt-6 inline-flex items-center gap-2 text-[10px] font-mono uppercase tracking-widest text-primary">
          <CheckCircle2 className="size-3" /> Selecionado · Cenário {id}
        </div>
      )}
    </button>
  );
}

/* ------------------------- STEP 4: DETALHE ------------------------- */
function DetalheStep({
  scenario,
  onBack,
  onNext,
}: {
  scenario: "A" | "B";
  onBack: () => void;
  onNext: () => void;
}) {
  const isB = scenario === "B";
  const roi = isB ? "156%" : "92%";
  const payback = isB ? "3.5 meses" : "1.2 meses";
  const prazo = isB ? "30 dias" : "48 horas";

  const actions = isB
    ? [
        {
          title: "Aquisição de 1 Unidade de Frota",
          desc: "Disparar ordem de compra para fornecedor preferencial (Cargo 2024).",
          tag: "Prazo 20 dias",
          icon: Truck,
        },
        {
          title: "Contratação CLT — 2 vagas",
          desc: "Abertura de requisições no ATS: 1 Motorista + 1 Ajudante de Carga.",
          tag: "Prazo 30 dias",
          icon: Users,
        },
        {
          title: "Reotimização de rotas",
          desc: "Recalcular malha logística incluindo o novo ativo na capacidade total.",
          tag: "Automático",
          icon: Sparkles,
        },
      ]
    : [
        {
          title: "Aluguel emergencial de 1 caminhão",
          desc: "Locação spot via marketplace de frete (contrato diário).",
          tag: "Prazo 48h",
          icon: Truck,
        },
        {
          title: "Contratação via agência",
          desc: "2 ajudantes temporários por 90 dias renováveis.",
          tag: "Prazo 48h",
          icon: Users,
        },
      ];

  return (
    <section className="animate-fade-in">
      <h2 className="text-xs font-mono uppercase tracking-[0.3em] text-muted-foreground mb-2">
        Plano Detalhado · Cenário {scenario}
      </h2>
      <p className="text-muted-foreground max-w-[60ch] text-pretty mb-10">
        {isB
          ? "Expansão de ativos próprios. Maior ROI no longo prazo e construção de capacidade permanente."
          : "Solução rápida via terceirização. Implementação imediata com menor capex."}
      </p>

      <div className="grid grid-cols-3 gap-6 mb-10">
        <MetricCard label="ROI do Cenário" value={roi} accent />
        <MetricCard label="Payback Estimado" value={payback} accent />
        <MetricCard label="Prazo de Execução" value={prazo} icon={Clock} />
      </div>

      <div className="bg-surface border border-border rounded-2xl p-8 mb-10">
        <h3 className="text-xs font-mono uppercase tracking-[0.3em] text-muted-foreground mb-6">
          Ordens Operacionais Integradas
        </h3>
        <div className="space-y-4">
          {actions.map((a, i) => {
            const Icon = a.icon;
            return (
              <div
                key={i}
                className="flex gap-5 items-start p-5 bg-background/50 border border-border rounded-xl"
              >
                <div className="size-10 rounded-lg bg-primary/10 flex items-center justify-center shrink-0">
                  <Icon className="size-4 text-primary" />
                </div>
                <div className="flex-1">
                  <div className="flex items-center gap-3 mb-1">
                    <span className="text-[10px] font-mono text-muted-foreground">
                      0{i + 1}
                    </span>
                    <h4 className="font-medium">{a.title}</h4>
                  </div>
                  <p className="text-sm text-muted-foreground text-pretty">{a.desc}</p>
                </div>
                <span className="shrink-0 text-[10px] font-mono uppercase tracking-widest px-3 py-1 bg-primary/10 text-primary rounded-full">
                  {a.tag}
                </span>
              </div>
            );
          })}
        </div>
      </div>

      <div className="flex justify-between">
        <button
          onClick={onBack}
          className="inline-flex items-center gap-2 text-muted-foreground hover:text-foreground px-4 py-3 text-xs font-mono uppercase tracking-widest"
        >
          <ArrowLeft className="size-4" /> Comparar cenários
        </button>
        <button
          onClick={onNext}
          className="inline-flex items-center gap-2 bg-primary text-primary-foreground px-8 py-4 rounded-lg font-bold uppercase text-xs tracking-widest hover:brightness-110 transition-all"
        >
          <CheckCircle2 className="size-4" /> Aprovar e Implementar Plano
        </button>
      </div>
    </section>
  );
}

function MetricCard({
  label,
  value,
  accent,
  icon: Icon,
}: {
  label: string;
  value: string;
  accent?: boolean;
  icon?: React.ComponentType<{ className?: string }>;
}) {
  return (
    <div className="bg-surface border border-border p-6 rounded-xl">
      <div className="flex items-center justify-between mb-3">
        <p className="text-[10px] text-muted-foreground font-mono uppercase tracking-wider">
          {label}
        </p>
        {Icon && <Icon className="size-4 text-muted-foreground" />}
      </div>
      <p
        className={
          "text-4xl font-display tabular-nums " +
          (accent ? "text-primary" : "text-foreground")
        }
      >
        {value}
      </p>
    </div>
  );
}

/* ------------------------- STEP 5: SUCESSO ------------------------- */
function SuccessModal({ onClose }: { onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-50 bg-background/80 backdrop-blur-sm flex items-center justify-center p-6 animate-fade-in">
      <div className="bg-surface border-2 border-primary rounded-2xl p-10 max-w-xl w-full ring-8 ring-primary/10 relative">
        <button
          onClick={onClose}
          className="absolute top-4 right-4 text-muted-foreground hover:text-foreground"
          aria-label="Fechar"
        >
          <X className="size-5" />
        </button>

        <div className="size-16 rounded-2xl bg-primary/10 flex items-center justify-center mb-6 animate-pulse-glow">
          <CheckCircle2 className="size-8 text-primary" />
        </div>

        <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-primary">
          Plano Aplicado
        </span>
        <h2 className="text-3xl font-display tracking-tight mt-2 mb-4">
          Ordens disparadas com sucesso
        </h2>
        <p className="text-sm text-muted-foreground text-pretty mb-8">
          A ordem de compra de frota foi enviada ao fornecedor e a requisição de 2 vagas
          (1 Motorista + 1 Ajudante) foi disparada ao RH com prazo de 30 dias.
        </p>

        <div className="space-y-3 mb-8">
          {[
            { label: "Ordem de Compra #OC-2026-0489", status: "ENVIADA" },
            { label: "Requisição RH #RQ-2026-1142", status: "ABERTA" },
            { label: "Reotimização de rotas", status: "PROCESSANDO" },
          ].map((r) => (
            <div
              key={r.label}
              className="flex items-center justify-between p-3 bg-background/50 border border-border rounded-lg"
            >
              <span className="text-sm">{r.label}</span>
              <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-primary/10 text-primary">
                {r.status}
              </span>
            </div>
          ))}
        </div>

        <div className="flex gap-3">
          <button
            onClick={onClose}
            className="flex-1 border border-border text-foreground py-3 rounded-lg font-medium text-sm hover:bg-white/5"
          >
            Nova simulação
          </button>
          <Link
            to="/"
            className="flex-1 bg-primary text-primary-foreground py-3 rounded-lg font-bold uppercase text-xs tracking-widest text-center"
          >
            Voltar ao Dashboard
          </Link>
        </div>
      </div>
    </div>
  );
}

/* ------------------------- AI FORECAST PANEL ------------------------- */
function AiForecastPanel({ demand }: { demand: number }) {
  // Histórico real (12M) — mesmos dados base da página Vendas (R$ milhões)
  const historical = [
    { m: "Jun/24", real: 2.8 },
    { m: "Jul/24", real: 2.95 },
    { m: "Ago/24", real: 3.05 },
    { m: "Set/24", real: 3.1 },
    { m: "Out/24", real: 3.18 },
    { m: "Nov/24", real: 3.32 },
    { m: "Dez/24", real: 3.55 },
    { m: "Jan/25", real: 3.28 },
    { m: "Fev/25", real: 3.35 },
    { m: "Mar/25", real: 3.42 },
    { m: "Abr/25", real: 3.48 },
    { m: "Mai/25", real: 3.5 },
  ];

  // Tendência baseline da IA (regressão histórica): +4.2% a.m. composto suavizado
  const baselineGrowth = 0.018; // ~1.8% a.m. tendência orgânica
  const simGrowth = demand / 100 / 6; // crescimento simulado distribuído nos próximos 6 meses
  const lastReal = historical[historical.length - 1].real;

  const forecast = Array.from({ length: 6 }).map((_, i) => {
    const month = ["Jun/25", "Jul/25", "Ago/25", "Set/25", "Out/25", "Nov/25"][i];
    const base = lastReal * Math.pow(1 + baselineGrowth, i + 1);
    const sim = lastReal * Math.pow(1 + baselineGrowth + simGrowth, i + 1);
    return { m: month, baseline: +base.toFixed(2), simulado: +sim.toFixed(2) };
  });

  const chartData = [
    ...historical.map((h) => ({ m: h.m, real: h.real })),
    { m: "Mai/25", real: lastReal, baseline: lastReal, simulado: lastReal },
    ...forecast,
  ];

  // Decisão da IA: comparar crescimento simulado com tendência histórica
  const trendPct = baselineGrowth * 6 * 100; // ~10.8% em 6 meses
  const simPct = demand;
  const delta = simPct - trendPct;
  const confidence = Math.min(96, 72 + Math.abs(delta) * 0.4);

  let veredito: "alta" | "estavel" | "risco";
  let titulo: string;
  let descricao: string;

  if (delta >= 8) {
    veredito = "alta";
    titulo = "Alta probabilidade de aumento de vendas";
    descricao = `O cenário simulado de +${simPct}% supera a tendência histórica orgânica (+${trendPct.toFixed(1)}% em 6M). Cruzando sazonalidade de Q4, recompra de clientes ativos e funil de novos clientes, a IA classifica como expansão sustentável.`;
  } else if (delta >= -3) {
    veredito = "estavel";
    titulo = "Crescimento alinhado à tendência histórica";
    descricao = `A simulação de +${simPct}% está em linha com o padrão orgânico (+${trendPct.toFixed(1)}% em 6M). A IA confirma viabilidade sem ruptura, mas sem ganho competitivo significativo.`;
  } else {
    veredito = "risco";
    titulo = "Projeção abaixo do potencial histórico";
    descricao = `O cenário simulado (+${simPct}%) fica abaixo da tendência natural detectada (+${trendPct.toFixed(1)}% em 6M). A IA sinaliza risco de subaproveitamento da demanda ativa do mercado.`;
  }

  const isAlta = veredito === "alta";
  const isRisco = veredito === "risco";
  const accent = isAlta ? "primary" : isRisco ? "danger" : "muted-foreground";

  return (
    <div className="bg-surface border border-border rounded-2xl p-8 mb-8 animate-fade-in">
      <div className="flex items-start justify-between mb-6 gap-6">
        <div className="flex items-start gap-4">
          <div
            className={
              "size-12 rounded-xl flex items-center justify-center shrink-0 " +
              (isAlta
                ? "bg-primary/10 text-primary"
                : isRisco
                  ? "bg-danger/10 text-danger"
                  : "bg-white/5 text-muted-foreground")
            }
          >
            <Brain className="size-6" />
          </div>
          <div>
            <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-muted-foreground flex items-center gap-2">
              <Activity className="size-3" /> Previsão IA · Histórico × Tendência
            </span>
            <h3 className="text-xl font-display mt-1.5">{titulo}</h3>
          </div>
        </div>

        <div className="text-right shrink-0">
          <p className="text-[10px] font-mono uppercase text-muted-foreground">
            Confiança do modelo
          </p>
          <p
            className={
              "text-3xl font-display tabular-nums " +
              (isAlta ? "text-primary" : isRisco ? "text-danger" : "text-foreground")
            }
          >
            {confidence.toFixed(0)}%
          </p>
        </div>
      </div>

      <p className="text-sm text-muted-foreground max-w-[80ch] text-pretty mb-6">
        {descricao}
      </p>

      <div className="grid grid-cols-[1fr_280px] gap-6">
        <div className="h-48 bg-background/40 rounded-lg p-3 border border-border/50">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={chartData} margin={{ top: 5, right: 8, left: -20, bottom: 0 }}>
              <defs>
                <linearGradient id="gReal" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="hsl(var(--muted-foreground))" stopOpacity={0.4} />
                  <stop offset="100%" stopColor="hsl(var(--muted-foreground))" stopOpacity={0} />
                </linearGradient>
                <linearGradient id="gSim" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="oklch(0.85 0.18 145)" stopOpacity={0.5} />
                  <stop offset="100%" stopColor="oklch(0.85 0.18 145)" stopOpacity={0} />
                </linearGradient>
              </defs>
              <XAxis
                dataKey="m"
                tick={{ fontSize: 9, fill: "hsl(var(--muted-foreground))" }}
                interval={2}
                axisLine={false}
                tickLine={false}
              />
              <YAxis
                tick={{ fontSize: 9, fill: "hsl(var(--muted-foreground))" }}
                axisLine={false}
                tickLine={false}
                tickFormatter={(v) => `${v}M`}
              />
              <Tooltip
                contentStyle={{
                  background: "hsl(var(--background))",
                  border: "1px solid hsl(var(--border))",
                  borderRadius: 8,
                  fontSize: 11,
                }}
                formatter={(v: number) => `R$ ${v}M`}
              />
              <ReferenceLine x="Mai/25" stroke="hsl(var(--border))" strokeDasharray="3 3" />
              <Area
                type="monotone"
                dataKey="real"
                stroke="hsl(var(--muted-foreground))"
                strokeWidth={2}
                fill="url(#gReal)"
                name="Histórico"
              />
              <Area
                type="monotone"
                dataKey="baseline"
                stroke="hsl(var(--muted-foreground))"
                strokeWidth={1}
                strokeDasharray="4 4"
                fill="none"
                name="Tendência IA"
              />
              <Area
                type="monotone"
                dataKey="simulado"
                stroke="oklch(0.85 0.18 145)"
                strokeWidth={2.5}
                fill="url(#gSim)"
                name="Simulado"
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>

        <div className="space-y-3">
          <ForecastSignal
            label="Tendência histórica 6M"
            value={`+${trendPct.toFixed(1)}%`}
            icon={<TrendingUp className="size-3.5" />}
            tone="muted"
          />
          <ForecastSignal
            label="Cenário simulado"
            value={`+${simPct}%`}
            icon={
              isRisco ? <TrendingDown className="size-3.5" /> : <TrendingUp className="size-3.5" />
            }
            tone={isAlta ? "primary" : isRisco ? "danger" : "muted"}
          />
          <ForecastSignal
            label="Delta vs. tendência"
            value={`${delta >= 0 ? "+" : ""}${delta.toFixed(1)} p.p.`}
            icon={<Activity className="size-3.5" />}
            tone={isAlta ? "primary" : isRisco ? "danger" : "muted"}
          />
          <div className="pt-3 border-t border-border">
            <p className="text-[9px] font-mono uppercase tracking-wider text-muted-foreground mb-1.5">
              Sinais cruzados
            </p>
            <ul className="space-y-1 text-[11px] text-muted-foreground">
              <li>· 12M de vendas reais</li>
              <li>· Recompra de clientes ativos</li>
              <li>· Sazonalidade Q4 (+18%)</li>
              <li>· Funil de novos clientes</li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}

function ForecastSignal({
  label,
  value,
  icon,
  tone,
}: {
  label: string;
  value: string;
  icon: React.ReactNode;
  tone: "primary" | "danger" | "muted";
}) {
  const color =
    tone === "primary"
      ? "text-primary"
      : tone === "danger"
        ? "text-danger"
        : "text-foreground";
  return (
    <div className="flex items-center justify-between bg-background/40 border border-border/50 rounded-lg px-3 py-2">
      <span className="text-[10px] font-mono uppercase tracking-wider text-muted-foreground">
        {label}
      </span>
      <span className={"inline-flex items-center gap-1.5 text-sm font-display tabular-nums " + color}>
        {icon} {value}
      </span>
    </div>
  );
}

