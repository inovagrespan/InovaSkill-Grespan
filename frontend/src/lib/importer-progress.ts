export type JobStageStatus = "pending" | "running" | "completed" | "failed";

export type FileJobStageProgress = {
  code: string;
  name: string;
  status: JobStageStatus;
  progressPercent: number;
  errorCount: number;
};

const PROGRESS_MIN_PERCENT = 0;
const PROGRESS_MAX_PERCENT = 100;
const ERROR_COUNT_NONE = 0;

export const IMPORT_PROGRESS_STAGES = [
  { code: "PRE_PROCESSING", name: "Pré-processamento" },
  { code: "VALIDATION", name: "Validação" },
  { code: "IMPORT", name: "Processamento" },
] as const;

export function clampProgressPercent(value: number): number {
  if (!Number.isFinite(value)) return PROGRESS_MIN_PERCENT;
  return Math.max(PROGRESS_MIN_PERCENT, Math.min(PROGRESS_MAX_PERCENT, Math.round(value)));
}

export function stageStatusLabel(status: JobStageStatus): string {
  const labels: Record<JobStageStatus, string> = {
    pending: "Pendente",
    running: "Em andamento",
    completed: "Concluída",
    failed: "Com erro",
  };

  return labels[status];
}

export function buildFallbackStages(input: {
  status: string;
  progressPercent: number;
  errorCount: number;
}): FileJobStageProgress[] {
  return IMPORT_PROGRESS_STAGES.map((stage) => {
    const status = resolveFallbackStageStatus(input.status, stage.code, input.errorCount);
    return {
      ...stage,
      status,
      progressPercent: resolveFallbackStageProgress(input.status, stage.code, input.progressPercent),
      errorCount: status === "failed" ? input.errorCount : ERROR_COUNT_NONE,
    };
  });
}

function resolveFallbackStageStatus(jobStatus: string, stageCode: string, errorCount: number): JobStageStatus {
  if (stageCode === "PRE_PROCESSING") {
    if (jobStatus === "PreProcessing") return "running";
    if (jobStatus === "ValidationFailed" && errorCount > 0) return "completed";
    if (["Validating", "ReadyToImport", "Importing", "Completed"].includes(jobStatus)) return "completed";
    return "pending";
  }

  if (stageCode === "VALIDATION") {
    if (jobStatus === "Validating") return "running";
    if (jobStatus === "ValidationFailed") return "failed";
    if (["ReadyToImport", "Importing", "Completed"].includes(jobStatus)) return "completed";
    return "pending";
  }

  if (jobStatus === "Importing") return "running";
  if (jobStatus === "Completed") return "completed";
  if (jobStatus === "Failed") return "failed";
  return "pending";
}

function resolveFallbackStageProgress(jobStatus: string, stageCode: string, progressPercent: number): number {
  if (stageCode === "PRE_PROCESSING") {
    if (jobStatus === "PreProcessing") return clampProgressPercent(progressPercent);
    if (["Validating", "ValidationFailed", "ReadyToImport", "Importing", "Completed"].includes(jobStatus)) return PROGRESS_MAX_PERCENT;
    return PROGRESS_MIN_PERCENT;
  }

  if (stageCode === "VALIDATION") {
    if (jobStatus === "Validating") return clampProgressPercent(progressPercent);
    if (["ReadyToImport", "Importing", "Completed"].includes(jobStatus)) return PROGRESS_MAX_PERCENT;
    if (jobStatus === "ValidationFailed") return PROGRESS_MAX_PERCENT;
    return PROGRESS_MIN_PERCENT;
  }

  if (jobStatus === "Importing") return clampProgressPercent(progressPercent);
  if (jobStatus === "Completed") return PROGRESS_MAX_PERCENT;
  return PROGRESS_MIN_PERCENT;
}

