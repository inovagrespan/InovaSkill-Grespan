import { Navigate, createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/logistica/rotas")({
  component: LogisticaRotasRedirect,
});

function LogisticaRotasRedirect() {
  return <Navigate to="/rotas" replace />;
}
