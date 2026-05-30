import { createFileRoute } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { ArrowLeft, ArrowRight, CheckCircle2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export const Route = createFileRoute("/simulacao")({ component: Simulacao });

type Step = "demanda" | "impacto" | "cenarios" | "plano";

function Simulacao() {
  const [step, setStep] = useState<Step>("demanda");
  const [demand, setDemand] = useState(20);
  const [scenario, setScenario] = useState<"A" | "B">("B");

  const impacto = useMemo(() => {
    const atraso = Math.max(0, Math.round(demand * 0.7));
    const necessidade = demand > 25 ? "Alta" : demand > 12 ? "Média" : "Controlada";
    return { atraso, necessidade };
  }, [demand]);

  return (
    <div className="page-shell">
      <header className="mb-8">
        <span className="page-header-kicker">Smart Core / Simulação</span>
        <h1 className="mt-2 text-4xl font-display tracking-tight">Simulador de Demanda</h1>
      </header>

      {step === "demanda" && (
        <Card>
          <CardHeader><CardTitle>1. Ajuste de Demanda</CardTitle></CardHeader>
          <CardContent className="space-y-6">
            <p className="text-sm text-muted-foreground">Ajuste o crescimento de vendas e avalie o impacto operacional.</p>
            <div className="rounded-xl border border-border bg-muted/35 p-6">
              <p className="text-5xl font-display text-primary">+{demand}%</p>
              <input type="range" min={0} max={50} value={demand} onChange={(e) => setDemand(Number(e.target.value))} className="mt-4 w-full" />
            </div>
            <div className="flex justify-end"><Button onClick={() => setStep("impacto")}>Continuar <ArrowRight className="size-4" /></Button></div>
          </CardContent>
        </Card>
      )}

      {step === "impacto" && (
        <Card>
          <CardHeader><CardTitle>2. Diagnóstico de Impacto</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm">Demanda simulada: <strong>+{demand}%</strong></p>
            <p className="text-sm">Necessidade operacional: <strong>{impacto.necessidade}</strong></p>
            <p className="text-sm text-muted-foreground">Estimativa de risco de atraso: {impacto.atraso}% sem ação corretiva.</p>
            <div className="flex justify-between"><Button variant="outline" onClick={() => setStep("demanda")}><ArrowLeft className="size-4" /> Voltar</Button><Button onClick={() => setStep("cenarios")}>Ver cenários <ArrowRight className="size-4" /></Button></div>
          </CardContent>
        </Card>
      )}

      {step === "cenarios" && (
        <div className="grid gap-4 md:grid-cols-2">
          {[
            { id: "A" as const, title: "Cenário A · Terceirização", desc: "Resposta rápida com menor capex." },
            { id: "B" as const, title: "Cenário B · Expansão de Ativos", desc: "Maior ROI no longo prazo." },
          ].map((c) => (
            <Card key={c.id} className={scenario === c.id ? "border-primary ring-2 ring-primary/15" : ""}>
              <CardHeader><CardTitle>{c.title}</CardTitle></CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">{c.desc}</p>
                <Button className="mt-4" variant={scenario === c.id ? "default" : "outline"} onClick={() => setScenario(c.id)}>
                  {scenario === c.id ? <><CheckCircle2 className="size-4" /> Selecionado</> : "Selecionar"}
                </Button>
              </CardContent>
            </Card>
          ))}
          <div className="md:col-span-2 flex justify-between"><Button variant="outline" onClick={() => setStep("impacto")}><ArrowLeft className="size-4" /> Voltar</Button><Button onClick={() => setStep("plano")}>Detalhar plano <ArrowRight className="size-4" /></Button></div>
        </div>
      )}

      {step === "plano" && (
        <Card>
          <CardHeader><CardTitle>4. Plano de Execução · Cenário {scenario}</CardTitle></CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm text-muted-foreground">Plano gerado para demanda de +{demand}%.</p>
            <ul className="list-disc pl-5 text-sm space-y-1">
              <li>Revisar capacidade logística e escala de equipes.</li>
              <li>Priorizar rotas com maior impacto em SLA.</li>
              <li>Acompanhar indicadores diariamente por 30 dias.</li>
            </ul>
            <div className="flex justify-between"><Button variant="outline" onClick={() => setStep("cenarios")}><ArrowLeft className="size-4" /> Voltar</Button><Button onClick={() => setStep("demanda")}>Nova simulação</Button></div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
