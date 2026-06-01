export type BuilderFieldType = "text" | "number" | "date" | "currency";

export type BuilderImportFileType = {
  id: string;
  name: string;
  code?: string;
  description?: string;
  allowedExtensions?: string;
};

export type BuilderTargetField = {
  name: string;
  displayName: string;
  required: boolean;
  dataType: BuilderFieldType;
  description?: string;
};

export type BuilderTransformRule = {
  id: string;
  name: string;
  code: string;
  description?: string;
  requiresParameters?: boolean;
};

export type BuilderAppliedRule = {
  id: string;
  transformRuleId: string;
  code: string;
  order: number;
  parametersJson: string;
};

export type BuilderMapping = {
  sourceColumnName: string;
  targetFieldName: string;
  isRequired: boolean;
  defaultValue: string;
  transformRules: BuilderAppliedRule[];
};

export type FieldRuleConfig = {
  trim: boolean;
  uppercase: boolean;
  removeSpecialCharacters: boolean;
  allowEmpty: boolean;
  decimalSeparator: "," | ".";
  thousandSeparator: "." | "," | "none";
  allowNegative: boolean;
  minValue: string;
  maxValue: string;
  decimalPlaces: string;
  dateFormats: string;
  timezone: string;
  removeCurrencySymbol: boolean;
  detectBrazilianFormat: boolean;
  detectInternationalFormat: boolean;
  positiveOnly: boolean;
};

export type SpreadsheetPreviewRow = Record<string, string>;

export type ProcessedPreviewCell = {
  rowIndex: number;
  sourceColumnName: string;
  targetFieldName: string;
  targetFieldLabel: string;
  originalValue: string;
  processedValue: string;
  status: "ok" | "warning" | "error";
  message: string;
};

export type TemplateValidationItem = {
  label: string;
  status: "ok" | "warning" | "error";
  message: string;
};

export type ImportTemplateBuilderDto = {
  id?: string;
  name: string;
  description: string;
  importFileTypeId: string;
  columnMappings: BuilderMapping[];
};
