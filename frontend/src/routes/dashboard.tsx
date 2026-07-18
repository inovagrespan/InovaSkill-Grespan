import { createFileRoute } from "@tanstack/react-router";
import { SalesControlTower } from "@/components/SalesControlTower";
import { getCurrentUserRole } from "@/lib/auth";
import { LogisticsDashboardMetrics } from "@/routes/logistica.index";

export const Route = createFileRoute("/dashboard")({ component: Dashboard });

function Dashboard() {
  if (getCurrentUserRole() === "vendas") {
    return <SalesControlTower />;
  }

  return <LogisticsDashboardMetrics />;
}
