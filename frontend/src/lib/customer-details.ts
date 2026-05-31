export function formatVariationPercent(value: number | null): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${value > 0 ? "+" : ""}${value.toFixed(1)}%`;
}

export function resolveComparisonColor(value: number | null): string {
  if (value == null || Number.isNaN(value)) return "text-muted-foreground";
  if (value > 0.05) return "text-[var(--success)]";
  if (value < -0.05) return "text-[var(--danger)]";
  return "text-muted-foreground";
}

export function resolveCustomerStatusVariant(status: string): "default" | "destructive" | "secondary" | "outline" {
  if (status === "Inativo" || status === "Em queda") return "destructive";
  if (status === "Em crescimento") return "default";
  return "secondary";
}

export function formatNullableCurrency(value: number | null, formatter: (input: number) => string): string {
  if (value == null) return "Sem dados";
  return formatter(value);
}

export function formatNullableCurrencyTooltip(value: number | null, formatter: (input: number) => string): string {
  if (value == null) return "Dados insuficientes no período filtrado";
  return formatter(value);
}

export function formatPurchaseFrequency(value: number | null): { value: string; tooltip: string } {
  if (value == null) {
    return {
      value: "Histórico insuficiente",
      tooltip: "Histórico insuficiente para calcular frequência.",
    };
  }

  return {
    value: `${value.toFixed(1)} dias`,
    tooltip: `Compra em média a cada ${value.toFixed(1)} dias`,
  };
}

export function resolveRiskBadgeVariant(riskLevel: string): "default" | "destructive" | "secondary" | "outline" {
  if (riskLevel === "Crítico") return "destructive";
  if (riskLevel === "Em risco") return "default";
  if (riskLevel === "Atenção") return "outline";
  return "secondary";
}
