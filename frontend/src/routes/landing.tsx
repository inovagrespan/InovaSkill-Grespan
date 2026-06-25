import { createFileRoute, Link } from "@tanstack/react-router";
import {
  ArrowRight,
  BarChart3,
  BellRing,
  CalendarCheck,
  CheckCircle2,
  Factory,
  LineChart,
  ShieldCheck,
  Truck,
  UsersRound,
} from "lucide-react";
import { BrandLogo } from "@/components/BrandLogo";
import { Button } from "@/components/ui/button";

export const Route = createFileRoute("/landing")({
  component: LandingPage,
});

const pillars = [
  {
    title: "Indicadores por área",
    text: "Vendas, logística, produção e administrativo enxergam os mesmos sinais com fórmulas, filtros e contexto operacional.",
    icon: BarChart3,
  },
  {
    title: "Reuniões com continuidade",
    text: "Problemas, perguntas, decisões e ações deixam de ficar soltos e passam a ter responsáveis, prazos e histórico.",
    icon: CalendarCheck,
  },
  {
    title: "Alertas acionáveis",
    text: "A IA ajuda a priorizar riscos, evidências e próximos passos antes que a rotina vire retrabalho.",
    icon: BellRing,
  },
] as const;

const modules = [
  { label: "Vendas", icon: BarChart3 },
  { label: "Logística", icon: Truck },
  { label: "Produção", icon: Factory },
  { label: "Reuniões", icon: UsersRound },
] as const;

const outcomes = [
  "Reduzir reuniões reativas sem dono claro",
  "Unificar KPI, causa, evidência e ação",
  "Acompanhar execução depois da decisão",
  "Dar visibilidade por perfil e responsabilidade",
] as const;

function LandingPage() {
  return (
    <div className="min-h-screen bg-[#f5f7fb] text-slate-950 dark:bg-[#0d1117] dark:text-white">
      <header className="fixed inset-x-0 top-0 z-30 border-b border-white/12 bg-slate-950/76 px-4 py-3 text-white backdrop-blur-md">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4">
          <BrandLogo
            markClassName="size-10"
            textClassName="text-xl text-white"
            taglineClassName="hidden"
          />
          <nav className="hidden items-center gap-6 text-sm text-white/72 md:flex">
            <a className="transition-colors hover:text-white" href="#plataforma">
              Plataforma
            </a>
            <a className="transition-colors hover:text-white" href="#modulos">
              Módulos
            </a>
            <a className="transition-colors hover:text-white" href="#resultado">
              Resultado
            </a>
          </nav>
          <Button asChild size="sm" className="bg-white text-slate-950 hover:bg-white/90">
            <Link to="/login">Entrar</Link>
          </Button>
        </div>
      </header>

      <section className="relative min-h-[92svh] overflow-hidden pt-20 text-white">
        <img
          src="/assets/conecta360-hero.png"
          alt="Sala executiva com dashboards e acompanhamento de indicadores"
          className="absolute inset-0 h-full w-full object-cover"
        />
        <div className="absolute inset-0 bg-[linear-gradient(90deg,rgba(3,8,24,0.92)_0%,rgba(3,8,24,0.78)_38%,rgba(3,8,24,0.28)_72%,rgba(3,8,24,0.12)_100%)]" />
        <div className="relative z-10 mx-auto flex min-h-[calc(92svh-5rem)] max-w-7xl items-center px-4 py-16">
          <div className="max-w-3xl">
            <p className="text-xs font-semibold uppercase tracking-[0.28em] text-red-200">
              Cultura de acompanhamento
            </p>
            <h1 className="mt-5 max-w-3xl font-display text-5xl font-black leading-[0.98] tracking-normal text-white md:text-7xl">
              Conecta360
            </h1>
            <p className="mt-6 max-w-2xl text-xl leading-8 text-white/84 md:text-2xl md:leading-9">
              Da reunião reativa para uma operação que acompanha indicadores, decisões e ações
              todos os dias.
            </p>
            <div className="mt-9 flex flex-col gap-3 sm:flex-row">
              <Button asChild size="lg" className="h-12 bg-[#d01825] px-6 font-semibold hover:bg-[#b91420]">
                <Link to="/login">
                  Acessar o sistema
                  <ArrowRight className="size-4" />
                </Link>
              </Button>
              <Button
                asChild
                size="lg"
                variant="outline"
                className="h-12 border-white/30 bg-white/8 px-6 text-white hover:bg-white/14 hover:text-white"
              >
                <a href="#plataforma">Ver proposta</a>
              </Button>
            </div>
          </div>
        </div>
      </section>

      <main id="plataforma" className="space-y-0">
        <section className="bg-white py-16 dark:bg-[#101722]">
          <div className="mx-auto grid max-w-7xl gap-10 px-4 lg:grid-cols-[0.9fr_1.1fr] lg:items-start">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.22em] text-[#d01825]">
                Plataforma
              </p>
              <h2 className="mt-3 max-w-xl font-display text-3xl font-bold tracking-normal md:text-5xl">
                Um ciclo único entre número, causa e execução.
              </h2>
            </div>
            <div className="grid gap-4 md:grid-cols-3">
              {pillars.map((pillar) => {
                const Icon = pillar.icon;
                return (
                  <article key={pillar.title} className="rounded-lg border border-slate-200 bg-slate-50 p-5 dark:border-white/10 dark:bg-white/5">
                    <span className="inline-flex size-10 items-center justify-center rounded-md bg-[#d01825]/10 text-[#d01825]">
                      <Icon className="size-5" />
                    </span>
                    <h3 className="mt-5 text-base font-semibold">{pillar.title}</h3>
                    <p className="mt-3 text-sm leading-6 text-slate-600 dark:text-slate-300">
                      {pillar.text}
                    </p>
                  </article>
                );
              })}
            </div>
          </div>
        </section>

        <section id="modulos" className="bg-[#eef1f6] py-16 dark:bg-[#0d1117]">
          <div className="mx-auto max-w-7xl px-4">
            <div className="grid gap-8 lg:grid-cols-[0.75fr_1.25fr] lg:items-center">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.22em] text-[#d01825]">
                  Módulos conectados
                </p>
                <h2 className="mt-3 font-display text-3xl font-bold tracking-normal md:text-5xl">
                  Cada área vê sua rotina, a diretoria vê o conjunto.
                </h2>
              </div>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                {modules.map((module) => {
                  const Icon = module.icon;
                  return (
                    <div key={module.label} className="flex items-center gap-3 rounded-lg border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5">
                      <span className="inline-flex size-10 shrink-0 items-center justify-center rounded-md bg-slate-950 text-white dark:bg-white dark:text-slate-950">
                        <Icon className="size-5" />
                      </span>
                      <span className="text-sm font-semibold">{module.label}</span>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        </section>

        <section id="resultado" className="bg-white py-16 dark:bg-[#101722]">
          <div className="mx-auto grid max-w-7xl gap-10 px-4 lg:grid-cols-[1fr_1fr] lg:items-center">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.22em] text-[#d01825]">
                Resultado esperado
              </p>
              <h2 className="mt-3 font-display text-3xl font-bold tracking-normal md:text-5xl">
                Acompanhamento vira rotina, não cobrança de última hora.
              </h2>
              <div className="mt-8 grid gap-3">
                {outcomes.map((item) => (
                  <div key={item} className="flex items-start gap-3 text-sm text-slate-700 dark:text-slate-200">
                    <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-emerald-600" />
                    <span>{item}</span>
                  </div>
                ))}
              </div>
            </div>
            <div className="rounded-lg border border-slate-200 bg-slate-950 p-6 text-white shadow-xl dark:border-white/10">
              <div className="flex items-center gap-3 border-b border-white/10 pb-5">
                <span className="inline-flex size-11 items-center justify-center rounded-md bg-[#d01825]">
                  <LineChart className="size-5" />
                </span>
                <div>
                  <p className="text-sm font-semibold">Gestão 360</p>
                  <p className="text-xs text-white/58">Indicadores, alertas e execução</p>
                </div>
              </div>
              <div className="mt-6 grid gap-4 sm:grid-cols-2">
                <Metric label="Indicadores monitorados" value="40+" />
                <Metric label="Áreas integradas" value="6" />
                <Metric label="Fluxo de acompanhamento" value="4 níveis" />
                <Metric label="Acesso por perfil" value="Seguro" />
              </div>
              <div className="mt-6 flex items-center gap-2 rounded-md border border-emerald-400/20 bg-emerald-400/10 px-4 py-3 text-sm text-emerald-100">
                <ShieldCheck className="size-4" />
                <span>Foco em decisão com evidência e responsável.</span>
              </div>
            </div>
          </div>
        </section>
      </main>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-white/10 bg-white/6 p-4">
      <p className="text-2xl font-display font-bold">{value}</p>
      <p className="mt-1 text-xs leading-5 text-white/62">{label}</p>
    </div>
  );
}
