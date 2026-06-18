import { authFetch } from "@/lib/auth";
import { buildServiceUrl } from "@/lib/api-url";
import {
  getDemoCustomerFinanceImpact,
  hasCustomerFinanceImpactData,
  type CustomerFinanceImpactData,
} from "@/lib/customer-finance-impact-demo";

export type CustomerFinanceImpactListType = "risco" | "crescimento" | "oportunidades";

const ATTENTION_SCORE_WEIGHT = 1;
const ATTENTION_VARIATION_WEIGHT = 1;
const ATTENTION_CURRENCY_WEIGHT = 1;
const CURRENCY_PRIORITY_LOG_BASE = 10;
const CURRENCY_PRIORITY_OFFSET = 1;

export async function fetchCustomerFinanceImpact(): Promise<CustomerFinanceImpactData> {
  try {
    const base = buildServiceUrl("api/analytics-financeiro");
    const response = await authFetch(`${base}/impacto`);
    if (!response.ok) return getDemoCustomerFinanceImpact();

    const data = await response.json();
    return hasCustomerFinanceImpactData(data) ? data : getDemoCustomerFinanceImpact();
  } catch {
    return getDemoCustomerFinanceImpact();
  }
}

export function getImpactCustomerName(customer: any): string {
  return customer.clienteNome ?? customer.ClienteNome ?? customer.clienteId ?? customer.ClienteId ?? "Cliente";
}

export function getImpactPercent(customer: any): number | null {
  return customer.crescimento12M ?? customer.Crescimento12M ?? customer.variacaoPercentual ?? null;
}

export function getImpactActionScorePercent(customer: any, type: CustomerFinanceImpactListType): number | null {
  const score =
    type === "risco"
      ? customer.scoreRisco ?? customer.ScoreRisco
      : customer.scorePotencial ?? customer.ScorePotencial;

  if (score == null) return null;
  return Number(score);
}

export function formatImpactPercent(value: number | null, positivePrefix = false): string {
  if (value == null) return "-";
  const prefix = positivePrefix && value > 0 ? "+" : "";
  return `${prefix}${value.toFixed(1)}%`;
}

export function formatImpactActionPercent(
  customer: any,
  type: CustomerFinanceImpactListType,
  positivePrefix = false,
): string {
  const variationPercent = getImpactPercent(customer);
  if (variationPercent != null) return formatImpactPercent(variationPercent, positivePrefix);

  const scorePercent = getImpactActionScorePercent(customer, type);
  if (scorePercent == null) return "-";

  return `${scorePercent.toFixed(0)}%`;
}

function getCurrencyPriorityScore(value: number | null | undefined): number {
  return Math.log(Math.max(0, Number(value ?? 0)) + CURRENCY_PRIORITY_OFFSET) / Math.log(CURRENCY_PRIORITY_LOG_BASE);
}

function getPositiveImpactPercent(customer: any): number {
  return Math.max(0, getImpactPercent(customer) ?? 0);
}

function getNegativeImpactPercent(customer: any): number {
  return Math.abs(Math.min(0, getImpactPercent(customer) ?? 0));
}

export function getImpactAttentionScore(customer: any, type: CustomerFinanceImpactListType): number {
  const actionScore = getImpactActionScorePercent(customer, type) ?? 0;

  if (type === "risco") {
    return (
      actionScore * ATTENTION_SCORE_WEIGHT +
      getNegativeImpactPercent(customer) * ATTENTION_VARIATION_WEIGHT +
      getCurrencyPriorityScore(customer.impactoFinanceiro ?? customer.ImpactoFinanceiro) * ATTENTION_CURRENCY_WEIGHT
    );
  }

  if (type === "crescimento") {
    return (
      actionScore * ATTENTION_SCORE_WEIGHT +
      getPositiveImpactPercent(customer) * ATTENTION_VARIATION_WEIGHT +
      getCurrencyPriorityScore(customer.valorGerado ?? customer.ValorGerado) * ATTENTION_CURRENCY_WEIGHT
    );
  }

  return (
    actionScore * ATTENTION_SCORE_WEIGHT +
    getPositiveImpactPercent(customer) * ATTENTION_VARIATION_WEIGHT +
    getCurrencyPriorityScore(customer.faturamento12M ?? customer.Faturamento12M) * ATTENTION_CURRENCY_WEIGHT
  );
}

export function sortImpactListByAttention(items: any[], type: CustomerFinanceImpactListType): any[] {
  return [...items].sort((left, right) => {
    const scoreDifference = getImpactAttentionScore(right, type) - getImpactAttentionScore(left, type);
    if (scoreDifference !== 0) return scoreDifference;

    return getImpactCustomerName(left).localeCompare(getImpactCustomerName(right), "pt-BR");
  });
}

export function getImpactList(data: CustomerFinanceImpactData, type: CustomerFinanceImpactListType): any[] {
  if (type === "crescimento") return sortImpactListByAttention(data.crescimento, type);
  if (type === "oportunidades") return sortImpactListByAttention(data.oportunidades, type);
  return sortImpactListByAttention(data.risco, type);
}

export function getImpactListTitle(type: CustomerFinanceImpactListType): string {
  if (type === "crescimento") return "Maiores Crescimentos";
  if (type === "oportunidades") return "Oportunidades";
  return "Clientes em Risco";
}
