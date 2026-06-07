import type {
  BuilderMapping,
  BuilderTargetField,
  FieldRuleConfig,
  ProcessedPreviewCell,
  SpreadsheetPreviewRow,
  TemplateValidationItem,
} from "../types";

export const DEFAULT_DATE_FORMATS = "dd/MM/yyyy, yyyy-MM-dd";
export const DEFAULT_TIMEZONE = "America/Sao_Paulo";

export const defaultRuleConfig: FieldRuleConfig = {
  trim: true,
  uppercase: false,
  removeSpecialCharacters: false,
  allowEmpty: false,
  decimalSeparator: ",",
  thousandSeparator: ".",
  allowNegative: false,
  minValue: "",
  maxValue: "",
  decimalPlaces: "2",
  dateFormats: DEFAULT_DATE_FORMATS,
  timezone: DEFAULT_TIMEZONE,
  removeCurrencySymbol: true,
  detectBrazilianFormat: true,
  detectInternationalFormat: false,
  positiveOnly: false,
};

export function normalizeText(value: string, config: FieldRuleConfig): string {
  let next = config.trim ? value.trim().replace(/\s+/g, " ") : value;
  if (config.removeSpecialCharacters) {
    next = next
      .normalize("NFD")
      .replace(/\p{Diacritic}/gu, "")
      .replace(/[^\p{L}\p{N}\s._@-]+/gu, "");
  }

  if (config.uppercase) {
    next = next.toUpperCase();
  }

  return next;
}

export function normalizeNumber(value: string, config: FieldRuleConfig): string {
  const raw = value.trim();
  if (!raw) return "";

  let next = raw;
  if (config.removeCurrencySymbol) {
    next = next.replace(/R\$/gi, "").replace(/\$/g, "");
  }

  next = next.replace(/\s/g, "");
  const decimalSeparator = config.detectInternationalFormat && !config.detectBrazilianFormat ? "." : config.decimalSeparator;
  const configuredThousandSeparator = config.thousandSeparator === "none" ? "" : config.thousandSeparator;
  const thousandSeparator = config.detectInternationalFormat && !config.detectBrazilianFormat
    ? ","
    : configuredThousandSeparator;
  if (thousandSeparator) {
    next = next.split(thousandSeparator).join("");
  }
  next = next.replace(decimalSeparator, ".");

  const parsed = Number(next);
  if (!Number.isFinite(parsed)) {
    throw new Error("Valor numérico inválido.");
  }

  if (!config.allowNegative && parsed < 0) {
    throw new Error("Valor negativo não permitido.");
  }

  if (config.positiveOnly && parsed <= 0) {
    throw new Error("Valor deve ser positivo.");
  }

  const min = parseOptionalNumber(config.minValue);
  if (min !== null && parsed < min) {
    throw new Error(`Valor menor que ${min}.`);
  }

  const max = parseOptionalNumber(config.maxValue);
  if (max !== null && parsed > max) {
    throw new Error(`Valor maior que ${max}.`);
  }

  const decimalPlaces = parseOptionalInteger(config.decimalPlaces);
  return decimalPlaces === null ? String(parsed) : parsed.toFixed(decimalPlaces);
}

export function normalizeDate(value: string, config: FieldRuleConfig): string {
  const raw = value.trim();
  if (!raw) return "";

  const formats = parseDateFormats(config.dateFormats);
  for (const format of formats) {
    const parsed = parseDateByFormat(raw, format);
    if (parsed) {
      return parsed;
    }
  }

  throw new Error("Data fora dos formatos aceitos.");
}

export function buildProcessedPreview(input: {
  rows: SpreadsheetPreviewRow[];
  mappings: Record<string, BuilderMapping>;
  fields: BuilderTargetField[];
  configs: Record<string, FieldRuleConfig>;
}): ProcessedPreviewCell[] {
  const cells: ProcessedPreviewCell[] = [];
  const fieldsByName = new Map(input.fields.map((field) => [field.name, field]));

  input.rows.slice(0, 5).forEach((row, rowIndex) => {
    for (const mapping of Object.values(input.mappings).filter((item) => item.sourceColumnName)) {
      const field = fieldsByName.get(mapping.targetFieldName);
      if (!field) continue;

      const originalValue = row[mapping.sourceColumnName] ?? "";
      const config = input.configs[mapping.targetFieldName] ?? defaultRuleConfig;
      const required = field.required || !config.allowEmpty;

      try {
        const processedValue = normalizeByFieldType(originalValue, field.dataType, config, mapping.defaultValue);
        const isEmpty = processedValue.trim() === "";
        cells.push({
          rowIndex,
          sourceColumnName: mapping.sourceColumnName,
          targetFieldName: field.name,
          targetFieldLabel: field.displayName,
          originalValue,
          processedValue,
          status: required && isEmpty ? "error" : isEmpty ? "warning" : "ok",
          message: required && isEmpty ? "Campo obrigatório vazio." : isEmpty ? "Valor vazio permitido." : "OK",
        });
      } catch (error) {
        cells.push({
          rowIndex,
          sourceColumnName: mapping.sourceColumnName,
          targetFieldName: field.name,
          targetFieldLabel: field.displayName,
          originalValue,
          processedValue: "",
          status: "error",
          message: error instanceof Error ? error.message : "Valor inválido.",
        });
      }
    }
  });

  return cells;
}

export function buildValidationChecklist(input: {
  name: string;
  importFileTypeId: string;
  headers: string[];
  mappings: Record<string, BuilderMapping>;
  fields: BuilderTargetField[];
  preview: ProcessedPreviewCell[];
}): TemplateValidationItem[] {
  const mapped = Object.values(input.mappings).filter((mapping) => mapping.sourceColumnName);
  const sourceCounts = countBy(mapped.map((mapping) => mapping.sourceColumnName.toLowerCase()));
  const targetCounts = countBy(mapped.map((mapping) => mapping.targetFieldName.toLowerCase()));
  const missingRequired = input.fields.filter((field) => field.required && !input.mappings[field.name]?.sourceColumnName);
  const duplicateHeaders = Object.entries(countBy(input.headers.map((header) => header.toLowerCase()))).filter(([, count]) => count > 1);
  const previewErrors = input.preview.filter((cell) => cell.status === "error");

  return [
    item("Nome informado", Boolean(input.name.trim()), "Informe um nome para reutilizar o template."),
    item("Tipo de arquivo selecionado", Boolean(input.importFileTypeId), "Selecione o tipo de arquivo."),
    item("Headers detectados", input.headers.length > 0, "Envie uma planilha ou cole os headers."),
    item("Campos obrigatórios mapeados", missingRequired.length === 0, `Faltam: ${missingRequired.map((field) => field.displayName).join(", ")}.`),
    item("Sem headers duplicados", duplicateHeaders.length === 0, `Duplicados: ${duplicateHeaders.map(([header]) => header).join(", ")}.`),
    item("Sem mapeamentos duplicados", Object.values(sourceCounts).every((count) => count === 1) && Object.values(targetCounts).every((count) => count === 1), "Cada coluna e campo interno deve ser usado uma vez."),
    item("Preview sem erros", previewErrors.length === 0, `${previewErrors.length} erro(s) encontrados no preview.`),
  ];
}

export function hasBlockingValidation(items: TemplateValidationItem[]): boolean {
  return items.some((item) => item.status === "error");
}

export function buildTransformRulesFromConfig(input: {
  field: BuilderTargetField;
  config: FieldRuleConfig;
  ruleCodeToId: Map<string, string>;
}) {
  const rules: Array<{ transformRuleId: string; order: number; parametersJson: unknown }> = [];
  const addRule = (code: string, parametersJson: unknown = null) => {
    const transformRuleId = input.ruleCodeToId.get(code);
    if (!transformRuleId) return;
    rules.push({ transformRuleId, order: rules.length + 1, parametersJson });
  };

  if (input.config.trim) addRule("Trim");
  if (input.config.removeSpecialCharacters) addRule("RemoveSpecialCharacters");
  if (input.config.uppercase) addRule("UpperCase");

  if (input.field.dataType === "number" || input.field.dataType === "currency") {
    addRule("BrazilianCurrency", buildNumberParameters(input.config));
  }

  if (input.field.dataType === "date") {
    addRule("BrazilianDate", { formats: parseDateFormats(input.config.dateFormats), timezone: input.config.timezone });
  }

  return rules;
}

function normalizeByFieldType(value: string, fieldType: BuilderTargetField["dataType"], config: FieldRuleConfig, defaultValue: string): string {
  const withDefault = value.trim() === "" && defaultValue.trim() ? defaultValue : value;
  if (fieldType === "number" || fieldType === "currency") return normalizeNumber(withDefault, config);
  if (fieldType === "date") return normalizeDate(withDefault, config);
  return normalizeText(withDefault, config);
}

function buildNumberParameters(config: FieldRuleConfig) {
  const isInternationalOnly = config.detectInternationalFormat && !config.detectBrazilianFormat;

  return {
    decimalSeparator: isInternationalOnly ? "." : config.decimalSeparator,
    thousandSeparator: isInternationalOnly ? "," : config.thousandSeparator === "none" ? "" : config.thousandSeparator,
    allowNegative: config.allowNegative,
    minValue: parseOptionalNumber(config.minValue),
    maxValue: parseOptionalNumber(config.maxValue),
    decimalPlaces: parseOptionalInteger(config.decimalPlaces),
    positiveOnly: config.positiveOnly,
  };
}

function parseDateFormats(value: string): string[] {
  return value.split(",").map((item) => item.trim()).filter(Boolean);
}

function parseDateByFormat(value: string, format: string): string | null {
  const escaped = format.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const pattern = escaped
    .replace("yyyy", "(?<yyyy>\\d{4})")
    .replace("MM", "(?<MM>\\d{1,2})")
    .replace("dd", "(?<dd>\\d{1,2})");
  const match = new RegExp(`^${pattern}$`).exec(value);
  if (!match?.groups) return null;

  const year = Number(match.groups.yyyy);
  const month = Number(match.groups.MM);
  const day = Number(match.groups.dd);
  const date = new Date(Date.UTC(year, month - 1, day));
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) {
    return null;
  }

  return `${year.toString().padStart(4, "0")}-${month.toString().padStart(2, "0")}-${day.toString().padStart(2, "0")}`;
}

function parseOptionalNumber(value: string): number | null {
  if (!value.trim()) return null;
  const parsed = Number(value.replace(",", "."));
  return Number.isFinite(parsed) ? parsed : null;
}

function parseOptionalInteger(value: string): number | null {
  if (!value.trim()) return null;
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : null;
}

function countBy(values: string[]): Record<string, number> {
  return values.reduce<Record<string, number>>((acc, value) => {
    acc[value] = (acc[value] ?? 0) + 1;
    return acc;
  }, {});
}

function item(label: string, ok: boolean, message: string): TemplateValidationItem {
  return { label, status: ok ? "ok" : "error", message: ok ? "OK" : message };
}
