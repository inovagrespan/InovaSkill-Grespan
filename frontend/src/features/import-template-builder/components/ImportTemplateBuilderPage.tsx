import { useMemo, useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { CheckCircle2, CircleAlert, FileSpreadsheet, Loader2, Save, Sparkles, Upload } from "lucide-react";
import { cn } from "@/lib/utils";
import { useImportTemplateBuilder } from "../hooks/useImportTemplateBuilder";
import type { BuilderFieldType, BuilderTargetField, FieldRuleConfig } from "../types";
import { parseManualHeaders } from "../utils/parse-manual-headers";

type Props = { templateId?: string };

const steps = [
  "Informações",
  "Exemplo",
  "Mapeamento",
  "Regras",
  "Preview",
  "Validação",
] as const;

const fieldTypeLabels: Record<BuilderFieldType, string> = {
  text: "Texto",
  number: "Número",
  date: "Data",
  currency: "Moeda",
};

export function ImportTemplateBuilderPage({ templateId }: Props) {
  const state = useImportTemplateBuilder(templateId);
  const [manualHeadersInput, setManualHeadersInput] = useState("");
  const manualHeaders = useMemo(() => parseManualHeaders(manualHeadersInput), [manualHeadersInput]);
  const activeField = state.targetFields.find((field) => field.name === state.activeTargetField) ?? state.targetFields[0];

  return (
    <div className="page-shell">
      <header className="animate-fade-in space-y-3">
        <span className="page-header-kicker">Smart Core / Importações</span>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <h1 className="text-3xl font-display tracking-tight md:text-4xl">Builder de Template de Importação</h1>
            <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
              Configure o template a partir de uma planilha real: detecte headers, conecte colunas, defina regras e valide o resultado antes de salvar.
            </p>
          </div>
          <Button onClick={() => void state.save()} disabled={state.saving || state.loading || !state.canSave}>
            {state.saving ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
            {templateId ? "Salvar alterações" : "Salvar template"}
          </Button>
        </div>
      </header>

      <Stepper currentStep={state.currentStep} onSelect={state.setCurrentStep} />

      {(state.error || state.success) && (
        <Alert variant={state.error ? "destructive" : "default"}>
          <AlertDescription>{state.error || state.success}</AlertDescription>
        </Alert>
      )}

      {state.loading ? (
        <LoadingState />
      ) : (
        <>
          {state.currentStep === 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Informações do Template</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-4 md:grid-cols-2">
                <Field label="Nome do template" className="md:col-span-2">
                  <Input value={state.name} onChange={(event) => state.setName(event.target.value)} placeholder="Template de vendas - Cliente Grespan" />
                </Field>
                <Field label="Tipo de arquivo">
                  <Select value={state.importFileTypeId} onValueChange={state.setImportFileTypeId}>
                    <SelectTrigger>
                      <SelectValue placeholder="Selecione o tipo" />
                    </SelectTrigger>
                    <SelectContent>
                      {state.fileTypes.map((type) => (
                        <SelectItem key={type.id} value={type.id}>
                          {type.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </Field>
                <Field label="Empresa vinculada">
                  <Input value={state.companyName} onChange={(event) => state.setCompanyName(event.target.value)} placeholder="Opcional" />
                </Field>
                <Field label="Descrição" className="md:col-span-2">
                  <Textarea value={state.description} onChange={(event) => state.setDescription(event.target.value)} rows={3} />
                </Field>
              </CardContent>
            </Card>
          )}

          {state.currentStep === 1 && (
            <div className="grid gap-6 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
              <Card>
                <CardHeader>
                  <CardTitle>Upload de exemplo</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  <label className="flex min-h-48 cursor-pointer flex-col items-center justify-center rounded-lg border border-dashed border-border bg-surface-soft p-6 text-center transition hover:border-primary/60 hover:bg-primary/5">
                    {state.headerLoading ? <Loader2 className="mb-3 size-8 animate-spin text-primary" /> : <Upload className="mb-3 size-8 text-primary" />}
                    <span className="text-sm font-semibold">Enviar CSV ou XLSX de exemplo</span>
                    <span className="mt-1 max-w-sm text-xs text-muted-foreground">O sistema lê os headers e separa algumas linhas para o preview processado.</span>
                    <Input
                      type="file"
                      accept=".csv,.xlsx"
                      className="sr-only"
                      onChange={(event) => {
                        const file = event.target.files?.[0];
                        if (file) void state.handleUpload(file);
                      }}
                    />
                  </label>

                  <div className="rounded-lg border border-border p-4">
                    <p className="text-sm font-medium">Colar headers manualmente</p>
                    <p className="mt-1 text-xs text-muted-foreground">Use quando ainda não tiver a planilha final. Aceita linha, vírgula, ponto-e-vírgula ou TAB.</p>
                    <Textarea
                      className="mt-3"
                      rows={4}
                      value={manualHeadersInput}
                      onChange={(event) => setManualHeadersInput(event.target.value)}
                      placeholder={"CLIENTE\nPRODUTO\nQUANTIDADE\nVlr. Unit.\nTotal\nData"}
                    />
                    <div className="mt-3 flex items-center justify-between gap-3">
                      <span className="text-xs text-muted-foreground">{manualHeaders.length} headers prontos</span>
                      <Button
                        type="button"
                        variant="secondary"
                        disabled={manualHeaders.length === 0}
                        onClick={() => {
                          state.setDetectedHeaders(manualHeaders);
                          state.setPreviewRows([]);
                          state.setCurrentStep(2);
                        }}
                      >
                        Aplicar headers
                      </Button>
                    </div>
                  </div>
                </CardContent>
              </Card>

              <HeadersAndRows headers={state.detectedHeaders} rows={state.previewRows} loading={state.headerLoading} />
            </div>
          )}

          {state.currentStep === 2 && (
            <div className="grid gap-6 xl:grid-cols-[minmax(280px,0.7fr)_minmax(0,1.3fr)]">
              <Card>
                <CardHeader>
                  <CardTitle>Headers encontrados</CardTitle>
                </CardHeader>
                <CardContent className="space-y-2">
                  {state.detectedHeaders.length === 0 ? (
                    <p className="text-sm text-muted-foreground">Envie uma planilha ou cole headers para começar.</p>
                  ) : (
                    state.detectedHeaders.map((header) => (
                      <div key={header} className={cn("rounded-lg border p-3 text-sm", state.mappedHeaders.has(header) ? "border-primary/40 bg-primary/10" : "border-border")}>
                        <div className="flex items-center justify-between gap-2">
                          <span className="font-mono text-xs">{header}</span>
                          {state.mappedHeaders.has(header) && <CheckCircle2 className="size-4 text-primary" />}
                        </div>
                      </div>
                    ))
                  )}
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>Mapeamento visual</CardTitle>
                </CardHeader>
                <CardContent className="grid gap-3 md:grid-cols-2">
                  {state.targetFields.map((field) => (
                    <MappingCard
                      key={field.name}
                      field={field}
                      headers={state.detectedHeaders}
                      value={state.mappings[field.name]?.sourceColumnName ?? ""}
                      onChange={(header) => state.assignHeader(field.name, header)}
                      selected={state.activeTargetField === field.name}
                      onSelect={() => state.setActiveTargetField(field.name)}
                    />
                  ))}
                </CardContent>
              </Card>
            </div>
          )}

          {state.currentStep === 3 && (
            <div className="grid gap-6 xl:grid-cols-[340px_minmax(0,1fr)]">
              <Card>
                <CardHeader>
                  <CardTitle>Campos mapeados</CardTitle>
                </CardHeader>
                <CardContent className="space-y-2">
                  {state.targetFields.filter((field) => state.mappings[field.name]?.sourceColumnName).map((field) => (
                    <button
                      key={field.name}
                      type="button"
                      onClick={() => state.setActiveTargetField(field.name)}
                      className={cn("w-full rounded-lg border p-3 text-left transition", activeField?.name === field.name ? "border-primary bg-primary/10" : "border-border hover:border-primary/50")}
                    >
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-sm font-semibold">{field.displayName}</span>
                        <Badge variant="outline">{fieldTypeLabels[field.dataType]}</Badge>
                      </div>
                      <p className="mt-1 font-mono text-xs text-muted-foreground">{state.mappings[field.name]?.sourceColumnName}</p>
                    </button>
                  ))}
                </CardContent>
              </Card>

              {activeField ? (
                <RulesEditor
                  field={activeField}
                  config={state.ruleConfigs[activeField.name]}
                  defaultValue={state.mappings[activeField.name]?.defaultValue ?? ""}
                  onConfigChange={(patch) => state.updateRuleConfig(activeField.name, patch)}
                  onDefaultValueChange={(value) => state.updateMapping(activeField.name, { defaultValue: value })}
                />
              ) : (
                <EmptyCard title="Nenhum campo mapeado" description="Mapeie ao menos um campo para configurar regras." />
              )}
            </div>
          )}

          {state.currentStep === 4 && <ProcessedPreview cells={state.processedPreview} hasRows={state.previewRows.length > 0} />}

          {state.currentStep === 5 && (
            <Card>
              <CardHeader>
                <CardTitle>Validação do template</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-3 md:grid-cols-2">
                {state.validationChecklist.map((item) => (
                  <div key={item.label} className={cn("rounded-lg border p-4", item.status === "ok" ? "border-emerald-200 bg-emerald-50 text-emerald-950" : "border-destructive/30 bg-destructive/10")}>
                    <div className="flex items-center gap-2 text-sm font-semibold">
                      {item.status === "ok" ? <CheckCircle2 className="size-4" /> : <CircleAlert className="size-4" />}
                      {item.label}
                    </div>
                    <p className="mt-1 text-xs opacity-80">{item.message}</p>
                  </div>
                ))}
              </CardContent>
            </Card>
          )}
        </>
      )}

      <footer className="flex items-center justify-between border-t border-border pt-4">
        <Button variant="outline" disabled={state.currentStep === 0} onClick={() => state.setCurrentStep(Math.max(0, state.currentStep - 1))}>
          Voltar
        </Button>
        <Button onClick={() => state.setCurrentStep(Math.min(steps.length - 1, state.currentStep + 1))} disabled={state.currentStep === steps.length - 1}>
          Próxima etapa
        </Button>
      </footer>
    </div>
  );
}

function Stepper({ currentStep, onSelect }: { currentStep: number; onSelect: (step: number) => void }) {
  return (
    <nav className="grid gap-2 md:grid-cols-6">
      {steps.map((label, index) => (
        <button
          key={label}
          type="button"
          onClick={() => onSelect(index)}
          className={cn("rounded-lg border px-3 py-3 text-left transition", currentStep === index ? "border-primary bg-primary text-primary-foreground shadow-sm" : "border-border bg-surface hover:border-primary/50")}
        >
          <span className="text-[10px] font-mono uppercase tracking-[0.2em] opacity-70">Etapa {index + 1}</span>
          <span className="mt-1 block text-sm font-semibold">{label}</span>
        </button>
      ))}
    </nav>
  );
}

function Field({ label, className, children }: { label: string; className?: string; children: React.ReactNode }) {
  return (
    <div className={cn("space-y-2", className)}>
      <Label>{label}</Label>
      {children}
    </div>
  );
}

function HeadersAndRows({ headers, rows, loading }: { headers: string[]; rows: Array<Record<string, string>>; loading: boolean }) {
  if (loading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Lendo planilha</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {Array.from({ length: 6 }).map((_, index) => (
            <Skeleton key={index} className="h-10 w-full" />
          ))}
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Headers e primeiras linhas</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap gap-2">
          {headers.map((header) => (
            <Badge key={header} variant="secondary" className="font-mono">
              {header}
            </Badge>
          ))}
          {headers.length === 0 && <p className="text-sm text-muted-foreground">Nenhum header carregado ainda.</p>}
        </div>
        {rows.length > 0 && (
          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full min-w-[720px] text-sm">
              <thead className="bg-muted/50">
                <tr>{headers.map((header) => <th key={header} className="px-3 py-2 text-left font-medium">{header}</th>)}</tr>
              </thead>
              <tbody>
                {rows.slice(0, 4).map((row, rowIndex) => (
                  <tr key={rowIndex} className="border-t border-border">
                    {headers.map((header) => <td key={header} className="px-3 py-2 text-muted-foreground">{row[header] ?? ""}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function MappingCard({
  field,
  headers,
  value,
  selected,
  onChange,
  onSelect,
}: {
  field: BuilderTargetField;
  headers: string[];
  value: string;
  selected: boolean;
  onChange: (header: string) => void;
  onSelect: () => void;
}) {
  return (
    <div className={cn("rounded-lg border p-4 transition", selected ? "border-primary bg-primary/10" : field.required && !value ? "border-destructive/40" : "border-border")}>
      <button type="button" onClick={onSelect} className="mb-3 w-full text-left">
        <div className="flex items-start justify-between gap-2">
          <div>
            <p className="text-sm font-semibold">{field.displayName}</p>
            <p className="mt-1 text-xs text-muted-foreground">{field.description || field.name}</p>
          </div>
          <Badge variant={field.required ? "secondary" : "outline"}>{field.required ? "Obrigatório" : "Opcional"}</Badge>
        </div>
      </button>
      <Select value={value || "__empty"} onValueChange={(header) => onChange(header === "__empty" ? "" : header)}>
        <SelectTrigger>
          <SelectValue placeholder="Escolha uma coluna" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="__empty">Sem mapeamento</SelectItem>
          {headers.map((header) => (
            <SelectItem key={header} value={header}>
              {header}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

function RulesEditor({
  field,
  config,
  defaultValue,
  onConfigChange,
  onDefaultValueChange,
}: {
  field: BuilderTargetField;
  config: FieldRuleConfig | undefined;
  defaultValue: string;
  onConfigChange: (patch: Partial<FieldRuleConfig>) => void;
  onDefaultValueChange: (value: string) => void;
}) {
  const current = config!;
  return (
    <Card>
      <CardHeader>
        <CardTitle>Regras de {field.displayName}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Valor padrão">
            <Input value={defaultValue} onChange={(event) => onDefaultValueChange(event.target.value)} placeholder="Opcional" />
          </Field>
          <Field label="Tipo">
            <Input value={fieldTypeLabels[field.dataType]} disabled />
          </Field>
        </div>

        {field.dataType === "text" && (
          <div className="grid gap-3 md:grid-cols-2">
            <RuleCheck label="Remover espaços extras" checked={current.trim} onCheckedChange={(checked) => onConfigChange({ trim: checked })} />
            <RuleCheck label="Converter para maiúsculo" checked={current.uppercase} onCheckedChange={(checked) => onConfigChange({ uppercase: checked })} />
            <RuleCheck label="Remover caracteres especiais" checked={current.removeSpecialCharacters} onCheckedChange={(checked) => onConfigChange({ removeSpecialCharacters: checked })} />
            <RuleCheck label="Permitir vazio" checked={current.allowEmpty} onCheckedChange={(checked) => onConfigChange({ allowEmpty: checked })} />
          </div>
        )}

        {(field.dataType === "number" || field.dataType === "currency") && (
          <div className="grid gap-4 md:grid-cols-3">
            <Field label="Separador decimal">
              <Select value={current.decimalSeparator} onValueChange={(value: "," | ".") => onConfigChange({ decimalSeparator: value })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value=",">Vírgula</SelectItem>
                  <SelectItem value=".">Ponto</SelectItem>
                </SelectContent>
              </Select>
            </Field>
            <Field label="Separador de milhar">
              <Select value={current.thousandSeparator} onValueChange={(value: "." | "," | "none") => onConfigChange({ thousandSeparator: value })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value=".">Ponto</SelectItem>
                  <SelectItem value=",">Vírgula</SelectItem>
                  <SelectItem value="none">Nenhum</SelectItem>
                </SelectContent>
              </Select>
            </Field>
            <Field label="Casas decimais">
              <Input value={current.decimalPlaces} onChange={(event) => onConfigChange({ decimalPlaces: event.target.value })} />
            </Field>
            <Field label="Valor mínimo">
              <Input value={current.minValue} onChange={(event) => onConfigChange({ minValue: event.target.value })} />
            </Field>
            <Field label="Valor máximo">
              <Input value={current.maxValue} onChange={(event) => onConfigChange({ maxValue: event.target.value })} />
            </Field>
            <div className="grid gap-3 md:col-span-3 md:grid-cols-3">
              <RuleCheck label="Permitir negativo" checked={current.allowNegative} onCheckedChange={(checked) => onConfigChange({ allowNegative: checked })} />
              <RuleCheck label="Remover símbolo R$" checked={current.removeCurrencySymbol} onCheckedChange={(checked) => onConfigChange({ removeCurrencySymbol: checked })} />
              <RuleCheck label="Validar valor positivo" checked={current.positiveOnly} onCheckedChange={(checked) => onConfigChange({ positiveOnly: checked })} />
            </div>
          </div>
        )}

        {field.dataType === "date" && (
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Formatos aceitos">
              <Input value={current.dateFormats} onChange={(event) => onConfigChange({ dateFormats: event.target.value })} placeholder="dd/MM/yyyy, yyyy-MM-dd" />
            </Field>
            <Field label="Timezone">
              <Input value={current.timezone} onChange={(event) => onConfigChange({ timezone: event.target.value })} />
            </Field>
            <RuleCheck label="Obrigatório" checked={!current.allowEmpty} onCheckedChange={(checked) => onConfigChange({ allowEmpty: !checked })} />
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function RuleCheck({ label, checked, onCheckedChange }: { label: string; checked: boolean; onCheckedChange: (checked: boolean) => void }) {
  return (
    <label className="flex items-center gap-3 rounded-lg border border-border p-3 text-sm">
      <Checkbox checked={checked} onCheckedChange={(value) => onCheckedChange(Boolean(value))} />
      {label}
    </label>
  );
}

function ProcessedPreview({ cells, hasRows }: { cells: ReturnType<typeof useImportTemplateBuilder>["processedPreview"]; hasRows: boolean }) {
  if (!hasRows) {
    return <EmptyCard title="Preview aguardando linhas" description="Envie uma planilha de exemplo para comparar valor original, valor processado e status." />;
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Preview processado</CardTitle>
      </CardHeader>
      <CardContent className="overflow-x-auto">
        <table className="w-full min-w-[860px] text-sm">
          <thead className="border-b border-border bg-muted/40">
            <tr>
              <th className="px-3 py-2 text-left font-medium">Linha</th>
              <th className="px-3 py-2 text-left font-medium">Coluna Original</th>
              <th className="px-3 py-2 text-left font-medium">Valor Original</th>
              <th className="px-3 py-2 text-left font-medium">Campo Interno</th>
              <th className="px-3 py-2 text-left font-medium">Valor Processado</th>
              <th className="px-3 py-2 text-left font-medium">Status</th>
            </tr>
          </thead>
          <tbody>
            {cells.slice(0, 40).map((cell, index) => (
              <tr key={`${cell.rowIndex}-${cell.targetFieldName}-${index}`} className="border-b border-border">
                <td className="px-3 py-2">{cell.rowIndex + 1}</td>
                <td className="px-3 py-2 font-mono text-xs">{cell.sourceColumnName}</td>
                <td className="px-3 py-2">{cell.originalValue}</td>
                <td className="px-3 py-2">{cell.targetFieldLabel}</td>
                <td className="px-3 py-2 font-mono text-xs">{cell.processedValue}</td>
                <td className="px-3 py-2"><StatusBadge status={cell.status} label={cell.message} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </CardContent>
    </Card>
  );
}

function StatusBadge({ status, label }: { status: "ok" | "warning" | "error"; label: string }) {
  if (status === "ok") return <Badge className="bg-emerald-600 text-white">OK</Badge>;
  if (status === "warning") return <Badge variant="secondary">{label}</Badge>;
  return <Badge variant="destructive">{label}</Badge>;
}

function EmptyCard({ title, description }: { title: string; description: string }) {
  return (
    <Card>
      <CardContent className="flex min-h-56 flex-col items-center justify-center p-8 text-center">
        <FileSpreadsheet className="mb-3 size-10 text-muted-foreground" />
        <p className="text-sm font-semibold">{title}</p>
        <p className="mt-1 max-w-md text-xs text-muted-foreground">{description}</p>
      </CardContent>
    </Card>
  );
}

function LoadingState() {
  return (
    <Card>
      <CardContent className="space-y-4 p-6">
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          <Sparkles className="size-4 text-primary" />
          Preparando o builder...
        </div>
        {Array.from({ length: 5 }).map((_, index) => (
          <Skeleton key={index} className="h-12 w-full" />
        ))}
      </CardContent>
    </Card>
  );
}
