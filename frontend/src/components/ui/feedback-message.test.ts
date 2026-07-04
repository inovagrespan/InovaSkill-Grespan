import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";

import { FeedbackMessage } from "./feedback-message";
import { InsightCard } from "./insight-card";

describe("feedback UI components", () => {
  it("renders dismissible error feedback with alert semantics", () => {
    const markup = renderToStaticMarkup(
      createElement(FeedbackMessage, {
        message: "Falha ao carregar clientes.",
        type: "error",
        onDismiss: () => undefined,
      }),
    );

    expect(markup).toContain('role="alert"');
    expect(markup).toContain("Falha ao carregar clientes.");
    expect(markup).toContain("Fechar mensagem");
  });

  it("keeps insight content visible for dashboard alerts", () => {
    const markup = renderToStaticMarkup(
      createElement(
        InsightCard,
        { type: "alert" },
        createElement("span", null, "Cliente com queda de receita."),
      ),
    );

    expect(markup).toContain("Cliente com queda de receita.");
    expect(markup).toContain("border-amber");
  });
});
