type WorkerSuccess = { ok: true; headers: string[] };
type WorkerError = { ok: false; message: string };
type WorkerResponse = WorkerSuccess | WorkerError;

function hasFirstThreeColumns(values: string[]): boolean {
  return values.length >= 3 && values[0] !== "" && values[1] !== "" && values[2] !== "";
}

function detectSeparator(lines: string[]): string {
  const commaScore = lines.reduce((acc, line) => acc + line.split(",").length, 0);
  const semiScore = lines.reduce((acc, line) => acc + line.split(";").length, 0);
  return semiScore > commaScore ? ";" : ",";
}

function extractCsvHeadersFallback(text: string): string[] {
  const lines = text.split(/\r?\n/).map((line) => line.trim()).filter(Boolean).slice(0, 5);
  if (lines.length === 0) return [];

  const separator = detectSeparator(lines);
  for (const line of lines) {
    const columns = line.split(separator).map((x) => x.trim());
    if (hasFirstThreeColumns(columns)) {
      return columns.filter(Boolean);
    }
  }

  return [];
}

async function extractHeadersFallback(file: File): Promise<string[]> {
  if (file.name.toLowerCase().endsWith(".csv")) {
    const text = await file.text();
    return extractCsvHeadersFallback(text);
  }

  const XLSX = await import("xlsx");
  const buffer = await file.arrayBuffer();
  const workbook = XLSX.read(buffer, { type: "array" });
  const firstSheetName = workbook.SheetNames[0];
  if (!firstSheetName) return [];

  const sheet = workbook.Sheets[firstSheetName];
  const rows = XLSX.utils.sheet_to_json<(string | number | null)[]>(sheet, {
    header: 1,
    blankrows: false,
    defval: "",
    raw: false,
  });

  for (const row of rows.slice(0, 5)) {
    const columns = row.map((cell) => String(cell ?? "").trim());
    if (hasFirstThreeColumns(columns)) {
      return columns.filter(Boolean);
    }
  }

  return [];
}

export async function extractHeadersInWorker(file: File): Promise<string[]> {
  try {
    const worker = new Worker(new URL("../workers/header-extractor.worker.ts", import.meta.url), { type: "module" });
    const buffer = await file.arrayBuffer();

    try {
      const result = await new Promise<WorkerResponse>((resolve, reject) => {
        worker.onmessage = (event: MessageEvent<WorkerResponse>) => resolve(event.data);
        worker.onerror = () => reject(new Error("Falha ao processar arquivo em background."));
        worker.postMessage({ fileName: file.name, buffer }, [buffer]);
      });

      if (!result.ok) throw new Error(result.message);
      return result.headers;
    } finally {
      worker.terminate();
    }
  } catch {
    return extractHeadersFallback(file);
  }
}
