export function formatVariationPercent(value: number | null): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${value > 0 ? "+" : ""}${value.toFixed(1)}%`;
}

export function resolveRankingTrend(value: number | null): {
  label: "Crescendo" | "Caindo" | "Estável" | "Sem base";
  badgeVariant: "default" | "destructive" | "secondary" | "outline";
  textClassName: string;
} {
  if (value == null || Number.isNaN(value)) {
    return {
      label: "Sem base",
      badgeVariant: "outline",
      textClassName: "text-muted-foreground",
    };
  }

  if (value >= 5) {
    return {
      label: "Crescendo",
      badgeVariant: "default",
      textClassName: "text-[var(--success)]",
    };
  }

  if (value <= -5) {
    return {
      label: "Caindo",
      badgeVariant: "destructive",
      textClassName: "text-[var(--danger)]",
    };
  }

  return {
    label: "Estável",
    badgeVariant: "secondary",
    textClassName: "text-muted-foreground",
  };
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
