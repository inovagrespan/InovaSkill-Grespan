export type ControlTowerPeriod = "today" | "next7" | "next30";
export type ControlTowerStatus = "green" | "yellow" | "red";
export type ControlTowerModule = "Vendas" | "Produtos" | "Finanças" | "Logística";

export type ControlTowerCard = {
  id: string;
  title: string;
  value: string;
  status: ControlTowerStatus;
  description: string;
  module: ControlTowerModule;
  href: string;
};

export type ControlTowerScenario = {
  period: ControlTowerPeriod;
  label: string;
  subtitle: string;
  cards: ControlTowerCard[];
};

const statusPriority: Record<ControlTowerStatus, number> = {
  red: 3,
  yellow: 2,
  green: 1,
};

function onlyUrgentCards(cards: ControlTowerCard[]): ControlTowerCard[] {
  return cards.filter((card) => card.status !== "green");
}

const scenarios: Record<ControlTowerPeriod, ControlTowerScenario> = {
  today: {
    period: "today",
    label: "Hoje",
    subtitle: "Situação atual da empresa com a base operacional disponível.",
    cards: [
      {
        id: "revenue-today",
        title: "Faturamento atual",
        value: "R$ 187,6 mil",
        status: "green",
        description: "Receita acima da média recente, com boa tração em notas fiscais.",
        module: "Vendas",
        href: "/clientes?aba=nota-fiscal&highlight=revenue-today",
      },
      {
        id: "stock-risk-today",
        title: "Produtos com risco de ruptura",
        value: "3 SKUs",
        status: "red",
        description: "Pães congelados e folhados exigem reposição para proteger vendas do dia.",
        module: "Produtos",
        href: "/produtos?highlight=stock-risk-today",
      },
      {
        id: "cash-risk-today",
        title: "Risco financeiro",
        value: "Baixo",
        status: "green",
        description: "Margem operacional preservada e faturamento suficiente para o ciclo atual.",
        module: "Finanças",
        href: "/clientes?aba=projecoes&highlight=cash-risk-today",
      },
      {
        id: "critical-customer-risk-today",
        title: "Cliente crítico em risco",
        value: "Supermercado Primavera",
        status: "red",
        description: "Cliente com queda recorrente e impacto financeiro relevante; priorizar plano de recuperação comercial.",
        module: "Finanças",
        href: "/clientes?aba=impacto&highlight=critical-customer-risk-today",
      },
      {
        id: "route-delay-today",
        title: "Possíveis atrasos logísticos",
        value: "2 rotas",
        status: "yellow",
        description: "Sorocaba e Campinas operam no limite de ocupação e exigem acompanhamento.",
        module: "Logística",
        href: "/logistica?highlight=route-delay-today",
      },
    ],
  },
  next7: {
    period: "next7",
    label: "Próximos 7 dias",
    subtitle: "Previsões baseadas no histórico de vendas, estoque, finanças e logística.",
    cards: [
      {
        id: "replenishment-7d",
        title: "Necessidade de reposição",
        value: "1.240 un.",
        status: "red",
        description: "Giro projetado indica reposição imediata para evitar ruptura em SKUs de panificação.",
        module: "Produtos",
        href: "/produtos?highlight=replenishment-7d",
      },
      {
        id: "demand-drop-7d",
        title: "Queda de demanda",
        value: "-6,4%",
        status: "yellow",
        description: "Clientes de conveniência mostram retração prevista nos próximos ciclos de compra.",
        module: "Vendas",
        href: "/clientes?aba=impacto&highlight=demand-drop-7d",
      },
      {
        id: "revenue-forecast-7d",
        title: "Previsão de faturamento",
        value: "R$ 224 mil",
        status: "green",
        description: "Projeção semanal positiva se a reposição dos SKUs críticos for concluída.",
        module: "Finanças",
        href: "/clientes?aba=projecoes&highlight=revenue-forecast-7d",
      },
      {
        id: "logistics-delay-7d",
        title: "Atrasos logísticos prováveis",
        value: "3 alertas",
        status: "yellow",
        description: "Rotas com ocupação acima do ideal podem pressionar SLA nos próximos embarques.",
        module: "Logística",
        href: "/logistica?highlight=logistics-delay-7d",
      },
    ],
  },
  next30: {
    period: "next30",
    label: "Próximos 30 dias",
    subtitle: "Tendências e previsões estratégicas para apoiar a tomada de decisão.",
    cards: [
      {
        id: "excess-stock-30d",
        title: "Possível excesso de estoque",
        value: "R$ 42 mil",
        status: "yellow",
        description: "Equipamentos têm giro mais lento e podem concentrar capital parado.",
        module: "Produtos",
        href: "/produtos?highlight=excess-stock-30d",
      },
      {
        id: "revenue-forecast-30d",
        title: "Previsão de faturamento",
        value: "R$ 782 mil",
        status: "green",
        description: "Tendência mensal favorece crescimento se ruptura e logística forem controladas.",
        module: "Vendas",
        href: "/clientes?aba=nota-fiscal&highlight=revenue-forecast-30d",
      },
      {
        id: "finance-risk-30d",
        title: "Riscos financeiros",
        value: "Médio",
        status: "yellow",
        description: "Capital em estoque e variação de demanda podem reduzir caixa disponível.",
        module: "Finanças",
        href: "/clientes?aba=projecoes&highlight=finance-risk-30d",
      },
      {
        id: "strategic-logistics-30d",
        title: "Capacidade logística",
        value: "86%",
        status: "red",
        description: "Crescimento previsto pressiona frota e aumenta chance de atraso sem redistribuição.",
        module: "Logística",
        href: "/logistica?highlight=strategic-logistics-30d",
      },
    ],
  },
};

export function getControlTowerScenario(period: ControlTowerPeriod): ControlTowerScenario {
  const scenario = scenarios[period];
  return {
    ...scenario,
    cards: onlyUrgentCards(scenario.cards),
  };
}

export function sortControlTowerCardsByRisk(cards: ControlTowerCard[]): ControlTowerCard[] {
  return [...cards].sort((left, right) => statusPriority[right.status] - statusPriority[left.status]);
}

export function getModuleHighlightFromLocation(): string {
  if (typeof window === "undefined") return "";
  return new URLSearchParams(window.location.search).get("highlight") ?? "";
}
