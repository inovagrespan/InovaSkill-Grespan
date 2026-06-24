import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/pendencias")({
  beforeLoad: () => {
    throw redirect({ to: "/alertas" });
  },
});
