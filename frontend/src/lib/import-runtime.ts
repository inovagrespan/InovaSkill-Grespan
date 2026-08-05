export const IMPORT_STATUS_POLL_INTERVAL_MS = 10_000;

export function isImportActive(status: string): boolean {
  return status === "Queued" || status === "Processing";
}

export function resolveImportProgressPercent(totalRows: number | null, importedRows: number | null): number | null {
  if (totalRows == null || importedRows == null || totalRows <= 0) return null;
  return Math.min(100, Math.max(0, (importedRows / totalRows) * 100));
}
