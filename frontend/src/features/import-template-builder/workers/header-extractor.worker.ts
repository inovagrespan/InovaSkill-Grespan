import * as XLSX from "xlsx";

type WorkerRequest = {
  fileName: string;
  buffer: ArrayBuffer;
};

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

function extractCsvHeaders(buffer: ArrayBuffer): string[] {
  const lines = new TextDecoder()
    .decode(buffer)
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .slice(0, 5);

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

function extractXlsxHeaders(buffer: ArrayBuffer): string[] {
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

self.onmessage = (event: MessageEvent<WorkerRequest>) => {
  try {
    const { fileName, buffer } = event.data;
    const extension = fileName.toLowerCase().split(".").pop() ?? "";

    const headers = extension === "csv" ? extractCsvHeaders(buffer) : extractXlsxHeaders(buffer);
    const response: WorkerResponse = { ok: true, headers };
    self.postMessage(response);
  } catch {
    const response: WorkerResponse = {
      ok: false,
      message: "Não foi possível extrair os headers do arquivo selecionado.",
    };
    self.postMessage(response);
  }
};
