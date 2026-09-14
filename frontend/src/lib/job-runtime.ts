export const JOB_STATUS_POLL_INTERVAL_MS = 5_000;

const ACTIVE_JOB_STATUSES = new Set(["Queued", "Processing", "Retrying"]);

export function isJobExecutionActive(status: string | null | undefined): boolean {
  return status !== null && status !== undefined && ACTIVE_JOB_STATUSES.has(status);
}
