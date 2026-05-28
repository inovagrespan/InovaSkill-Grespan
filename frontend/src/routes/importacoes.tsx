import { Outlet, createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/importacoes")({
  component: ImportacoesLayoutPage,
});

function ImportacoesLayoutPage() {
  return <Outlet />;
}
