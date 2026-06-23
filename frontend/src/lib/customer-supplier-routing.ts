export type SupplierRiskLevel = "attention" | "high" | "critical";
export type SupplierEscalationStatus = "observing" | "notified" | "escalated";

export type SupplierCustomerSituation = {
  customerId: string;
  customerName: string;
  supplierId: string;
  supplierName: string;
  routeName: string;
  riskLevel: SupplierRiskLevel;
  riskLabel: string;
  riskReason: string;
  occurrenceHistory: string[];
  actionChecklist: string[];
  monthlyImpact: number;
  notificationSentAt: string | null;
  responseDeadlineAt: string | null;
  responseDeadlineHours: number;
  elapsedHours: number;
  remainingHours: number;
  status: SupplierEscalationStatus;
};

export type SupplierRouteGroup = {
  routeName: string;
  supplierName: string;
  customers: SupplierCustomerSituation[];
};

export type SupplierDashboardBySupplier = {
  supplierId: string;
  supplierName: string;
  totalCustomers: number;
  riskCustomers: number;
  criticalCustomers: number;
  awaitingAction: number;
  escalatedCustomers: number;
  averageResponseHours: number | null;
};

export type SupplierRouteDashboard = {
  summary: {
    totalCustomers: number;
    riskCustomers: number;
    notifiedCustomers: number;
    awaitingAction: number;
    escalatedCustomers: number;
    criticalCustomers: number;
    monitoredRoutes: number;
    averageSupplierResponseHours: number | null;
  };
  suppliers: SupplierDashboardBySupplier[];
  riskBuckets: Record<SupplierRiskLevel, number>;
  routes: SupplierRouteGroup[];
  managementQueue: SupplierCustomerSituation[];
  selectedSupplier: string;
};

const SUPPLIER_RESPONSE_LIMIT_HOURS = 24;
const RISK_ATTENTION_MIN_SCORE = 40;
const RISK_HIGH_MIN_SCORE = 60;
const RISK_CRITICAL_MIN_SCORE = 80;
const DEFAULT_ROUTE_NAME = "Rota não informada";
const DEFAULT_SUPPLIER_NAME = "Fornecedor não informado";
const DEFAULT_SUPPLIER_ID = "sem-fornecedor";
const DEFAULT_OCCURRENCE_HISTORY_LIMIT = 3;

function getTextValue(customer: any, fieldNames: string[]): string | null {
  for (const fieldName of fieldNames) {
    const value = customer[fieldName];
    if (value != null && String(value).trim() !== "") return String(value).trim();
  }

  return null;
}

function getNumberValue(customer: any, fieldNames: string[]): number | null {
  for (const fieldName of fieldNames) {
    const value = customer[fieldName];
    if (value == null || value === "") continue;

    const numberValue = Number(value);
    if (!Number.isNaN(numberValue)) return numberValue;
  }

  return null;
}

function getDateValue(customer: any, fieldNames: string[]): Date | null {
  const value = getTextValue(customer, fieldNames);
  if (!value) return null;

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

function getCustomerId(customer: any): string {
  return getTextValue(customer, ["clienteId", "ClienteId", "customerCode", "CustomerCode"]) ?? "";
}

function getCustomerName(customer: any): string {
  return getTextValue(customer, ["clienteNome", "ClienteNome", "customerName", "CustomerName"]) ?? getCustomerId(customer) ?? "Cliente";
}

function getSupplierName(customer: any): string {
  return getTextValue(customer, [
    "fornecedorNome",
    "FornecedorNome",
    "vendedorNome",
    "VendedorNome",
    "representanteNome",
    "RepresentanteNome",
    "supplierName",
    "SupplierName",
    "sellerName",
    "SellerName",
  ]) ?? DEFAULT_SUPPLIER_NAME;
}

function getSupplierId(customer: any): string {
  return getTextValue(customer, [
    "fornecedorId",
    "FornecedorId",
    "vendedorId",
    "VendedorId",
    "representanteId",
    "RepresentanteId",
    "supplierId",
    "SupplierId",
    "sellerId",
    "SellerId",
  ]) ?? getSupplierName(customer).toLocaleLowerCase("pt-BR");
}

function getRouteName(customer: any): string {
  return getTextValue(customer, ["rotaNome", "RotaNome", "routeName", "RouteName", "rota", "Rota"]) ?? DEFAULT_ROUTE_NAME;
}

function normalizeRiskLevel(value: string | null, score: number): SupplierRiskLevel {
  const normalized = value?.normalize("NFD").replace(/\p{Diacritic}/gu, "").toLocaleLowerCase("pt-BR") ?? "";

  if (normalized.includes("critic") || score >= RISK_CRITICAL_MIN_SCORE) return "critical";
  if (normalized.includes("alto") || normalized.includes("em risco") || score >= RISK_HIGH_MIN_SCORE) return "high";
  if (normalized.includes("atenc") || normalized.includes("medio") || score >= RISK_ATTENTION_MIN_SCORE) return "attention";

  return "attention";
}

function getRiskLabel(riskLevel: SupplierRiskLevel): string {
  if (riskLevel === "critical") return "Crítico";
  if (riskLevel === "high") return "Alto";
  return "Atenção";
}

function buildRiskReason(customer: any, riskLabel: string): string {
  const declinePeriod = getTextValue(customer, ["mesesQueda", "MesesQueda"]) ?? "período recente";
  const trend = getTextValue(customer, ["tendencia", "Tendencia"]) ?? "queda comercial";
  const impact = getNumberValue(customer, ["impactoFinanceiro", "ImpactoFinanceiro"]) ?? 0;

  return `${riskLabel} por ${trend.toLocaleLowerCase("pt-BR")} em ${declinePeriod}; impacto estimado de ${impact.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}/mês.`;
}

function buildOccurrenceHistory(customer: any): string[] {
  const explicitHistory = customer.historicoOcorrencias ?? customer.HistoricoOcorrencias;
  if (Array.isArray(explicitHistory) && explicitHistory.length > 0) {
    return explicitHistory.map((item) => String(item)).slice(0, DEFAULT_OCCURRENCE_HISTORY_LIMIT);
  }

  return [
    `Tendência: ${getTextValue(customer, ["tendencia", "Tendencia"]) ?? "queda não especificada"}.`,
    `Período afetado: ${getTextValue(customer, ["mesesQueda", "MesesQueda"]) ?? "período recente"}.`,
    `Score de risco: ${getNumberValue(customer, ["scoreRisco", "ScoreRisco"]) ?? 0}.`,
  ];
}

function buildActionChecklist(customer: any): string[] {
  const explicitChecklist = customer.checklistAcoes ?? customer.ChecklistAcoes;
  if (Array.isArray(explicitChecklist) && explicitChecklist.length > 0) {
    return explicitChecklist.map((item) => String(item));
  }

  return [
    `Contatar ${getCustomerName(customer)} e registrar retorno comercial.`,
    "Revisar pedidos, frequência, preço, ruptura e atendimento.",
    "Definir plano de recuperação com prazo e fornecedor responsável.",
    "Registrar a ação tomada para encerrar ou manter o monitoramento.",
  ];
}

function getNotificationDate(customer: any, riskLevel: SupplierRiskLevel, referenceDate: Date): Date | null {
  const explicitNotificationDate = getDateValue(customer, [
    "fornecedorNotificadoEm",
    "FornecedorNotificadoEm",
    "notificationSentAt",
    "NotificationSentAt",
  ]);
  if (explicitNotificationDate) return explicitNotificationDate;
  if (riskLevel === "attention") return null;

  const elapsedHours = getNumberValue(customer, ["horasDesdeNotificacao", "HorasDesdeNotificacao", "elapsedHours", "ElapsedHours"]) ?? 0;
  return new Date(referenceDate.getTime() - elapsedHours * 60 * 60 * 1000);
}

function getElapsedHours(notificationDate: Date | null, referenceDate: Date): number {
  if (!notificationDate) return 0;
  return Math.max(0, Math.floor((referenceDate.getTime() - notificationDate.getTime()) / (60 * 60 * 1000)));
}

function hasRegisteredAction(customer: any): boolean {
  return Boolean(
    getDateValue(customer, ["acaoRegistradaEm", "AcaoRegistradaEm", "lastActionAt", "LastActionAt"]) ||
    getTextValue(customer, ["acaoRegistrada", "AcaoRegistrada", "registeredAction", "RegisteredAction"]),
  );
}

function buildSituation(customer: any, referenceDate: Date): SupplierCustomerSituation {
  const score = getNumberValue(customer, ["scoreRisco", "ScoreRisco"]) ?? 0;
  const riskLevel = normalizeRiskLevel(getTextValue(customer, ["nivelRisco", "NivelRisco", "riskLevel", "RiskLevel"]), score);
  const notificationDate = getNotificationDate(customer, riskLevel, referenceDate);
  const elapsedHours = getElapsedHours(notificationDate, referenceDate);
  const remainingHours = notificationDate ? Math.max(0, SUPPLIER_RESPONSE_LIMIT_HOURS - elapsedHours) : 0;
  const actionRegistered = hasRegisteredAction(customer);
  const status: SupplierEscalationStatus =
    riskLevel === "attention"
      ? "observing"
      : !actionRegistered && elapsedHours >= SUPPLIER_RESPONSE_LIMIT_HOURS
        ? "escalated"
        : "notified";

  return {
    customerId: getCustomerId(customer),
    customerName: getCustomerName(customer),
    supplierId: getSupplierId(customer) || DEFAULT_SUPPLIER_ID,
    supplierName: getSupplierName(customer),
    routeName: getRouteName(customer),
    riskLevel,
    riskLabel: getRiskLabel(riskLevel),
    riskReason: buildRiskReason(customer, getRiskLabel(riskLevel)),
    occurrenceHistory: buildOccurrenceHistory(customer),
    actionChecklist: buildActionChecklist(customer),
    monthlyImpact: getNumberValue(customer, ["impactoFinanceiro", "ImpactoFinanceiro"]) ?? 0,
    notificationSentAt: notificationDate?.toISOString() ?? null,
    responseDeadlineAt: notificationDate ? new Date(notificationDate.getTime() + SUPPLIER_RESPONSE_LIMIT_HOURS * 60 * 60 * 1000).toISOString() : null,
    responseDeadlineHours: SUPPLIER_RESPONSE_LIMIT_HOURS,
    elapsedHours,
    remainingHours,
    status,
  };
}

function matchesSupplier(situation: SupplierCustomerSituation, selectedSupplier: string): boolean {
  return selectedSupplier === "todos" || situation.supplierId === selectedSupplier || situation.supplierName === selectedSupplier;
}

function calculateAverageResponseHours(situations: SupplierCustomerSituation[]): number | null {
  const responseTimes = situations
    .filter((situation) => situation.notificationSentAt)
    .map((situation) => Math.min(situation.elapsedHours, situation.responseDeadlineHours));

  if (responseTimes.length === 0) return null;

  return Math.round(responseTimes.reduce((total, value) => total + value, 0) / responseTimes.length);
}

export function buildSupplierRouteDashboard(
  riskCustomers: any[],
  selectedSupplier = "todos",
  referenceDate = new Date(),
): SupplierRouteDashboard {
  const allSituations = riskCustomers.map((customer) => buildSituation(customer, referenceDate));
  const situations = allSituations.filter((situation) => matchesSupplier(situation, selectedSupplier));

  const routes = Array.from(
    situations.reduce((groups, situation) => {
      const current = groups.get(situation.routeName) ?? {
        routeName: situation.routeName,
        supplierName: situation.supplierName,
        customers: [],
      };
      current.customers.push(situation);
      groups.set(situation.routeName, current);
      return groups;
    }, new Map<string, SupplierRouteGroup>()).values(),
  ).sort((left, right) => left.routeName.localeCompare(right.routeName, "pt-BR"));

  const suppliers = Array.from(
    allSituations.reduce((groups, situation) => {
      const current = groups.get(situation.supplierId) ?? {
        supplierId: situation.supplierId,
        supplierName: situation.supplierName,
        totalCustomers: 0,
        riskCustomers: 0,
        criticalCustomers: 0,
        awaitingAction: 0,
        escalatedCustomers: 0,
        averageResponseHours: null,
        situations: [] as SupplierCustomerSituation[],
      };

      current.totalCustomers += 1;
      current.riskCustomers += 1;
      if (situation.riskLevel === "critical") current.criticalCustomers += 1;
      if (situation.status === "notified") current.awaitingAction += 1;
      if (situation.status === "escalated") current.escalatedCustomers += 1;
      current.situations.push(situation);
      groups.set(situation.supplierId, current);
      return groups;
    }, new Map<string, SupplierDashboardBySupplier & { situations: SupplierCustomerSituation[] }>()).values(),
  )
    .map(({ situations: supplierSituations, ...supplier }) => ({
      ...supplier,
      averageResponseHours: calculateAverageResponseHours(supplierSituations),
    }))
    .sort((left, right) => left.supplierName.localeCompare(right.supplierName, "pt-BR"));

  return {
    summary: {
      totalCustomers: situations.length,
      riskCustomers: situations.length,
      notifiedCustomers: situations.filter((situation) => situation.status === "notified" || situation.status === "escalated").length,
      awaitingAction: situations.filter((situation) => situation.status === "notified").length,
      escalatedCustomers: situations.filter((situation) => situation.status === "escalated").length,
      criticalCustomers: situations.filter((situation) => situation.riskLevel === "critical").length,
      monitoredRoutes: routes.length,
      averageSupplierResponseHours: calculateAverageResponseHours(situations),
    },
    suppliers,
    riskBuckets: {
      attention: situations.filter((situation) => situation.riskLevel === "attention").length,
      high: situations.filter((situation) => situation.riskLevel === "high").length,
      critical: situations.filter((situation) => situation.riskLevel === "critical").length,
    },
    routes,
    managementQueue: situations
      .filter((situation) => situation.status === "escalated")
      .sort((left, right) => right.monthlyImpact - left.monthlyImpact),
    selectedSupplier,
  };
}
