import { createFileRoute, Link } from "@tanstack/react-router";
import {
  ArrowRight,
  BarChart3,
  Bot,
  Boxes,
  Check,
  CircleGauge,
  PackageCheck,
  Route as RouteIcon,
  Sparkles,
  TrendingUp,
  Truck,
} from "lucide-react";
import { BrandLogo } from "@/components/BrandLogo";
import { Button } from "@/components/ui/button";

export const Route = createFileRoute("/")({
  component: LandingPage,
});

const capabilities = [
  {
    title: "Visão comercial",
    description: "Faturamento, clientes e produtos no mesmo contexto.",
    icon: TrendingUp,
  },
  {
    title: "Operação logística",
    description: "Rotas, ocupação e frota acompanhadas em tempo real.",
    icon: Truck,
  },
  {
    title: "Inteligência conectada",
    description: "Respostas objetivas a partir dos dados da operação.",
    icon: Bot,
  },
] as const;

const signals = [
  { label: "Comercial", value: "R$ 2,4 mi", change: "+12,8%", width: "78%" },
  { label: "Logística", value: "87,4%", change: "+4,2%", width: "87%" },
] as const;

function LandingPage() {
  return (
    <div className="min-h-dvh bg-background font-body text-foreground">
      <header className="sticky top-0 z-40 border-b border-border/80 bg-background/90 backdrop-blur-xl">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-5 lg:px-8">
          <Link to="/" aria-label="Página inicial do Conecta360">
            <BrandLogo markClassName="size-9" textClassName="text-lg" taglineClassName="hidden" />
          </Link>

          <nav
            className="hidden items-center gap-7 text-sm text-muted-foreground md:flex"
            aria-label="Navegação principal"
          >
            <a className="transition-colors hover:text-foreground" href="#plataforma">
              Plataforma
            </a>
            <a className="transition-colors hover:text-foreground" href="#recursos">
              Recursos
            </a>
            <a className="transition-colors hover:text-foreground" href="#visao-360">
              Visão 360
            </a>
          </nav>

          <Button asChild size="sm" className="rounded-lg px-4 shadow-sm">
            <Link to="/login">
              Entrar
              <ArrowRight className="size-3.5" />
            </Link>
          </Button>
        </div>
      </header>

      <main>
        <section id="plataforma" className="relative overflow-hidden border-b border-border/70">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_18%_18%,rgba(180,35,47,0.08),transparent_28%),radial-gradient(circle_at_85%_50%,rgba(17,24,39,0.05),transparent_30%)]" />
          <div className="relative mx-auto grid max-w-7xl items-center gap-14 px-5 py-20 lg:grid-cols-[0.88fr_1.12fr] lg:px-8 lg:py-28">
            <div className="max-w-xl">
              <div className="inline-flex items-center gap-2 rounded-full border border-primary/15 bg-primary/5 px-3 py-1.5 text-xs font-semibold text-primary">
                <Sparkles className="size-3.5" />
                Gestão conectada, de ponta a ponta
              </div>
              <h1 className="mt-7 font-display text-4xl font-bold leading-[1.05] tracking-[-0.04em] sm:text-5xl lg:text-6xl">
                Uma visão clara para decisões que movem a operação.
              </h1>
              <p className="mt-6 max-w-lg text-base leading-7 text-muted-foreground sm:text-lg sm:leading-8">
                O Conecta360 reúne vendas, logística e inteligência em uma experiência única,
                simples e orientada à ação.
              </p>
              <div className="mt-8 flex flex-col gap-3 sm:flex-row">
                <Button asChild size="lg" className="h-11 rounded-lg px-6 shadow-sm">
                  <Link to="/login">
                    Acessar o sistema
                    <ArrowRight className="size-4" />
                  </Link>
                </Button>
                <Button
                  asChild
                  size="lg"
                  variant="outline"
                  className="h-11 rounded-lg border-border bg-surface px-6 shadow-xs"
                >
                  <a href="#recursos">Conhecer a plataforma</a>
                </Button>
              </div>
              <div className="mt-9 flex flex-wrap gap-x-6 gap-y-2 text-xs text-muted-foreground">
                <span className="flex items-center gap-2">
                  <Check className="size-3.5 text-primary" />
                  Dados centralizados
                </span>
                <span className="flex items-center gap-2">
                  <Check className="size-3.5 text-primary" />
                  Acesso por perfil
                </span>
                <span className="flex items-center gap-2">
                  <Check className="size-3.5 text-primary" />
                  Indicadores confiáveis
                </span>
              </div>
            </div>

            <DashboardPreview />
          </div>
        </section>

        <section id="recursos" className="bg-surface">
          <div className="mx-auto max-w-7xl px-5 py-20 lg:px-8 lg:py-24">
            <div className="grid gap-8 lg:grid-cols-[0.72fr_1.28fr] lg:gap-16">
              <div className="max-w-md">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-primary">
                  Uma só plataforma
                </p>
                <h2 className="mt-4 font-display text-3xl font-bold tracking-tight sm:text-4xl">
                  Menos ruído. Mais contexto para decidir.
                </h2>
                <p className="mt-4 text-sm leading-7 text-muted-foreground">
                  As áreas acompanham o que importa sem perder a visão do conjunto. Cada perfil
                  encontra a informação certa, no momento certo.
                </p>
              </div>

              <div className="grid divide-y divide-border rounded-xl border border-border bg-background sm:grid-cols-3 sm:divide-x sm:divide-y-0">
                {capabilities.map((capability) => {
                  const Icon = capability.icon;
                  return (
                    <article key={capability.title} className="p-6 sm:p-7">
                      <span className="flex size-10 items-center justify-center rounded-lg border border-primary/15 bg-primary/5 text-primary">
                        <Icon className="size-4.5" />
                      </span>
                      <h3 className="mt-5 text-sm font-semibold">{capability.title}</h3>
                      <p className="mt-2 text-sm leading-6 text-muted-foreground">
                        {capability.description}
                      </p>
                    </article>
                  );
                })}
              </div>
            </div>
          </div>
        </section>

        <section id="visao-360" className="border-y border-border bg-background">
          <div className="mx-auto grid max-w-7xl gap-8 px-5 py-20 lg:grid-cols-[1.1fr_0.9fr] lg:px-8 lg:py-24">
            <div className="overflow-hidden rounded-xl border border-border bg-surface p-2 shadow-sm">
              <div className="grid min-h-72 gap-2 sm:grid-cols-[0.9fr_1.1fr]">
                <div className="rounded-lg bg-[#111827] p-6 text-white">
                  <CircleGauge className="size-5 text-red-400" />
                  <p className="mt-14 text-xs text-white/50">Ocupação consolidada</p>
                  <p className="mt-2 font-display text-4xl font-semibold">87,4%</p>
                  <p className="mt-3 text-xs text-emerald-300">Faixa operacional saudável</p>
                </div>
                <div className="grid gap-2 sm:grid-rows-2">
                  <div className="rounded-lg border border-border bg-background p-5">
                    <div className="flex items-center justify-between">
                      <Boxes className="size-4 text-primary" />
                      <span className="text-xs text-muted-foreground">Produtos</span>
                    </div>
                    <p className="mt-7 text-xl font-semibold">Catálogo conectado</p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Preço, estoque e histórico em um só lugar.
                    </p>
                  </div>
                  <div className="rounded-lg border border-border bg-background p-5">
                    <div className="flex items-center justify-between">
                      <RouteIcon className="size-4 text-primary" />
                      <span className="text-xs text-muted-foreground">Rotas</span>
                    </div>
                    <p className="mt-7 text-xl font-semibold">Operação visível</p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Da visão geral ao detalhe de cada rota.
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <div className="flex flex-col justify-center lg:pl-8">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-primary">
                Visão 360
              </p>
              <h2 className="mt-4 font-display text-3xl font-bold tracking-tight sm:text-4xl">
                O detalhe de cada área. A clareza do todo.
              </h2>
              <p className="mt-4 text-sm leading-7 text-muted-foreground">
                Acompanhe indicadores, encontre desvios e avance da análise para a ação sem alternar
                entre ferramentas desconectadas.
              </p>
              <Button
                asChild
                variant="link"
                className="mt-5 h-auto w-fit p-0 font-semibold text-primary"
              >
                <Link to="/login">
                  Explorar o Conecta360 <ArrowRight className="size-4" />
                </Link>
              </Button>
            </div>
          </div>
        </section>

        <section className="bg-surface">
          <div className="mx-auto max-w-7xl px-5 py-16 lg:px-8">
            <div className="flex flex-col items-start justify-between gap-7 rounded-2xl bg-[#111827] px-7 py-9 text-white sm:flex-row sm:items-center sm:px-10">
              <div>
                <p className="text-xs font-medium text-white/55">Sua operação, conectada.</p>
                <h2 className="mt-2 font-display text-2xl font-semibold tracking-tight sm:text-3xl">
                  Entre e transforme dados em direção.
                </h2>
              </div>
              <Button asChild size="lg" className="h-11 shrink-0 rounded-lg px-6">
                <Link to="/login">
                  Acessar o sistema <ArrowRight className="size-4" />
                </Link>
              </Button>
            </div>
          </div>
        </section>
      </main>

      <footer className="border-t border-border bg-surface">
        <div className="mx-auto flex max-w-7xl flex-col gap-3 px-5 py-7 text-xs text-muted-foreground sm:flex-row sm:items-center sm:justify-between lg:px-8">
          <BrandLogo markClassName="size-7" textClassName="text-sm" taglineClassName="hidden" />
          <p>Gestão integrada para decisões mais claras.</p>
        </div>
      </footer>
    </div>
  );
}

function DashboardPreview() {
  return (
    <div className="relative mx-auto w-full max-w-2xl" aria-label="Prévia da plataforma Conecta360">
      <div className="absolute -inset-5 -z-10 rounded-[2rem] bg-primary/5 blur-2xl" />
      <div className="overflow-hidden rounded-xl border border-border bg-surface shadow-lg">
        <div className="flex h-12 items-center justify-between border-b border-border px-4">
          <div className="flex items-center gap-2">
            <span className="size-2 rounded-full bg-primary" />
            <span className="text-xs font-semibold">Visão executiva</span>
          </div>
          <span className="rounded-md bg-muted px-2 py-1 text-[10px] text-muted-foreground">
            Hoje
          </span>
        </div>
        <div className="grid gap-3 bg-background/70 p-4 sm:grid-cols-[0.9fr_1.1fr]">
          <div className="space-y-3">
            {signals.map((signal) => (
              <div
                key={signal.label}
                className="rounded-lg border border-border bg-surface p-4 shadow-xs"
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-[11px] text-muted-foreground">{signal.label}</p>
                    <p className="mt-1 text-lg font-semibold">{signal.value}</p>
                  </div>
                  <span className="rounded-full bg-emerald-50 px-2 py-1 text-[10px] font-medium text-emerald-700">
                    {signal.change}
                  </span>
                </div>
                <div className="mt-4 h-1.5 overflow-hidden rounded-full bg-muted">
                  <div className="h-full rounded-full bg-primary" style={{ width: signal.width }} />
                </div>
              </div>
            ))}
          </div>
          <div className="rounded-lg border border-border bg-surface p-4 shadow-xs">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-[11px] text-muted-foreground">Evolução mensal</p>
                <p className="mt-1 text-sm font-semibold">Desempenho da operação</p>
              </div>
              <BarChart3 className="size-4 text-primary" />
            </div>
            <div className="mt-8 flex h-32 items-end gap-2 border-b border-border px-1">
              {[44, 62, 51, 74, 68, 91, 82, 96].map((height, index) => (
                <span
                  key={height}
                  className="flex-1 rounded-t-sm bg-primary/15"
                  style={{ height: `${height}%` }}
                >
                  <span
                    className={`block h-full rounded-t-sm ${index === 7 ? "bg-primary" : "bg-primary/45"}`}
                  />
                </span>
              ))}
            </div>
            <div className="mt-5 flex items-center gap-3 rounded-lg bg-muted/70 p-3">
              <span className="flex size-8 items-center justify-center rounded-md bg-surface text-primary shadow-xs">
                <PackageCheck className="size-4" />
              </span>
              <div>
                <p className="text-[11px] font-medium">Operação atualizada</p>
                <p className="text-[10px] text-muted-foreground">
                  Indicadores prontos para análise
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
