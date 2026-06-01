import { useEffect, useMemo, useState } from "react";
import {
  createImportTemplate,
  extractSpreadsheetSample,
  fetchImportTemplateById,
  fetchImportTemplateFileTypes,
  fetchImportTemplateTargetFields,
  fetchTransformRules,
  updateImportTemplate,
  type ApiImportTemplate,
} from "@/lib/importer-api";
import type {
  BuilderImportFileType,
  BuilderMapping,
  BuilderTargetField,
  BuilderTransformRule,
  FieldRuleConfig,
  SpreadsheetPreviewRow,
} from "../types";
import {
  buildProcessedPreview,
  buildTransformRulesFromConfig,
  buildValidationChecklist,
  defaultRuleConfig,
  hasBlockingValidation,
} from "../utils/template-processing";

export function useImportTemplateBuilder(templateId?: string) {
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [headerLoading, setHeaderLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [currentStep, setCurrentStep] = useState(0);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [importFileTypeId, setImportFileTypeId] = useState("");
  const [companyName, setCompanyName] = useState("");

  const [fileTypes, setFileTypes] = useState<BuilderImportFileType[]>([]);
  const [targetFields, setTargetFields] = useState<BuilderTargetField[]>([]);
  const [transformRules, setTransformRules] = useState<BuilderTransformRule[]>([]);
  const [detectedHeaders, setDetectedHeadersState] = useState<string[]>([]);
  const [previewRows, setPreviewRows] = useState<SpreadsheetPreviewRow[]>([]);
  const [mappings, setMappings] = useState<Record<string, BuilderMapping>>({});
  const [ruleConfigs, setRuleConfigs] = useState<Record<string, FieldRuleConfig>>({});
  const [activeTargetField, setActiveTargetField] = useState<string | null>(null);

  useEffect(() => {
    void bootstrap();
  }, []);

  useEffect(() => {
    if (!importFileTypeId) return;
    void loadTargetFields(importFileTypeId);
  }, [importFileTypeId]);

  async function bootstrap() {
    setLoading(true);
    setError("");
    try {
      const [types, rules] = await Promise.all([fetchImportTemplateFileTypes(), fetchTransformRules()]);
      setFileTypes(types);
      setTransformRules(rules);
      if (templateId) {
        const template = await fetchImportTemplateById(templateId);
        hydrateFromTemplate(template, rules);
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }

  function hydrateFromTemplate(template: ApiImportTemplate, ruleCatalog: BuilderTransformRule[]) {
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
          .map((rule, index) => ({
            id: `${mapping.targetFieldName}-${rule.transformRuleId}-${index}`,
            transformRuleId: rule.transformRuleId,
            code: ruleCatalog.find((item) => item.id === rule.transformRuleId)?.code ?? rule.transformRuleId,
            order: index + 1,
            parametersJson:
              rule.parametersJson == null ? "" : typeof rule.parametersJson === "string" ? rule.parametersJson : JSON.stringify(rule.parametersJson),
          })),
      };
    }

    setMappings(nextMappings);
    setRuleConfigs((prev) => hydrateRuleConfigs(nextMappings, ruleCatalog, prev));
    setDetectedHeaders(headers);
  }

  async function loadTargetFields(typeId: string) {
    try {
      const fields = await fetchImportTemplateTargetFields(typeId);
      const nextFields = fields.map((field) => ({
        ...field,
        dataType: field.dataType ?? inferFieldType(field.name),
      })) as BuilderTargetField[];

      setTargetFields(nextFields);
      setRuleConfigs((prev) => {
        const next = { ...prev };
        for (const field of nextFields) {
          next[field.name] = next[field.name] ?? buildDefaultConfigForField(field);
        }
        return next;
      });
    } catch (e) {
      setError((e as Error).message);
    }
  }

  async function handleUpload(file: File) {
    setHeaderLoading(true);
    setError("");
    setSuccess("");
    try {
      const sample = await extractSpreadsheetSample(file, importFileTypeId || undefined);
      if (sample.headers.length === 0) {
        throw new Error("Não foi possível detectar headers na planilha. Verifique se a primeira linha útil contém os nomes das colunas.");
      }
      setDetectedHeaders(sample.headers);
      setPreviewRows(sample.previewRows);
      setSuccess(`${sample.headers.length} headers detectados com sucesso.`);
      setCurrentStep((step) => Math.max(step, 2));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setHeaderLoading(false);
    }
  }

  function setDetectedHeaders(headers: string[]) {
    setDetectedHeadersState(headers);
    setMappings((prev) => {
      const next = { ...prev };
      for (const field of targetFields) {
        if (next[field.name]?.sourceColumnName) continue;
        const guessedHeader = guessHeaderForField(headers, field);
        if (guessedHeader) {
          next[field.name] = buildMapping(field, guessedHeader, prev[field.name]?.defaultValue ?? "");
        }
      }
      return next;
    });
  }

  function assignHeader(targetFieldName: string, sourceColumnName: string) {
    const field = targetFields.find((item) => item.name === targetFieldName);
    if (!field) return;

    setMappings((prev) => {
      const next = { ...prev };
      for (const [fieldName, mapping] of Object.entries(next)) {
        if (fieldName !== targetFieldName && mapping.sourceColumnName === sourceColumnName) {
          next[fieldName] = { ...mapping, sourceColumnName: "" };
        }
      }

      next[targetFieldName] = buildMapping(field, sourceColumnName, prev[targetFieldName]?.defaultValue ?? "");
      return next;
    });
  }

  function updateMapping(targetFieldName: string, patch: Partial<BuilderMapping>) {
    setMappings((prev) => ({
      ...prev,
      [targetFieldName]: { ...prev[targetFieldName], ...patch },
    }));
  }

  function updateRuleConfig(targetFieldName: string, patch: Partial<FieldRuleConfig>) {
    setRuleConfigs((prev) => ({
      ...prev,
      [targetFieldName]: { ...(prev[targetFieldName] ?? defaultRuleConfig), ...patch },
    }));
  }

  const mappedHeaders = useMemo(() => new Set(Object.values(mappings).map((mapping) => mapping.sourceColumnName).filter(Boolean)), [mappings]);

  const missingRequiredFields = useMemo(
    () => targetFields.filter((field) => field.required && !mappings[field.name]?.sourceColumnName),
    [targetFields, mappings],
  );

  const processedPreview = useMemo(
    () => buildProcessedPreview({ rows: previewRows, mappings, fields: targetFields, configs: ruleConfigs }),
    [previewRows, mappings, targetFields, ruleConfigs],
  );

  const validationChecklist = useMemo(
    () => buildValidationChecklist({ name, importFileTypeId, headers: detectedHeaders, mappings, fields: targetFields, preview: processedPreview }),
    [name, importFileTypeId, detectedHeaders, mappings, targetFields, processedPreview],
  );

  const canSave = !hasBlockingValidation(validationChecklist);

  async function save() {
    if (!canSave) {
      setError("Revise os itens pendentes antes de salvar o template.");
      setCurrentStep(5);
      return false;
    }

    setSaving(true);
    setError("");
    setSuccess("");
    try {
      const ruleCodeToId = new Map(transformRules.map((rule) => [rule.code, rule.id]));
      const payload = {
        name: name.trim(),
        description: description.trim(),
        importFileTypeId,
        columnMappings: Object.values(mappings)
          .filter((mapping) => mapping.sourceColumnName)
          .map((mapping) => {
            const field = targetFields.find((item) => item.name === mapping.targetFieldName);
            return {
              sourceColumnName: mapping.sourceColumnName,
              targetFieldName: mapping.targetFieldName,
              isRequired: mapping.isRequired,
              defaultValue: mapping.defaultValue || null,
              transformRules: field
                ? buildTransformRulesFromConfig({
                    field,
                    config: ruleConfigs[field.name] ?? defaultRuleConfig,
                    ruleCodeToId,
                  })
                : [],
            };
          }),
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
    currentStep,
    name,
    description,
    importFileTypeId,
    companyName,
    fileTypes,
    targetFields,
    transformRules,
    detectedHeaders,
    previewRows,
    mappings,
    ruleConfigs,
    activeTargetField,
    mappedHeaders,
    missingRequiredFields,
    processedPreview,
    validationChecklist,
    canSave,
    setCurrentStep,
    setName,
    setDescription,
    setImportFileTypeId,
    setCompanyName,
    setDetectedHeaders,
    setPreviewRows,
    setActiveTargetField,
    handleUpload,
    assignHeader,
    updateMapping,
    updateRuleConfig,
    save,
  };
}

function buildMapping(field: BuilderTargetField, sourceColumnName: string, defaultValue: string): BuilderMapping {
  return {
    sourceColumnName,
    targetFieldName: field.name,
    isRequired: field.required,
    defaultValue,
    transformRules: [],
  };
}

function buildDefaultConfigForField(field: BuilderTargetField): FieldRuleConfig {
  return {
    ...defaultRuleConfig,
    allowEmpty: !field.required,
    decimalPlaces: field.dataType === "number" ? "3" : "2",
    positiveOnly: field.dataType === "currency",
  };
}

function hydrateRuleConfigs(
  mappings: Record<string, BuilderMapping>,
  ruleCatalog: BuilderTransformRule[],
  current: Record<string, FieldRuleConfig>,
): Record<string, FieldRuleConfig> {
  const rulesById = new Map(ruleCatalog.map((rule) => [rule.id, rule]));
  const next = { ...current };

  for (const mapping of Object.values(mappings)) {
    const config: FieldRuleConfig = { ...defaultRuleConfig, ...(next[mapping.targetFieldName] ?? {}) };
    for (const appliedRule of mapping.transformRules) {
      const code = rulesById.get(appliedRule.transformRuleId)?.code ?? appliedRule.code;
      const parameters = parseParameters(appliedRule.parametersJson);

      if (code === "Trim") config.trim = true;
      if (code === "UpperCase") config.uppercase = true;
      if (code === "RemoveSpecialCharacters") config.removeSpecialCharacters = true;
      if (code === "BrazilianCurrency" && parameters) {
        config.decimalSeparator = parameters.decimalSeparator === "." ? "." : ",";
        config.thousandSeparator = parameters.thousandSeparator === "," || parameters.thousandSeparator === "." ? parameters.thousandSeparator : "none";
        config.allowNegative = parameters.allowNegative ?? config.allowNegative;
        config.positiveOnly = parameters.positiveOnly ?? config.positiveOnly;
        config.minValue = parameters.minValue == null ? "" : String(parameters.minValue);
        config.maxValue = parameters.maxValue == null ? "" : String(parameters.maxValue);
        config.decimalPlaces = parameters.decimalPlaces == null ? config.decimalPlaces : String(parameters.decimalPlaces);
      }
      if (code === "BrazilianDate" && parameters) {
        config.dateFormats = Array.isArray(parameters.formats) ? parameters.formats.join(", ") : config.dateFormats;
        config.timezone = typeof parameters.timezone === "string" ? parameters.timezone : config.timezone;
      }
    }

    next[mapping.targetFieldName] = config;
  }

  return next;
}

function parseParameters(parametersJson: string): Record<string, any> | null {
  if (!parametersJson.trim()) return null;
  try {
    return JSON.parse(parametersJson);
  } catch {
    return null;
  }
}

function guessHeaderForField(headers: string[], field: BuilderTargetField): string | null {
  const aliases: Record<string, string[]> = {
    documentnumber: ["documento", "doc", "nota"],
    transactiondate: ["data", "emissao", "venda"],
    customercode: ["cliente", "codigo cliente", "cod cliente"],
    customername: ["nome", "cliente nome", "razao social"],
    productcode: ["produto", "codigo produto", "cod produto"],
    productdescription: ["descricao", "descrição", "produto descricao"],
    quantity: ["quantidade", "qtd", "qtde"],
    unitprice: ["vlr. unit.", "valor unitario", "unitario"],
    totalamount: ["total", "valor total"],
    city: ["cidade", "municipio"],
    productgroup: ["grupo", "grupo descricao"],
    grossweightkg: ["peso bruto", "peso bruto(kg)"],
  };

  const candidates = [field.name, field.displayName, ...(aliases[field.name] ?? [])].map(normalize);
  return headers.find((header) => candidates.some((candidate) => normalize(header).includes(candidate) || candidate.includes(normalize(header)))) ?? null;
}

function normalize(value: string): string {
  return value
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase()
    .trim();
}

function inferFieldType(fieldName: string): BuilderTargetField["dataType"] {
  if (/date|data/i.test(fieldName)) return "date";
  if (/price|amount|total|valor|peso/i.test(fieldName)) return "currency";
  if (/quantity|quantidade|weight/i.test(fieldName)) return "number";
  return "text";
}
