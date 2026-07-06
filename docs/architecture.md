# Arquitetura do InovaSkill-Grespan

Este documento é o mapa arquitetural do repositório. Ele descreve os limites entre
as aplicações, as dependências permitidas e os principais fluxos de execução.
Detalhes exclusivos da importação de rotas estão em
[route-import-architecture.md](./route-import-architecture.md).

## Visão geral

O sistema é composto por uma aplicação web, uma API HTTP, um Worker assíncrono e
dois serviços de infraestrutura:

```text
┌──────────────────────┐       HTTP/JSON       ┌──────────────────────┐
│ Frontend             │ ────────────────────> │ API ASP.NET Core     │
│ React + TanStack     │                       │ autenticação e HTTP  │
└──────────────────────┘                       └──────────┬───────────┘
                                                        │
                                      ┌─────────────────┼─────────────────┐
                                      │                 │                 │
                                      v                 v                 v
                              ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
                              │ PostgreSQL   │  │ Redis Stream │  │ Storage XLSX │
                              │ persistência │  │ route-imports│  │ compartilhado│
                              └──────▲───────┘  └──────┬───────┘  └──────▲───────┘
                                     │                 │                 │
                                     └─────────────────┼─────────────────┘
                                                       v
                                             ┌──────────────────────┐
                                             │ Worker .NET          │
                                             │ processamento XLSX  │
                                             └──────────────────────┘
```

- `frontend/`: SPA em React, TypeScript, Vite e TanStack Router/Query.
- `backend/InovaSkill.Importer.Api`: entrada HTTP, autenticação, contratos e
  publicação de trabalhos.
- `backend/InovaSkill.Importer.Worker`: consumidor assíncrono das importações.
- `backend/InovaSkill.Importer.Application`: contratos e regras de aplicação
  reutilizáveis.
- `backend/InovaSkill.Importer.Domain`: entidades, estados e regras centrais do
  domínio.
- `backend/InovaSkill.Importer.Infrastructure`: Entity Framework Core,
  PostgreSQL, arquivos, parsing de planilhas e implementações dos processadores.
- `backend/InovaSkill.Importer.Tests`: testes automatizados do backend.
- `postgres`: fonte persistente dos dados de negócio e históricos de execução.
- `redis`: transporte das mensagens assíncronas com Wolverine.
- volume `route_imports_data`: arquivos de importação compartilhados entre API e
  Worker.

## Limites e dependências

As dependências do backend apontam para o centro do domínio:

```text
Domain
  ↑
Application
  ↑
Infrastructure
  ↑
Api / Worker
```

Na prática:

- `Domain` não referencia outros projetos da solução.
- `Application` referencia apenas `Domain`.
- `Infrastructure` implementa persistência e integrações e referencia
  `Application` e `Domain`.
- `Api` e `Worker` são pontos de composição: configuram infraestrutura,
  mensageria e processo de hospedagem.
- `Api` e `Worker` não se chamam diretamente. A comunicação assíncrona ocorre
  pelo Redis.
- O frontend acessa o backend por HTTP e não conhece banco, fila ou storage.

Regras de negócio devem permanecer em `Application` ou `Domain`. Controllers,
componentes React e configuração dos hosts não devem concentrar cálculos ou
regras que precisem ser reutilizados e testados isoladamente.

## Frontend

O frontend começa em `frontend/src/main.tsx`; o roteador é configurado em
`frontend/src/router.tsx` e sua árvore é gerada em
`frontend/src/routeTree.gen.ts`.

- `src/routes`: páginas e layouts baseados em arquivos.
- `src/components`: componentes da aplicação.
- `src/components/ui`: componentes visuais reutilizáveis.
- `src/lib`: clientes HTTP, autenticação, transformações, métricas e utilitários.
- `src/hooks`: hooks compartilhados.
- `src/styles.css`: estilos globais e tokens visuais.

O layout raiz fornece o `QueryClient`, controla autenticação das rotas privadas,
tema e barra lateral. Requisições autenticadas passam pelos clientes de
`src/lib`, que centralizam a URL da API e o token JWT.

Buscas textuais reativas usam `useDebouncedValue` e o intervalo compartilhado
`TEXT_SEARCH_DEBOUNCE_MS`, de 400 ms. O intervalo compartilhado evita uma
requisição por tecla sem tornar a busca perceptivelmente lenta. Consultas mais pesadas podem usar
`COMPLEX_TEXT_SEARCH_DEBOUNCE_MS`, de 500 ms, desde que a escolha seja explícita
e testada. Filtros não textuais, como data e seleções fechadas, não recebem
atraso artificial.

As telas de rotas iniciam com a data local atual e oferecem uma data de
referência livre, sem limitar a navegação aos dias que possuem planilha. A API
resolve a versão aplicável escolhendo o snapshot `Completed` mais recente que
estava concluído até o fim do dia solicitado no fuso `America/Sao_Paulo`; antes
do primeiro snapshot, a consulta retorna vazia. Versões `NeedsReview` não compõem esse histórico por não
representarem estado publicado. A consulta também aceita criticidade e aplica o
filtro antes da paginação, preservando totais coerentes.
Busca por rotas também é executada no banco, sem filtragem local no frontend, e
compara o nome da rota ou o nome de qualquer cidade da rota. O termo recebido é
compactado, convertido para caixa alta e tem os acentos removidos antes da
consulta, acompanhando a normalização dos nomes armazenados pelas planilhas.

Cada rota apresenta a ocupação com uma barra e um indicador circular. A
classificação visual usa faixas explícitas de eficiência logística: abaixo de
60% é `Ocioso` (azul), de 60% até menos de 80% é `Médio` (amarelo), de 80% até
100% é `Bom` (verde), e acima de 100% é `Crítico` (vermelho) por sobrecarga.
Sobrecargas preservam e exibem o percentual excedente;
ausência de capacidade aparece como `Indisponível`, sem ser convertida em zero.

Alguns clientes de `frontend/src/lib/importer-api.ts` representam contratos de
serviços do ecossistema que não estão implementados neste backend. Ao alterar um
contrato realmente atendido por este repositório, frontend e backend devem ser
atualizados juntos.

## Backend

### API

`InovaSkill.Importer.Api/Program.cs` é a composição da API. Ele registra
controllers, infraestrutura, JWT, CORS, limites de upload e Wolverine. Na
inicialização, aplica as migrations do Entity Framework e garante os usuários
padrão.

Os controllers atuais atendem:

- autenticação e cadastro;
- upload, consulta, correção e reprocessamento de importações de rotas;
- consulta das rotas processadas;
- manutenção de tipos de veículo;
- consulta, retry e cancelamento de jobs administrativos.
- consulta paginada dos clientes da importação atualmente publicada.
- consulta paginada e detalhe de documentos fiscais, além do resumo histórico de
  consumo em vendas por cliente.

O middleware JWT libera endpoints públicos e valida as demais requisições antes
de chegarem aos controllers.

### Application e Domain

`Application/RouteImports` contém os contratos do pipeline de importação, a
mensagem `ProcessImport`, interfaces de storage/processadores, ciclo de vida,
cálculo de ocupação, política de capacidade dos veículos logísticos e resumo de
execuções.

`Domain/Entities` contém usuários, notificações, fontes de dados, importações,
erros, execuções, tipos de veículo, rotas, entradas de rota, clientes, snapshots
de clientes e municípios compartilhados. Os estados da
importação ficam em `Domain/Enums`.

### Infrastructure

`ImportDbContext` mapeia as entidades para PostgreSQL. A configuração de
dependências registra:

- `ImportDbContext` com Npgsql;
- `LocalImportFileStorage`;
- `RoutesSpreadsheetParser`;
- `RoutesByCityProcessor` como `IDataSourceProcessor`.
- `CustomersSpreadsheetParser` e `CustomersProcessor` como segundo processador,
  reutilizando o mesmo ciclo de vida, storage, fila e publicação versionada.
- `FiscalMovementsSpreadsheetParser` e `FiscalMovementsProcessor` para a fonte
  `FISCAL_MOVEMENTS`, em modo `Upsert`, acumulando fatos históricos.

Parsing, acesso a arquivos e persistência ficam nesta camada porque dependem de
formatos ou tecnologias externas. As decisões de domínio extraídas desses dados
devem continuar testáveis sem depender do host HTTP.

### Worker

`InovaSkill.Importer.Worker/Program.cs` configura o mesmo acesso a banco e
storage da API e escuta o stream Redis `route-imports`, no grupo
`route-import-workers`. O Wolverine descobre o `ProcessImportHandler` na
Infrastructure e agenda retries após 5 segundos, 30 segundos e 2 minutos.

O Worker é o local para parsing, consolidações e cálculos pesados. A API apenas
registra/consulta o trabalho e publica a mensagem.
Mensagens de importação possuem timeout de execução de 30 minutos, definido pela
constante compartilhada `WorkerExecutionTimeoutMinutes`. O intervalo acomoda
planilhas fiscais grandes sem transformar cancelamentos normais do host em
falhas silenciosas; retries continuam seguindo a política do Wolverine.
Processadores podem limpar o `ChangeTracker` entre lotes para limitar memória.
Por isso, o handler sempre recarrega `JobExecution` após o processador retornar
e antes de persistir o estado terminal, evitando divergência entre um import
`Completed` e um job ainda `Processing`.

### Versionamento e publicação

Cada upload cria um import com versão crescente por `DataSource`. A fonte possui
uma chave de processador, um modo (`Snapshot`, `Append` ou `Upsert`) e ponteiros
opcionais para o import atual e o último import bem-sucedido.

`ImportLifecycleService` protege a criação da versão e a publicação com
transação e advisory lock no PostgreSQL. Fontes `Snapshot`, como rotas, somente
trocam `CurrentImportId` depois do processamento completo e quando a versão
candidata é maior que a atual. Assim, jobs fora de ordem não voltam o estado
publicado e todos os snapshots permanecem disponíveis para histórico.

## Fluxos principais

### Requisição HTTP

1. O TanStack Router renderiza a página.
2. Um cliente em `frontend/src/lib` envia HTTP/JSON com o JWT quando necessário.
3. O middleware autentica a requisição.
4. O controller valida a entrada e coordena o caso de uso.
5. Application/Domain aplicam as regras; Infrastructure acessa PostgreSQL,
   storage ou integração.
6. A API devolve o contrato HTTP e o frontend atualiza a interface.

### Importação assíncrona de rotas

1. O frontend envia o XLSX para `POST /api/route-imports`.
2. A API salva o arquivo, cria a importação e a execução em fila.
3. A API publica `ProcessImport` no stream Redis `route-imports` e responde
   `202 Accepted`.
4. O Worker consome a mensagem e abre o arquivo no storage compartilhado.
5. O processador interpreta, valida, calcula ocupações e persiste um snapshot
   vinculado exclusivamente àquela importação.
6. Depois de concluir, o Worker tenta publicar o snapshot por comparação segura
   de versões.
7. O frontend consulta o ponteiro atual sem precisar conhecer o ID da versão
   publicada; consultas históricas recebem explicitamente o ID do import.

O arquivo não trafega pelo Redis. API e Worker precisam usar o mesmo
`Storage__ImportsPath`. Consulte o documento específico da importação para
estados, idempotência, correções e estrutura das tabelas.

O XLSX imutável no storage é o registro bruto auditável desta fonte. As tabelas
`routes` e `route_entries` são dados interpretados do domínio e nunca substituem
o arquivo bruto.

Na planilha de rotas, a coluna `Média/Dia` informa a carga destinada a cada
cidade. O processador soma essa coluna para obter o peso total da rota. As
capacidades conhecidas são regras explícitas de domínio: Truck com 10.300 kg,
Toco com 7.700 kg e Acelo com 3.300 kg. Tipos desconhecidos não recebem
capacidade inventada e ficam com ocupação indisponível até serem configurados.
A migration de versionamento também preenche essas capacidades conhecidas e
recalcula peso e ocupação dos snapshots que já existiam antes da mudança.

Valores numéricos são lidos pelo conteúdo real da célula, nunca pelo texto
formatado exibido pelo Excel. `Média/Dia` é persistida com três casas decimais;
o peso total deve ser igual à soma das entradas e o frontend exibe até três
casas com a unidade `kg/dia`.
`RouteLoadPolicy` normaliza cada entrada uma única vez, com três casas e
arredondamento de ponto médio para longe de zero. O total e a ocupação são
calculados somente após essa normalização, garantindo igualdade exata entre a
soma das partes persistidas e o total.

### Clientes e municípios compartilhados

A fonte `CUSTOMERS`, em modo snapshot, usa a identidade estável
`DataSourceId + BranchCode + ExternalCode`. Os dados mutáveis ficam em
`customer_snapshots`, vinculados ao `ImportId`; `/api/customers` consulta apenas
o `CurrentImportId` da fonte e pagina no banco.
O parser localiza o cabeçalho pelas oito colunas obrigatórias nas primeiras
linhas da aba, permitindo títulos de relatório antes de `Codigo`, sem depender
de um número fixo de linha.

### Movimentações fiscais

A fonte `FISCAL_MOVEMENTS` reutiliza o upload, storage, fila, Worker e ciclo de
vida genéricos. Ela não é um snapshot: documentos e itens são persistidos de
forma acumulativa e idempotente em `fiscal_documents` e
`fiscal_document_items`; produtos possuem identidade em `products`.
O endpoint aceita arquivos de até 100 MB, alinhado entre atributo HTTP, limite
multipart, frontend e Nginx.
O usuário não escolhe a fonte. `SpreadsheetDataSourceDetector` lê em streaming
as primeiras 50 linhas das abas pelo OpenXML, sem materializar o workbook
completo, e identifica clientes e movimentos fiscais pelos cabeçalhos
obrigatórios ou rotas pela aba de dia da semana e pelo marcador `Cidades da
Rota`. Arquivos desconhecidos ou ambíguos são rejeitados antes da criação do
import.
O parser lê o valor numérico real da célula (não a máscara contábil exibida) e
o processador resolve documentos em lotes de 500, evitando uma consulta por
documento e limitando o crescimento do Change Tracker.

A chave de documento é composta por fonte, tipo, número, série, data de emissão,
código do cliente e loja. A chave do item é documento mais número do item.
Constraints únicas no PostgreSQL sustentam ambas as invariantes e tornam
arquivos sobrepostos seguros. `FirstSeenImportId` e `LastSeenImportId` mantêm a
rastreabilidade sem usar `CurrentImportId` para consultar fatos.

Clientes são resolvidos pela identidade cadastral `ExternalCode + BranchCode`.
Municípios só são vinculados quando o nome normalizado identifica uma única
entidade; o texto da emissão sempre é preservado, assim como código, loja e nome
do cliente. Produtos são reutilizados por `DataSourceId + ExternalCode`.

As categorias `Sale`, `Return`, `Bonus`, `Loan`, `Exchange` e `Unknown` são
definidas por uma política central. O resumo inicial do cliente soma no banco
somente `GrossWeightKg` de itens em documentos `Sale`: últimos 30 dias, os 30
dias anteriores e últimos 90 dias divididos por três. Base anterior zerada
produz variação nula e estado `NEW_ACTIVITY`. As movimentações recentes não são
restritas a vendas.

O resumo do cliente também agrega no banco uma série de 12 meses-calendário,
preenchendo meses sem movimento com zero. Ela sustenta nove indicadores
factuais: peso vendido em 30 dias, variação contra os 30 dias anteriores, média
mensal de peso vendido em 12 meses, última compra, quantidade de notas de venda
em 30 dias, peso médio por nota de venda em 12 meses, peso devolvido em 12 meses
e peso bonificado em 12 meses, além do faturamento médio mensal calculado.
Peso vendido considera apenas `Sale`; devoluções
e bonificações são exibidas separadamente e não alteram o consumo inicial.
Os indicadores aplicáveis abrem uma linha mensal de 12 pontos, reutilizando a
mesma resposta agregada sem novas requisições ou cálculo sobre itens no browser.

O faturamento calculado segue a regra de negócio explícita
`Quantity × UnitValue` para itens de documentos `Sale`. Valores unitários
ausentes contribuem com zero e outras categorias não compõem o faturamento.
A média mensal soma os 12 meses-calendário da série e divide por 12, com
arredondamento monetário em duas casas. `SourceTotalValue` continua preservado
como dado bruto da planilha, mas nunca participa dessa fórmula porque apresentou
divergências sistemáticas na fonte validada.
O detalhe do documento fiscal expõe pela mesma fórmula o valor calculado total
e o subtotal de cada item, além de quantidade total, contagem de itens e peso
bruto. Nenhum desses valores derivados lê `SourceTotalValue`.

### Projeção exploratória do cliente

`GET /api/customers/{id}/projection` consulta no banco os fatos `Sale` e monta
12 meses-calendário consecutivos de peso bruto e faturamento calculado. A data
máxima de todo o histórico fiscal define a cobertura da fonte; o mês dessa data
é excluído por poder estar incompleto, e a janela termina no mês integral
anterior. Meses sem venda do cliente entram explicitamente com zero.

`CustomerProjectionCalculator`, na camada Application, ajusta separadamente
peso e faturamento por mínimos quadrados ordinários:

```text
y = intercepto + inclinação × índice_do_mês
variação mensal = inclinação
variação mensal percentual = inclinação / média histórica
```

O cálculo projeta os três meses seguintes, limita valores e limites inferiores
a zero e retorna intervalo de previsão de 95%. O intervalo usa o erro residual,
a distância do horizonte à média temporal e o valor t de Student para dez graus
de liberdade (12 observações e dois parâmetros). A resposta também expõe R²,
RMSE normalizado, meses ativos e qualidade:

- `HIGH`: pelo menos 8 meses ativos, R² ≥ 0,70 e RMSE normalizado ≤ 25%;
- `MODERATE`: pelo menos 6 meses ativos, R² ≥ 0,40 e RMSE normalizado ≤ 50%;
- `LOW`: base suficiente sem os critérios anteriores;
- `INSUFFICIENT`: menos de 4 meses ativos ou média histórica zerada.

A projeção é explicável e exploratória, não uma garantia de demanda ou receita.
O frontend mostra realizado, projetado e limites de 95%, e alerta quando a
qualidade é baixa ou insuficiente. Esta etapa não calcula risco de ruptura,
ocupação futura de rota nem recomendação automática. O índice composto
`CustomerId + IssueDate + MovementCategory` atende a consulta da janela.

Os índices compostos em cliente, data e categoria atendem às agregações do
resumo; índices por data/categoria, número de documento, documento do item e
produto atendem listagem, detalhe e relacionamentos sem N+1. Não há previsão,
alerta ou cálculo financeiro neste fluxo.

A Central de Processamentos consulta `/api/admin/jobs` e
`/api/admin/jobs/summary` a cada cinco segundos, além da atualização manual. O
polling impede que a tela preserve indefinidamente um estado antigo depois que
o Worker conclui o job.

No frontend, `/clientes` é uma listagem cadastral simples, com busca feita no
backend por código, razão social, nome fantasia, documento ou nome parcial do
município. O termo de município é normalizado para caixa e acentuação antes da
consulta. Como esse acesso usa correspondência parcial sem UF, um índice GIN
trigram em `municipalities.NormalizedName` complementa o índice único
`StateCode + NormalizedName`; o custo adicional de escrita é baixo porque
municípios mudam com pouca frequência.
backend, tabela e paginação. Ela não possui métricas, gráficos, detalhes,
edição, mapas ou associação visual com rotas.

`municipalities` identifica municípios por `StateCode + NormalizedName`. A
normalização remove acentos, compacta espaços e usa caixa alta, sem fuzzy
matching. `CustomerSnapshot.MunicipalityId` é obrigatório.
`RouteEntry.MunicipalityId` é opcional porque a planilha de rotas não contém UF:
o processador associa a entrada somente quando existe exatamente um município
com aquele nome normalizado entre todos os estados. Casos desconhecidos ou
ambíguos preservam `RouteEntry.Name` e ficam sem associação, sem inventar UF.

### Estratégia de índices

Os índices acompanham os padrões reais de leitura:

- rotas usam `ImportId + Weekday + Name` para listagem, `ImportId +
  OverallOccupancy` para criticidade e índices GIN trigram sobre o nome da rota
  e da cidade para buscas textuais com `contains`;
- clientes mantêm a unicidade por fonte, filial e código, além de
  `DataSourceId + ExternalCode + BranchCode` para a ordenação da listagem;
- snapshots usam `ImportId + CustomerId` para idempotência, `ImportId +
  MunicipalityId` e `ImportId + CustomerType` para filtros;
- razão social, nome fantasia e documento possuem índices GIN trigram porque a
  API oferece busca por trecho, que não é atendida eficientemente por B-tree;
- municípios mantêm a unicidade e resolução por `StateCode + NormalizedName`.

A extensão PostgreSQL `pg_trgm` é criada pela migration de índices. Novas
consultas devem revisar seletividade, ordenação, joins e custo de escrita antes
de adicionar ou dispensar um índice.

## Dados e infraestrutura

O `docker-compose.yml` da raiz define `frontend`, `api`, `worker`, `postgres` e
`redis`, com volumes persistentes para PostgreSQL, Redis e uploads. No
desenvolvimento local, apenas PostgreSQL e Redis devem rodar no Docker:

```bash
docker compose up -d postgres redis
```

Frontend, API e Worker devem ser executados localmente pelos comandos de seus
projetos. Em uma stack completa, o Nginx do frontend entrega os arquivos
estáticos e encaminha chamadas `/api` para a API.

### Execução completa em desenvolvimento local

Na raiz do repositório, suba somente a infraestrutura:

```bash
docker compose up -d postgres redis
docker compose ps
```

Em um terminal separado, execute a API. A inicialização da API aplica as
migrations pendentes:

```bash
cd backend
dotnet run --project InovaSkill.Importer.Api/InovaSkill.Importer.Api.csproj --launch-profile http
```

Em outro terminal, execute o Worker:

```bash
cd backend
dotnet run --project InovaSkill.Importer.Worker/InovaSkill.Importer.Worker.csproj
```

Em outro terminal, execute o frontend:

```bash
cd frontend
npm install
npm run dev
```

Endereços padrão:

- frontend: `http://localhost:5173`;
- API: `http://localhost:5279`;
- PostgreSQL: `localhost:5432`;
- Redis: `localhost:6379`.

Para encerrar a infraestrutura depois de parar os processos locais:

```bash
docker compose stop postgres redis
```

Configurações essenciais:

- `ConnectionStrings__ImportDb`: conexão PostgreSQL usada pela API e pelo Worker.
- `ConnectionStrings__Redis`: conexão Redis usada pelo Wolverine.
- `Storage__ImportsPath`: caminho compartilhado dos arquivos importados.
- `VITE_API_URL`: base da API incorporada ao build do frontend.

Em desenvolvimento local, quando `VITE_API_URL` não é informado, o frontend usa
`http://localhost:5279/api`. No build servido pelo Nginx, `VITE_API_URL=/api`
mantém as chamadas no mesmo host e o proxy encaminha para a API.
Falhas de conexão no login nunca criam sessão simulada: o frontend informa a
indisponibilidade e exige um token real emitido pela API.

## Testes

- Frontend: Vitest, com testes próximos das bibliotecas e componentes em
  arquivos `*.test.ts` e `*.test.tsx`.
- Backend: xUnit no projeto `InovaSkill.Importer.Tests`.

Toda mudança de comportamento deve atualizar seus testes. Métricas, agregações,
parsers e estados assíncronos exigem cenários de sucesso, borda, erro e
invariantes de negócio, conforme as regras do `AGENTS.md`.

## Como evoluir a arquitetura

- Uma nova página entra em `frontend/src/routes` e reutiliza os componentes de
  `frontend/src/components/ui`.
- Um novo endpoint entra na API, mas sua regra vai para `Application/Domain`.
- Uma nova integração ou persistência é implementada em `Infrastructure` e
  registrada na composição da API/Worker.
- Um processamento pesado é publicado pela API, consumido pelo Worker e tem seu
  resultado persistido antes da consulta.
- Uma nova fonte de importação implementa `IDataSourceProcessor`, mantém um
  código estável de fonte e reutiliza fila, storage e histórico de jobs.

Qualquer mudança em componentes, limites de camada, dependências entre projetos,
fluxos, contratos, persistência, mensageria, infraestrutura ou estratégia de
execução deve atualizar este arquivo na mesma alteração.
