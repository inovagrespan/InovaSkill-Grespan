import { describe, expect, it } from "vitest";
import { formatTokenCount, formatUsd } from "@/routes/simulador-whatsapp";

describe("consumo da conversa no simulador", () => {
  it("mantém contagens pequenas explícitas e compacta milhares", () => {
    expect(formatTokenCount(0)).toBe("0");
    expect(formatTokenCount(999)).toBe("999");
    expect(formatTokenCount(1_200)).toContain("1,2");
    expect(formatTokenCount(1_200).toLocaleLowerCase("pt-BR")).toContain("mil");
  });

  it("exibe custo USD pequeno com precisão suficiente e sem arredondar para zero", () => {
    const formatted = formatUsd(0.003);

    expect(formatted).toContain("US$");
    expect(formatted).toContain("0,0030");
  });
});
