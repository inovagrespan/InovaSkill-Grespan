import type { UploadDestinationCode } from "./importer-api";

export type UploadDestinationSuggestion = {
  code: UploadDestinationCode;
  label: string;
  confidence: "high" | "medium" | "low";
  reason: string;
};

const DESTINATION_LABEL: Record<UploadDestinationCode, string> = {
  SALES_INVOICE: "Vendas (Notas Fiscais)",
  CUSTOMER_LIST: "Clientes",
  PRODUCT_LIST: "Produtos",
  FINANCIAL_ENTRY: "Pedidos",
};

export function destinationLabel(code: UploadDestinationCode): string {
  return DESTINATION_LABEL[code];
}

export function detectUploadDestination(fileName: string): UploadDestinationSuggestion {
  const normalized = fileName
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase();

  if (/(venda|nota fiscal|nf|nfe|saida|faturamento|invoice)/.test(normalized)) {
    return { code: "SALES_INVOICE", label: DESTINATION_LABEL.SALES_INVOICE, confidence: "high", reason: "Nome do arquivo sugere vendas/nota fiscal." };
  }

  if (/(cliente|customer|cadastro cliente)/.test(normalized)) {
    return { code: "CUSTOMER_LIST", label: DESTINATION_LABEL.CUSTOMER_LIST, confidence: "high", reason: "Nome do arquivo sugere base de clientes." };
  }

  if (/(produto|sku|catalogo|catalog)/.test(normalized)) {
    return { code: "PRODUCT_LIST", label: DESTINATION_LABEL.PRODUCT_LIST, confidence: "high", reason: "Nome do arquivo sugere cadastro de produtos." };
  }

  if (/(pedido|order|orders|ordem)/.test(normalized)) {
    return { code: "FINANCIAL_ENTRY", label: DESTINATION_LABEL.FINANCIAL_ENTRY, confidence: "high", reason: "Nome do arquivo sugere pedidos." };
  }

  return { code: "SALES_INVOICE", label: DESTINATION_LABEL.SALES_INVOICE, confidence: "low", reason: "Sem padrão claro no nome do arquivo; revise manualmente." };
}
