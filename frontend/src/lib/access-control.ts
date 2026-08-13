import { normalizeUserRole } from "@/lib/auth";

export const APPLICATION_ROLES = ["diretor", "vendas", "logistica", "admin", "admin_system"] as const;
export type ApplicationRole = (typeof APPLICATION_ROLES)[number];

type NavigationAccess = {
  path: string;
  roles: readonly ApplicationRole[];
};

const ALL_ROLES: readonly ApplicationRole[] = APPLICATION_ROLES;
const MANAGEMENT_ROLES: readonly ApplicationRole[] = ["diretor", "admin", "admin_system"];
const ROUTE_VIEW_ROLES: readonly ApplicationRole[] = ALL_ROLES;
const LOGISTICS_ROLES: readonly ApplicationRole[] = ["diretor", "logistica", "admin", "admin_system"];
const COMMERCIAL_CONTEXT_ROLES: readonly ApplicationRole[] = ALL_ROLES;
const ADMIN_ROLES: readonly ApplicationRole[] = ["admin", "admin_system"];
const SYSTEM_ADMIN_ROLES: readonly ApplicationRole[] = ["admin_system"];
const ROUTE_SIMULATION_ROLES: readonly ApplicationRole[] = ["vendas", "logistica", "admin", "admin_system"];

const NAVIGATION_ACCESS: readonly NavigationAccess[] = [
  { path: "/administracao/whatsapp", roles: SYSTEM_ADMIN_ROLES },
  { path: "/administracao/consumo-ia", roles: ADMIN_ROLES },
  { path: "/administracao/memorias", roles: ADMIN_ROLES },
  { path: "/assistente", roles: ALL_ROLES },
  { path: "/meu-whatsapp", roles: ALL_ROLES },
  { path: "/simulador-whatsapp", roles: ALL_ROLES },
  { path: "/processamentos", roles: ADMIN_ROLES },
  { path: "/importacoes", roles: ADMIN_ROLES },
  { path: "/veiculos/tipos", roles: LOGISTICS_ROLES },
  { path: "/logistica/rotas", roles: ROUTE_VIEW_ROLES },
  { path: "/logistica", roles: LOGISTICS_ROLES },
  { path: "/rotas", roles: ROUTE_VIEW_ROLES },
  { path: "/producao", roles: LOGISTICS_ROLES },
  { path: "/financas", roles: MANAGEMENT_ROLES },
  { path: "/relatorios", roles: MANAGEMENT_ROLES },
  { path: "/simulacao", roles: MANAGEMENT_ROLES },
  { path: "/vendas", roles: ["diretor", "vendas", "admin", "admin_system"] },
  { path: "/mapa", roles: COMMERCIAL_CONTEXT_ROLES },
  { path: "/clientes", roles: COMMERCIAL_CONTEXT_ROLES },
  { path: "/notas-fiscais", roles: COMMERCIAL_CONTEXT_ROLES },
  { path: "/produtos", roles: COMMERCIAL_CONTEXT_ROLES },
  { path: "/estoque", roles: COMMERCIAL_CONTEXT_ROLES },
  { path: "/dashboard", roles: ALL_ROLES },
];

export function canRoleAccessPath(role: string | null, pathname: string): boolean {
  const normalizedRole = normalizeUserRole(role);
  const rule = NAVIGATION_ACCESS.find(
    ({ path }) => pathname === path || pathname.startsWith(`${path}/`),
  );

  if (!rule) return false;
  return rule.roles.includes(normalizedRole as ApplicationRole);
}

export function canRoleUseRouteSimulation(role: string | null): boolean {
  const normalizedRole = normalizeUserRole(role);
  return ROUTE_SIMULATION_ROLES.includes(normalizedRole as ApplicationRole);
}

export function getDefaultPathForRole(role: string | null): string {
  return canRoleAccessPath(role, "/dashboard") ? "/dashboard" : "/login";
}
