import { Outlet, createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/reunioes")({
  component: ReunioesLayout,
});

function ReunioesLayout() {
  return <Outlet />;
}
