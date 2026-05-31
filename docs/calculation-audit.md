# Auditoria de cálculos

Data da auditoria: 2026-05-31.

## Escopo mapeado

Foram verificados backend, frontend, queries LINQ/SQL, processors pós-importação, buffers de importação, regras de transformação, DTOs, endpoints e helpers visuais. Não foram encontradas procedures, views ou funções SQL persistidas; as agregações estão em LINQ/EF e em calculadoras/processors C#.

## Backend

### Importação e normalização

- `CommercialTransactionBuffer`: calcula `TotalAmount` como `Abs(quantity * unitPrice)` na entrada de venda, normaliza chave de deduplicação por documento, data, cliente, produto, tipo, cidade, grupo, quantidade, preço unitário e peso.
- `BrazilianCurrencyRule`: remove `R$`, espaços, separadores de milhar e converte vírgula decimal para decimal invariável.
- `BrazilianDateRule` e `UtcDateTimeParser`: convertem datas em formatos configurados ou invariantes para `DateTime`.
- `PreProcessorRuleEngine`: normaliza números, datas e converte peso para kg (`g / 1000`, `kg` mantém valor).

Cobertura existente validada:

- `CommercialTransactionBufferTests`
- `BrazilianCurrencyRuleTests`
- `BrazilianDateRuleTests`
- `CommercialTransactionReducedSampleTests`

### Resumos materializados

- `SalesSummaryProcessor`: agrega vendas por dia e semana; calcula contagem de transações, quantidade total, faturamento total e peso bruto total por cidade, grupo de produto e tipo.
- `CustomerSummaryProcessor`: agrega clientes por dia, semana e mês; calcula pedidos distintos no dia, faturamento, quantidade e peso por cliente, cidade, grupo e tipo.
- `SalesCompanySummaryCalculator`: calcula totais por empresa, período atual/anterior, crescimento percentual, ordenação por crescimento, faturamento, quantidade ou peso; também protege contra `TotalAmount` inconsistente usando `quantity * unitPrice`.

Cobertura adicionada/validada:

- `CustomerSummaryProcessorTests`
- `SalesSummaryProcessorTests`
- `SalesCompanySummaryCalculatorTests`

### Clientes e dashboards

Calculadora extraída para `InovaSkill.Importer.Application.Analytics.CustomerCalculators`:

- Períodos atual/anterior e normalização UTC.
- Faturamento total.
- Ticket médio por pedidos distintos.
- Média mensal e semanal considerando apenas períodos com compra.
- Quantidade total e peso total.
- Total de pedidos distintos.
- Frequência média de compra por dias distintos.
- Status do cliente: ativo, inativo, em crescimento ou em queda.
- Resumo de clientes ativos, novos e inativos.
- Ranking de clientes por faturamento, crescimento, queda, quantidade, peso ou ticket.
- Variação percentual entre períodos.
- Novos clientes por mês incluindo meses vazios.
- Participação percentual de produtos.
- Média móvel de faturamento e quantidade.
- Tendência de consumo.
- Próxima compra estimada.
- Risco do cliente e score de risco.

Endpoints que consomem esses cálculos:

- `GET /api/customer-analytics-v2/summary`
- `GET /api/customer-analytics-v2/ranking`
- `GET /api/customer-analytics-v2/new-customers-monthly`
- `GET /api/customers/{customerId}/summary`
- `GET /api/customers/{customerId}/timeline`
- `GET /api/customers/{customerId}/top-products`
- `GET /api/customers/{customerId}/comparison`
- `GET /api/customers/{customerId}/insights`

Cobertura adicionada/validada:

- `CustomerCalculatorsTests`
- `CustomersControllerTests`
- `CustomerAnalyticsV2ControllerTests`

Regressões cobertas:

- Ticket médio por pedidos distintos.
- Média mensal/semanal não zera quando há compras no período.
- Período sem compras mantém cliente existente com totais zerados e médias nulas.
- Comparativo usa fallback de transações quando tabelas de resumo estão vazias.
- Insights usam fallback de transações quando não há resumo mensal.
- Média móvel exige histórico mínimo.
- Risco retorna motivo quando não há frequência suficiente.
- Ranking calcula variação contra período anterior.
- Novos clientes incluem meses sem entrada.
- Participação de produtos usa receita total do agrupamento.

## Frontend

### Helpers e componentes

- `customer-details.ts`: formatação de variação, cor de comparação, status visual, valores monetários nulos e frequência média.
- `customer-commercial-intelligence.ts`: classifica saúde, tendência, potencial, estabilidade, produtos relevantes e recomendação comercial a partir dos dados calculados pelo backend.
- `customer-new-customers.ts`: calcula total de meses, média mensal e pico de novos clientes.
- `vendas-formatters.ts`: compacta números e moedas em `mil`, `mi` e `bi`.
- `kpi-card.utils.ts`: resolve direção de tendência e pontos de sparkline.
- `clientes.tsx`: monta ponto de previsão no gráfico mensal quando há `predictedRevenue`.
- `vendas.tsx`: exibe variação do resumo comercial e formata decimais.

Cobertura existente validada:

- `customer-details.test.ts`
- `customer-commercial-intelligence.test.ts`
- `customer-new-customers.test.ts`
- `vendas-formatters.test.ts`
- `kpi-card.test.ts`
- `clientes-visual-refinement.test.ts`
- `design-system.test.ts`

## Pontos de atenção

- `CustomerAnalyticsController` legado ainda replica parte dos cálculos do V2. O frontend usa o V2, mas o endpoint legado deve ser removido ou refatorado em uma etapa dedicada caso ainda seja contrato público.
- O cálculo de `TotalAmount` na importação força valor absoluto em `CommercialTransactionBuffer`, enquanto `SalesCompanySummaryCalculator` preserva sinal quando o total persistido é coerente. Se devoluções/notas negativas forem regra de negócio, esse contrato deve ser confirmado.
