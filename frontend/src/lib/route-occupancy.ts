export const MEDIUM_OCCUPANCY_THRESHOLD = 0.6;
export const GOOD_OCCUPANCY_THRESHOLD = 0.8;
export const OVERLOAD_OCCUPANCY_THRESHOLD = 1;

export type OccupancyLevel = "idle" | "good" | "medium" | "critical" | "unavailable";

export type OccupancyPresentation = {
  level: OccupancyLevel;
  label: "Ocioso" | "Saudável" | "Médio" | "Crítico" | "Indisponível";
  color: string;
  backgroundColor: string;
};

const OCCUPANCY_PRESENTATIONS: Record<OccupancyLevel, OccupancyPresentation> = {
  idle: {
    level: "idle",
    label: "Ocioso",
    color: "#2563eb",
    backgroundColor: "rgba(37, 99, 235, 0.14)",
  },
  good: {
    level: "good",
    label: "Saudável",
    color: "#16a34a",
    backgroundColor: "rgba(22, 163, 74, 0.14)",
  },
  medium: {
    level: "medium",
    label: "Médio",
    color: "#eab308",
    backgroundColor: "rgba(234, 179, 8, 0.14)",
  },
  critical: {
    level: "critical",
    label: "Crítico",
    color: "#dc2626",
    backgroundColor: "rgba(220, 38, 38, 0.14)",
  },
  unavailable: {
    level: "unavailable",
    label: "Indisponível",
    color: "#64748b",
    backgroundColor: "rgba(100, 116, 139, 0.14)",
  },
};

export function classifyOccupancy(value: number | null): OccupancyPresentation {
  if (value === null || !Number.isFinite(value)) {
    return OCCUPANCY_PRESENTATIONS.unavailable;
  }
  if (value > OVERLOAD_OCCUPANCY_THRESHOLD) {
    return OCCUPANCY_PRESENTATIONS.critical;
  }
  if (value >= GOOD_OCCUPANCY_THRESHOLD) {
    return OCCUPANCY_PRESENTATIONS.good;
  }
  if (value >= MEDIUM_OCCUPANCY_THRESHOLD) {
    return OCCUPANCY_PRESENTATIONS.medium;
  }
  return OCCUPANCY_PRESENTATIONS.idle;
}

export function formatOccupancy(value: number | null): string {
  if (value === null || !Number.isFinite(value)) return "Capacidade não configurada";
  return value.toLocaleString("pt-BR", {
    style: "percent",
    minimumFractionDigits: 0,
    maximumFractionDigits: 1,
  });
}

export function formatCapacityKg(value: number | null): string {
  return value === null
    ? "Capacidade não configurada"
    : `${value.toLocaleString("pt-BR")} kg`;
}

export function formatRouteLoadKg(value: number): string {
  return `${value.toLocaleString("pt-BR", {
    minimumFractionDigits: 0,
    maximumFractionDigits: 3,
  })} kg/dia`;
}
