// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { OTHER_TOLL_PLAZAS } from "@/lib/logistics-map-data";
import type { RouteTollEstimate } from "@/lib/route-toll-cost";
import { RouteTollKpi } from "./RouteTollKpi";

const plaza = OTHER_TOLL_PLAZAS.find((item) => item.id === "CART-PIRATININGA")!;
const estimate: RouteTollEstimate = {
  totalAutomaticCost: 20.33,
  totalPassages: 1,
  tolls: [{ plaza, passages: 1, automaticCost: 20.33 }],
};

describe("RouteTollKpi", () => {
  afterEach(() => cleanup());

  it("mantém o KPI compacto e abre as praças ao clicar", async () => {
    const user = userEvent.setup();
    render(<RouteTollKpi estimate={estimate} />);

    const title = screen.getByText("Gasto estimado com pedágio");
    expect(title).toBeTruthy();
    expect(title.className).toContain("uppercase");
    expect(title.className).toContain("text-primary");
    expect(screen.queryByText("Piratininga")).toBeNull();

    await user.click(screen.getByRole("button", { name: "Ver detalhes dos pedágios da rota" }));

    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(screen.getByText("Piratininga")).toBeTruthy();
    expect(screen.getByText(/CART · SP-225 km/)).toBeTruthy();
    expect(screen.getByText(/manual R\$\s*21,40 · automático R\$\s*20,33/)).toBeTruthy();
  });

  it("informa quando a rota não possui pedágios", () => {
    render(<RouteTollKpi estimate={{ tolls: [], totalAutomaticCost: 0, totalPassages: 0 }} />);

    expect(screen.getByText("0 passagem(ns) · Clique para ver os pedágios")).toBeTruthy();
  });
});
