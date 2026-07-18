import { Outlet, createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/detections")({
  component: DetectionsLayoutPage,
});

function DetectionsLayoutPage() {
  return <Outlet />;
}
