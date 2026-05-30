export function parseManualHeaders(input: string): string[] {
  return Array.from(
    new Set(
      input
        .split(/\r?\n|\t|,|;/)
        .map((item) => item.trim())
        .filter(Boolean),
    ),
  );
}
