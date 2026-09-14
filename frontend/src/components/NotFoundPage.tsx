import { Link, useRouter } from "@tanstack/react-router";
import { ArrowLeft, Compass, Home } from "lucide-react";
import { BrandLogo } from "@/components/BrandLogo";
import { Button } from "@/components/ui/button";

export function NotFoundPage() {
  const router = useRouter();

  return (
    <section className="relative isolate flex min-h-dvh items-center justify-center overflow-hidden px-5 py-12 sm:px-8">
      <div
        aria-hidden="true"
        className="absolute left-1/2 top-1/2 -z-20 size-[34rem] -translate-x-1/2 -translate-y-1/2 rounded-full bg-primary/10 blur-3xl dark:bg-primary/15"
      />
      <div
        aria-hidden="true"
        className="absolute -right-20 -top-20 -z-10 size-72 rounded-full border-[42px] border-primary/5"
      />
      <div
        aria-hidden="true"
        className="absolute -bottom-28 -left-28 -z-10 size-80 rounded-full border-[52px] border-foreground/5"
      />

      <div className="w-full max-w-2xl text-center">
        <BrandLogo
          className="mb-10 justify-center"
          markClassName="size-10"
          textClassName="text-xl"
          taglineClassName="hidden"
        />

        <div className="relative mx-auto mb-7 flex w-fit items-center justify-center">
          <span className="font-display text-[8rem] font-black leading-none tracking-[-0.08em] text-foreground/5 sm:text-[11rem] dark:text-white/5">
            404
          </span>
          <span className="absolute flex size-20 items-center justify-center rounded-3xl border border-primary/20 bg-surface/90 text-primary shadow-lg backdrop-blur sm:size-24">
            <Compass className="size-10 sm:size-12" strokeWidth={1.5} aria-hidden="true" />
          </span>
        </div>

        <p className="mb-3 text-xs font-bold uppercase tracking-[0.24em] text-primary">
          Caminho não encontrado
        </p>
        <h1 className="font-display text-3xl font-bold tracking-tight sm:text-4xl">
          Parece que você saiu da rota.
        </h1>
        <p className="mx-auto mt-4 max-w-lg text-sm leading-6 text-muted-foreground sm:text-base">
          A página que você procura não existe, foi movida ou o endereço informado está incorreto.
          Você pode retornar à página anterior ou seguir para o painel principal.
        </p>

        <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
          <Button
            type="button"
            variant="outline"
            size="lg"
            className="w-full rounded-xl sm:w-auto"
            onClick={() => router.history.back()}
          >
            <ArrowLeft aria-hidden="true" />
            Voltar
          </Button>
          <Button asChild size="lg" className="w-full rounded-xl sm:w-auto">
            <Link to="/dashboard">
              <Home aria-hidden="true" />
              Ir para o painel
            </Link>
          </Button>
        </div>
      </div>
    </section>
  );
}
