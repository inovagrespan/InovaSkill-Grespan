import { useEffect, useMemo, useState } from "react";
import {
  createImportTemplate,
  extractSpreadsheetHeaders,
  fetchImportTemplateById,
  fetchImportTemplateFileTypes,
  fetchImportTemplateTargetFields,
  fetchTransformRules,
  updateImportTemplate,
  type ApiImportTemplate,
} from "@/lib/importer-api";
import type {
  BuilderAppliedRule,
  BuilderImportFileType,
  BuilderMapping,
  BuilderTargetField,
  BuilderTransformRule,
} from "../types";

export function useImportTemplateBuilder(templateId?: string) {
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [headerLoading, setHeaderLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [importFileTypeId, setImportFileTypeId] = useState("");

  const [fileTypes, setFileTypes] = useState<BuilderImportFileType[]>([]);
  const [targetFields, setTargetFields] = useState<BuilderTargetField[]>([]);
  const [transformRules, setTransformRules] = useState<BuilderTransformRule[]>([]);
  const [detectedHeaders, setDetectedHeaders] = useState<string[]>([]);
  const [mappings, setMappings] = useState<Record<string, BuilderMapping>>({});
  const [activeTargetField, setActiveTargetField] = useState<string | null>(null);

  useEffect(() => {
    void bootstrap();
  }, []);

  useEffect(() => {
    if (!importFileTypeId) return;
    void loadTargetFields(importFileTypeId);
  }, [importFileTypeId]);

  function normalizeDisplayText(value: string): string {
    return value
      .replaceAll("Ã§", "ç")
      .replaceAll("Ã£", "ã")
      .replaceAll("Ã¡", "á")
      .replaceAll("Ã©", "é")
      .replaceAll("Ãª", "ê")
      .replaceAll("Ã­", "í")
      .replaceAll("Ã³", "ó")
      .replaceAll("Ã´", "ô")
      .replaceAll("Ãº", "ú")
      .replaceAll("Ã ", "à")
      .replaceAll("Ã‰", "É")
      .replaceAll("Ã‡", "Ç")
      .replaceAll("Â·", "·")
      .replaceAll("â†’", "→")
      .replaceAll("â€“", "–")
      .replaceAll("â€”", "—")
      .replaceAll("â€œ", "\"")
      .replaceAll("â€", "\"")
      .replaceAll("â€˜", "'")
      .replaceAll("â€™", "'");
  }

  async function bootstrap() {
    setLoading(true);
    setError("");
    try {
      const [types, rules] = await Promise.all([fetchImportTemplateFileTypes(), fetchTransformRules()]);
      setFileTypes(types.map((type) => ({ ...type, name: normalizeDisplayText(type.name) })));
      setTransformRules(rules.map((rule) => ({ ...rule, name: normalizeDisplayText(rule.name) })));
      if (templateId) {
        const template = await fetchImportTemplateById(templateId);
        hydrateFromTemplate(template);
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }

  function hydrateFromTemplate(template: ApiImportTemplate) {
    setName(template.name ?? "");
    setDescription(template.description ?? "");
    setImportFileTypeId(template.importFileTypeId ?? "");
    const nextMappings: Record<string, BuilderMapping> = {};
    const headers: string[] = [];
    for (const mapping of template.columnMappings ?? []) {
      headers.push(mapping.sourceColumnName);
      nextMappings[mapping.targetFieldName] = {
        sourceColumnName: mapping.sourceColumnName,
        targetFieldName: mapping.targetFieldName,
        isRequired: Boolean(mapping.isRequired),
        defaultValue: mapping.defaultValue ?? "",
        transformRules: (mapping.transformRules ?? [])
          .sort((a, b) => a.order - b.order)
          .map((r, idx) => ({
            id: `${mapping.targetFieldName}-${r.transformRuleId}-${idx}`,
            transformRuleId: r.transformRuleId,
            order: idx + 1,
            parametersJson:
              r.parametersJson == null ? "" : typeof r.parametersJson === "string" ? r.parametersJson : JSON.stringify(r.parametersJson),
          })),
      };
    }
    setMappings(nextMappings);
    setDetectedHeaders(headers);
  }

  async function loadTargetFields(typeId: string) {
    try {
      const fields = await fetchImportTemplateTargetFields(typeId);
      setTargetFields(
        fields.map((field) => ({
          ...field,
          displayName: normalizeDisplayText(field.displayName),
          name: normalizeDisplayText(field.name),
        })),
      );
    } catch (e) {
      setError((e as Error).message);
    }
  }

  async function handleUpload(file: File) {
    setHeaderLoading(true);
    setError("");
    setSuccess("");
    try {
      const headers = await extractSpreadsheetHeaders(file, importFileTypeId || undefined);
      if (headers.length === 0) {
        throw new Error("Não foi possível detectar headers na planilha. Verifique se a primeira linha útil contém os nomes das colunas.");
      }
      setDetectedHeaders(headers);
      setSuccess(`${headers.length} headers detectados com sucesso.`);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setHeaderLoading(false);
    }
  }

  function assignHeader(targetFieldName: string, sourceColumnName: string) {
    const field = targetFields.find((x) => x.name === targetFieldName);
    setMappings((prev) => {
      const next = { ...prev };

      // Garante mapeamento 1:1: remove o header de qualquer outro campo.
      for (const [fieldName, mapping] of Object.entries(next)) {
        if (fieldName !== targetFieldName && mapping.sourceColumnName === sourceColumnName) {
          next[fieldName] = { ...mapping, sourceColumnName: "" };
        }
      }

      next[targetFieldName] = {
        sourceColumnName,
        targetFieldName,
        isRequired: field?.required ?? false,
        defaultValue: prev[targetFieldName]?.defaultValue ?? "",
        transformRules: prev[targetFieldName]?.transformRules ?? [],
      };

      return next;
    });
  }

  function upsertRule(targetFieldName: string, ruleId: string) {
    setMappings((prev) => {
      const current = prev[targetFieldName];
      if (!current) return prev;
      const exists = current.transformRules.some((x) => x.transformRuleId === ruleId);
      if (exists) return prev;
      const nextRule: BuilderAppliedRule = {
        id: `${targetFieldName}-${ruleId}-${Date.now()}`,
        transformRuleId: ruleId,
        order: current.transformRules.length + 1,
        parametersJson: "",
      };
      return {
        ...prev,
        [targetFieldName]: { ...current, transformRules: [...current.transformRules, nextRule] },
      };
    });
  }

  function reorderRules(targetFieldName: string, rules: BuilderAppliedRule[]) {
    setMappings((prev) => {
      const current = prev[targetFieldName];
      if (!current) return prev;
      return {
        ...prev,
        [targetFieldName]: {
          ...current,
          transformRules: rules.map((r, idx) => ({ ...r, order: idx + 1 })),
        },
      };
    });
  }

  function removeRule(targetFieldName: string, ruleInstanceId: string) {
    setMappings((prev) => {
      const current = prev[targetFieldName];
      if (!current) return prev;
      const nextRules = current.transformRules
        .filter((rule) => rule.id !== ruleInstanceId)
        .map((rule, idx) => ({ ...rule, order: idx + 1 }));

      return {
        ...prev,
        [targetFieldName]: {
          ...current,
          transformRules: nextRules,
        },
      };
    });
  }

  function updateMapping(targetFieldName: string, patch: Partial<BuilderMapping>) {
    setMappings((prev) => ({
      ...prev,
      [targetFieldName]: { ...prev[targetFieldName], ...patch },
    }));
  }

  const mappedHeaders = useMemo(() => new Set(Object.values(mappings).map((m) => m.sourceColumnName)), [mappings]);

  const missingRequiredFields = useMemo(
    () => targetFields.filter((f) => f.required && !mappings[f.name]?.sourceColumnName),
    [targetFields, mappings],
  );

  function validate(): string {
    if (!name.trim()) return "Informe o nome do template.";
    if (!importFileTypeId) return "Selecione o tipo de importação.";
    if (missingRequiredFields.length > 0) return "Mapeie todos os campos obrigatórios.";
    for (const mapping of Object.values(mappings)) {
      for (const rule of mapping.transformRules) {
        if (!rule.parametersJson.trim()) continue;
        try { JSON.parse(rule.parametersJson); } catch { return `JSON inválido na regra do campo '${mapping.targetFieldName}'.`; }
      }
    }
    return "";
  }

  async function save() {
    const validationError = validate();
    if (validationError) {
      setError(validationError);
      return false;
    }

    setSaving(true);
    setError("");
    setSuccess("");
    try {
      const payload = {
        name: name.trim(),
        description: description.trim(),
        importFileTypeId,
        columnMappings: Object.values(mappings).map((mapping) => ({
          sourceColumnName: mapping.sourceColumnName,
          targetFieldName: mapping.targetFieldName,
          isRequired: mapping.isRequired,
          defaultValue: mapping.defaultValue || null,
          transformRules: mapping.transformRules.map((rule, idx) => ({
            transformRuleId: rule.transformRuleId,
            order: idx + 1,
            parametersJson: rule.parametersJson.trim() ? JSON.parse(rule.parametersJson) : null,
          })),
        })),
      };

      if (templateId) await updateImportTemplate(templateId, payload);
      else await createImportTemplate(payload);

      setSuccess(templateId ? "Template atualizado com sucesso." : "Template criado com sucesso.");
      return true;
    } catch (e) {
      setError((e as Error).message);
      return false;
    } finally {
      setSaving(false);
    }
  }

  return {
    loading,
    saving,
    headerLoading,
    error,
    success,
    name,
    description,
    importFileTypeId,
    fileTypes,
    targetFields,
    transformRules,
    detectedHeaders,
    mappings,
    activeTargetField,
    mappedHeaders,
    missingRequiredFields,
    setName,
    setDescription,
    setImportFileTypeId,
    setDetectedHeaders,
    setActiveTargetField,
    handleUpload,
    assignHeader,
    upsertRule,
    reorderRules,
    removeRule,
    updateMapping,
    save,
  };
}
