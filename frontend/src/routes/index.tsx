import { createFileRoute, Link } from "@tanstack/react-router";
import {
  ArrowRight,
  BarChart3,
  CheckCircle2,
  ChevronDown,
  ClipboardCheck,
  LineChart,
  ShieldCheck,
  TrendingUp,
  Truck,
  Package,
} from "lucide-react";
import { BrandLogo } from "@/components/BrandLogo";
import { Button } from "@/components/ui/button";

export const Route = createFileRoute("/")({
  component: LandingPage,
});

const steps = [
  {
    step: "01",
    title: "Indicadores em tempo real",
    text: "Cada área acompanha seus KPIs com filtros, comparativos e visões detalhadas de vendas e logística.",
    icon: TrendingUp,
  },
  {
    step: "02",
    title: "Acompanhamento diário",
    text: "A diretoria enxerga o faturamento, a ocupação das rotas, as margens comerciais e as tendências de mercado.",
    icon: ClipboardCheck,
  },
] as const;

const pillars = [
  {
    title: "Indicadores por área",
    text: "Vendas, logística e produtos enxergam os mesmos sinais com fórmulas, filtros e contexto operacional.",
    icon: BarChart3,
  },
  {
    title: "Foco operacional",
    text: "Entenda o que aconteceu, por que aconteceu e quais os principais fatores com análises regionais e mapas.",
    icon: Truck,
  },
] as const;

const modules = [
  { label: "Vendas", icon: BarChart3, desc: "Pipeline, faturamento, ticket médio e metas por equipe." },
  { label: "Logística", icon: Truck, desc: "Entregas, congestionamento, rotas e indicadores de frota." },
  { label: "Produtos", icon: Package, desc: "Consulte o catálogo de produtos e preços importados." },
] as const;

const outcomes = [
  "Unificar KPI, causa e evidência operacional",
  "Dar visibilidade por perfil e responsabilidade",
  "Reduzir decisões reativas sem visibilidade",
] as const;

function LandingPage() {
  return (
    <div className="min-h-screen bg-[#f5f7fb] text-slate-950 dark:bg-[#0d1117] dark:text-white">
      <header className="fixed inset-x-0 top-0 z-30 border-b border-white/10 bg-slate-950/80 px-4 py-3 text-white backdrop-blur-lg">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4">
          <BrandLogo
            markClassName="size-10"
            textClassName="text-xl text-white"
            taglineClassName="hidden"
          />
          <nav className="hidden items-center gap-6 text-sm text-white/70 md:flex">
            <a className="transition-colors hover:text-white" href="#">
              Início
            </a>
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
          <Button asChild size="sm" className="bg-[#d01825] text-white shadow-lg shadow-red-800/25 transition-all duration-200 hover:bg-[#b91420] hover:shadow-xl hover:shadow-red-800/35 active:scale-[0.96]">
            <Link to="/login">Entrar</Link>
          </Button>
        </div>
      </header>

      <section className="relative min-h-screen overflow-hidden text-white">
        <img
          src="/assets/conecta360-hero.png"
          alt="Sala executiva com dashboards e acompanhamento de indicadores"
          className="absolute inset-0 h-full w-full object-cover"
        />
        <div className="absolute inset-0 bg-[linear-gradient(110deg,rgba(3,8,24,0.94)_0%,rgba(3,8,24,0.80)_40%,rgba(3,8,24,0.30)_75%,rgba(3,8,24,0.08)_100%)]" />
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top_right,rgba(208,24,37,0.20),transparent_50%),radial-gradient(ellipse_at_bottom_left,rgba(208,24,37,0.08),transparent_50%)]" />
        <div className="relative z-10 mx-auto flex min-h-screen max-w-7xl items-center px-4 pt-20">
          <div className="max-w-3xl">
            <span className="inline-block rounded-full border border-red-300/30 bg-red-500/10 px-4 py-1 text-xs font-semibold uppercase tracking-[0.22em] text-red-200 backdrop-blur-sm">
              Cultura de acompanhamento
            </span>
            <h1 className="mt-6 max-w-3xl font-display text-5xl font-black leading-[0.95] tracking-tight text-white md:text-7xl">
              Conecta<span className="text-[#d01825]">360</span>
            </h1>
            <p className="mt-6 max-w-2xl text-lg leading-8 text-white/75 md:text-xl md:leading-9">
              Da operação desarticulada para uma operação que acompanha indicadores e execuções todos os dias.
            </p>
            <div className="mt-10 flex flex-col gap-3 sm:flex-row">
              <Button asChild size="lg" className="h-12 bg-[#d01825] px-7 font-semibold shadow-lg shadow-red-800/30 transition-all duration-200 hover:bg-[#b91420] hover:shadow-xl hover:shadow-red-800/40 active:scale-[0.97]">
                <Link to="/login">
                  Acessar o sistema
                  <ArrowRight className="size-4" />
                </Link>
              </Button>
              <Button
                asChild
                size="lg"
                variant="outline"
                className="h-12 border-white/25 bg-white/10 px-7 text-white backdrop-blur-sm transition-all duration-200 hover:bg-white/20 hover:text-white active:scale-[0.97]"
              >
                <a href="#plataforma">Ver proposta</a>
              </Button>
            </div>
          </div>
        </div>
        <a
          href="#plataforma"
          className="absolute bottom-6 left-1/2 z-10 -translate-x-1/2 text-white/40 transition-colors hover:text-white/70"
          aria-label="Rolar para plataforma"
        >
          <ChevronDown className="size-6 animate-bounce" />
        </a>
      </section>

      <section
        id="plataforma"
        className="relative flex min-h-screen items-center overflow-hidden bg-white dark:bg-[#101722]"
      >
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top_left,rgba(208,24,37,0.06),transparent_50%),radial-gradient(ellipse_at_bottom_right,rgba(208,24,37,0.03),transparent_50%)]" />
        <div className="relative mx-auto w-full max-w-7xl px-4 py-16 md:py-20">
          <div className="mx-auto max-w-2xl text-center">
            <span className="inline-block rounded-full border border-red-200/40 bg-red-50 px-4 py-1 text-xs font-semibold uppercase tracking-[0.22em] text-[#d01825] dark:border-red-800/40 dark:bg-red-950/40">
              Como funciona
            </span>
            <h2 className="mt-5 font-display text-3xl font-bold tracking-tight md:text-5xl">
              Um ciclo único entre <span className="text-[#d01825]">número</span>,{" "}
              <span className="text-[#d01825]">causa</span> e{" "}
              <span className="text-[#d01825]">evidência</span>.
            </h2>
            <p className="mt-4 text-sm leading-7 text-slate-500 dark:text-slate-400">
              O Conecta360 substitui planilhas soltas por um fluxo integrado: os dados importados alimentam painéis interativos de vendas, logística e catálogos de produtos com total consistência.
            </p>
          </div>
          <div className="mt-12 grid gap-6 md:grid-cols-2">
            {steps.map((s, i) => {
              const Icon = s.icon;
              return (
                <div
                  key={s.title}
                  className="group relative overflow-hidden rounded-xl border border-slate-200 bg-white/80 p-6 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:border-[#d01825]/25 hover:shadow-lg hover:shadow-red-900/5 dark:border-white/10 dark:bg-white/[0.04] dark:hover:border-red-500/20"
                >
                  <div className="absolute right-0 top-0 size-32 translate-x-8 -translate-y-8 rounded-full bg-[#d01825]/[0.04] transition-all duration-500 group-hover:scale-150 dark:bg-red-500/[0.04]" />
                  <span className="relative flex size-11 items-center justify-center rounded-lg bg-[#d01825]/10 text-lg font-bold text-[#d01825]">
                    {s.step}
                  </span>
                  <h3 className="relative mt-5 text-lg font-semibold">{s.title}</h3>
                  <p className="relative mt-3 text-sm leading-6 text-slate-500 dark:text-slate-400">
                    {s.text}
                  </p>
                  {i < steps.length - 1 && (
                    <ArrowRight className="mx-auto mt-4 size-5 text-[#d01825]/20 md:absolute md:-right-3 md:top-1/2 md:mt-0 md:-translate-y-1/2" />
                  )}
                </div>
              );
            })}
          </div>
        </div>
      </section>

      <section
        id="modulos"
        className="relative flex min-h-screen items-center overflow-hidden bg-[#eef1f6] dark:bg-[#090c12]"
      >
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_center,rgba(208,24,37,0.05),transparent_50%)]" />
        <div className="relative mx-auto w-full max-w-7xl px-4 py-16 md:py-20">
          <div className="mx-auto max-w-2xl text-center">
            <span className="inline-block rounded-full border border-red-200/30 bg-red-50 px-4 py-1 text-xs font-semibold uppercase tracking-[0.22em] text-[#d01825] dark:border-red-800/30 dark:bg-red-950/30">
              Módulos conectados
            </span>
            <h2 className="mt-5 font-display text-3xl font-bold tracking-tight md:text-5xl">
              Cada área vê sua rotina, a diretoria vê o <span className="text-[#d01825]">conjunto</span>.
            </h2>
            <p className="mt-4 text-sm leading-7 text-slate-500 dark:text-slate-400">
              Módulos estruturados que compartilham a mesma inteligência analítica com total precisão.
            </p>
          </div>
          <div className="mt-12 grid gap-4 sm:grid-cols-3">
            {modules.map((module) => {
              const Icon = module.icon;
              return (
                <div
                  key={module.label}
                  className="group rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition-all duration-200 hover:-translate-y-1 hover:border-[#d01825]/30 hover:shadow-lg hover:shadow-red-900/8 dark:border-white/10 dark:bg-white/[0.04] dark:hover:border-red-500/20"
                >
                  <span className="inline-flex size-11 items-center justify-center rounded-lg bg-slate-950 text-white ring-1 ring-white/10 transition-all duration-200 group-hover:bg-[#d01825] group-hover:shadow-lg group-hover:shadow-red-800/20 dark:bg-white dark:text-slate-950 dark:group-hover:bg-[#d01825] dark:group-hover:text-white">
                    <Icon className="size-5" />
                  </span>
                  <p className="mt-4 text-base font-semibold">{module.label}</p>
                  <p className="mt-2 text-sm leading-6 text-slate-500 dark:text-slate-400">
                    {module.desc}
                  </p>
                </div>
              );
            })}
          </div>
        </div>
      </section>

      <section
        id="resultado"
        className="relative flex min-h-screen items-center overflow-hidden bg-white dark:bg-[#101722]"
      >
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_bottom_left,rgba(208,24,37,0.07),transparent_50%),radial-gradient(ellipse_at_top_right,rgba(208,24,37,0.04),transparent_50%)]" />
        <div className="relative mx-auto w-full max-w-7xl px-4 py-16 md:py-20">
          <div className="grid gap-12 lg:grid-cols-[1fr_1fr] lg:items-center">
            <div>
              <span className="inline-block rounded-full border border-red-200/30 bg-red-50 px-4 py-1 text-xs font-semibold uppercase tracking-[0.22em] text-[#d01825] dark:border-red-800/30 dark:bg-red-950/30">
                Resultado esperado
              </span>
              <h2 className="mt-5 font-display text-3xl font-bold tracking-tight md:text-5xl">
                Acompanhamento vira rotina, não <span className="text-[#d01825]">cobrança operacional</span>.
              </h2>
              <div className="mt-8 grid gap-3">
                {outcomes.map((item) => (
                  <div key={item} className="flex items-start gap-3 rounded-lg bg-emerald-50/50 px-3 py-2 text-sm text-slate-700 transition-colors hover:bg-emerald-50 dark:bg-emerald-950/10 dark:text-slate-200 dark:hover:bg-emerald-950/20">
                    <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-emerald-600" />
                    <span>{item}</span>
                  </div>
                ))}
              </div>
            </div>
            <div className="relative overflow-hidden rounded-xl border border-slate-200 bg-gradient-to-b from-slate-950 via-slate-900 to-slate-950 p-6 shadow-xl dark:border-white/10">
              <div className="absolute right-0 top-0 size-64 translate-x-16 -translate-y-32 rounded-full bg-[#d01825]/10 blur-2xl" />
              <div className="relative flex items-center gap-3 border-b border-white/10 pb-5">
                <span className="inline-flex size-11 items-center justify-center rounded-md bg-gradient-to-br from-[#d01825] to-red-700 shadow-lg shadow-red-800/30">
                  <LineChart className="size-5" />
                </span>
                <div>
                  <p className="text-sm font-semibold text-white">Gestão 360</p>
                  <p className="text-xs text-white/50">Indicadores, alertas e execução</p>
                </div>
              </div>
              <div className="relative mt-6 grid gap-4 sm:grid-cols-2">
                <Metric label="Indicadores monitorados" value="30+" />
                <Metric label="Áreas integradas" value="3" />
                <Metric label="Fluxo de acompanhamento" value="Filtros" />
                <Metric label="Acesso por perfil" value="Seguro" />
              </div>
              <div className="relative mt-6 flex items-center gap-2 rounded-md border border-emerald-400/20 bg-emerald-400/10 px-4 py-3 text-sm text-emerald-100">
                <ShieldCheck className="size-4" />
                <span>Foco em decisão com evidências claras.</span>
              </div>
            </div>
          </div>
          <div className="relative mt-16 text-center">
            <div className="mx-auto max-w-lg rounded-2xl border border-slate-200 bg-gradient-to-b from-slate-50 to-white p-8 shadow-sm dark:border-white/10 dark:from-white/[0.04] dark:to-white/[0.02]">
              <p className="text-lg font-semibold">Pronto para transformar sua gestão?</p>
              <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
                Comece hoje mesmo. Sua operação mais conectada em poucos minutos.
              </p>
              <Button asChild size="lg" className="mt-6 h-12 bg-[#d01825] px-8 font-semibold shadow-lg shadow-red-800/25 transition-all duration-200 hover:bg-[#b91420] hover:shadow-xl hover:shadow-red-800/35 active:scale-[0.97]">
                <Link to="/login">
                  Acessar o sistema
                  <ArrowRight className="size-4" />
                </Link>
              </Button>
            </div>
          </div>
        </div>
      </section>

      <footer className="border-t border-slate-200 bg-slate-50 px-4 py-10 dark:border-white/8 dark:bg-[#090c12]">
        <div className="mx-auto flex max-w-7xl flex-col items-center justify-between gap-4 text-center md:flex-row md:text-left">
          <BrandLogo
            markClassName="size-9"
            textClassName="text-base"
            taglineClassName="hidden"
          />
          <p className="text-xs text-slate-500 dark:text-slate-400">
            &copy; {new Date().getFullYear()} Conecta360. Todos os direitos reservados.
          </p>
          <nav className="flex gap-4 text-xs text-slate-500 dark:text-slate-400">
            <Link to="/login" className="transition-colors hover:text-slate-800 dark:hover:text-white">
              Entrar
            </Link>
            <a href="#" className="transition-colors hover:text-slate-800 dark:hover:text-white">
              Início
            </a>
            <a href="#plataforma" className="transition-colors hover:text-slate-800 dark:hover:text-white">
              Plataforma
            </a>
            <a href="#resultado" className="transition-colors hover:text-slate-800 dark:hover:text-white">
              Resultado
            </a>
          </nav>
        </div>
      </footer>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-white/10 bg-white/[0.06] p-4 backdrop-blur-sm">
      <p className="text-2xl font-display font-bold text-white">{value}</p>
      <p className="mt-1 text-xs leading-5 text-white/50">{label}</p>
    </div>
  );
}
