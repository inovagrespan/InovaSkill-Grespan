import { createFileRoute } from "@tanstack/react-router";
import { ImportTemplateBuilderPage } from "@/features/import-template-builder/components/ImportTemplateBuilderPage";

export const Route = createFileRoute("/importacoes/templates")({
  validateSearch: (search: Record<string, unknown>) => ({
    templateId: typeof search.templateId === "string" ? search.templateId : undefined,
  }),
  component: ImportacoesTemplatesPage,
});

function ImportacoesTemplatesPage() {
  const { templateId } = Route.useSearch();
  return <ImportTemplateBuilderPage templateId={templateId} />;
}
