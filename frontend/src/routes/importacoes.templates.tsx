import { useEffect, useMemo, useState } from "react";
import { createFileRoute } from "@tanstack/react-router";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { fetchTemplateConfigs, saveTemplateConfig, type TemplateConfig } from "@/lib/importer-api";

export const Route = createFileRoute("/importacoes/templates")({
  component: ImportacoesTemplatesPage,
});

type FileTemplateType = "Customers" | "Products" | "Orders";
type AliasRow = { from: string; to: string };

function parseHeadersCsv(value: string): string[] {
  return value
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);
}

function parseAliasesText(value: string): AliasRow[] {
  return value
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const [from, to] = line.split("=").map((part) => part.trim());
      return { from: from ?? "", to: to ?? "" };
    })
    .filter((item) => item.from && item.to);
}

function formatAliasesText(rows: AliasRow[]): string {
  return rows
    .filter((row) => row.from.trim() && row.to.trim())
    .map((row) => `${row.from.trim()}=${row.to.trim()}`)
    .join("\n");
}

function ImportacoesTemplatesPage() {
  const [message, setMessage] = useState("");
  const [templateType, setTemplateType] = useState<FileTemplateType>("Customers");
  const [templateConfigs, setTemplateConfigs] = useState<TemplateConfig[]>([]);
  const [templateSaving, setTemplateSaving] = useState(false);

  const [templateName, setTemplateName] = useState("");
  const [templateActive, setTemplateActive] = useState(true);

  const [requiredHeaders, setRequiredHeaders] = useState<string[]>([]);
  const [aliasesRows, setAliasesRows] = useState<AliasRow[]>([]);

  const [advancedMode, setAdvancedMode] = useState(false);
  const [requiredHeadersCsvAdvanced, setRequiredHeadersCsvAdvanced] = useState("");
  const [aliasesTextAdvanced, setAliasesTextAdvanced] = useState("");

  useEffect(() => {
    void loadTemplateConfigs();
  }, []);

  useEffect(() => {
    const current = templateConfigs.find((x) => x.fileType === templateType);

    setTemplateName(current?.name ?? "");
    setTemplateActive(current?.isActive ?? true);

    const headersFromConfig = parseHeadersCsv(current?.requiredHeadersCsv ?? "");
    setRequiredHeaders(headersFromConfig.length > 0 ? headersFromConfig : [""]);

    const aliasesFromConfig = (current?.aliases ?? []).filter((item) => item.from?.trim() && item.to?.trim());
    setAliasesRows(aliasesFromConfig.length > 0 ? aliasesFromConfig : [{ from: "", to: "" }]);

    setRequiredHeadersCsvAdvanced(current?.requiredHeadersCsv ?? "");
    setAliasesTextAdvanced((current?.aliases ?? []).map((x) => `${x.from}=${x.to}`).join("\n"));
  }, [templateType, templateConfigs]);

  const normalizedHeaders = useMemo(
    () => requiredHeaders.map((item) => item.trim()).filter(Boolean),
    [requiredHeaders],
  );

  const normalizedAliases = useMemo(
    () => aliasesRows.map((row) => ({ from: row.from.trim(), to: row.to.trim() })).filter((row) => row.from && row.to),
    [aliasesRows],
  );

  async function loadTemplateConfigs() {
    try {
      const data = await fetchTemplateConfigs();
      setTemplateConfigs(data);
    } catch (error) {
      setMessage((error as Error).message);
    }
  }

  function addHeader() {
    setRequiredHeaders((prev) => [...prev, ""]);
  }

  function updateHeader(index: number, value: string) {
    setRequiredHeaders((prev) => prev.map((item, i) => (i === index ? value : item)));
  }

  function removeHeader(index: number) {
    setRequiredHeaders((prev) => {
      const next = prev.filter((_, i) => i !== index);
      return next.length > 0 ? next : [""];
    });
  }

  function addAliasRow() {
    setAliasesRows((prev) => [...prev, { from: "", to: "" }]);
  }

  function updateAliasRow(index: number, field: keyof AliasRow, value: string) {
    setAliasesRows((prev) => prev.map((item, i) => (i === index ? { ...item, [field]: value } : item)));
  }

  function removeAliasRow(index: number) {
    setAliasesRows((prev) => {
      const next = prev.filter((_, i) => i !== index);
      return next.length > 0 ? next : [{ from: "", to: "" }];
    });
  }

  async function handleSaveTemplate() {
    setTemplateSaving(true);
    setMessage("");

    try {
      const requiredHeadersCsv = advancedMode && requiredHeadersCsvAdvanced.trim()
        ? requiredHeadersCsvAdvanced
        : normalizedHeaders.join(",");

      const aliases = advancedMode && aliasesTextAdvanced.trim()
        ? parseAliasesText(aliasesTextAdvanced)
        : normalizedAliases;

      await saveTemplateConfig({
        fileType: templateType,
        name: templateName,
        isActive: templateActive,
        requiredHeadersCsv,
        aliases,
      });

      await loadTemplateConfigs();
      setMessage("Configuração de template salva com sucesso.");
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setTemplateSaving(false);
    }
  }

  return (
    <div className="p-12 space-y-6">
      <header className="animate-fade-in">
        <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-muted-foreground">Smart Core / Importações</span>
        <h1 className="text-4xl font-display tracking-tight mt-2">Configuração de Templates</h1>
      </header>

      {message && (
        <Alert>
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      )}

      <Card className="bg-surface border-border">
        <CardHeader>
          <CardTitle>Template Simplificado por Tipo de Arquivo</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <Tabs value={templateType} onValueChange={(value) => setTemplateType(value as FileTemplateType)}>
            <TabsList>
              <TabsTrigger value="Customers">Clientes</TabsTrigger>
              <TabsTrigger value="Products">Produtos</TabsTrigger>
              <TabsTrigger value="Orders">Pedidos</TabsTrigger>
            </TabsList>

            <TabsContent value={templateType} className="space-y-5 mt-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div className="space-y-2">
                  <Label>Nome do template</Label>
                  <Input value={templateName} onChange={(e) => setTemplateName(e.target.value)} placeholder="Template padrão" />
                </div>

                <div className="space-y-2">
                  <Label>Ativo</Label>
                  <label className="flex items-center gap-2 text-sm">
                    <input type="checkbox" checked={templateActive} onChange={(e) => setTemplateActive(e.target.checked)} />
                    Habilitar uso deste template
                  </label>
                </div>
              </div>

              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <Label>Headers obrigatórios</Label>
                  <Button type="button" variant="outline" size="sm" onClick={addHeader}>+ Adicionar header</Button>
                </div>
                <div className="space-y-2 rounded-md border border-border/60 p-3">
                  {requiredHeaders.map((header, index) => (
                    <div key={`header-${index}`} className="flex gap-2">
                      <Input
                        value={header}
                        onChange={(e) => updateHeader(index, e.target.value)}
                        placeholder="Ex.: email"
                      />
                      <Button type="button" variant="outline" onClick={() => removeHeader(index)}>
                        Remover
                      </Button>
                    </div>
                  ))}
                </div>
              </div>

              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <Label>Aliases de colunas</Label>
                  <Button type="button" variant="outline" size="sm" onClick={addAliasRow}>+ Adicionar alias</Button>
                </div>

                <div className="space-y-2 rounded-md border border-border/60 p-3">
                  {aliasesRows.map((row, index) => (
                    <div key={`alias-${index}`} className="grid grid-cols-1 md:grid-cols-[1fr_1fr_auto] gap-2">
                      <Input
                        value={row.from}
                        onChange={(e) => updateAliasRow(index, "from", e.target.value)}
                        placeholder="Origem (ex.: e-mail)"
                      />
                      <Input
                        value={row.to}
                        onChange={(e) => updateAliasRow(index, "to", e.target.value)}
                        placeholder="Destino (ex.: email)"
                      />
                      <Button type="button" variant="outline" onClick={() => removeAliasRow(index)}>
                        Remover
                      </Button>
                    </div>
                  ))}
                </div>
              </div>

              <div className="space-y-3 rounded-md border border-dashed border-border/70 p-4">
                <label className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={advancedMode} onChange={(e) => setAdvancedMode(e.target.checked)} />
                  Modo avançado (opcional)
                </label>

                {advancedMode && (
                  <div className="space-y-3">
                    <div className="space-y-2">
                      <Label>Headers por CSV (opcional)</Label>
                      <Input
                        value={requiredHeadersCsvAdvanced}
                        onChange={(e) => setRequiredHeadersCsvAdvanced(e.target.value)}
                        placeholder="name,email,phone"
                      />
                    </div>

                    <div className="space-y-2">
                      <Label>Aliases por texto (opcional)</Label>
                      <Textarea
                        value={aliasesTextAdvanced}
                        onChange={(e) => setAliasesTextAdvanced(e.target.value)}
                        placeholder={formatAliasesText(normalizedAliases) || "orderdate=ordered_at"}
                        className="min-h-28"
                      />
                    </div>
                  </div>
                )}
              </div>

              <div className="flex justify-end">
                <Button type="button" onClick={() => void handleSaveTemplate()} disabled={templateSaving || !templateName.trim()}>
                  {templateSaving ? "Salvando..." : "Salvar template"}
                </Button>
              </div>
            </TabsContent>
          </Tabs>
        </CardContent>
      </Card>
    </div>
  );
}