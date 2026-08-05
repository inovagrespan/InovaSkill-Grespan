import { afterEach, describe, expect, it, vi } from "vitest";
import { createClientMessageId } from "@/lib/client-message-id";

describe("createClientMessageId", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("usa o UUID nativo quando o navegador oferece suporte", () => {
    const randomUUID = vi.fn(() => "native-uuid");
    vi.stubGlobal("crypto", { randomUUID });

    expect(createClientMessageId()).toBe("native-uuid");
    expect(randomUUID).toHaveBeenCalledOnce();
  });

  it("gera identificadores distintos quando randomUUID não está disponível", () => {
    vi.stubGlobal("crypto", {});
    vi.spyOn(Date, "now").mockReturnValue(1_700_000_000_000);
    vi.spyOn(Math, "random").mockReturnValue(0.25);

    const firstId = createClientMessageId();
    const secondId = createClientMessageId();

    expect(firstId).toMatch(/^message-/);
    expect(secondId).toMatch(/^message-/);
    expect(secondId).not.toBe(firstId);
  });

  it("funciona mesmo quando o objeto crypto não existe", () => {
    vi.stubGlobal("crypto", undefined);

    expect(() => createClientMessageId()).not.toThrow();
  });
});
