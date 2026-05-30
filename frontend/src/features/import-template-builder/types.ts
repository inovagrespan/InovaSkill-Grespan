export type BuilderImportFileType = { id: string; name: string; code?: string };

export type BuilderTargetField = {
  name: string;
  displayName: string;
  required: boolean;
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

export type ImportTemplateBuilderDto = {
  id?: string;
  name: string;
  description: string;
  importFileTypeId: string;
  columnMappings: BuilderMapping[];
};
