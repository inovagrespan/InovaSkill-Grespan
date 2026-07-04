import { authFetch } from "@/lib/auth";
import { buildServiceUrl } from "@/lib/api-url";

export type CustomerFinanceProjectionData = {
  projecoes: {
    faturamentoMensalAtual: number;
    proximoMes: number;
    proximos3Meses: number;
    proximos6Meses: number;
    proximos12Meses: number;
  };
  cenarioOtimista: { margem: number; descricao: string };
  cenarioRealista: { margem: number; descricao: string };
  cenarioConservador: { margem: number; descricao: string };
  tendencias: Array<{ label: string; total: number; faturamento: number }>;
  evolucaoClientes: Array<{
    clienteId: string;
    clienteNome: string;
    valorAtual: number;
    valorProjetado: number;
    custoProjetado?: number;
    margemProjetadaPercentual?: number;
    diferenca: number;
    tendenciaPrevista: string;
    confiancaModelo: number;
  }>;
};

const SIMULATED_PROJECTED_COST_RATIOS = [0.61, 0.64, 0.58, 0.66, 0.62];
const PERCENTAGE_BASE = 100;
const PROJECTED_MARGIN_DECIMAL_PLACES = 1;

export function calculateSimulatedProjectedCost(projectedRevenue: number, rowIndex: number): number {
  if (projectedRevenue <= 0) return 0;

  const ratio = SIMULATED_PROJECTED_COST_RATIOS[rowIndex % SIMULATED_PROJECTED_COST_RATIOS.length];
  return Math.round(projectedRevenue * ratio);
}

export function calculateProjectedMarginPercent(projectedRevenue: number, projectedCost: number): number {
  if (projectedRevenue <= 0) return 0;

  const margin = ((projectedRevenue - projectedCost) / projectedRevenue) * PERCENTAGE_BASE;
  return Number(margin.toFixed(PROJECTED_MARGIN_DECIMAL_PLACES));
}

export function enrichCustomerFinanceProjectionCosts(data: CustomerFinanceProjectionData): CustomerFinanceProjectionData {
  return {
    ...data,
    evolucaoClientes: data.evolucaoClientes.map((cliente, index) => {
      const projectedRevenue = cliente.valorProjetado ?? 0;
      const projectedCost = cliente.custoProjetado ?? calculateSimulatedProjectedCost(projectedRevenue, index);

      return {
        ...cliente,
        custoProjetado: projectedCost,
        margemProjetadaPercentual: cliente.margemProjetadaPercentual ?? calculateProjectedMarginPercent(projectedRevenue, projectedCost),
      };
    }),
  };
}

export function getDemoCustomerFinanceProjections(): CustomerFinanceProjectionData {
  return enrichCustomerFinanceProjectionCosts({
    projecoes: {
      faturamentoMensalAtual: 156_325,
      proximoMes: 168_900,
      proximos3Meses: 512_400,
      proximos6Meses: 1_048_700,
      proximos12Meses: 2_185_000,
    },
    cenarioOtimista: { margem: 15, descricao: "Otimista (+15%)" },
    cenarioRealista: { margem: 0, descricao: "Realista" },
    cenarioConservador: { margem: -15, descricao: "Conservador (-15%)" },
    tendencias: [
      { label: "Crescimento acelerado", total: 4, faturamento: 284_700 },
      { label: "Crescimento saudável", total: 8, faturamento: 612_300 },
      { label: "Estabilidade", total: 11, faturamento: 438_900 },
      { label: "Risco de retração", total: 3, faturamento: 126_400 },
    ],
    evolucaoClientes: [
      {
        clienteId: "CLI-001",
        clienteNome: "Padaria São Bento",
        valorAtual: 26_650,
        valorProjetado: 31_200,
        diferenca: 4_550,
        tendenciaPrevista: "Crescimento",
        confiancaModelo: 0.88,
      },
      {
        clienteId: "CLI-002",
        clienteNome: "Supermercado Primavera",
        valorAtual: 24_400,
        valorProjetado: 21_850,
        diferenca: -2_550,
        tendenciaPrevista: "Queda",
        confiancaModelo: 0.74,
      },
      {
        clienteId: "CLI-003",
        clienteNome: "Cafeteria Grão & Massa",
        valorAtual: 24_740,
        valorProjetado: 27_300,
        diferenca: 2_560,
        tendenciaPrevista: "Crescimento",
        confiancaModelo: 0.81,
      },
      {
        clienteId: "CLI-004",
        clienteNome: "Rede Conveniência Rota 12",
        valorAtual: 18_900,
        valorProjetado: 18_450,
        diferenca: -450,
        tendenciaPrevista: "Estavel",
        confiancaModelo: 0.69,
      },
    ],
  });
}

export function hasCustomerFinanceProjectionData(value: Partial<CustomerFinanceProjectionData> | null | undefined): boolean {
  if (!value) return false;
  const projectionTotal =
    (value.projecoes?.faturamentoMensalAtual ?? 0) +
    (value.projecoes?.proximoMes ?? 0) +
    (value.projecoes?.proximos3Meses ?? 0);

  return projectionTotal > 0 || (value.tendencias?.length ?? 0) > 0 || (value.evolucaoClientes?.length ?? 0) > 0;
}

export async function fetchCustomerFinanceProjections(): Promise<CustomerFinanceProjectionData> {
  try {
    const base = buildServiceUrl("api/analytics-financeiro");
    const response = await authFetch(`${base}/projecoes`);
    if (!response.ok) return getDemoCustomerFinanceProjections();

    const data = await response.json();
    return hasCustomerFinanceProjectionData(data) ? enrichCustomerFinanceProjectionCosts(data) : getDemoCustomerFinanceProjections();
  } catch {
    return getDemoCustomerFinanceProjections();
  }
}
