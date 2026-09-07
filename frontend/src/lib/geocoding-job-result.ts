export type GeocodingJobResult = {
  total: number;
  processed: number;
  skippedResolved: number;
  providerRequests: number;
  resolved: number;
  exactCoordinates: number;
  streetCoordinates: number;
  postalCodeCoordinates: number;
  municipalityCoordinates: number;
  cached: number;
  notFound: number;
  failed: number;
  pending: number;
};

const numericFields: (keyof GeocodingJobResult)[] = [
  "total",
  "processed",
  "skippedResolved",
  "providerRequests",
  "resolved",
  "exactCoordinates",
  "streetCoordinates",
  "postalCodeCoordinates",
  "municipalityCoordinates",
  "cached",
  "notFound",
  "failed",
  "pending",
];

export function parseGeocodingJobResult(value?: string | null): GeocodingJobResult | null {
  if (!value) return null;
  try {
    const parsed = JSON.parse(value) as Record<string, unknown>;
    if (!numericFields.every((field) => Number.isFinite(parsed[field]) && Number(parsed[field]) >= 0)) {
      return null;
    }
    return Object.fromEntries(numericFields.map((field) => [field, Number(parsed[field])])) as GeocodingJobResult;
  } catch {
    return null;
  }
}

export function geocodingResolutionTotal(result: GeocodingJobResult): number {
  return result.exactCoordinates + result.streetCoordinates + result.postalCodeCoordinates +
    result.municipalityCoordinates;
}
