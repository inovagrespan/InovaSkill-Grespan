import { createFileRoute } from "@tanstack/react-router";
import { LogisticsDashboardMetrics } from "@/routes/logistica.index";

export const Route = createFileRoute("/dashboard")({ component: Dashboard });

function Dashboard() {
  return <LogisticsDashboardMetrics />;
}
