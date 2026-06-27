export function formatKpiCompactNumber(value: number): string {
  const normalizedValue = Number.isFinite(value) ? value : 0;
  const abs = Math.abs(normalizedValue);

  if (abs >= 1_000_000_000) {
    return `${formatWithOneDecimal(normalizedValue / 1_000_000_000)} bi`;
  }

  if (abs >= 1_000_000) {
    return `${formatWithOneDecimal(normalizedValue / 1_000_000)} M`;
  }

  if (abs >= 1_000) {
    return `${formatWithOneDecimal(normalizedValue / 1_000)} mil`;
  }

  return new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 2 }).format(normalizedValue);
}

export function formatKpiCompactCurrency(value: number): string {
  return `R$ ${formatKpiCompactNumber(value)}`;
}

function formatWithOneDecimal(value: number): string {
  const rounded = Number(value.toFixed(1));
  return new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1, minimumFractionDigits: 0 }).format(rounded);
}
