import { createFileRoute } from "@tanstack/react-router";
import { BusinessAssistant } from "@/components/BusinessAssistant";

export const Route = createFileRoute("/assistente")({ component: AssistantPage });

function AssistantPage() {
  return (
    <div className="flex h-dvh min-h-0 flex-col overflow-hidden">
      <BusinessAssistant variant="page" />
    </div>
  );
}
