export type CustomerFinanceImpactAlert = {
  tipo: string;
  severidade: string;
  mensagem: string;
  total: number;
};

export type CustomerFinanceImpactRisk = {
  clienteId: string;
  clienteNome: string;
  faturamento12M: number;
  faturamento6M: number;
  faturamento3M: number;
  variacaoPercentual: number | null;
  tendencia: string;
  scoreRisco: number;
  nivelRisco: string;
  impactoFinanceiro: number;
  mesesQueda: string;
  classificacao: string;
  scoreRecencia: number;
  participacao: number;
};

export type CustomerFinanceImpactGrowth = {
  clienteId: string;
  clienteNome: string;
  faturamento12M: number;
  crescimento12M: number | null;
  scorePotencial: number;
  scoreCrescimento: number;
  valorGerado: number;
  potencialFuturo: string;
  participacao: number;
};

export type CustomerFinanceImpactOpportunity = {
  clienteId: string;
  clienteNome: string;
  scorePotencial: number;
  crescimento12M: number | null;
  faturamento12M: number;
  potencial: string;
  ticketMedioGeral: number;
  frequenciaCompra: number;
};

export type CustomerFinanceImpactData = {
  risco: CustomerFinanceImpactRisk[];
  crescimento: CustomerFinanceImpactGrowth[];
  alertas: CustomerFinanceImpactAlert[];
  oportunidades: CustomerFinanceImpactOpportunity[];
  resumo: {
    maiorCliente: string;
    maiorFaturamento: number;
    maiorCrescimentoNome: string;
    maiorCrescimentoPct: number;
    maiorQuedaNome: string;
    maiorQuedaPct: number;
    consistenteNome: string;
    maiorPotencialNome: string;
    maiorPotencialScore: number;
    totalClientes: number;
  };
};

export function getDemoCustomerFinanceImpact(): CustomerFinanceImpactData {
  return {
    risco: [
      {
        clienteId: "CLI-002",
        clienteNome: "Supermercado Primavera",
        faturamento12M: 52_300,
        faturamento6M: 24_400,
        faturamento3M: 11_100,
        variacaoPercentual: -14.8,
        tendencia: "Queda",
        scoreRisco: 72,
        nivelRisco: "Alto",
        impactoFinanceiro: 1_642.8,
        mesesQueda: "2+ meses consecutivos",
        classificacao: "C",
        scoreRecencia: 38,
        participacao: 27.9,
      },
      {
        clienteId: "CLI-004",
        clienteNome: "Rede Conveniência Rota 12",
        faturamento12M: 31_500,
        faturamento6M: 18_900,
        faturamento3M: 8_400,
        variacaoPercentual: -8.2,
        tendencia: "Queda",
        scoreRisco: 58,
        nivelRisco: "Médio",
        impactoFinanceiro: 688.8,
        mesesQueda: "1 mês",
        classificacao: "C",
        scoreRecencia: 42,
        participacao: 16.8,
      },
    ],
    crescimento: [
      {
        clienteId: "CLI-001",
        clienteNome: "Padaria São Bento",
        faturamento12M: 66_850,
        crescimento12M: 24.6,
        scorePotencial: 88,
        scoreCrescimento: 100,
        valorGerado: 18_200,
        potencialFuturo: "Alto potencial",
        participacao: 35.6,
      },
      {
        clienteId: "CLI-003",
        clienteNome: "Cafeteria Grão & Massa",
        faturamento12M: 38_940,
        crescimento12M: 18.3,
        scorePotencial: 76,
        scoreCrescimento: 80,
        valorGerado: 10_540,
        potencialFuturo: "Bom potencial",
        participacao: 20.7,
      },
    ],
    alertas: [
      {
        tipo: "queda-abrupta",
        severidade: "Alto",
        mensagem: "2 clientes demonstram queda de faturamento e pedem ação comercial.",
        total: 2,
      },
      {
        tipo: "dependencia",
        severidade: "Médio",
        mensagem: "Top 3 clientes concentram 84,2% do faturamento demonstrativo.",
        total: 3,
      },
    ],
    oportunidades: [
      {
        clienteId: "CLI-001",
        clienteNome: "Padaria São Bento",
        scorePotencial: 88,
        crescimento12M: 24.6,
        faturamento12M: 66_850,
        potencial: "Alto potencial",
        ticketMedioGeral: 2_228.33,
        frequenciaCompra: 2.4,
      },
      {
        clienteId: "CLI-003",
        clienteNome: "Cafeteria Grão & Massa",
        scorePotencial: 76,
        crescimento12M: 18.3,
        faturamento12M: 38_940,
        potencial: "Bom potencial",
        ticketMedioGeral: 2_163.33,
        frequenciaCompra: 1.8,
      },
    ],
    resumo: {
      maiorCliente: "Padaria São Bento",
      maiorFaturamento: 66_850,
      maiorCrescimentoNome: "Padaria São Bento",
      maiorCrescimentoPct: 24.6,
      maiorQuedaNome: "Supermercado Primavera",
      maiorQuedaPct: -14.8,
      consistenteNome: "Rede Conveniência Rota 12",
      maiorPotencialNome: "Padaria São Bento",
      maiorPotencialScore: 88,
      totalClientes: 4,
    },
  };
}

export function hasCustomerFinanceImpactData(value: Partial<CustomerFinanceImpactData> | null | undefined): boolean {
  if (!value) return false;

  return (
    (value.risco?.length ?? 0) > 0 ||
    (value.crescimento?.length ?? 0) > 0 ||
    (value.alertas?.length ?? 0) > 0 ||
    (value.oportunidades?.length ?? 0) > 0 ||
    Object.keys(value.resumo ?? {}).length > 0
  );
}
