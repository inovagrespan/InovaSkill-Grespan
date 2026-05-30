import { useMemo, useState } from "react";
import { closestCenter, DndContext, DragEndEvent, DragOverlay, PointerSensor, useDraggable, useDroppable, useSensor, useSensors } from "@dnd-kit/core";
import { CSS } from "@dnd-kit/utilities";
import {
  SortableContext,
  arrayMove,
  verticalListSortingStrategy,
  useSortable,
} from "@dnd-kit/sortable";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Textarea } from "@/components/ui/textarea";
import { CheckCircle2, GripVertical, Loader2, Save, TriangleAlert, X } from "lucide-react";
import { cn } from "@/lib/utils";
import { useImportTemplateBuilder } from "../hooks/useImportTemplateBuilder";
import { parseManualHeaders } from "../utils/parse-manual-headers";

type Props = { templateId?: string };

export function ImportTemplateBuilderPage({ templateId }: Props) {
  const state = useImportTemplateBuilder(templateId);
  const [manualHeadersInput, setManualHeadersInput] = useState("");
  const [activeDragId, setActiveDragId] = useState<string | null>(null);
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 8 } }));
  const normalizedManualHeaders = useMemo(() => parseManualHeaders(manualHeadersInput), [manualHeadersInput]);
  const activeHeaderLabel = useMemo(() => {
    if (!activeDragId) return null;
    return state.detectedHeaders.includes(activeDragId) ? activeDragId : null;
  }, [activeDragId, state.detectedHeaders]);
  const activeRuleLabel = useMemo(() => {
    if (!activeDragId || !state.activeTargetField) return null;
    const activeRule = state.mappings[state.activeTargetField]?.transformRules.find((rule) => rule.id === activeDragId);
    if (!activeRule) return null;
    return state.transformRules.find((rule) => rule.id === activeRule.transformRuleId)?.name ?? activeRule.transformRuleId;
  }, [activeDragId, state.activeTargetField, state.mappings, state.transformRules]);

  function handleDragEnd(event: DragEndEvent) {
    setActiveDragId(null);
    const header = String(event.active.id || "");
    const dropId = String(event.over?.id || "");
    if (dropId.startsWith("field:")) {
      state.assignHeader(dropId.replace("field:", ""), header);
      return;
    }

    if (!state.activeTargetField || !dropId.startsWith("rule:")) return;
    const rules = state.mappings[state.activeTargetField]?.transformRules ?? [];
    const oldIndex = rules.findIndex((x) => x.id === String(event.active.id));
    const newIndex = rules.findIndex((x) => x.id === String(event.over?.id));
    if (oldIndex >= 0 && newIndex >= 0 && oldIndex !== newIndex) {
      state.reorderRules(state.activeTargetField, arrayMove(rules, oldIndex, newIndex));
    }
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragStart={(event) => setActiveDragId(String(event.active.id))}
      onDragCancel={() => setActiveDragId(null)}
      onDragEnd={handleDragEnd}
    >
      <div className="p-12 space-y-6">
        <header className="animate-fade-in space-y-2">
          <span className="text-[10px] font-mono uppercase tracking-[0.3em] text-muted-foreground">Smart Core / Importações</span>
          <h1 className="text-4xl font-display tracking-tight">Configurar Template de Importação</h1>
          <p className="text-sm text-muted-foreground max-w-2xl">Monte o template visualmente: detecte headers, mapeie campos internos e configure regras de transformação.</p>
        </header>

        {(state.error || state.success) && (
          <Alert variant={state.error ? "destructive" : "default"}>
            <AlertDescription>{state.error || state.success}</AlertDescription>
          </Alert>
        )}

        <Card className="bg-surface border-border">
          <CardHeader><CardTitle>Informações básicas</CardTitle></CardHeader>
          <CardContent className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="space-y-2 md:col-span-2"><Label>Nome do template</Label><Input value={state.name} onChange={(e) => state.setName(e.target.value)} placeholder="Template Nota Fiscal Cliente X" /></div>
            <div className="space-y-2"><Label>Tipo de importação</Label>
              <Select value={state.importFileTypeId} onValueChange={state.setImportFileTypeId}>
                <SelectTrigger><SelectValue placeholder="Selecione" /></SelectTrigger>
                <SelectContent>{state.fileTypes.map((t) => <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div className="space-y-2 md:col-span-3"><Label>Descrição (opcional)</Label><Textarea value={state.description} onChange={(e) => state.setDescription(e.target.value)} rows={2} /></div>
            <div className="md:col-span-3 rounded-lg border border-border/70 bg-background/40 p-4 space-y-3">
              <div>
                <p className="text-sm font-medium">Colar headers manualmente</p>
                <p className="text-xs text-muted-foreground">Aceita uma linha por header, TAB (copiado do Excel), vírgula ou ponto-e-vírgula.</p>
              </div>
              <Textarea
                rows={4}
                value={manualHeadersInput}
                onChange={(e) => setManualHeadersInput(e.target.value)}
                placeholder={"documento\ndata\ncliente\nvalor_total"}
              />
              <div className="flex items-center justify-between gap-3">
                <span className="text-xs text-muted-foreground">{normalizedManualHeaders.length} headers prontos para aplicar</span>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={normalizedManualHeaders.length === 0}
                  onClick={() => state.setDetectedHeaders(normalizedManualHeaders)}
                >
                  Aplicar headers
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>

        <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
          <Card className="bg-surface border-border">
            <CardHeader><CardTitle>Headers encontrados</CardTitle></CardHeader>
            <CardContent className="space-y-2">
              {state.detectedHeaders.length === 0 && <p className="text-sm text-muted-foreground">Nenhum header detectado.</p>}
              {state.detectedHeaders.map((header) => <HeaderCard key={header} header={header} mapped={state.mappedHeaders.has(header)} />)}
            </CardContent>
          </Card>

          <Card className="bg-surface border-border">
            <CardHeader><CardTitle>Campos internos</CardTitle></CardHeader>
            <CardContent className="space-y-2">
              {state.targetFields.map((field) => {
                const mapped = state.mappings[field.name];
                const requiredMissing = field.required && !mapped?.sourceColumnName;
                return (
                  <TargetFieldDropZone key={field.name} id={field.name} onClick={() => state.setActiveTargetField(field.name)}>
                    <div className={cn("rounded-lg border p-3 transition-colors", requiredMissing ? "border-destructive/50" : "border-border", mapped?.sourceColumnName && "bg-primary/10 border-primary/40")}>
                      <div className="flex items-center justify-between gap-2">
                        <p className="text-sm font-semibold">{field.displayName}</p>
                        {field.required ? <Badge variant={requiredMissing ? "destructive" : "secondary"}>Obrigatório</Badge> : <Badge variant="outline">Opcional</Badge>}
                      </div>
                      <p className="text-xs text-muted-foreground mt-1">{mapped?.sourceColumnName ? `${mapped.sourceColumnName} → ${field.name}` : "Arraste um header para mapear"}</p>
                    </div>
                  </TargetFieldDropZone>
                );
              })}
            </CardContent>
          </Card>
        </div>

        {state.missingRequiredFields.length > 0 && (
          <Alert variant="destructive"><TriangleAlert className="size-4" /><AlertDescription>Campos obrigatórios não mapeados: {state.missingRequiredFields.map((f) => f.displayName).join(", ")}.</AlertDescription></Alert>
        )}

        <div className="flex justify-end"><Button onClick={() => void state.save()} disabled={state.saving || state.loading}>{state.saving ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />} {templateId ? "Salvar alterações" : "Salvar template"}</Button></div>
      </div>

      <Sheet open={Boolean(state.activeTargetField)} onOpenChange={(open) => !open && state.setActiveTargetField(null)}>
        <SheetContent className="sm:max-w-xl bg-surface border-border overflow-y-auto">
          {state.activeTargetField && (
            <>
              <SheetHeader>
                <SheetTitle>Detalhes do mapeamento</SheetTitle>
                <SheetDescription>Configure valor padrão e ordem das regras para o campo selecionado.</SheetDescription>
              </SheetHeader>
              <div className="mt-6 space-y-4">
                <div className="rounded-lg border border-border p-3 text-sm">
                  <p><span className="text-muted-foreground">Header:</span> {state.mappings[state.activeTargetField]?.sourceColumnName || "Não mapeado"}</p>
                  <p><span className="text-muted-foreground">Destino:</span> {state.activeTargetField}</p>
                </div>
                <div className="space-y-2"><Label>Valor padrão (opcional)</Label><Input value={state.mappings[state.activeTargetField]?.defaultValue ?? ""} onChange={(e) => state.updateMapping(state.activeTargetField!, { defaultValue: e.target.value })} /></div>
                <div className="space-y-2"><Label>Adicionar regra</Label>
                  <Select onValueChange={(value) => state.upsertRule(state.activeTargetField!, value)}>
                    <SelectTrigger><SelectValue placeholder="Selecione uma regra" /></SelectTrigger>
                    <SelectContent>{state.transformRules.map((rule) => <SelectItem key={rule.id} value={rule.id}>{rule.name}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <SortableContext items={(state.mappings[state.activeTargetField]?.transformRules ?? []).map((r) => r.id)} strategy={verticalListSortingStrategy}>
                  <div className="space-y-2">
                    {(state.mappings[state.activeTargetField]?.transformRules ?? []).map((rule) => (
                      <SortableRuleCard
                        key={rule.id}
                        id={rule.id}
                        label={state.transformRules.find((x) => x.id === rule.transformRuleId)?.name ?? rule.transformRuleId}
                        onRemove={() => state.removeRule(state.activeTargetField!, rule.id)}
                      >
                        <Textarea rows={3} placeholder='ParametersJson ex.: {"culture":"pt-BR"}' value={rule.parametersJson} onChange={(e) => {
                          const list = [...(state.mappings[state.activeTargetField!]?.transformRules ?? [])];
                          const idx = list.findIndex((x) => x.id === rule.id);
                          if (idx >= 0) { list[idx] = { ...list[idx], parametersJson: e.target.value }; state.updateMapping(state.activeTargetField!, { transformRules: list }); }
                        }} />
                      </SortableRuleCard>
                    ))}
                  </div>
                </SortableContext>
              </div>
            </>
          )}
        </SheetContent>
      </Sheet>

      <DragOverlay dropAnimation={{ duration: 180, easing: "cubic-bezier(0.16, 1, 0.3, 1)" }}>
        {activeDragId ? (
          <div className="pointer-events-none rounded-lg border border-primary/50 bg-background/95 px-4 py-2 shadow-2xl ring-1 ring-primary/20 backdrop-blur-sm">
            <span className="font-mono text-xs">{activeHeaderLabel ?? activeRuleLabel ?? activeDragId}</span>
          </div>
        ) : null}
      </DragOverlay>
    </DndContext>
  );
}

function HeaderCard({ header, mapped }: { header: string; mapped: boolean }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({ id: header });
  return (
    <button
      ref={setNodeRef}
      type="button"
      {...listeners}
      {...attributes}
      style={{ transform: CSS.Translate.toString(transform) }}
      className={cn(
        "w-full rounded-lg border p-3 text-left transition-[transform,box-shadow,background-color,border-color,opacity] duration-200",
        mapped ? "border-primary/50 bg-primary/10" : "border-border",
        isDragging && "opacity-0",
      )}
    >
      <div className="flex items-center justify-between gap-2"><span className="font-mono text-xs">{header}</span>{mapped && <CheckCircle2 className="size-4 text-primary" />}</div>
    </button>
  );
}

function TargetFieldDropZone({ id, onClick, children }: { id: string; onClick: () => void; children: React.ReactNode }) {
  const { setNodeRef, isOver } = useDroppable({ id: `field:${id}` });
  return (
    <div
      ref={setNodeRef}
      onClick={onClick}
      className={cn(
        "rounded-lg transition-[transform,box-shadow] duration-200",
        isOver && "scale-[1.01] shadow-lg ring-2 ring-primary/50",
      )}
    >
      {children}
    </div>
  );
}

function SortableRuleCard({
  id,
  label,
  children,
  onRemove,
}: {
  id: string;
  label: string;
  children: React.ReactNode;
  onRemove: () => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id,
    transition: {
      duration: 200,
      easing: "cubic-bezier(0.16, 1, 0.3, 1)",
    },
  });
  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={cn(
        "rounded-lg border border-border p-3 bg-background/40 transition-[transform,box-shadow,border-color,background-color,opacity] duration-200 hover:border-primary/40 hover:shadow-md",
        isDragging && "opacity-90 scale-[1.01] shadow-xl border-primary/50 bg-primary/10",
      )}
    >
      <div className="flex items-center justify-between gap-2 text-sm font-medium mb-2">
        <div className="flex items-center gap-2">
          <button type="button" {...attributes} {...listeners} className="text-muted-foreground hover:text-foreground">
            <GripVertical className="size-4" />
          </button>
          <span>{label}</span>
        </div>
        <button
          type="button"
          onClick={onRemove}
          className="inline-flex items-center justify-center rounded-md p-1 text-muted-foreground transition-colors hover:bg-destructive/10 hover:text-destructive"
          aria-label="Remover regra"
          title="Remover regra"
        >
          <X className="size-4" />
        </button>
      </div>
      {children}
    </div>
  );
}
