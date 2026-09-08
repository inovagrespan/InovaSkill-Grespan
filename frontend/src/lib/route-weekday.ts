import type { ImportedRouteItem } from "@/lib/importer-api";

const WEEKDAY_ORDER = ["MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY"] as const;

export function groupRoutesByWeekday(routes: ImportedRouteItem[]) {
  return WEEKDAY_ORDER
    .map((weekday) => ({ weekday, routes: routes.filter((route) => route.weekday === weekday) }))
    .filter((group) => group.routes.length > 0);
}
