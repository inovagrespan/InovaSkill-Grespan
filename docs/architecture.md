# Arquitetura do InovaSkill-Grespan

Este documento é o mapa arquitetural do repositório. Ele descreve os limites entre
as aplicações, as dependências permitidas e os principais fluxos de execução.
Detalhes exclusivos da importação de rotas estão em
[route-import-architecture.md](./route-import-architecture.md).

## Visão geral

O sistema é composto por uma aplicação web, uma API HTTP, um Worker assíncrono e
um serviço de infraestrutura:

```text
┌──────────────────────┐       HTTP/JSON       ┌──────────────────────┐
│ Frontend             │ ────────────────────> │ API ASP.NET Core     │
│ React + TanStack     │                       │ autenticação e HTTP  │
└──────────────────────┘                       └──────────┬───────────┘
                                                        │
                                      ┌─────────────────┼─────────────────┐
                                      │                 │                 │
                                      v                 v                 v
                              ┌──────────────┐                  ┌──────────────┐
                              │ PostgreSQL   │                  │ Storage XLSX │
                              │ domínio +    │                  │ compartilhado│
                              │ Hangfire     │                  └──────▲───────┘
                              └──────▲───────┘                         │
                                     │                                 │
                                     └─────────────────────────────────┘
                                                       │
                                                       v
                                             ┌──────────────────────┐
                                             │ Worker .NET          │
                                             │ Hangfire queues      │
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
- `postgres`: fonte persistente dos dados de negócio, históricos de execução e
  storage técnico do Hangfire no schema `hangfire`.
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
  Hangfire e processo de hospedagem.
- `Api` e `Worker` não se chamam diretamente. A comunicação assíncrona ocorre
  pelo storage persistente do Hangfire no PostgreSQL.
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

Todas as telas privadas também renderizam o painel flutuante `Conecta IA`. O
painel mantém o histórico apenas na sessão atual do navegador, oferece perguntas
sugeridas e envia perguntas autenticadas para `POST /api/assistant/ask`. As
respostas exibem os conjuntos de dados consultados e informam se foram redigidas
diretamente pelo catálogo demonstrativo ou com auxílio do modelo configurado.
Nesta primeira etapa, o cabeçalho, a mensagem inicial e o rodapé identificam
explicitamente que todos os números são fictícios.
O mesmo componente possui uma variante de página completa em `/assistente`,
disponível para todos os perfis pelo item `Chat IA` da navegação principal. Essa
rota usa toda a área útil para histórico, fontes, sugestões e composição da
pergunta. Enquanto ela está ativa, o layout raiz não renderiza o acionador
flutuante, evitando duas instâncias concorrentes da conversa.
Nos dois modos, a ação `Limpar conversa` descarta apenas o estado visual local,
restaura a saudação e as sugestões iniciais e não envia comandos ao backend.
Antes da limpeza, um diálogo de confirmação exige uma segunda ação explícita,
evitando que cliques acidentais removam o histórico visível.
No modo flutuante, essa confirmação é renderizada dentro dos limites do próprio
painel de conversa; na página completa, permanece como diálogo centralizado.

O acesso funcional usa os perfis `diretor`, `vendas`, `logistica`, `admin` e
`admin_system`. A política compartilhada do frontend controla menu e navegação
direta: Vendas recebe dashboard comercial e módulos de clientes, documentos,
produtos, estoque e mapa; Logística recebe dashboard operacional, rotas,
veículos, mapa, contexto comercial, estoque e produção;
Diretor consulta os módulos gerenciais e operacionais, sem importações ou
processamentos; administradores acessam todos os módulos. Usuários com o perfil
genérico `gestor` não recebem acesso funcional até serem classificados em um
perfil explícito.

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
100% é `Saudável` (verde), e acima de 100% é `Crítico` (vermelho) por sobrecarga.
Sobrecargas preservam e exibem o percentual excedente;
ausência de capacidade aparece como `Indisponível`, sem ser convertida em zero.
Na tela principal de rotas, a ação `Simular`, visível para Vendas, Logística e
administradores, consulta o detalhe da rota e o
catálogo existente de tipos de veículo para comparar visualmente a ocupação
atual com `TotalWeightKg / capacidade selecionada`. O cálculo ocorre somente no
frontend, mantém carga, cidades e entregas inalteradas e não envia comandos de
criação ou atualização para a API.

Na tela de rotas, a ação `Sugestão de IA` consulta
`GET /api/route-optimization-runs/latest` para abrir o último cenário global já
pré-processado. O cenário recomendado principal é um plano global de
distribuição: dadas as cidades existentes, os caminhões atuais e as distâncias
pré-calculadas, o solver propõe como as cidades deveriam ficar distribuídas
entre as rotas do mesmo dia. Quando houver alternativa útil de curto prazo, o
mesmo run também pode persistir um cenário secundário de realocação emergencial,
tratado na UI em aba separada como medida paliativa para aliviar sobrecarga sem
redesenhar toda a malha. Na aba do plano global, a UI não usa nomes das rotas
antigas como identidade da rota sugerida; cada grupo é nomeado por uma cidade de
referência do próprio agrupamento sugerido, preservando os nomes antigos apenas
na lógica de origem/destino persistida. Cada grupo do plano ideal exibe uma
análise operacional única gerada pelo endpoint `assistant/ask`, usando um prompt
curto com o resumo do cenário persistido. Se o assistente estiver indisponível,
a UI usa um texto local de fallback baseado nos mesmos números do job. Em cada card
ou detalhe, `Ver recomendação` consulta
`GET /api/routes/{routeId}/latest-optimization` e mostra somente o recorte
persistido da última otimização global: cenário atual, cenário simulado, cidades
adicionadas/removidas, versão dos dados, avisos e justificativas. Abrir uma rota
nunca cria run nem recalcula matriz, realocação ou troca de veículo. A execução
manual de otimização global fica na Central de Processamentos como job
operacional, reutilizando `POST /api/route-optimization-runs` ou o catálogo de
jobs administrativos para enfileirar o processamento.

O card executivo `Taxa de Ocupação` do dashboard logístico usa somente o
snapshot atual publicado de rotas, sem filtro de data ou comparação com período
anterior. A API soma `Route.TotalWeightKg` das rotas com capacidade configurada
e divide pela soma de `VehicleType.CapacityKg` dessas mesmas rotas; rotas sem
capacidade ficam fora do numerador e denominador, mas são retornadas como
contagem operacional separada. O card usa as mesmas faixas visuais das rotas: abaixo de
60% é `Ocioso`, de 60% até menos de 80% é `Médio`, de 80% até 100% é
`Saudável`, e acima de 100% é `Crítico`.

Alguns clientes de `frontend/src/lib/importer-api.ts` representam contratos de
serviços do ecossistema que não estão implementados neste backend. Ao alterar um
contrato realmente atendido por este repositório, frontend e backend devem ser
atualizados juntos.

## Backend

### API

`InovaSkill.Importer.Api/Program.cs` é a composição da API. Ele registra
controllers, infraestrutura, JWT, CORS, limites de upload, storage Hangfire e o
Dashboard em `/hangfire`. Na inicialização, aplica as migrations do Entity
Framework e garante os usuários padrão.

Os controllers atuais atendem:

- autenticação e cadastro;
- upload, consulta, correção e reprocessamento de importações de rotas;
- consulta das rotas processadas;
- consulta do resumo de ocupação do snapshot atual de rotas;
- manutenção de tipos de veículo;
- consulta, retry e cancelamento de jobs administrativos;
- catálogo e execução manual de jobs operacionais declarados como executáveis;
- consulta paginada dos clientes da importação atualmente publicada.
- solicitação manual de otimização global de rotas e consulta dos resultados
  persistidos por execução ou por rota.
- consulta paginada e detalhe de documentos fiscais, além do resumo histórico de
  consumo em vendas por cliente e da taxa fiscal de devolução por peso.
- consulta de clientes reais para mapa logístico, posicionados pela coordenada
  do município cadastrado.
- upload versionado de cadastro de produtos, estoque atual e controle diário de
  estoque pelo mesmo endpoint genérico de importações.
- consulta paginada de produtos, detalhe do produto, estoque atual e métricas
  operacionais de estoque/produção.

O middleware JWT libera endpoints públicos e valida as demais requisições antes
de chegarem aos controllers.
Depois da autenticação, `ApiAuthorizationMiddleware` aplica a mesma separação
por domínio e método HTTP. Rotas podem ser consultadas pelos cinco perfis;
produção é restrita a Logística, Diretor e administradores; leitura de clientes,
documentos fiscais, produtos, estoque
e mapa atende os cinco perfis; importações e jobs administrativos são exclusivos
de `admin` e `admin_system`. Tipos de veículo são consultáveis pelos cinco
perfis para sustentar consultas e simulações, mas a aba de cadastro permanece
restrita a Diretor, Logística e administradores, e somente Logística e
administradores podem alterá-los. Violações autenticadas retornam HTTP `403`.

### Assistente corporativo

`AssistantController` expõe `POST /api/assistant/ask` como uma operação somente
leitura para o chat. O request aceita `sessionId` opcional e `message`; `question`
continua aceito apenas por compatibilidade com clientes antigos. O controller
valida tamanho e vazio, obtém `sub` e `role` do JWT e não aceita identidade,
empresa, perfil ou permissão vindos do frontend ou da OpenAI.

`BusinessAssistantService` orquestra o ciclo de tool calling com a OpenAI
Responses API. Ele carrega até `Assistant:MaximumHistoryMessages` mensagens,
envia o prompt centralizado em `AssistantPrompts`, registra as ferramentas
permitidas por DI e limita cada pergunta a
`Assistant:MaximumToolExecutionsPerMessage` execuções. Timeouts de modelo e de
ferramenta são configuráveis por `Assistant:OpenAiTimeoutSeconds` e
`Assistant:ToolTimeoutSeconds`. Respostas controladas são usadas quando a OpenAI
fica indisponível, excede timeout, devolve resposta vazia ou tenta executar uma
ferramenta inválida. Detalhes técnicos ficam em logs; a resposta HTTP expõe
somente texto final, `sessionId`, sugestões, fontes resumidas e nomes públicos
em `consultedTools`.

`OpenAiChatModelClient` é a única classe que conhece a API da OpenAI. A chave é
carregada exclusivamente de `OPENAI_API_KEY`; ela não é versionada, logada ou
enviada ao frontend. O modelo padrão vem de `Assistant:Model`. A OpenAI recebe
apenas definições de ferramentas com JSON Schema e payloads pequenos retornados
por essas ferramentas. Ela nunca recebe connection string, SQL, schema completo,
nomes internos de tabelas/colunas ou entidades do Entity Framework.

As ferramentas do chat implementam `IChatTool` e são registradas como coleção no
container. Nesta versão existem ferramentas de rotas (`search_routes`,
`get_route_details`, `get_critical_routes`, `list_routes_by_occupancy`,
`get_route_cities`, `get_route_customers`), otimização
(`get_latest_route_optimization`, `request_global_route_optimization`) e
consultas corporativas somente leitura (`search_customers`,
`get_customer_consumption_summary`, `list_recent_fiscal_documents`,
`get_fiscal_return_rate`, `search_products`, `get_product_details`,
`get_inventory_summary`, `list_inventory_positions`,
`list_stockout_products`, `get_production_summary` e
`list_production_records`).
`list_routes_by_occupancy` dá ao modelo uma consulta ampla, mas ainda limitada,
para responder rankings e recortes como rotas ociosas, maiores ocupações,
menores ocupações, rotas por classificação e faixas percentuais. Cada ferramenta
valida os argumentos recebidos do modelo, aplica limites configurados ou
constantes explícitas e chama `IRouteChatQueryService`; não há ferramenta
genérica de banco, SQL ou consulta livre. Adicionar uma nova ferramenta exige
criar a classe, registrar no container e cobrir permissões/testes, sem alterar
um bloco central de decisão do orquestrador. Os nomes das ferramentas ficam
restritos aos logs operacionais e não são expostos no contrato público do chat.

As ferramentas corporativas chamam `IBusinessChatQueryService`, definido em
`Application/RouteImports` e implementado em `Infrastructure`. Esse serviço
reutiliza as mesmas fórmulas, fontes publicadas e limites das telas de clientes,
notas fiscais, produtos e estoque: cadastro atual de clientes, histórico fiscal
persistido, snapshot atual de `INVENTORY_CURRENT` e última data publicada de
`DAILY_INVENTORY`. Os payloads para a OpenAI são DTOs reduzidos e não incluem
documento cadastral de cliente, schema, SQL, conexão ou detalhes técnicos.
Consultas de busca exigem termo mínimo e limite máximo; consultas fiscais,
estoque, produção e ruptura retornam listas pequenas; a taxa de devolução limita
o período a 365 dias. Produção no chat usa somente o controle diário publicado e
pode ser consultada como resumo agregado ou como registros limitados por produto
e período, sem cálculo pesado síncrono.

O modelo pode fazer cálculos leves durante a resposta apenas sobre dados
pequenos retornados pelas ferramentas na própria interação, como soma, média,
menor/maior valor, diferença, percentual, variação, ranking pequeno ou
comparação direta. Essa capacidade não substitui métricas oficiais: se a
pergunta depender de fórmula de negócio não definida, histórico grande,
consulta livre, previsão, otimização, margem, custo ou regra fiscal/financeira
sensível, o assistente deve informar a limitação e não inventar o indicador.
Métricas recorrentes ou executivas devem ser promovidas para query/serviço do
backend com testes automatizados dedicados.

`get_route_customers` prepara o contrato de vínculo cliente-rota. Enquanto não
existir arquivo ou cadastro manual de associação entre cliente e rota, o
backend materializa uma associação simulada em `route_customer_assignments`:
todo cliente ativo do snapshot atual cujo município está entre as cidades
reconhecidas da rota atual é vinculado à rota com origem
`InferredByMunicipality`. A resposta identifica essa origem com descrição
explícita de que é uma inferência por município. Quando a associação manual
existir no domínio, ela deve reutilizar a mesma tabela e o mesmo contrato
externo, alterando apenas a origem do vínculo para `Manual` ou `Imported`.

`IRouteChatQueryService` e `IBusinessChatQueryService`, em
`Application/RouteImports`, definem DTOs pequenos e seguros para exposição ao
modelo. `RouteChatQueryService` e `BusinessChatQueryService`, em
`Infrastructure`, implementam essas consultas com `ImportDbContext`. Consultas
de rotas usam sempre o snapshot atual publicado de rotas. A busca reutiliza a
normalização de município já existente e procura por nome de rota ou cidade.
Rotas críticas usam `RouteOccupancyLevelPolicy`, a mesma política de
classificação exibida nas telas de rotas, mantendo a IA fora do cálculo de
criticidade. Consultas de estoque e consumo usam as fórmulas já publicadas nas
telas de domínio, com testes dedicados para arredondamento, limites, bases
zeradas, listas limitadas e exclusão de dados sensíveis.

O histórico mínimo fica em `chat_sessions` e `chat_messages`, associado ao
`AppUser` autenticado. Ele armazena apenas mensagens do usuário e respostas do
assistente, com índices por usuário/sessão e data para retomada limitada da
conversa. Resultados brutos de ferramentas, prompts, argumentos, chaves e dados
sensíveis não são persistidos no histórico.

As respostas textuais do assistente seguem um contrato de apresentação simples
para o frontend não inferir estrutura visual de forma ambígua. Registros de
rotas devem sair em linhas iniciadas por `[ROTA]`, no formato
`[ROTA] Nome | Ocupação: 97,4% | Status: Crítico | Motivo: ...`; registros de
clientes da rota devem sair como
`[CLIENTE] Nome fantasia | Código: 0001/01 | Cidade: Marília-SP | Tipo: Mercado | Relação: inferido por município`.
O componente renderiza essas linhas como cartões compactos do tipo adequado.
Recomendações e ações operacionais usam bullets simples e são renderizadas como
lista textual normal. Parágrafos explicativos continuam como texto. O modelo não
deve misturar rotas, clientes e ações na mesma lista nem usar marcadores
técnicos ou markdown de destaque para representar dados estruturados.
Pedidos de recomendação feitos ao chat usam `get_latest_route_optimization`,
quando a pergunta for sobre uma rota específica, ou
`get_latest_global_route_optimization`, quando a pergunta for sobre a sugestão
geral de rotas; ambas consultam apenas resultados já persistidos da última
otimização global. Usuários autorizados podem solicitar novo processamento com
`request_global_route_optimization`; essa ferramenta apenas cria ou reutiliza um
run e enfileira `ProcessRouteOptimizationJob`. O modelo não decide realocação,
caminhão, distância, ocupação ou motivos e não aguarda conclusão do job.

### Application e Domain

`Application/RouteImports` contém os contratos do pipeline de importação, filas
assíncronas, dispatcher de jobs em background, interfaces de
storage/processadores, ciclo de vida, catálogo de jobs operacionais, cálculo de
ocupação, política de capacidade dos veículos logísticos, resumo de execuções e
contratos de otimização de rotas. `RouteOptimizationProblem` é o snapshot
imutável usado pelo solver e não depende de HTTP, chat, frontend, EF ou
serviços externos.

Não existe mais módulo de detecção, findings, central de notificações ou
alertas. Sinais operacionais que antes seriam exibidos como alertas devem ser
consultados pelo chat ou pelas telas de domínio existentes, sem persistência ou
fila paralela de alertas.

`Domain/Entities` contém usuários, fontes de dados, importações,
erros, execuções, tipos de veículo, rotas, entradas de rota, clientes, vínculos
cliente-rota, snapshots de clientes, municípios compartilhados, coordenadas
municipais, produtos, snapshots de estoque e registros diários de estoque.
Simulações de otimização ficam em
`RouteOptimizationRun` e `RouteOptimizationScenario`; cenários persistem JSON
estruturado com métricas, motivos, avisos, realocações, plano global balanceado
e troca simulada de caminhão, sem alterar `routes` ou `route_entries`. Os
estados da importação, da otimização e a origem do vínculo cliente-rota ficam em
`Domain/Enums`.

`RouteCustomerAssignment` é a entidade de vínculo entre `Route` e `Customer`.
Hoje ela é populada por `RouteCustomerAssignmentSynchronizer` com origem
`InferredByMunicipality`, cruzando municípios das entradas da rota atual com
municípios dos clientes ativos do snapshot atual. Quando a entrada de rota ainda
não possui `MunicipalityId`, o sincronizador usa o nome normalizado da cidade da
rota para encontrar o município do cliente e materializar o mesmo vínculo. O
índice único
`RouteId + CustomerId` evita duplicidade quando uma rota possui a mesma cidade
mais de uma vez. Índices por `RouteId + Source`, `CustomerId + Source` e
`RouteId + MunicipalityId` cobrem os padrões esperados de listagem por rota,
auditoria da origem do vínculo e futuras consultas por cliente ou município.
O pipeline de otimização usa `RouteEntry.MunicipalityId` quando disponível e,
quando a planilha de rotas ainda não trouxe esse vínculo, resolve as coordenadas
da cidade pelo município inferido em `route_customer_assignments`. Cidades que
continuarem sem coordenadas são mantidas na rota atual no plano global e geram
aviso no cenário; elas só tornam o run insuficiente quando nenhuma cidade útil
possui coordenada. O solver global persiste primeiro o cenário
`BuildBalancedRoutePlan`, que redistribui cidades entre rotas do mesmo dia
priorizando redução de rotas críticas, menor pico de ocupação, respeito à
capacidade dos caminhões e proximidade. O cenário `ReallocateCities` permanece
como alternativa emergencial para execução manual pontual.
O cálculo de distância é configurável por `RouteOptimization:DistanceProvider`.
`Geographic` mantém a estimativa por latitude/longitude. `Osrm` consulta um
serviço OSRM local com dados OpenStreetMap e grava no cenário o aviso de
distância rodoviária estimada; se o serviço OSRM estiver indisponível, o job
falha de forma explícita em vez de misturar metodologias silenciosamente.

### Infrastructure

`ImportDbContext` mapeia as entidades para PostgreSQL. A configuração de
dependências registra:

- `ImportDbContext` com Npgsql;
- `LocalImportFileStorage`;
- `RoutesSpreadsheetParser`;
- `RoutesByCityProcessor` como `IDataSourceProcessor`.
- `CustomersSpreadsheetParser` e `CustomersProcessor` como segundo processador,
  reutilizando o mesmo ciclo de vida, storage, fila e publicação versionada.
- `RouteCustomerAssignmentSynchronizer`, executado após a ativação de imports
  atuais de rotas ou clientes, para recalcular os vínculos simulados
  cliente-rota sem depender de consulta ad hoc no chat.
- `FiscalMovementsSpreadsheetParser` e `FiscalMovementsProcessor` para a fonte
  `FISCAL_MOVEMENTS`, em modo `Upsert`, acumulando fatos históricos.
- `ProductsSpreadsheetParser` e `ProductsProcessor` para a fonte `PRODUCTS`,
  em modo `Upsert`, mantendo `Product` como cadastro mestre global por
  `ErpCode` e enriquecendo produtos já vistos nas movimentações fiscais.
- `InventoryCurrentSpreadsheetParser` e `InventoryCurrentProcessor` para a
  fonte `INVENTORY_CURRENT`, em modo `Snapshot`, gravando a fotografia de
  estoque em `inventory_snapshots` sem duplicar atributos cadastrais do produto.
- `DailyInventorySpreadsheetParser` e `DailyInventoryProcessor` para a fonte
  `DAILY_INVENTORY`, em modo `Snapshot`, transformando abas mensais em registros
  normalizados por produto e data em `daily_inventory_records`.
- `EmbeddedMunicipalityCoordinateProvider`, baseado no CSV versionado de
  `github.com/kelvins/municipios-brasileiros`, e
  `MunicipalityCoordinateEnrichmentProcessor` para enriquecer coordenadas por
  município em job operacional.
- `HangfireBackgroundJobDispatcher`, `ProcessImportJob` e
  `ProcessOperationalJob` como camada fina de execução assíncrona. As regras
  permanecem em `ImportProcessingService`, `OperationalJobProcessingService` e
  processadores de aplicação/infraestrutura.
- `OsrmDistanceMatrixProvider`, ativado por `RouteOptimization:DistanceProvider
  = Osrm`, consulta `RouteOptimization:OsrmBaseUrl` para obter distância
  rodoviária estimada. O compose inclui o serviço opcional `osrm` no profile
  `osrm`; o grafo local é preparado por `scripts/prepare-osrm-sudeste.sh` em
  `infra/osrm`, diretório ignorado pelo git por conter arquivos grandes.

Parsing, acesso a arquivos e persistência ficam nesta camada porque dependem de
formatos ou tecnologias externas. As decisões de domínio extraídas desses dados
devem continuar testáveis sem depender do host HTTP.

### Worker

`InovaSkill.Importer.Worker/Program.cs` configura o mesmo acesso a banco e
storage da API, registra o storage Hangfire em PostgreSQL e sobe servidores
separados para as filas `imports`, `route-optimization` e `default`. A
quantidade de workers de cada fila vem de
`Hangfire:Workers:{Imports,RouteOptimization,Default}`.

O Worker é o local para parsing, consolidações e cálculos pesados. A API apenas
registra/consulta o trabalho e enfileira o job no Hangfire após persistir o
estado de negócio. Jobs de importação rodam na fila `imports`; jobs
de otimização rodam na fila dedicada `route-optimization`; jobs operacionais
genéricos rodam na fila `default`. Retries técnicos são explícitos:
5 segundos, 30 segundos e 2 minutos, preservando o total de quatro execuções
incluindo a tentativa inicial.
Processadores podem limpar o `ChangeTracker` entre lotes para limitar memória.
Por isso, o serviço de processamento sempre recarrega `JobExecution` após o
processador retornar e antes de persistir o estado terminal, evitando divergência entre um import
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

Produtos, estoque atual e controle diário reutilizam esse mesmo mecanismo de
importações e jobs. `Product` é o elo global entre itens fiscais, estoque e
produção diária: o código ERP/TOTVS fica em `ErpCode`, o código operacional
normalizado fica em `OperationalCode`, e o normalizador central remove espaços,
normaliza caixa e retira apenas o prefixo `V` quando existir. Produtos vindos de
nota fiscal continuam relacionados por `ProductId`, mas a importação fiscal
passa a localizar produtos globalmente pelo código ERP, evitando duplicidade
entre fontes.

`InventorySnapshot` pertence ao `ImportId` da fonte `INVENTORY_CURRENT` e se
relaciona com `ProductId`. A tabela armazena filial, armazém, saldo físico,
empenhado, disponível e valores monetários; nome, unidade, grupo e pesos ficam
somente em `Product`. A versão atual de estoque é resolvida pelo ponteiro
`CurrentImportId` da fonte, sem campo `IsCurrent` nos registros. Os índices
especializados cobrem unicidade lógica por import/produto/filial/armazém,
filtros por estoque disponível no import atual e navegação por produto.

`DailyInventoryRecord` pertence ao `ImportId` da fonte `DAILY_INVENTORY` e
normaliza cada produto + data das abas mensais em uma linha com produção, saída,
ajuste e estoque final. A planilha operacional é vinculada ao produto pelo
código operacional normalizado. Células vazias de produção, saída e ajuste viram
zero; fórmulas simples de soma/subtração são aceitas; erros de fórmula são
registrados em `import_errors`. Duplicidade idêntica por produto/data é ignorada
com aviso, e duplicidade conflitante registra erro sem escolher um valor
arbitrário. Os índices cobrem unicidade por import/produto/data e consultas
históricas por produto/data.

`GET /api/products` lista produtos paginados com busca por nome, `ErpCode` ou
`OperationalCode`, filtros por tipo, grupo e status de estoque. O status usa
somente o import atual de `INVENTORY_CURRENT`: disponível quando a soma de
`AvailableQuantity` é positiva, ruptura quando há snapshot e a soma é menor ou
igual a zero, e sem informação quando não há snapshot para o produto. `GET
/api/products/{id}` retorna cadastro, estoque atual por filial/armazém,
histórico de snapshots, histórico diário atual e itens fiscais recentes.

`GET /api/inventory` consulta o snapshot atual de estoque por produto, grupo,
tipo, armazém, status e ordenações operacionais. `GET /api/inventory/summary`
expõe apenas métricas suportadas pelos dados atuais: rupturas, percentual
comprometido, produção, saída e saldo operacional. A métrica `stockouts` /
`stockoutProducts` conta produtos em ruptura de forma consolidada: agrupa as
linhas do `CurrentImportId` de `INVENTORY_CURRENT` por `ProductId`, soma
`AvailableQuantity` em todos os armazéns e conta o produto quando o saldo
disponível total é menor ou igual a zero. `stockoutWarehousePositions` é apenas
contexto operacional e conta posições de armazém com `AvailableQuantity <= 0`,
sem substituir a métrica executiva por produto. `GET /api/inventory/stockouts`
retorna a lista paginada dos produtos em ruptura, com cadastro do produto,
saldo físico, empenhado, disponível, valor de estoque e quantidade de posições
de armazém afetadas. Como a fonte é uma fotografia, "hoje" significa a última
importação de estoque publicada, não uma leitura transacional em tempo real.
Produção, saída e saldo usam a maior data publicada em `DAILY_INVENTORY`. A
fórmula de comprometimento é `SUM(CommittedQuantity) /
SUM(OnHandQuantity) * 100`, com zero quando a base física é zero.

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
3. Após confirmar o estado de negócio no PostgreSQL, a API enfileira
   `ProcessImportJob` no Hangfire na fila `imports` e responde `202 Accepted`.
4. O Worker consome a fila `imports` e abre o arquivo no storage compartilhado.
5. O processador interpreta, valida, calcula ocupações e persiste um snapshot
   vinculado exclusivamente àquela importação.
6. Depois de concluir, o Worker tenta publicar o snapshot por comparação segura
   de versões.
7. O frontend consulta o ponteiro atual sem precisar conhecer o ID da versão
   publicada; consultas históricas recebem explicitamente o ID do import.

O arquivo não trafega pelo Hangfire. API e Worker precisam usar o mesmo
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

`GET /api/fiscal-documents/return-rate` calcula a taxa exibida no card
`Taxa de Devolução` do dashboard logístico. A API usa o peso bruto dos itens em
documentos fiscais importados no período: `SUM(Return.GrossWeightKg) /
SUM(Sale.GrossWeightKg) * 100`, arredondado para uma casa decimal e retornando
zero quando a base de vendas é zero. O período padrão é de 30 dias e, sem
`dateTo` explícito, termina na maior data fiscal importada; isso evita depender
de datas demonstrativas no frontend. O índice existente por `IssueDate` e
`MovementCategory` sustenta o filtro temporal e por categoria, e os itens são
agregados pelo relacionamento com seus documentos fiscais.

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
O frontend mostra realizado, projetado e limites de 95%, e destaca quando a
qualidade é baixa ou insuficiente. Esta etapa não calcula risco de ruptura,
ocupação futura de rota nem recomendação automática. O índice composto
`CustomerId + IssueDate + MovementCategory` atende a consulta da janela.

Os índices compostos em cliente, data e categoria atendem às agregações do
resumo; índices por data/categoria, número de documento, documento do item e
produto atendem listagem, detalhe e relacionamentos sem N+1. Não há notificação
ou cálculo financeiro neste fluxo.

A Central de Processamentos consulta `/api/admin/jobs` e
`/api/admin/jobs/summary` a cada cinco segundos, além da atualização manual. O
polling impede que a tela preserve indefinidamente um estado antigo depois que
o Worker conclui o job.
`GET /api/admin/jobs/definitions` expõe apenas jobs operacionais declarados no
catálogo da Application, e `POST /api/admin/jobs/definitions/{jobType}/run`
permite executar manualmente somente os que possuem `ManualRunAllowed`. Jobs de
importação de planilha não entram nesse catálogo porque dependem de upload,
arquivo e import específico; eles continuam sendo criados por upload,
reprocessamento ou retry técnico.

### Mapa de clientes por município

Clientes reais no mapa usam a precisão municipal. A importação de clientes
continua gravando `CustomerSnapshot.MunicipalityId`; a coordenada fica separada
em `municipality_coordinates`, como enriquecimento externo do cadastro de
municípios. A tabela guarda `MunicipalityId`, latitude, longitude, fonte,
status, tentativa, resolução e eventual motivo de falha. Não há coordenada de
endereço do cliente neste fluxo.

Quando uma importação de clientes é concluída e publicada como snapshot atual,
o `ProcessImportHandler` enfileira o job operacional
`MUNICIPALITY_COORDINATE_ENRICHMENT`, vinculado ao `ImportId` publicado. O job
é idempotente: consulta os municípios distintos usados pelos clientes daquele
snapshot, ignora os que já possuem coordenada resolvida e tenta resolver apenas
pendências. A fonte primária é o CSV embutido de
`github.com/kelvins/municipios-brasileiros`, casando primeiro por `IbgeCode`
quando disponível e depois por `StateCode + NormalizedName`. O job atualiza
`municipalities.IbgeCode` quando resolve pela base e registra falha controlada
quando o município não aparece na fonte.

`GET /api/logistics/map/customers` consulta somente o snapshot atual de clientes
e retorna pins para clientes cujo município tem coordenada resolvida. Clientes
sem coordenada não aparecem no mapa, mas são contabilizados em
`withoutCoordinates`. Para evitar pins sobrepostos na mesma cidade, a API aplica
um deslocamento visual determinístico em memória; a coordenada persistida
continua sendo a do município. A tela `/mapa` consome esse endpoint e mantém os
trajetos demonstrativos como contexto visual enquanto os clientes vêm da API
real.

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
- coordenadas municipais usam unicidade por `MunicipalityId` e índice por
  `Status`; a consulta do mapa chega nelas por relacionamento 1:1 a partir dos
  municípios presentes no snapshot atual, e o índice de status apoia auditoria
  e reprocessamento de pendências;
- razão social, nome fantasia e documento possuem índices GIN trigram porque a
  API oferece busca por trecho, que não é atendida eficientemente por B-tree;
- municípios mantêm a unicidade e resolução por `StateCode + NormalizedName`.

A extensão PostgreSQL `pg_trgm` é criada pela migration de índices. Novas
consultas devem revisar seletividade, ordenação, joins e custo de escrita antes
de adicionar ou dispensar um índice.

### Alertas e Detecções

O sistema não mantém mais a ideia de alertas, notificações, detecções,
findings, evidências ou fila dedicada de detectores. A migration
`202607180005_RemoveAlertsAndDetectionModule` remove as tabelas legadas
`detector_definitions`, `detection_runs`, `findings`, `finding_evidences` e
`Notifications` em bancos que já receberam essa funcionalidade. Novas
capacidades de análise devem ser expostas pelo chat ou por consultas diretas dos
módulos existentes, sem recriar central paralela de alertas.

## Dados e infraestrutura

O `docker-compose.yml` da raiz define `frontend`, `api`, `worker` e `postgres`,
com volumes persistentes para PostgreSQL e uploads. No desenvolvimento local,
apenas PostgreSQL deve rodar no Docker:

```bash
docker compose up -d postgres
```

Frontend, API e Worker devem ser executados localmente pelos comandos de seus
projetos. Em uma stack completa, o Nginx do frontend entrega os arquivos
estáticos e encaminha chamadas `/api` para a API.

### Execução completa em desenvolvimento local

Na raiz do repositório, suba somente a infraestrutura:

```bash
docker compose up -d postgres
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
- Hangfire Dashboard: `http://localhost:5279/hangfire`;
- PostgreSQL: `localhost:5432`.

Para encerrar a infraestrutura depois de parar os processos locais:

```bash
docker compose stop postgres
```

Configurações essenciais:

- `ConnectionStrings__ImportDb`: conexão PostgreSQL usada pela API e pelo Worker.
- `Hangfire__Storage__ConnectionString`: conexão opcional específica do
  Hangfire; quando ausente, usa `ConnectionStrings__ImportDb`.
- `Hangfire__Workers__Imports`, `Hangfire__Workers__RouteOptimization` e
  `Hangfire__Workers__Default`:
  concorrência por fila do Worker.
- `Hangfire__Dashboard__AllowAnonymous`: libera acesso ao dashboard fora de
  desenvolvimento somente quando configurado explicitamente.
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
- Um processamento pesado é enfileirado pela API no Hangfire, consumido pelo
  Worker e tem seu resultado persistido antes da consulta.
- Uma nova fonte de importação implementa `IDataSourceProcessor`, mantém um
  código estável de fonte e reutiliza Hangfire, storage e histórico de jobs.

Qualquer mudança em componentes, limites de camada, dependências entre projetos,
fluxos, contratos, persistência, mensageria, infraestrutura ou estratégia de
execução deve atualizar este arquivo na mesma alteração.
