import { describe, expect, it } from "vitest";
import { geocodingResolutionTotal, parseGeocodingJobResult } from "./geocoding-job-result";

const completeResult = {
  total: 20,
  processed: 20,
  skippedResolved: 5,
  providerRequests: 13,
  resolved: 12,
  exactCoordinates: 2,
  streetCoordinates: 3,
  postalCodeCoordinates: 5,
  municipalityCoordinates: 2,
  cached: 2,
  notFound: 1,
  failed: 0,
  pending: 0,
};

describe("resultado do job de geocodificação", () => {
  it("interpreta contagens válidas e soma todos os níveis de precisão", () => {
    const result = parseGeocodingJobResult(JSON.stringify(completeResult));
    expect(result).not.toBeNull();
    expect(geocodingResolutionTotal(result!)).toBe(12);
  });

  it("rejeita resultado incompleto, negativo ou JSON inválido", () => {
    expect(parseGeocodingJobResult("{}" )).toBeNull();
    expect(parseGeocodingJobResult(JSON.stringify({ ...completeResult, failed: -1 }))).toBeNull();
    expect(parseGeocodingJobResult("inválido")).toBeNull();
  });
});
