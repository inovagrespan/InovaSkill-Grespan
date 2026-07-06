import { describe, expect, it } from "vitest";
import { getCurrentLocalDate } from "./route-snapshot-history";

describe("route snapshot history", () => {
  it("uses the local calendar date instead of the UTC date", () => {
    const lateEveningInSaoPaulo = new Date("2026-07-06T02:30:00.000Z");
    lateEveningInSaoPaulo.getTimezoneOffset = () => 180;

    expect(getCurrentLocalDate(lateEveningInSaoPaulo)).toBe("2026-07-05");
  });

  it("formats the local date with leading zeroes", () => {
    const morning = new Date("2026-01-03T12:00:00.000Z");
    morning.getTimezoneOffset = () => 0;

    expect(getCurrentLocalDate(morning)).toBe("2026-01-03");
  });
});
