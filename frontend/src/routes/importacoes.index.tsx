import { Navigate, createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/importacoes/")({
  component: ImportacoesIndexRedirectPage,
});

function ImportacoesIndexRedirectPage() {
  return <Navigate to="/importacoes/files" />;
}
