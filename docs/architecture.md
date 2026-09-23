# Arquitetura do InovaSkill-Grespan

## Importação de coordenadas HERE de clientes

A tela de Importações aceita o CSV de coordenadas HERE como a fonte
`HERE_CUSTOMER_COORDINATES`. O upload cria uma execução `PROCESS_IMPORT` na fila
existente de importações, preservando acompanhamento, falha e auditoria na Central
de Processamentos. Não existe fila ou monitoramento paralelo.

O processador vincula `COD TOTVS` ao `ExternalCode` do snapshot atual de clientes,
normalizando zeros à esquerda em códigos exclusivamente numéricos.
Códigos ausentes, duplicados no arquivo ou associados a mais de uma loja invalidam
a importação. Somente linhas com `STATUS HERE` igual a `número exato` ou
`número interpolado` atualizam `customer_address_coordinates`; resultados
divergentes ou apenas no logradouro são contabilizados como ignorados. A precisão
fica persistida em `CustomerAddressCoordinate.Precision` como `EXACT` ou
`INTERPOLATED`, sem alterar o contrato da importação cadastral de clientes.

`GET /api/logistics/map/customers` mantém a classificação interna
`ADDRESS_EXACT`, `ADDRESS_INTERPOLATED`, `ADDRESS_APPROXIMATE` ou `MUNICIPALITY`
e publica todo cliente que possua uma coordenada utilizável. O frontend aplica o
filtro de precisão, iniciado em todas as localizações, e permite restringir a
visualização a coordenadas exatas ou aproximadas. Coordenadas interpoladas, de
logradouro, CEP e município continuam identificadas como aproximações nos
marcadores. `withoutCoordinates` contabiliza somente clientes sem latitude e
longitude em qualquer uma das fontes disponíveis.
A leitura parte do snapshot atual pelo índice existente de `ImportId`; como o
arquivo pode omitir zeros à esquerda, a comparação final ocorre em memória sobre
esse conjunto já delimitado. Não foi criado índice especializado adicional.

## Localizações simuladas de clientes

A rota `/administracao/localizacoes-simuladas` e os contratos em
`/api/admin/customer-coordinate-simulations` são exclusivos de `admin_system`.
Eles preenchem, para validação operacional dos cálculos, clientes do snapshot
atual que ainda não possuam coordenada `EXACT`. A ação cria um
`CUSTOMER_COORDINATE_SIMULATION` em `job_executions`; busca e reversão são
processadas pelo Worker e acompanhadas na Central de Processamentos, sem fila ou
monitoramento paralelo.

O Worker usa a coordenada aproximada do endereço como ponto-base e recorre à
coordenada municipal quando necessário. O provedor selecionado em
`Geocoding:Provider` é consultado no ponto e em sondagens determinísticas
próximas. No padrão `Geoapify`, somente um resultado predial/endereço
(`building`, `residential`, `house`, `street` ou tipos comerciais equivalentes)
com endereço formatado, município e UF compatíveis e distância máxima de 25 km
pode ser ativado; o provedor pode não informar número para um endereço residencial,
mas a coordenada ainda precisa ser única.
No Google, permanecem obrigatórios `street_address` e granularidade `ROOFTOP`.
Quando a sondagem reversa não encontra ponto novo e o cliente possui logradouro
cadastrado, o Worker também consulta o geocodificador de endereço configurado
usando esse logradouro, número, bairro e CEP. O retorno só é aceito após as
mesmas validações de município, UF, distância e unicidade; assim uma falha do
reverse não transforma automaticamente um cliente em pendente.
Para clientes sem endereço cadastral, o Geoapify Places também recebe uma consulta
municipal por categorias de lugares e imóveis, incluindo `building.residential`,
`building.commercial`, `building.industrial`, `building.public_and_civil`,
`building.transportation`, `service`, `commercial` e `catering`
(uma vez por município na execução). Essas
categorias concentram a maior cobertura de imóveis e estabelecimentos; o pool
mínimo é ampliado para não esgotar uma única categoria quando já existem centenas
de coordenadas simuladas na cidade;
os resultados são deduplicados por coordenada antes da distribuição determinística.
e seleciona estabelecimentos, lojas, prédios ou endereços retornados pelo catálogo,
desde que tenham texto de endereço/identificação, município e UF compatíveis e
estejam dentro do limite configurado de 25 km. A origem é persistida como
`GEOAPIFY_CITY_ADDRESS`. O município e a UF continuam obrigatórios, evitando
aceitar imóvel de outra cidade apenas por ampliar o raio.
Como último fallback para esses clientes, o Worker consulta o Overpass do
OpenStreetMap por edificações reais no raio municipal, usando o centroide da
cidade apenas como referência espacial. Os centróides de edificações retornados
são filtrados pelo raio ampliado e pela unicidade; nenhuma posição é calculada ou
deslocada artificialmente.
Se o catálogo Overpass não responder ou não tiver cobertura, o Photon (Komoot)
é consultado como segunda fonte pública, aceitando somente resultados com
município compatível e coordenada dentro do raio configurado.
Na comparação municipal, acentos e caixa são ignorados e a equivalência
ortográfica brasileira `CH`/`X` é aceita para corrigir variantes do provedor
(por exemplo, `Echaporã`/`Exaporã`); UF e limite geográfico continuam obrigatórios.
O cliente HTTP do Worker usa conexão IPv4 explícita para evitar falhas de
conectividade dual-stack no ambiente local. O gate compartilhado do Geoapify limita o processo a quatro consultas por
segundo e aplica retry exponencial para HTTP 429. O Nominatim público não é
aceito nesse fluxo porque sua política proíbe sondagem sistemática. Ausência de
chave, ponto-base ou resultado válido nunca gera coordenada inventada: o cliente
permanece pendente e o motivo compõe o resultado do job.

Clientes CPF sem endereço cadastral recebem, somente quando o provedor confirma
um imóvel substituto, um registro técnico contendo apenas documento, município e
UF conhecidos, com origem `COORDINATE_SIMULATION`. A auditoria marca que esse
registro foi criado pela simulação para que a reversão também o remova; rua e
número cadastrais nunca são inventados nem copiados do imóvel substituto. O
histórico referencia diretamente o cliente e mantém a referência opcional ao
endereço, permitindo apagar esse registro técnico sem perder a auditoria.

Um resultado aceito substitui a coordenada ativa com origem específica do
provedor (`GEOAPIFY_CITY_ADDRESS`, `GEOAPIFY_SIMULATED_NEARBY`,
`OPENSTREETMAP_CITY_ADDRESS`, `PHOTON_CITY_ADDRESS` ou `GOOGLE_SIMULATED_NEARBY`), estado
`RESOLVED` e precisão `EXACT`. Isso é
intencionalmente transparente para mapas e consolidação de custos, mas a tela
administrativa mantém o aviso de que o ponto é substituto. Cada aplicação é
registrada em `customer_coordinate_simulation_audits`, incluindo estado anterior
completo, ponto-base, resultado, origem do provedor, Place ID, distância, usuário e execução. A
reversão restaura exatamente o estado anterior e rejeita registros cuja
coordenada ativa tenha sido alterada depois da simulação.

O preenchimento mantém um conjunto de coordenadas exatas já ocupadas e rejeita
qualquer resultado que repita latitude/longitude de outro cliente; o candidato
fica pendente para não inventar um deslocamento artificial. A otimização continua
operando por cliente, mas a tela de sugestão exibe município, código do cliente e
endereço cadastral para que paradas do mesmo município não pareçam duplicações.
Fontes históricas de CEP, logradouro, município e interpolação continuam sendo
tratadas como aproximações mesmo quando uma versão antiga persistiu `Precision =
EXACT`; elas podem voltar ao fluxo de simulação. A matriz rodoviária também rejeita
coordenadas repetidas entre clientes, em vez de produzir trechos falsos de `0 km /
0 min`, e a execução fica pendente até que os pontos sejam distintos.
Sondagens sem candidato são memorizadas por ponto-base durante a execução para
evitar repetir chamadas externas idênticas sem alterar o limite do provedor.

Antes de iniciar o preenchimento, a tela exige que `admin_system` escolha entre
somente aplicar as coordenadas ou também atualizar os cálculos dependentes. Com
`recalculateDependents=true`, conclusões de aplicação e reversão solicitam
`DAILY_ROUTE_OPTIMIZATION` para o snapshot atual de rotas. A conclusão da
otimização encadeia `ROUTE_COST_CONSOLIDATION`; com `false`, nenhum job dependente
é encadeado. Mapas e consultas diretas refletem a coordenada sem
reprocessamento. O fingerprint existente detecta a mudança de
coordenadas e substitui atomicamente o snapshot materializado de custos. A
auditoria possui índice por `JobExecutionId` e índice parcial único por endereço
enquanto `RevertedAt` é nulo. A seleção de candidatos parte do índice existente
de `CustomerSnapshot.ImportId`; não foi criado índice de precisão porque a
consulta já está restrita ao único snapshot publicado e a escrita dessas
coordenadas é pouco frequente.

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

Na execução integral pelo `docker-compose.yml`, frontend, API, Worker e
PostgreSQL sobem com valores demonstrativos e sem dependência de arquivo
secreto. A API escuta HTTP internamente na porta 8080 e desabilita o
redirecionamento HTTPS nesse cenário, pois o Nginx do frontend atua como gateway
para `/api`. As portas e credenciais locais podem ser sobrescritas por `.env`,
usando `.env.example` como referência; esse arquivo particular não é versionado.

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

O frontend mantém somente componentes e adaptadores alcançáveis pela aplicação
ou por testes de comportamento ativos. Componentes gerados sem consumidor não
fazem parte do catálogo de UI; quando uma nova tela precisar deles, devem ser
adicionados com a dependência mínima correspondente. O progresso das
importações usa consultas HTTP periódicas, sem cliente SignalR paralelo.

O layout raiz fornece o `QueryClient`, controla autenticação das rotas privadas,
tema e barra lateral. Requisições autenticadas passam pelos clientes de
`src/lib`, que centralizam a URL da API e o token JWT.

Endereços que não correspondem a uma rota conhecida exibem uma página 404
customizada do Conecta360, preservando o layout privado para usuários
autenticados e oferecendo retorno ao histórico ou ao painel. O controle de
acesso continua sendo aplicado antes da renderização para toda rota válida;
caminhos inexistentes autenticados seguem ao fallback em vez de serem
confundidos com uma rota sem permissão.

Todas as telas privadas também renderizam o painel flutuante `CONECTA360`. O
painel mantém o histórico persistido e isolado pelo usuário autenticado. Na
página completa, o chat ocupa toda a área de conteúdo sem moldura externa e uma
coluna recolhível à direita carrega inicialmente as 20 conversas mais recentes,
destaca a sessão ativa e permite buscar blocos anteriores de 20 sob demanda; ela
permite retomar até 1.000 mensagens de cada sessão. O posicionamento à direita
evita competir visualmente com a navegação principal da aplicação. Em telas
menores, a coluna abre como painel sobreposto. O seletor compacto permanece
apenas na variante flutuante, que também permite carregar blocos anteriores. A
variante flutuante usa até 680 px de largura e 92% da altura visível, limitada a
900 px, enquanto as mensagens do assistente podem ocupar 92% dessa largura. A
página completa permite mensagens de até 960 px, mantendo tabelas e análises de
rotas extensas legíveis sem ocultar conteúdo em truncamento visual.

consulta usa paginação por deslocamento sobre o índice existente de usuário e
data de atualização; nenhum índice adicional é necessário para esse padrão de
acesso. Esses limites protegem a API contra respostas ilimitadas e
não alteram o contexto enviado ao modelo, que continua restrito às mensagens
recentes configuradas em `Assistant:MaximumHistoryMessages`. Oferece
perguntas sugeridas e envia perguntas autenticadas para `POST /api/assistant/ask`.
O cliente da Responses API envia `Assistant:MaximumOutputTokens`, atualmente
8.192, como `max_output_tokens`; o limite inclui texto visível e tokens de
raciocínio e evita que uma configuração implícita reduza análises consolidadas.
As respostas não completam lacunas, declaram quando os dados são insuficientes,
identificam o período ou snapshot consultado e apresentam resultados e análises
diretamente, sem rótulos sobre a origem dos dados. Perguntas com mais de uma
interpretação relevante exigem esclarecimento antes da consulta; o contexto da
conversa pode resolver a ambiguidade quando isso não exigir suposição. O rodapé
do chat informa de forma concisa que período e contexto são considerados.
O assistente aceita cumprimentos, apresentações, preferências, fatos pessoais e
interações sociais breves sem exigir vínculo empresarial. Declarações pessoais
comuns seguem diretamente para a conversa, enquanto classificações ambíguas não
são bloqueadas e podem resultar em um pedido natural de contexto. Somente temas
classificados explicitamente como alheios à Grespan são redirecionados, mantendo
o foco corporativo sem recusar conversa casual inofensiva.
O botão `Nova conversa` mantém o registro anterior e reinicia o estado visual sem
`sessionId`; assim, a próxima pergunta cria uma nova sessão persistida.
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

O perfil `admin_system` possui uma área exclusiva de administração de usuários
em `/administracao/usuarios`. O cadastro envia nome, e-mail, senha e perfil
funcional para `POST /api/admin/users`; a política da API restringe esse contrato
ao `admin_system`, valida duplicidade e permite atribuir somente os perfis
funcionais reconhecidos pela aplicação. A criação é síncrona por ser uma única
operação transacional sobre `AppUsers` e reutiliza os índices únicos existentes
de nome e e-mail, sem exigir índice adicional.

No menu lateral expandido, os atalhos são condensados por domínio nos grupos
sanfonados `Inteligência Artificial`, `WhatsApp`, `Logística`, `Cadastros` e
`Administração`; o Dashboard permanece como acesso principal independente. Cada
grupo contém somente as rotas permitidas ao perfil autenticado e inicia aberto
quando a rota atual pertence a ele. Quando resta apenas um item visível, o
atalho continua direto, e a sidebar recolhida preserva ícones individuais com
tooltip.

Buscas textuais reativas usam `useDebouncedValue` e o intervalo compartilhado
`TEXT_SEARCH_DEBOUNCE_MS`, de 300 ms. O intervalo compartilhado evita uma
requisição por tecla sem tornar a busca perceptivelmente lenta. Consultas mais pesadas podem usar
`COMPLEX_TEXT_SEARCH_DEBOUNCE_MS`, de 500 ms, desde que a escolha seja explícita
e testada. Filtros não textuais, como data e seleções fechadas, não recebem
atraso artificial.

A administração de consumo de IA consulta usuários por nome ou e-mail com
paginação limitada em `GET /api/admin/ai-consumption/users`, tanto no filtro do
relatório quanto na edição dos limites individuais. Nome e e-mail usam índices
trigram especializados, pois a busca aceita fragmentos em qualquer posição. O
detalhamento de chamadas também é paginado no servidor, enquanto os totais são
calculados sobre todo o período filtrado. A ordenação temporal do relatório usa
índice próprio em `ai_provider_calls.CreatedAt`.

A administração de Memórias da IA apresenta os registros em uma grade responsiva
de cartões compactos, com edição aberta sob demanda. A consulta combina busca por
assunto ou conteúdo, usuário proprietário e estado ativo. O filtro de usuário
reutiliza o `ownerUserId` já aceito por `GET /api/admin/knowledge-memories`; suas
opções são derivadas de uma leitura limitada aos 100 registros mais recentes. Os
índices existentes por escopo, proprietário, atividade e atualização atendem a
esse acesso, portanto não foi criado um índice especializado adicional.

As telas de rotas iniciam com a data local atual e oferecem uma data de
referência livre, sem limitar a navegação aos dias que possuem planilha. A API
resolve a versão aplicável escolhendo o snapshot `Completed` mais recente que
estava concluído até o fim do dia solicitado no fuso `America/Sao_Paulo`; antes
do primeiro snapshot, a consulta retorna vazia. Versões `NeedsReview` não compõem esse histórico por não
representarem estado publicado. A consulta também aceita dia da semana e
criticidade, normaliza o dia para o identificador canônico em caixa alta e
aplica os filtros antes da paginação, preservando totais coerentes.
Busca por rotas também é executada no banco, sem filtragem local no frontend, e
compara o nome da rota ou o nome de qualquer cidade da rota. O termo recebido é
compactado, convertido para caixa alta e tem os acentos removidos antes da
consulta, acompanhando a normalização dos nomes armazenados pelas planilhas.

Cada rota apresenta a ocupação com uma barra e um indicador circular. A
classificação visual usa faixas explícitas de eficiência logística: abaixo de
60% é `Ocioso` (azul), de 60% até menos de 85% é `Médio` (amarelo), de 85% até
95% é `Saudável` (verde), e acima de 95% é `Crítico` (vermelho).
O cálculo, a persistência e o texto apresentado preservam a ocupação real acima
de 100%; somente o preenchimento visual da barra e do círculo termina em 100%,
permitindo exibir e ordenar sobrecargas como 140% sem distorcer o indicador.
ausência de capacidade aparece como `Indisponível`, sem ser convertida em zero.
Na tela principal de rotas, os cards não exibem ações individuais de simulação
ou recomendação. O detalhe mantém o apoio à decisão calculado sobre o catálogo
existente de tipos de veículo para os perfis autorizados, sem enviar comandos de
criação ou atualização para a API.

Ao abrir o detalhe de uma rota, `RouteDecisionSupport` classifica os veículos
cadastrados capazes de transportar a carga, priorizando ocupação próxima de 90%
e respeitando as faixas operacionais e o teto de 100%. A tela apresenta até três
cenários, com capacidade, ocupação, justificativa e risco. Opcionalmente, o
usuário pode enviar esses cenários calculados ao assistente existente para obter
uma explicação comparativa. A IA é instruída a não inventar custos, distâncias
ou tempos ausentes e responde em até 80 palavras, separadas em recomendação,
motivo, risco e próximo passo; essa análise não altera rota, veículo nem
persistência. O prompt usa a contagem de cidades e nomes truncados, e o frontend
impõe o mesmo limite máximo de 800 caracteres configurado pela API.
O apoio à decisão, seu cálculo e a análise por IA reutilizam exatamente a mesma
permissão da simulação de veículo: `vendas`, `logistica`, `admin` e
`admin_system`. Para os demais perfis, esses componentes não são renderizados e
o catálogo de veículos não é consultado ao abrir o detalhe.

O subsistema anterior de roteirização foi substituído por uma simulação diária
baseada no pacote oficial Google OR-Tools para .NET. O job
`DAILY_ROUTE_OPTIMIZATION` é configurável por cron na Central de Processamentos,
resolve o snapshot publicado e processa cada dia de forma independente. A
simulação não altera a importação nem publica rotas operacionais. Usuários com
permissão de simulação também podem enfileirá-la pelo botão `Recalcular sugestões` da
tela de Rotas, que chama `POST /api/route-optimizations/simulate`; o cálculo
continua ocorrendo exclusivamente no Worker. O recálculo sempre resolve o
snapshot atualmente publicado; consultas históricas permanecem disponíveis em
modo somente leitura. A tela alterna entre `Rotas reais` e `Sugestões`, com
filtros compartilhados de data e dia da semana. O dia é enviado tanto para
`GET /api/routes` quanto para `GET /api/route-optimizations`, mantendo a seleção
ao alternar a visão. As sugestões mantêm cards equivalentes aos reais,
agrupados por dia, e o detalhe informa a sequência de clientes, município, peso
atribuído à parada, distância, duração, veículo adicional e ociosidade. O
problema usa um nó por cliente vinculado à rota e exige coordenada `EXACT`; a
matriz rodoviária e o OR-Tools deixam de usar o centro municipal. Quando a cidade
textual do endereço exato diverge do município do vínculo, o Worker usa a
coordenada municipal resolvida para impedir que um cadastro inconsistente arraste
a rota para outro município; essa exceção fica registrada no diagnóstico do
processamento. Como a fonte
de rotas contém peso médio apenas por município, o Worker distribui esse peso
em gramas igualmente entre os clientes vinculados ao município, atribuindo o
resto determinística e unitariamente pela ordem do código externo. Essa regra
preserva exatamente a carga total e deve ser substituída por demanda individual
quando essa granularidade passar a existir na fonte de dados.
Cada rota otimizada admite no máximo 15 entregas e 10 horas de jornada (incluindo
15 minutos de atendimento por parada); uma penalidade suave prioriza jornadas de
até 8 horas. Se uma parada isolada já exigir mais que o limite com ida, atendimento
e retorno, o dia é marcado como `Infeasible` com motivo explícito, em vez de
persistir uma rota que viole a jornada.
Na interface, cada veículo distingue `deliveryCount` (clientes/paradas) de
`municipalityCount` (municípios distintos); uma parada municipal sem cliente
vinculado é ignorada, pois não existe entrega concreta para roteirizar.
Antes do cálculo, o Worker sincroniza os vínculos inferidos usando o snapshot
publicado, inclusive os clientes marcados como inativos: quando não existe uma
planilha de associação publicada, cada entrada municipal seleciona de forma
determinística no máximo a quantidade de `Deliveries` informada na própria
entrada. A seleção é ordenada pelo código externo e não repete o mesmo cliente
em outra rota do mesmo dia; portanto, essa relação continua identificada como
simulação e não como vínculo operacional oficial. Se não houver clientes
distintos suficientes, somente os disponíveis são associados, sem inventar
clientes ou repetir pontos. O peso diário da rota representa histórico
operacional e não pode perder uma cidade apenas pelo estado comercial atual do
cadastro. Paradas com carga zero não entram na matriz nem bloqueiam o dia; peso
negativo continua sendo erro de entrada explícito.
Quando existe uma planilha de associação publicada, seus vínculos comprovados
prevalecem, mas a fonte é tratada como sobreposição parcial: cada rota e município
é limitado à soma de `Deliveries` das entradas correspondentes. Os vínculos
importados são consumidos primeiro, sem repetir cliente no mesmo dia; os slots
restantes usam a mesma seleção municipal determinística. Assim, uma planilha que
traga linhas duplicadas, clientes não identificáveis ou mais mercados que a rota
operacional não aumenta artificialmente a quantidade de entregas nem transforma
um conflito em uma atribuição silenciosa.
Até 200 blocos, a seleção de frota usa o modelo exato CP-SAT. Acima desse limite,
usa alocação determinística best-fit decrescente para construir rapidamente uma
solução viável, abrindo primeiro os veículos de maior capacidade e preferindo um
veículo existente quando a capacidade empata; o Routing Solver ainda otimiza a
sequência a partir dessa solução inicial dentro do timeout configurado. O objetivo
principal é minimizar a quantidade de veículos ativos, depois a quantidade de
veículos adicionais e, por fim, a capacidade adicional. A etapa de roteirização
aplica uma penalidade fixa superior à variação máxima de distância para veículos
adicionais, preservando esse dimensionamento. Veículos não selecionados não são
persistidos como rotas ociosas; resultados históricos que já contenham veículos
sem carga continuam legíveis sem serem contabilizados em `ProposedVehicleCount`.
Ao clicar em uma rota real, o detalhe consulta `GET /api/routes/{id}/road-path` e
exibe o percurso rodoviário no mapa, incluindo origem, paradas, retorno ao depósito,
distância e duração, com estados próprios de carregamento e erro. O detalhe não
exibe mais o apoio à decisão local; apresenta quilometragem, tempo para concluir e
o último custo consolidado retornado por `GET /api/routes/{id}/cost`.
O contrato `GET /api/routes/{id}` também retorna `customers`, ordenados pela sequência
municipal da rota e pelo código externo, com nome, código, município e endereço cadastral;
assim a lista operacional mostra quem receberá a entrega, mantendo as cidades apenas como
contexto e fallback quando não houver vínculo de cliente.
Combustível, pedágio e total não são recalculados pelo frontend.
Nas sugestões otimizadas, cada veículo também possui mapa próprio: o detalhe chama
`GET /api/route-optimizations/{resultId}/vehicles/{vehicleId}/road-path`, usando a
sequência persistida de clientes exatos, a Matriz e o retorno ao depósito. Assim,
rotas diferentes não são misturadas em um mapa municipal agregado. O modal das
sugestões mantém a mesma composição do detalhe de rota real: indicadores de
quilometragem, duração, combustível, pedágio (com abertura das praças), gasto total,
carga, ocupação, entregas e cidades. O custo do veículo sugerido é localizado por
`optimizationVehicleId` no cenário `OPTIMIZED` de `GET /api/route-costs`.
Na sequência, os valores à direita são explicitamente rotulados como `Trecho anterior`;
eles representam apenas o deslocamento entre a parada anterior e a atual (na primeira,
da Matriz até o cliente), e não o total da rota. Um trecho `0 m / 0 min` significa que
a matriz rodoviária identificou os dois pontos como a mesma localização; o total sempre
fica nos indicadores da rota.
Clientes distintos que compartilham uma coordenada oficial são mantidos na mesma
matriz (podem ter trecho zero, pois representam o mesmo endereço real). Se o OSRM
retornar zero para pontos com coordenadas diferentes, o serviço substitui somente
esse par por distância haversine e duração na velocidade de fallback configurada,
sem interromper toda a otimização. O mesmo fallback é aplicado quando o provedor
retorna um valor positivo submétrico que seria arredondado para `0 m / 0 min` na
persistência; coordenadas distintas nunca produzem um trecho nulo por arredondamento.
As rotas também persistem o horário de saída (`DepartureTime`) como horário local
opcional, importado do arquivo operacional de horários e exposto nas listagens e
no detalhe. O vínculo usa dia da semana e nome normalizado; nomes compostos são
aplicados às rotas correspondentes somente quando não há ambiguidade.
Na comparação diária, `ProposedVehicleCount` representa somente veículos com ao
menos uma parada. Veículos existentes sem carga continuam persistidos e
aparecem nos cards como ociosos, mas não inflam o KPI `Veículos em rota`;
`AdditionalVehicleCount` e `AdditionalCapacityKg` também consideram somente
veículos adicionais efetivamente utilizados. A API recalcula essas três
métricas a partir da distribuição persistida para manter corretos inclusive os
resultados históricos criados antes dessa regra.
Os KPIs do dia usam cartões visuais por categoria e separam valor atual,
sugestão e impacto. Distância e duração exibem a variação percentual com base
explícita; base atual zero com proposta diferente aparece como `Sem base`, sem
produzir divisão por zero. Veículos exibem a variação absoluta, capacidade é
identificada como introduzida e carga total informa que foi preservada.
O card diário também materializa visualmente a movimentação reconstruída pelos
veículos persistidos: quantidade e capacidade de veículos existentes liberados,
quantidade e capacidade dos veículos introduzidos e os dois saldos líquidos.
Como a redistribuição é global no dia, a tela não inventa um pareamento direto
entre um veículo liberado e outro introduzido.

A fundação rodoviária permanece separada do solver. Existe um cadastro
singleton de depósito logístico, usado como origem e retorno, com nome, endereço
informativo e latitude/longitude obrigatórias. A manutenção ocorre em
`PUT /api/logistics-depot`; Diretor e Logística podem consultar, mas somente
Logística e administradores podem alterar. `GET /api/osrm/health` usa a
coordenada cadastrada para verificar se a API OSRM interna está acessível e
localiza o depósito no grafo.

Ambientes novos recebem idempotentemente a matriz da Grespan como depósito
inicial (`Avenida República, 7000`, Marília; `-22.213890, -49.945830`). A carga
inicial ocorre somente quando a tabela está vazia e nunca sobrescreve uma
configuração posteriormente editada pela operação.

`IOsrmDailyMatrixService` monta uma entrada para um único `Route.Weekday` e um
snapshot explícito. O primeiro ponto é o depósito e os demais são municípios
distintos das `route_entries`, ordenados deterministicamente e limitados a
`MunicipalityCoordinate` com estado `RESOLVED`. Coordenadas cadastrais de
clientes não participam desta fase: o objetivo é calcular custos entre blocos
indivisíveis de cidade, não ordenar entregas porta a porta. Cidade sem vínculo
municipal ou coordenada resolvida invalida a matriz inteira.

Antes da validação, o processador tenta completar vínculos municipais ausentes
por correspondência única do nome normalizado no cadastro e na base municipal
embutida. Para rótulos operacionais com parênteses, o último trecho entre
parênteses também é candidato. A associação encontrada é persistida na entrada
importada. Nomes ambíguos ou sem correspondência não são inferidos: o resultado
diário fica como dados insuficientes e informa nominalmente as cidades sem
vínculo, sem coordenada ou com peso inválido.

O solver OR-Tools é isolado por `Route.Weekday`: cidade, carga,
rota e veículo nunca atravessam dias. Cada município e a soma de sua
`Média/Dia` constituem inicialmente um bloco. Quando essa carga excede a maior
capacidade de veículo cadastrada, o processador a divide deterministicamente em
parcelas de no máximo essa capacidade, permitindo que o mesmo município seja
atendido por mais de um caminhão no mesmo dia. Blocos que já cabem em um único
veículo permanecem indivisíveis. Nenhum veículo pode ultrapassar 100% da
capacidade e a soma das parcelas deve conservar exatamente a carga municipal.
A frota inicial é composta pelos veículos das
rotas daquele dia; quando ela for insuficiente, a simulação poderá acrescentar
instâncias virtuais de tipos cadastrados com capacidade válida, sem criar
cadastros ou alterar o snapshot. A seleção minimiza primeiro a quantidade total
de veículos ativos, depois a quantidade de adicionais e, entre empates, a
capacidade adicional. Assim, veículos pequenos não ficam ativos apenas para
preservar uma frota maior quando um conjunto menor de caminhões comporta a mesma
carga, mas um tipo adicional só é preferido quando reduz a quantidade ativa ou
resolve a capacidade. Distância, duração e equilíbrio de ocupação são avaliados
somente depois dessas restrições de viabilidade e dimensionamento da frota. O
CP-SAT aplica essa ordem lexicográfica, com quebra de simetria entre instâncias
virtuais equivalentes. A atribuição viável encontrada é convertida em solução
inicial do Routing Solver; se a busca rodoviária atingir o timeout, essa
solução continua disponível em vez de ser descartada. Antes da persistência, um
validador confirma bloco exatamente uma vez, conservação da carga, capacidades,
trechos da matriz e consistência dos totais.

Os resultados ficam em `daily_route_optimization_results`,
`daily_route_optimization_vehicles` e `daily_route_optimization_stops`, com
unicidade por snapshot e dia. Uma nova conclusão substitui esse resultado,
enquanto uma falha técnica preserva a última sugestão válida e
`job_executions` mantém o histórico técnico. Apenas `Optimized` persiste
veículos e paradas; os demais estados registram o diagnóstico sem distribuição
alternativa. A API expõe resumo por
snapshot/data em `GET /api/route-optimizations` e detalhe em
`GET /api/route-optimizations/{id}`. O resumo informa `isCurrentSnapshot` e a
última execução do snapshot, com estado, progresso, erro e datas. O frontend
consulta jobs ativos a cada cinco segundos e recarrega os resultados ao chegar
a um estado terminal. Execução concorrente retorna HTTP 409 com mensagem de
domínio.

Após a publicação das rotas e cada conclusão do otimizador, o job
`ROUTE_COST_CONSOLIDATION` materializa os custos atual e otimizado. Alterações no
diesel, no veículo e no enriquecimento de coordenadas também solicitam o job. Se
uma solicitação chegar durante uma execução ativa, `job_executions` recebe a
marca de reexecução e o Worker agenda outra passagem ao concluir; o fingerprint
dos insumos evita trabalho quando nada mudou. Falha técnica não remove o último
snapshot válido.

O cenário `ACTUAL` usa depósito e clientes com coordenadas cadastrais exatas. O
cenário `OPTIMIZED` usa a sequência de clientes exatos persistida pelo solver
canônico em `daily_route_optimization_results`; resultados históricos que ainda
possuem sequência municipal continuam legíveis. O cálculo não consulta o fluxo
legado de `ResultJson`. As bases são comparáveis somente no nível agregado do dia e não
estabelecem correspondência 1:1 entre rota real e veículo sugerido. Ausência de
coordenada exata, diesel, eixos, consumo ou resultado do solver produz item
indisponível com motivo explícito, sem estimativa inventada.

Os resultados são persistidos em `route_cost_snapshots`, `route_cost_items` e
`route_cost_toll_passages`. A substituição ocorre atomicamente após as
integrações rodoviárias. Litros usam três casas e valores monetários duas, com
`total = combustível + pedágio` a partir das parcelas já arredondadas. As
leituras são expostas por `GET /api/route-costs?date=&weekday=` e
`GET /api/routes/{id}/cost`.

`route_cost_items.OptimizationResultId` e `OptimizationVehicleId` são referências
opcionais com `ON DELETE SET NULL`: ao substituir uma otimização diária, o snapshot de custo
histórico permanece auditável e apenas perde os vínculos com o resultado removido.
Isso evita que uma consolidação antiga impeça a reotimização do mesmo dia.

`IOsrmTableClient` consulta `/table/v1/driving` com `duration,distance`, preserva
custos direcionais e divide matrizes grandes em blocos configuráveis de
`sources × destinations`. Para o OpenRouteService, o corpo usa pares numéricos
`[longitude, latitude]` em `locations`, índices como strings em `sources` e
`destinations` e a chave diretamente no cabeçalho `Authorization`, conforme o
contrato v2. O cliente recompõe o resultado na ordem original e rejeita timeout,
erro HTTP, resposta vazia ou dimensão divergente. Valor negativo ou `null` que o
provedor não tenha resolvido. Para pares desconectados no grafo público, envia
`fallback_speed` configurável (50 km/h por padrão). Se o servidor público ainda
devolver um par nulo sem marcá-lo, o cliente calcula distância haversine e duração
na mesma velocidade somente para esse par. Toda matriz que usar qualquer dessas
estimativas é marcada como
`OSRM_TABLE_DRIVING_WITH_GEOGRAPHIC_FALLBACK`, mantendo a aproximação visível.
Não há persistência paralela de matrizes. A configuração `Osrm` define URL base,
timeout, tamanho do bloco, paralelismo máximo e velocidade do fallback.

O depósito possui índice único sobre a chave singleton, suficiente para leitura
e atualização do único registro. A montagem diária reutiliza os índices já
existentes de rotas por `ImportId + Weekday + Name`, entradas por rota e
coordenadas por município; não foi criado índice adicional porque a consulta
parte do snapshot e do dia antes de percorrer as cidades.
O polling consulta a última execução por tipo e snapshot ordenada pela criação;
por isso `job_executions` possui o índice composto
`JobType + RelatedEntityId + CreatedAt`. A seletividade por tipo e entidade e a
ordenação temporal justificam o custo adicional de escrita.

### Remediação de dados da otimização diária

Um resultado `InsufficientData` persiste todas as causas em
`daily_route_optimization_issues`; o avaliador compartilhado não interrompe na
primeira falha e cobre vínculo municipal, peso não positivo, coordenada ausente
e capacidade de veículo ausente. Resultados legados sem linhas estruturadas são
avaliados em leitura. `reason` permanece como resumo compatível. O otimizador é
somente leitura sobre `routes` e `route_entries` e nunca corrige
`MunicipalityId` implicitamente.

No snapshot operacional atual, Diretor, Vendas, Logística, `admin` e
`admin_system` editam as pendências diretamente no modal e publicam a versão
derivada; qualquer consulta a snapshot histórico permanece em modo somente
leitura.

As correções operacionais criam um novo `imports` com `DerivedFromImportId`,
reutilizando o mesmo `FilePath` imutável. O Worker bloqueia a fonte, confirma que
o pai ainda é o snapshot atual, clona rotas e entradas, aplica
`route_import_corrections`, recalcula os totais e somente então publica a versão.
`route_entries.IsExcludedFromOptimization` preserva a parada no detalhe, mas a
remove da matriz, dos blocos e das métricas; apenas peso exatamente zero pode
receber essa marca. `routes.VehicleCapacityKgSnapshot` congela a capacidade usada
por cada versão, embora a correção também atualize `vehicle_types` para
importações futuras.

`municipality_aliases` é único por `DataSourceId + NormalizedAlias` e reaplica o
vínculo nas ocorrências equivalentes e nos próximos imports. Coordenadas manuais
usam a origem auditável `MANUAL_AUDITADO`; coordenadas oficiais conservam a
origem do catálogo embutido. Dias afetados ficam em
`route_import_affected_weekdays`. Resultados de outros dias são clonados com
`InheritedFromResultId` e rotas remapeadas; somente os afetados entram em um novo
`DAILY_ROUTE_OPTIMIZATION` com `weekdays[]`. O `PROCESS_IMPORT` pai e o job do
solver são relacionados por `ParentJobExecutionId`, mantendo fila, progresso,
falha e retry exclusivamente em `job_executions` e na Central de Processamentos.

Os índices especializados refletem os acessos reais: alias por fonte/nome
normalizado, correções por import/tipo, issues por resultado/código, derivação de
imports, herança de resultados e relação pai/filho entre jobs. A origem de aba,
cabeçalho e linha é nullable porque snapshots legados podem exigir localização
segura no XLSX preservado.

O enriquecimento cadastral de clientes consulta a BrasilAPI pelo CNPJ em um job
operacional assíncrono. O Worker limita as chamadas pela configuração
`BrasilApi:RequestsPerSecond` (uma por segundo por padrão), de forma sequencial,
e persiste o endereço em `customer_registration_addresses`, separado dos
snapshots importados. Respostas HTTP 429 são repetidas somente para o CNPJ atual:
o cliente respeita `Retry-After` quando presente ou usa espera exponencial de
um, dois, quatro e até oito minutos, com jitter. Depois do limite configurado de
tentativas, o CNPJ permanece pendente e o job continua nos demais clientes, sem
registrar a indisponibilidade temporária como falha cadastral. A
relação única por `CustomerId` reaproveita o resultado entre versões da fonte;
resultados resolvidos, inválidos ou não encontrados não são consultados outra
vez. Quando o CNPJ retorna logradouro ou bairro ausente e fornece um CEP válido,
o mesmo provedor consulta `cep/v2`, valida município e UF e preenche somente os
campos ausentes. A origem `BRASIL_API_CNPJ_CEP` distingue essa complementação de
`BRASIL_API_CNPJ`; divergências nunca sobrescrevem o cadastro. O endereço é único
e representa exclusivamente o cadastro do CNPJ. O job
`CUSTOMER_REGISTRATION_ADDRESS_ENRICHMENT` reutiliza Hangfire,
`job_executions` e a Central de Processamentos e pode ser iniciado manualmente ou
agendado. Sem `importId` explícito, cada execução resolve o snapshot de clientes
publicado naquele momento. Resultados e progresso são persistidos a cada 25
clientes por padrão no próprio `job_executions`, com contagens de resolvidos,
inválidos, não encontrados, pendentes, completos, sem número, somente CEP,
complementados pelo CEP, CEPs incompatíveis/não encontrados e falhas técnicas;
não existe monitoramento paralelo.
O contrato versão 1 aceita `customerStatus` com `ACTIVE`, `INACTIVE` ou `ALL`;
quando omitido, usa `ACTIVE`. O filtro é aplicado sobre `Customer.IsActive` antes
das consultas externas. O botão da tela de Clientes envia `ACTIVE`, enquanto a
execução manual e os agendamentos na Central podem selecionar os demais valores
pelo JSON de parâmetros. Valores desconhecidos são rejeitados antes da fila.
O mesmo contrato aceita `refreshResolved` (padrão `false`); quando habilitado,
endereços já resolvidos são consultados novamente para atualizar campos novos do
provedor, incluindo `StreetType`, mapeado de `descricao_tipo_de_logradouro` da
BrasilAPI. O tipo é persistido separadamente do nome do logradouro para evitar
duplicação e permitir formatação correta em integrações posteriores.
Também aceita `refreshMissingNumber` (padrão `false`), que consulta novamente
somente resultados resolvidos cujo número ainda está ausente. Assim é possível
tentar completar o número sem consumir chamadas para todos os endereços já
finalizados; se o provedor continuar sem número, o campo permanece opcional.
`refreshIncomplete` (padrão `false`) reprocessa somente resultados resolvidos
sem logradouro, permitindo a complementação dos dados legados sem consultar
novamente toda a base. A API deriva `addressCompleteness` como `COMPLETE`,
`WITHOUT_NUMBER` ou `POSTAL_ONLY`; CEP geral de município pode permanecer no
último nível sem que rua ou número sejam inferidos.
O índice único por `CustomerId` sustenta a consulta e impede duplicidade; não há
índice por status porque o processamento parte dos clientes do snapshot e faz a
comparação pelo identificador, sem filtrar globalmente por esse campo.
Não existe fonte paralela de endereço de entrega ou classificação A–G. O fluxo
de endereço possui dois jobs independentes e observáveis na Central de
Processamentos: `CUSTOMER_REGISTRATION_ADDRESS_ENRICHMENT` consulta
a BrasilAPI para clientes ainda sem resultado cadastral e, com
`refreshResolved=true`, tenta atualizar inclusive o número quando o provedor o
possuir; por fim, `CUSTOMER_ADDRESS_COORDINATE_ENRICHMENT` converte o endereço
cadastral resolvido em coordenadas. A grafia oficial vem da resposta da API, não
de transformação textual ou correção aproximada local. A ausência de número é
aceita e os níveis de precisão da geocodificação deixam explícito quando a
coordenada representa rua, CEP ou apenas município.
O job `CUSTOMER_ADDRESS_COORDINATE_ENRICHMENT` geocodifica somente endereços
cadastrais `RESOLVED` e persiste o resultado em
`customer_address_coordinates`, relacionado 1:1 a
`customer_registration_addresses`. Seu contrato versão 1 aceita
`customerStatus` (`ACTIVE`, `INACTIVE` ou `ALL`, padrão `ACTIVE`) e
`reprocessFailed` (padrão `false`), `refreshApproximate` (padrão `false`) e
`maximumRequests` opcional entre 1 e 10.000. O limite conta somente chamadas
externas, sem incluir resultados ignorados ou recuperados do cache. Quando
`refreshApproximate=true`, resultados resolvidos por rua, CEP ou município que
já possuem logradouro cadastral voltam ao provedor para tentar promoção para
endereço exato ou uma precisão melhor; o cache aproximado é ignorado nessa
tentativa e a coordenada anterior é preservada se o provedor não resolver a
nova consulta. A tabela mantém endereço normalizado, fonte,
status, latitude/longitude, identificador e nome do provedor, datas e falha
auditável. A unicidade por endereço cadastral garante idempotência; índices por
endereço normalizado e status sustentam cache e reprocessamento.
O identificador opaco retornado pelo provedor aceita até 512 caracteres, pois o
`place_id` do Geoapify pode exceder o limite legado de 64. Esse campo não recebe
índice: não participa de filtros, junções ou ordenações e indexá-lo aumentaria o
custo de escrita sem atender ao padrão real de acesso.

O contrato `ICustomerAddressCoordinateProvider` isola o provedor externo. A
seção `Geocoding:Provider` seleciona explicitamente `Google`, `Geoapify` ou
`Nominatim`. O Geoapify é a configuração padrão. O Google recorre ao Nominatim
no enriquecimento cadastral quando `GOOGLE_MAPS_API_KEY` não está preenchida;
esse fallback não se aplica à simulação por sondagem. O Geoapify usa a Geocoding API em
`Geoapify:BaseUrl`, restringe resultados ao Brasil e exige a chave em
`Geoapify:ApiKey`; a chave é fornecida por user-secrets no desenvolvimento ou
pela variável `GEOAPIFY_API_KEY` no Docker e não é versionada. Município, UF e,
na precisão exata, número devem conferir antes de a coordenada ser aceita.
O log informativo do cliente HTTP Geoapify fica desabilitado porque a API exige
a chave na query string; falhas continuam registradas sem expor a credencial.
Origens `GEOAPIFY_EXACT`, `GEOAPIFY_STREET` e `GEOAPIFY_MUNICIPALITY` mantêm a
mesma semântica auditável das origens anteriores.

Quando `Google` é selecionado e possui chave, a integração usa o Google
Geocoding v4, envia a credencial no cabeçalho `X-Goog-Api-Key` e limita a
resposta por máscara de campos. Somente `street_address` com granularidade
`ROOFTOP`, número, município e UF compatíveis é aceito como exato. Um gate
singleton limita o início das chamadas a uma a cada 500 ms por padrão e HTTP
429 é repetido somente para o endereço atual com espera exponencial.

Quando `Nominatim` é selecionado, um gate singleton mantém no mínimo 1.000 ms
entre o início das requisições da instância, sempre sequenciais e com
`User-Agent` identificável. Em ambos os provedores, a busca é restrita ao Brasil
e coordenadas resolvidas para o mesmo endereço normalizado são reutilizadas sem
chamada externa. HTTP 429 interrompe a tentativa para o retry do job. Falhas
transitórias de transporte recebem até três novas tentativas com espera
incremental configurável na seção do provedor; a validação do certificado
permanece obrigatória.
Quando o endereço não possui número e contém um CEP válido, o provedor consulta
primeiro o CEP v2 da BrasilAPI, reduzindo o uso do Nominatim e retornando uma
coordenada aproximada de nível `POSTAL_CODE`. Se o CEP não for resolvido, o
fluxo mantém os fallbacks de logradouro e município no provedor selecionado. Para endereços
com número, o texto enviado combina `StreetType + Street`, número, bairro,
município, UF, CEP no formato `00000-000` e Brasil. Se o logradouro já contém o
tipo, ele não é duplicado. A resolução ocorre em níveis auditáveis: endereço
com número confirmado (`GOOGLE_GEOCODING`, `GEOAPIFY_EXACT` ou
`NOMINATIM_EXACT`), logradouro sem número confirmado (`GEOAPIFY_STREET` ou `NOMINATIM_STREET`), CEP validado pela BrasilAPI
(`BRASIL_API_POSTAL_CODE`) e, por último, centro municipal
(`GEOAPIFY_MUNICIPALITY` ou `NOMINATIM_MUNICIPALITY`). Município e UF devem conferir em todos os níveis;
o fallback por CEP também exige cidade, UF e coordenadas no retorno da
BrasilAPI. Coordenadas aproximadas recebem uma justificativa persistida e não
são apresentadas como endereço exato. O resultado do job separa as contagens
por nível de precisão, além de cache, não encontrados, falhas e pendentes.
O resumo também separa candidatos percorridos, coordenadas previamente
resolvidas ignoradas e chamadas efetivamente enviadas ao provedor. Assim, uma
reexecução idempotente não apresenta registros apenas verificados como novas
geocodificações. Falhas definitivas são agrupadas em categorias estáveis para
dados insuficientes, CEP inválido ou incompatível, divergência de município/UF
e esgotamento dos fallbacks. Timeout, resposta HTTP inválida e rate limit
continuam como falhas técnicas recuperáveis do job.
Em reprocessamentos, coordenadas existentes com `NOT_FOUND` ou `FAILED` são
carregadas com rastreamento e atualizadas no mesmo registro 1:1; o progresso só
contabiliza resolução depois que essa alteração participa do lote persistido.
O contrato do mapa deriva `coordinateAccuracy` e `coordinatePrecision` da
precisão e da fonte persistidas. Google predial, Geoapify/Nominatim exatos e
HERE com número exato são `EXACT`; interpolação, rua, CEP e município permanecem
auditáveis como aproximações. A API publica ambas as categorias quando há
coordenada utilizável, e somente a ausência de coordenada de endereço e de
fallback municipal entra em `withoutCoordinates`. O filtro de precisão é
aplicado no frontend, mostra todas as categorias por padrão e permite selecionar
somente exatas ou somente aproximadas sem nova consulta à API.
Na tela de Clientes, administradores podem iniciar o enriquecimento diretamente;
a listagem apresenta somente município e UF do snapshot importado. O endereço
cadastral persistido fica restrito ao detalhe de consumo: nele, município e UF
importados continuam identificados como localização operacional, enquanto
logradouro, número, bairro, complemento, cidade, UF e CEP da BrasilAPI aparecem
em um bloco cadastral separado. A grade também não exibe vínculos de rota; o
detalhe lista todas as rotas atuais do cliente e marca explicitamente `Sem rota`
quando não existe vínculo. Logística, `admin` e `admin_system` podem adicionar o
cliente a uma rota do snapshot atual, criando um `RouteCustomerAssignment` de
origem `Manual`; Diretor e Vendas mantêm acesso somente de leitura. Os demais
perfis consultam os resultados, mas não recebem a ação administrativa.

O card executivo `Taxa de Ocupação` do dashboard logístico usa somente o
snapshot atual publicado de rotas, sem filtro de data ou comparação com período
anterior. A API soma `Route.TotalWeightKg` das rotas com capacidade configurada
e divide pela soma de `VehicleType.CapacityKg` dessas mesmas rotas; rotas sem
capacidade ficam fora do numerador e denominador, mas são retornadas como
contagem de alerta. O card usa as mesmas faixas visuais das rotas: abaixo de
60% é `Ocioso`, de 60% até menos de 85% é `Médio`, de 85% até 95% é
`Saudável`, e acima de 95% é `Crítico`. O resumo consolidado preserva valores
acima de 100% quando o peso informado supera a capacidade disponível.

Alguns clientes de `frontend/src/lib/importer-api.ts` representam contratos de
serviços do ecossistema que não estão implementados neste backend. Ao alterar um
contrato realmente atendido por este repositório, frontend e backend devem ser
atualizados juntos.

O dashboard logístico combina indicadores integrados e demonstrativos. Taxa de
devolução, ocupação e ruptura consultam as importações reais disponíveis. Tempo
de carregamento e trânsito, custos, acuracidade, ocorrências e Fill Rate são
calculados no frontend sobre a base demonstrativa tipada de
`frontend/src/lib/logistics-dashboard.ts`. Essa base representa viagens,
estoques de pães e salgados congelados e contratos de locação de fornos e
freezers vinculados às rotas de atendimento. Ela é explícita e isolada da
persistência PostgreSQL: não cria registros falsos nas tabelas operacionais e
deve ser substituída pelos contratos da API quando esses eventos forem
persistidos.

## Backend

### Canal WhatsApp

O assistente também recebe mensagens privadas por uma conta corporativa conectada
como dispositivo do WhatsApp Web. O processo `whatsapp-bridge/`, em Node.js,
usa Baileys para gerar o QR Code, preservar as credenciais multi-dispositivo e
receber/enviar mensagens. `IWhatsAppGateway`, definido em Application e
implementado em Infrastructure, acessa somente a interface HTTP local desse
bridge. Não há chave da Meta nem chave de provedor; o segredo configurado protege
apenas o webhook interno bridge → API. O QR Code é consultado sob demanda por
`admin_system` e nunca é persistido.

Cada telefone pessoal passa por confirmação com código temporário e corresponde
a um único `AppUser`. Somente vínculos `active` são admitidos. O webhook público
valida `X-Webhook-Secret`, ignora remetentes desconhecidos, mensagens próprias,
grupos e formatos fora de texto/áudio, e deduplica pelo identificador do provedor.
Os índices únicos de usuário, telefone e mensagem sustentam resolução de identidade
e idempotência; o índice de vínculo/data atende auditoria cronológica sem criar
índices adicionais de baixa seletividade.

Mensagens válidas geram `job_executions` do tipo
`WHATSAPP_MESSAGE_PROCESSING` e são publicadas na fila `default` do Hangfire. O
Worker baixa e transcreve áudio quando necessário, resolve perfil e limites do
usuário, chama o mesmo `BusinessAssistantService`, persiste a resposta e então a
envia pelo gateway. O núcleo do assistente fica em Infrastructure para ser
composto tanto pela API quanto pelo Worker, sem chamadas internas entre hosts.

Antes da publicação, o webhook aplica anti-flood por vínculo confirmado. A
política padrão aceita até oito mensagens em uma janela móvel de 30 segundos e
persiste `FloodBlockedUntil` no vínculo ao iniciar um cooldown de 30 segundos.
A primeira mensagem excedente cria um `WHATSAPP_MESSAGE_PROCESSING` para enviar
um único aviso; as demais são auditadas como `rate_limited`, sem job e sem chamada
à IA. Os limites vêm de `WhatsApp:{FloodWindowSeconds,FloodMaximumMessages,
FloodCooldownSeconds}`. A consulta reutiliza o índice composto existente por
`WhatsAppUserLinkId + CreatedAt`; não foi criado outro índice porque o acesso tem
o mesmo prefixo e intervalo temporal.

No Worker, textos do mesmo vínculo recebidos no intervalo configurável de um
segundo (`MessageAggregationMilliseconds`) são concatenados na ordem de chegada,
respeitando o limite máximo da pergunta. Os recibos incorporados ficam como
`grouped`, e somente o primeiro chama a IA. Um gate por vínculo serializa as
execuções concorrentes dentro do Worker, evitando respostas sobrepostas na mesma
conversa; todo processamento efetivo e todo aviso continuam registrados em
`job_executions` e visíveis na Central de Processamentos.

Antes do envio, `WhatsAppAnswerFormatter` converte o contrato textual de rotas
usado pelo assistente em blocos numerados próprios para leitura no WhatsApp,
com nome destacado e atributos em linhas separadas. A resposta canônica continua
persistida sem alteração para preservar o histórico e a renderização estruturada
do frontend; tentativas de reenvio aplicam o mesmo formatador.

Sessões usam o canal explícito `web` ou `whatsapp`. Cada vínculo confirmado mantém
uma sessão contínua própria do WhatsApp, enquanto os endpoints de histórico web
filtram apenas `web`. Ambos os canais compartilham identidade, permissões,
ferramentas, consumo e `KnowledgeMemory`, mas nunca o histórico de mensagens.

No frontend, `/meu-whatsapp` oferece cadastro, confirmação e revogação para todos
os perfis funcionais. `/administracao/whatsapp` oferece estado, QR, reconexão e
desconexão somente para `admin_system`, atualizando o estado em intervalo definido
por constante.
A indisponibilidade do bridge local é exposta como
estado operacional `unavailable`, e não como erro genérico da tela. O polling
automático ocorre somente durante `connecting`; nos demais estados a atualização
é manual. Se o envio do código falhar, a alteração pendente do vínculo é desfeita,
evitando solicitar ao usuário um código que nunca foi entregue.

Enquanto o bridge local não estiver disponível, `/simulador-whatsapp` reproduz
a interface de uma conversa privada e chama `POST
/api/assistant/whatsapp-simulator`. O endpoint é autenticado e reutiliza o mesmo
assistente, usuário, perfil, limites e memórias, persistindo uma sessão do canal
`whatsapp`; ele não passa pelo webhook, não exige telefone confirmado e não envia
mensagens ao provedor externo.

As respostas do assistente no simulador reutilizam `AssistantResponseText`, o
mesmo renderizador estruturado do chat web, para transformar contratos textuais
de rotas, clientes, listas e tabelas em blocos visuais dentro dos balões. Mensagens
do usuário permanecem em texto simples, como na conversa real. A apresentação
do canal usa emojis semânticos com parcimônia para identificar rotas críticas,
clientes, tabelas, seções e o período dos dados sem alterar o conteúdo consultado.
Nos cartões de rotas e clientes, o título ocupa uma linha própria e os atributos
ficam em uma linha flexível abaixo; essa separação impede que badges comprimam
nomes longos em telas estreitas ou nos balões do simulador.

O canal `whatsapp` acrescenta ao prompt principal uma política conversacional
específica: interações sociais devem ser breves, acolhedoras e naturais em PT-BR,
com no máximo um emoji contextual. Consultas operacionais continuam submetidas
às mesmas regras de ferramentas, confiabilidade e apresentação do chat web.

### Cache de aplicação

O contrato genérico `IApplicationCache` fica em `Application/Caching` e expõe o
padrão cache-aside por `GetOrCreateAsync`: o consumidor fornece uma chave, uma
política de expiração e a função de fallback que consulta o PostgreSQL. Casos de
uso e controllers não conhecem o provedor nem tratam indisponibilidade de cache.

`Infrastructure/Caching` implementa `ResilientApplicationCache`. Um hit é
desserializado e retornado; em miss, erro de leitura ou conteúdo inválido, o
fallback é executado. Falhas ao preencher ou remover o cache são registradas e
não derrubam a operação principal. Cancelamentos solicitados pelo consumidor
continuam sendo propagados. Misses concorrentes da mesma chave dentro do mesmo
processo compartilham uma única execução do fallback; cada chamador ainda pode
cancelar sua própria espera.

O provedor inicial é `MemoryCacheStore`, registrado por `AddImportInfrastructure`.
`ICacheStore` isola armazenamento de bytes e permite adicionar Redis depois sem
alterar casos de uso. Como o cache em memória é local a cada processo, somente a
API compartilha entradas entre suas próprias requisições; ele não substitui
persistência nem coordenação entre API e Worker.

Chaves devem ser criadas por `CacheKey.Create(area, contractVersion, parts)`.
Elas recebem prefixo da aplicação, normalização determinística, versão explícita
do contrato e hash SHA-256 quando excedem o limite legível. Dados versionados
devem incluir o identificador da importação publicada na chave, permitindo que
uma nova publicação deixe a versão anterior naturalmente inacessível. Nenhuma
chave deve conter senha, token, documento pessoal ou outro conteúdo sensível.

Cada aplicação futura do cache deve declarar uma `CachePolicy` nomeada, testar
hit, miss, fallback, expiração e invalidação e manter o acesso ao banco dentro
da função fornecida. Resultados nulos não são armazenados por padrão. Nesta
primeira etapa nenhum endpoint foi cacheado; a estrutura foi criada isoladamente
para que a adoção aconteça por consulta, com medição e política próprias.

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
- consulta e manutenção do depósito logístico e diagnóstico da API OSRM;
- consulta, retry e cancelamento de jobs administrativos;
- catálogo e execução manual de jobs operacionais declarados como executáveis;
- consulta paginada dos clientes da importação atualmente publicada.
- consulta paginada e detalhe de documentos fiscais, além do resumo histórico de
  consumo em vendas por cliente e da taxa fiscal de devolução por peso.
- consulta de clientes reais para mapa logístico, priorizando a coordenada do
  endereço e usando a coordenada municipal como fallback aproximado.
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
leitura para o chat, além de `GET /api/assistant/sessions` e
`GET /api/assistant/sessions/{sessionId}` para consultar o histórico do próprio
usuário autenticado. O request aceita `sessionId` opcional e `message`; `question`
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

O prompt permite combinar várias ferramentas na mesma resposta quando consultas
complementares aumentam a cobertura, a comparação ou a confiabilidade. Essa
liberdade continua limitada ao escopo da pergunta e ao teto configurado pelo
orquestrador: cada consulta deve ter finalidade clara, consultas equivalentes não
devem ser repetidas e o ciclo deve parar assim que houver evidência suficiente.

Antes do ciclo de resposta, `AssistantScopeClassifier` faz uma classificação
estruturada em `InScope`, `OutOfScope` ou `Ambiguous`, considerando a pergunta e
o histórico limitado da sessão. Declarações pessoais comuns são admitidas
localmente; resultados ambíguos, inválidos, timeout ou falha também seguem para
o modelo de resposta, que pode conversar brevemente ou pedir contexto. Somente
`OutOfScope` bloqueia a pergunta antes do ciclo, impedindo a execução de
ferramentas internas ou pesquisa externa para temas claramente alheios.

Pesquisa externa é opcional por `Assistant:ExternalResearchEnabled`, limitada
por `Assistant:MaximumExternalResearchesPerMessage` e pelo timeout
`Assistant:ExternalResearchTimeoutSeconds`. O modelo principal pode solicitá-la
somente para complementar uma necessidade da Grespan que os dados internos não
resolvam. `ExternalResearchQuerySanitizer` remove documentos, códigos, valores e
textos presentes nos payloads internos; consulta insegura ou vazia é bloqueada.
A busca usa `web_search` em uma chamada isolada da Responses API que recebe
somente a consulta pública sanitizada, sem histórico ou resultados internos. O
resultado volta ao ciclo principal como saída de ferramenta para síntese sem
web habilitada. As citações HTTP/HTTPS são deduplicadas, retornadas em `sources`
e exibidas como links no frontend; não são persistidas no histórico.
Falhas HTTP do provedor registram somente status, código, parâmetro e mensagem
de validação retornados pela OpenAI; chave, payload, prompts e dados consultados
não são incluídos nesse diagnóstico.

`OpenAiChatModelClient` é a única classe que conhece a API da OpenAI. A chave é
carregada exclusivamente de `OPENAI_API_KEY`; ela não é versionada, logada ou
enviada ao frontend. O modelo padrão vem de `Assistant:Model`. A OpenAI recebe
apenas definições de ferramentas com JSON Schema e payloads pequenos retornados
por essas ferramentas. Ela nunca recebe connection string, SQL, schema completo,
nomes internos de tabelas/colunas ou entidades do Entity Framework.

### Memória semântica do assistente

O chat mantém conhecimento aprendido automaticamente nas conversas em
`knowledge_memories`. Cada memória possui escopo `company`, compartilhado por
todos os usuários autenticados, ou `user`, recuperado apenas quando
`OwnerUserId` corresponde ao usuário da conversa. Administradores acessam os
dois escopos em `/administracao/memorias` e podem pesquisar, corrigir,
desativar e reativar registros; versões substituídas permanecem inativas e
ligadas por `SupersedesMemoryId` para auditoria.

Antes da resposta, `KnowledgeMemoryService` gera um embedding da pergunta com
`text-embedding-3-small`, limita os candidatos no PostgreSQL pelo escopo e
calcula similaridade cosseno, injetando somente as memórias mais relevantes
como dados não executáveis. Depois da resposta, uma chamada estruturada extrai
até cinco fatos duráveis explicitamente informados pelo usuário, classifica o
escopo e atualiza assuntos equivalentes preservando a versão anterior. Senhas,
tokens, chaves de API e chaves privadas detectadas não são persistidos. Falhas
de recuperação ou aprendizado não impedem a resposta principal do assistente.

A recuperação é híbrida para não perder fatos curtos de perfil em perguntas
também curtas. Além do corte semântico geral, memórias do próprio usuário podem
ser recuperadas pela correspondência entre assuntos estáveis (como nome, cargo,
localização e preferência) e os termos da pergunta, ou por um corte semântico
específico quando a pergunta contém referência pessoal explícita. O corte geral
continua valendo para memórias corporativas, e o filtro por `OwnerUserId` é
aplicado antes do ranqueamento.

Em toda resposta, a seleção injeta no contexto até 30 memórias que atendam aos
critérios de relevância, ordenadas por similaridade. O limite é apenas um teto:
memórias irrelevantes não são incluídas para completar a quantidade. Nome
preferido (`preferred name`), nome informado (`name`) e cargo ou função (`role`)
seguem a recuperação híbrida aplicada às demais memórias pessoais e só entram
quando forem pertinentes à pergunta. A extração usa esses três assuntos
canônicos para substituir informações anteriores corretamente. A consulta ao
banco, o limite de 500 candidatos e os índices permanecem inalterados.

Os embeddings ficam serializados em `jsonb`. A busca limita-se aos 500 registros
ativos mais recentes autorizados antes do cálculo em memória; essa decisão evita
uma dependência de extensão do PostgreSQL na primeira versão. Ao ultrapassar
esse volume operacional, a evolução prevista é migrar a coluna para `pgvector`
e criar índice vetorial sustentado por medições reais de consulta.

### Gestão de consumo da IA

Cada pergunta admitida cria uma execução em `ai_response_executions`, vinculada
ao `AppUser` e, depois da criação do histórico, à sessão do chat. Todas as
chamadas técnicas necessárias para entregar a resposta — classificação de
escopo, ciclos de ferramentas, resposta e pesquisa externa — são registradas em
`ai_provider_calls`. O cliente da OpenAI captura `usage.input_tokens` e
`usage.output_tokens`; tentativas sem `usage` permanecem auditáveis com consumo
zero. A soma das chamadas forma o custo e o consumo da resposta visível.

`GET /api/assistant/sessions/{sessionId}/usage` agrega entrada, saída e custos
USD já persistidos em todas as execuções da sessão, depois de validar que a
conversa pertence ao usuário autenticado. O simulador mostra o total de tokens
em uma pílula no cabeçalho e detalha entrada, saída e custo estimado em popover.
O frontend não recalcula preços; uma nova conversa zera apenas o contador visual
ao trocar de sessão, enquanto a auditoria histórica permanece intacta.

Os preços em USD por milhão de tokens ficam versionados em
`ai_model_prices`. Cada chamada preserva o preço vigente e os custos de entrada
e saída calculados, portanto mudanças futuras não alteram relatórios antigos.
O modelo global, limite mensal padrão e percentual padrão de alerta ficam em
`ai_consumption_settings`; `Assistant:Model` serve apenas como valor inicial.
Exceções opcionais por usuário ficam em `ai_user_limits`.

O limite considera o mês-calendário no fuso `America/Sao_Paulo` e é verificado
antes da pergunta. A resposta que cruza o teto é concluída, pois o uso só é
conhecido depois da resposta do provedor; perguntas seguintes recebem `429`.
`admin_system` é contabilizado, mas não bloqueado. Alertas preventivos e de
limite atingido são persistidos em `ai_consumption_alerts`, deduplicados por
usuário, mês e nível, e podem ser marcados como lidos.

`/api/admin/ai-consumption` expõe relatório, configurações, preços, exceções e
alertas somente para `admin` e `admin_system`. O frontend apresenta esses dados
em `/administracao/consumo-ia`. As consultas usam índices por usuário/período,
execução/período, modelo/período e estado/data do alerta; esses índices atendem
os filtros reais sem adicionar índices a campos de baixa seletividade isolados.
Alertas de consumo não usam `job_executions`, pois não representam processamento
assíncrono ou administrativo.

As ferramentas do chat implementam `IChatTool` e são registradas como coleção no
container. Nesta versão existem ferramentas de rotas (`search_routes`,
`get_route_details`, `get_critical_routes`, `list_routes_by_occupancy`,
`get_route_cities`, `get_route_customers`) e
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

Os DTOs de rota preservam dia da semana, veículo, capacidades, carga total e as
ocupações geral, por peso, volume e paletes. O detalhamento de cidades mantém a
sequência operacional, entregas, média diária e observações importadas. Campos de
origem técnica da planilha continuam excluídos. As projeções reutilizam o snapshot
atual e os relacionamentos já indexados de rota, veículo e entradas, sem criar
consulta ou índice persistido adicional.

As ferramentas corporativas chamam `IBusinessChatQueryService`, definido em
`Application/RouteImports` e implementado em `Infrastructure`. Esse serviço
reutiliza as mesmas fórmulas, fontes publicadas e limites das telas de clientes,
notas fiscais, produtos e estoque: cadastro atual de clientes, histórico fiscal
persistido, snapshot atual de `INVENTORY_CURRENT` e última data publicada de
`DAILY_INVENTORY`. Os payloads para a OpenAI são DTOs de negócio delimitados.
Eles preservam os campos operacionais necessários para responder sem nova perda
de contexto: produto inclui descrição, códigos externo/ERP/operacional, GTIN e
pesos; estoque inclui quantidades e valores físico e comprometido; itens fiscais
incluem grupo, quantidade, peso, valores, despesas, tributos, CFOP, TES, pedido e
armazém. Documento cadastral de cliente, números de linha/planilha,
identificadores de importação, schema, SQL, conexão e demais metadados técnicos
ou sensíveis ficam deliberadamente fora desses DTOs.
O cabeçalho fiscal preserva tipo e movimento do documento, código e descrição da
operação e referência ao documento original. Produção preserva identificação
completa do produto, produção por turno, saída, ajustes e fechamento, tanto nos
registros quanto no resumo da última data; o resumo mensal inclui produção, saída
e ajustes.
Consultas de busca exigem termo mínimo e limite máximo; consultas fiscais,
estoque, produção e ruptura retornam listas pequenas; a taxa de devolução limita
o período a 365 dias. Produção no chat usa somente o controle diário publicado e
pode ser consultada como resumo agregado ou como registros limitados por produto
e período, sem cálculo pesado síncrono.

Resultados anteriores da conversa não são considerados prova de inexistência de
um campo. Quando a pergunta atual exigir dados ausentes no histórico, o modelo
deve executar a ferramenta corporativa mais adequada antes de declarar
insuficiência; ele não deve pedir autorização para uma consulta que já pode
executar. `list_recent_fiscal_documents` inclui `PricingItems` com identificação
do produto, grupo, quantidade, peso, valores, despesas, tributos e referências
operacionais. Para cada item,
`CalculatedAmount` preserva `SourceTotalValue` quando disponível e, na ausência,
usa `Quantity * UnitValue`; se ambos os valores de preço estiverem ausentes, o
resultado permanece nulo. Isso permite somas e médias pequenas sobre as notas
retornadas sem criar uma consulta paralela ou inferir peso como preço. A projeção
usa o relacionamento já consultado entre nota e itens, portanto não requer índice
adicional e mantém o limite existente de resultados fiscais.

As buscas de produto em `search_products` e `list_inventory_positions` aceitam
nome, descrição, código externo, código ERP, código operacional e GTIN. Essas
consultas continuam sobre o snapshot atual, com limites pequenos e sem nova
ordenação ou agregação persistida; por isso não foi criado índice especializado
nesta alteração. Se o volume ou a telemetria indicar degradação, a busca textual
deverá migrar para uma estratégia indexada específica em vez de multiplicar
índices B-tree de baixa utilidade para `Contains`.

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
para cada entrada municipal, seleciona no máximo a quantidade de `Deliveries`
do snapshot atual, em ordem determinística pelo código externo, sem repetir um
cliente em outra rota do mesmo dia. A resposta identifica essa origem com
descrição explícita de que é uma inferência limitada pela quantidade de
entregas da entrada. Quando a associação manual existir no domínio, ela deve
reutilizar a mesma tabela e o mesmo contrato externo, alterando apenas a origem
do vínculo para `Manual` ou `Imported`.

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
Listas com três ou mais registros comparáveis de notas fiscais, produtos,
estoque, produção ou consumo usam o contrato delimitado `[TABELA]`, uma linha
`[COLUNAS]` com 2 a 8 células separadas por ` | `, até 50 linhas `[LINHA]` com a
mesma quantidade e ordem de células e o fechamento `[/TABELA]`. O frontend só
renderiza a tabela quando o contrato estiver completo e consistente; contratos
inválidos permanecem legíveis como texto, evitando perda de conteúdo. A tabela
usa cabeçalhos semânticos e rolagem horizontal em telas estreitas.
Recomendações e ações operacionais usam bullets simples e são renderizadas como
lista textual normal. Parágrafos explicativos continuam como texto. O modelo não
deve misturar rotas, clientes e ações na mesma lista nem usar marcadores
técnicos ou markdown de destaque para representar dados estruturados.
### Application e Domain

`Application/RouteImports` contém os contratos do pipeline de importação, filas
assíncronas, dispatcher de jobs em background, interfaces de
storage/processadores, ciclo de vida, catálogo de jobs operacionais, cálculo de
ocupação, política de capacidade dos veículos logísticos e resumo de execuções.

Não existe mais módulo de detecção, findings, central de notificações ou
alertas. Sinais operacionais que antes seriam exibidos como alertas devem ser
consultados pelo chat ou pelas telas de domínio existentes, sem persistência ou
fila paralela de alertas.

`Domain/Entities` contém usuários, fontes de dados, importações,
erros, execuções, tipos de veículo, rotas, entradas de rota, clientes, vínculos
cliente-rota, perfis de entrega, snapshots de clientes, municípios compartilhados, coordenadas
municipais, produtos, snapshots de estoque e registros diários de estoque. Os
estados da importação e a origem do vínculo cliente-rota ficam em `Domain/Enums`.

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
Vínculos `Manual` são preservados pelas sincronizações posteriores. Quando uma
planilha importada ou a inferência municipal produzir o mesmo par rota-cliente,
o vínculo manual existente prevalece e o índice único `RouteId + CustomerId`
continua impedindo duplicidade; nenhum índice adicional é necessário para a
inclusão individual por identificadores.

A fonte snapshot `CUSTOMER_ROUTE_ASSIGNMENTS` importa planilhas com `Dia`, `Mercado`,
`Rota` e `Cidade`. As linhas resolvidas ficam em `customer_route_mappings`, preservando
import, aba e linha de origem. Cliente exige correspondência única por nome e município;
rota exige nome e dia únicos. Ausências e ambiguidades usam o fluxo de correção da
Central de Importações. Enquanto não existe snapshot publicado, permanece a inferência
por município. Depois da publicação, o sincronizador substitui todas as inferências por
associações `Imported`; clientes não presentes ficam sem rota. Os índices por import e
cliente atendem a reconstrução, e o índice por import, dia e nome normalizado atende a
resolução contra novos snapshots de rotas.

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
Parsing, acesso a arquivos e persistência ficam nesta camada porque dependem de
formatos ou tecnologias externas. As decisões de domínio extraídas desses dados
devem continuar testáveis sem depender do host HTTP.

### Worker

`InovaSkill.Importer.Worker/Program.cs` configura o mesmo acesso a banco e
storage da API, registra o storage Hangfire em PostgreSQL e sobe servidores
separados para as filas `imports` e `default`. A quantidade de workers de cada
fila vem de `Hangfire:Workers:{Imports,Default}`.

O Worker é o local para parsing, consolidações e cálculos pesados. A API apenas
registra/consulta o trabalho e enfileira o job no Hangfire após persistir o
estado de negócio. Jobs de importação rodam na fila `imports`; jobs operacionais
genéricos rodam na fila `default`. Retries técnicos são explícitos:
5 segundos, 30 segundos e 2 minutos, preservando o total de quatro execuções
incluindo a tentativa inicial.
Processadores podem limpar o `ChangeTracker` entre lotes para limitar memória.
Por isso, o serviço de processamento sempre recarrega `JobExecution` após o
processador retornar e antes de persistir o estado terminal, evitando divergência entre um import
`Completed` e um job ainda `Processing`.

Enquanto houver uma importação `Queued` ou `Processing`, a tela de Importações
consulta a listagem a cada dez segundos. A API expõe `StartedAt` no contrato da
listagem e calcula a duração no servidor no instante de cada consulta. O
frontend não mantém relógio nem estima progresso localmente: exibe somente o
último estado recebido, que pode ficar defasado em até um ciclo de polling. Os
contadores parciais persistidos alimentam a barra percentual; sem total conhecido,
a barra permanece indeterminada.
Falhas técnicas são persistidas em `job_executions.ErrorMessage` e expostas
tanto na Central de Processamentos quanto no detalhe da importação; os registros
de `import_errors` permanecem reservados a problemas de validação por linha.

A importação fiscal não usa o modelo em memória do ClosedXML no caminho de
produção. `FiscalMovementsSpreadsheetParser` percorre `sheet1.xml` com
`OpenXmlReader`, mantendo somente a linha corrente e a pequena tabela de strings
compartilhadas. `FiscalMovementsProcessor` grava lotes de 500 linhas em
`fiscal_import_staging` por cópia binária do PostgreSQL. Cada lote é confirmado
separadamente e atualiza os contadores do import; uma retentativa consulta a
maior linha já armazenada e retoma a carga sem regravar os lotes concluídos.
Após a carga, comandos set-based consolidam produtos, documentos e itens com
`INSERT ... ON CONFLICT` dentro de uma única transação curta. A publicação do
estado `Completed` e a remoção do staging ocorrem na mesma transação, impedindo
que uma falha de merge publique resultado parcial. Se a carga ou o merge falhar,
o staging permanece ligado ao `ImportId` para retomada e também é removido por
cascade quando o import for excluído. A chave primária `ImportId + RowNumber`
cobre retomada e deduplicação; o merge filtra sempre por `ImportId`, portanto não
há necessidade de índice adicional.
Importações fiscais são serializadas por um advisory lock de sessão derivado do
`DataSourceId`. Versões diferentes da mesma fonte aguardam umas às outras durante
carga e merge, enquanto fontes distintas continuam independentes. O caminho de
produção não usa entidades EF rastreadas para atualizar fatos fiscais em massa,
eliminando conflitos de concorrência otimista linha a linha.

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
registrados em `import_errors`. Em cada aba mensal, o parser lê somente o bloco
operacional anterior ao segundo cabeçalho de produtos e aceita apenas datas do
mês indicado no nome da aba; blocos-resumo de entradas/saídas e dias sobrepostos
do mês seguinte não criam registros duplicados. Quando existe uma aba mensal
canônica (`MM.aaaa`), cópias sufixadas do mesmo mês são ignoradas. As colunas de
código operacional, produto e saldo inicial, além da primeira linha de dados,
são detectadas pelos cabeçalhos para suportar as disposições usadas antes e a
partir de 2026. Quando a aba fornece `CÓD.TOTVS`, o vínculo e a deduplicação
priorizam esse código ERP; abas antigas continuam usando o código operacional.
Duplicidade idêntica por produto/data é ignorada
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

O único endereço do cliente fica em `customer_registration_addresses`; a antiga
fonte de classificação/endereço de entrega foi removida. Os registros genéricos
de imports já executados permanecem para auditoria, mas a tabela derivada
`customer_delivery_profiles` não existe e a fonte não aceita novos uploads.

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
O contrato permanece disponível na API, mas não é consumido pelo detalhe do
cliente no frontend. A tela apresenta somente indicadores calculados sobre o
histórico realizado e permite abrir sua evolução mensal, sem valores futuros,
faixas estimadas ou classificação de qualidade da projeção. Esta etapa não
calcula risco de ruptura, ocupação futura de rota nem recomendação automática. O índice composto
`CustomerId + IssueDate + MovementCategory` atende a consulta da janela.

Os índices compostos em cliente, data e categoria atendem às agregações do
resumo; índices por data/categoria, número de documento, documento do item e
produto atendem listagem, detalhe e relacionamentos sem N+1. Não há notificação
ou cálculo financeiro neste fluxo.

A Central de Processamentos consulta `/api/admin/jobs` e
`/api/admin/jobs/summary` a cada cinco segundos, além da atualização manual. O
polling impede que a tela preserve indefinidamente um estado antigo depois que
o Worker conclui o job. A interface separa esse fluxo em duas abas: a aba
`Monitoramento` concentra indicadores e o histórico das execuções, enquanto
`Serviços disponíveis` apresenta o catálogo de serviços operacionais que podem
ser iniciados manualmente. Essa grade filtra `ManualRunAllowed` e não renderiza
jobs internos, de webhook ou exclusivos de outros fluxos como cartões
desabilitados. Ambas continuam usando os mesmos contratos e o ciclo de vida
centralizado em `job_executions`.
`GET /api/admin/jobs/definitions` expõe apenas jobs operacionais declarados no
catálogo da Application, e `POST /api/admin/jobs/definitions/{jobType}/run`
permite executar manualmente somente os que possuem `ManualRunAllowed`. Jobs de
importação de planilha dependem de upload, arquivo e import específico; por isso
não aparecem entre os serviços executáveis e continuam sendo criados por
upload, reprocessamento ou retry técnico.

O catálogo de jobs é indexado por `JobType`, com busca sem diferença entre
maiúsculas e minúsculas. Cada definição declara fila, versão do contrato,
exemplo de parâmetros e permissões de execução manual e agendamento. Todas as
execuções persistem um envelope imutável em `job_executions`, com
`ParametersJson`, `ResultJson`, versão, fila, gatilho, progresso, origem do
retry, usuário solicitante e eventual agendamento. Os JSONs são limitados a 1
MB e não recebem índice de conteúdo porque os acessos operacionais usam tipo,
status, datas e relacionamentos. Importações guardam somente `importId` no
payload: a fonte persistida da importação seleciona o `IDataSourceProcessor`, e
o arquivo nunca é incorporado ao JSON.

No enriquecimento de endereços cadastrais, `ResultJson` também funciona como
checkpoint do mesmo `job_execution`: total, quantidade processada, resultados e
parâmetros são atualizados a cada lote persistido. Ao retomar depois de uma
reinicialização ou retentativa do Worker, o processador valida o total e os
parâmetros do snapshot antes de continuar pelo próximo cliente. Um snapshot
inválido ou incompatível é ignorado, evitando consumir progresso de outra
seleção de clientes. O acesso continua pela chave da execução, portanto o
campo `jsonb` não requer índice de conteúdo.

`job_schedules` mantém agendamentos administrativos com cron, fuso horário,
payload versionado, estado e auditoria. O fuso padrão é
`America/Sao_Paulo`; cada disparo cria uma nova `job_execution`, e ocorrências
perdidas durante indisponibilidade não são recuperadas. Pausa, reativação,
edição e exclusão atualizam o registro recorrente do Hangfire. Importações e
webhooks continuam não agendáveis nem executáveis manualmente. O cancelamento é
cooperativo: a API registra a solicitação e o Worker encerra em ponto seguro com
estado `Cancelled`.

Antes do enfileiramento manual, a API resolve a publicação exigida pelo job:
o enriquecimento municipal usa a importação atual de clientes e a detecção de
clientes inativos usa a importação fiscal atual. Se a fonte necessária ainda
não tiver uma publicação, a API retorna conflito sem criar uma execução órfã.

### Mapa de clientes geocodificados

Clientes reais no mapa priorizam a coordenada de endereço resolvida e recorrem
à precisão municipal quando ela não existe. A importação de clientes
continua gravando `CustomerSnapshot.MunicipalityId`; a coordenada fica separada
em `municipality_coordinates`, como enriquecimento externo do cadastro de
municípios. A tabela guarda `MunicipalityId`, latitude, longitude, fonte,
status, tentativa, resolução e eventual motivo de falha. As coordenadas de
endereço ficam separadas dos snapshots e das coordenadas municipais.

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
e prioriza `customer_address_coordinates`, com fallback para
`municipality_coordinates`. Clientes sem nenhuma fonte não aparecem no mapa e
são contabilizados em `withoutCoordinates`. Para evitar pins municipais
sobrepostos, a API aplica deslocamento visual determinístico somente ao fallback;
coordenadas de endereço, rua e CEP permanecem no ponto retornado pelo provedor.
Cada item informa `locationPrecision` como `ADDRESS_EXACT`,
`ADDRESS_INTERPOLATED`, `ADDRESS_APPROXIMATE` ou `MUNICIPALITY`,
`coordinateAccuracy` como `EXACT` ou `APPROXIMATE` e `coordinatePrecision` como
`EXACT`, `INTERPOLATED`, `STREET`, `POSTAL_CODE` ou `MUNICIPALITY`. A tela usa
`E` para endereço e número confirmados, `I` para número interpolado, `~` para rua
ou CEP e `C` quando existe somente a coordenada municipal. O filtro de precisão
é local à tela, começa em todas as localizações e pode restringir os marcadores a
exatos ou aproximados; as opções de cidade continuam derivadas do conjunto
completo recebido da API. A tela `/mapa` consome esse endpoint e mantém os
trajetos demonstrativos como contexto visual enquanto os clientes vêm da API
real.
O marcador fixo da matriz representa a Grespan Pães Congelados Ltda., CNPJ
`10.809.214/0001-67`, na Avenida República, 7000, Distrito Industrial Santo
Barion, Marília/SP, CEP 17512-035. A coordenada `-22.21389, -49.94583` vem da
consulta do CEP na BrasilAPI v2; tooltip e popup exibem o endereço completo.
Quando `locationPrecision` identifica uma coordenada de endereço, o item também
retorna o endereço cadastral formatado e o frontend o apresenta no tooltip e no
popup do marcador. Marcadores municipais informam explicitamente que o endereço
não foi geocodificado e que a posição é aproximada pela cidade; somente esses
marcadores recebem o espalhamento visual para evitar sobreposição.

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

### Praças de pedágio no mapa regional

O backend mantém o catálogo oficial da aplicação com as 21 praças da EIXO SP e das
praças complementares identificadas nas geometrias das rotas atuais (ViaRondon,
Rodovias do Tietê, Triunfo Transbrasiliana, CART, Entrevias e Arteris
ViaPaulista). Cada registro mantém concessionária, rodovia, km, município,
coordenada de referência e tarifa manual para passeio e veículos comerciais de
2 a 9 eixos. A tarifa automática aplica no backend o desconto básico versionado
de 5%. `GET /api/logistics/toll-plazas` é a fonte do mapa e informa versão e
vigência somente quando conhecida; datas desconhecidas permanecem nulas. Os
marcadores ficam visíveis independentemente do filtro de clientes; o popup
identifica a concessionária e a praça e mostra os valores manual/automático.

Na consolidação, a geometria rodoviária é comparada com todo o catálogo para
contar passagens, inclusive repetições depois de uma saída da área da praça. O
veículo comercial usa a tarifa automática correspondente aos eixos configurados
no tipo de veículo; tipos sem configuração completa ficam indisponíveis. O cálculo
mantém o número de passagens e o valor de pedágio separados. Os três KPIs do
detalhe são combustível (faixa de consumo multiplicada pelo preço cadastrado
do diesel), pedágio e gasto total, que soma as duas parcelas. As tarifas são
dados de referência versionados no backend e devem ser revisadas quando as
concessionárias publicarem novo reajuste.

No detalhe das rotas atuais, o KPI de pedágio permanece compacto e acionável:
ao clicar nele, a interface abre um modal com as praças identificadas, operador,
localização, número de passagens e tarifas manual/automática estimadas. A lista
de praças não é concatenada no card, preservando a legibilidade do grid de
indicadores.

O cliente de geometria usa o OpenRouteService como provedor primário e o OSRM
como fallback quando o primeiro retorna erro, inclusive limite de requisições
(HTTP 429). Consultas em lote da tela de custos são serializadas para evitar
rajadas contra o provedor externo. Rotas sem paradas distintas da Matriz não
geram consulta externa e ficam marcadas como percurso indisponível.
Os clientes HTTP do OSRM enviam `Osrm:UserAgent`; a matriz divide clientes em
blocos configuráveis para respeitar o limite de coordenadas por chamada. Após a
resposta, a matriz também rejeita distância ou duração zero entre clientes com
coordenadas diferentes; somente a diagonal e pontos realmente coincidentes
podem ser zero.

Uma sugestão de otimização é válida somente enquanto as coordenadas usadas no
cálculo permanecerem inalteradas. A API compara `UpdatedAt` das coordenadas dos
clientes do mesmo snapshot/dia com `DailyRouteOptimizationResult.CreatedAt`;
quando há alteração posterior, a listagem marca `isStale=true` e o detalhe
recusa a leitura da rota antiga. O frontend não carrega mapa, ordem ou custos de
uma sugestão obsoleta e orienta novo processamento. Isso evita exibir trechos
zero artificiais de uma matriz calculada antes do preenchimento de endereços.

O solver limita cada rota a 15 entregas e impõe uma jornada máxima rígida de 10
horas por veículo. O limite inclui o tempo rodoviário da matriz e 15 minutos
configuráveis de atendimento por cliente
(`RouteOptimization:ServiceTimePerStopMinutes`), somados uma vez por parada, além
do retorno ao depósito. Rotas de até 8 horas são preferidas pelo objetivo por
meio de uma penalidade suave, sem criar veículos ociosos apenas para cumprir a
preferência. As dimensões de entregas e `WorkDuration` do OR-Tools impedem que
uma rota proposta ultrapasse qualquer dos limites; quando a frota mínima por
carga não comporta a jornada, veículos adicionais são testados em lotes
determinísticos de dez até encontrar uma distribuição viável ou retornar
`Infeasible`; há no máximo três tentativas de
reparo por dia para evitar bloquear a Central de Processamentos em buscas
repetidas. O tempo exibido no total da rota inclui atendimento, enquanto cada
trecho continua exibindo somente o deslocamento rodoviário. A configuração aceita
de 1 a 10 horas e mantém 8 horas como preferência operacional.

### Relatório de custos da logística

A rota `/logistica/relatorios-custos` pertence ao grupo de navegação de Logística
e consulta a consolidação oficial de combustível e pedágio do snapshot selecionado.
O frontend permite alternar a periodicidade diária/semanal e o agrupamento por rota
ou tipo de veículo e os cenários real/otimizado. Todos os totais vêm de
`GET /api/route-costs`; o relatório exibe data do cálculo, diesel, versão
tarifária, base do percurso e indisponibilidades. O custo atual usa clientes
exatos e o cenário otimizado novo também percorre clientes exatos, identificado
por `OPTIMIZED_CUSTOMERS`. Resultados históricos municipais continuam legíveis
como `OPTIMIZED_MUNICIPALITIES`. Quando uma consolidação combina mais de uma
base no mesmo cenário, a API retorna `MIXED` em `pathBasis`, evitando assumir
uma única origem e mantendo o relatório disponível.

`vehicle_types` armazena eixos e as eficiências mínima/máxima em km/L. Eixos
aceitos ficam entre 2 e 9, consumos são positivos e o mínimo não supera o
máximo. O backfill configura Accelo e Toco com dois eixos e Truck com três,
preservando as faixas anteriores; nomes desconhecidos ficam pendentes.

O assistente registra três ferramentas somente leitura sobre o snapshot atual:
`get_route_operational_analysis`, `get_daily_route_optimization` e
`list_route_costs`. Elas combinam criticidade e custo real com o último cenário
válido do solver. Web e WhatsApp compartilham o mesmo prompt: consultar dados
persistidos, explicar a comparação diária e as bases diferentes, nunca prometer
substituição 1:1 nem iniciar recálculo, e responder `Dados insuficientes` quando
o custo ou a otimização estiver ausente ou desatualizado.

### Estratégia de índices

Os índices acompanham os padrões reais de leitura:

- rotas usam `ImportId + Weekday + Name` para listagem e para o filtro de dia
  dentro do snapshot, `ImportId + OverallOccupancy` para criticidade e índices
  GIN trigram sobre o nome da rota e da cidade para buscas textuais com
  `contains`; o novo uso do filtro de dia é atendido pelo índice composto já
  existente e não exige outro índice;
- clientes mantêm a unicidade por fonte, filial e código, além de
  `DataSourceId + ExternalCode + BranchCode` para a ordenação da listagem;
- snapshots usam `ImportId + CustomerId` para idempotência, `ImportId +
  MunicipalityId` e `ImportId + CustomerType` para filtros;
- coordenadas municipais usam unicidade por `MunicipalityId` e índice por
  `Status`; a consulta do mapa chega nelas por relacionamento 1:1 a partir dos
  municípios presentes no snapshot atual, e o índice de status apoia auditoria
  e reprocessamento de pendências;
- custos têm unicidade do snapshot por import/fingerprint, busca por
  `SnapshotId + Scenario + Weekday`, unicidade da rota real e do veículo
  otimizado no respectivo cenário, além de acesso pelo resultado de otimização;
  passagens são únicas por item e praça. Rankings operam sobre o conjunto já
  restrito ao snapshot atual, portanto não recebem índices paralelos por total;
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

Ao iniciar a API ou o Worker localmente, o processo procura um arquivo `.env`
na pasta atual e em seus diretórios pai. Variáveis já definidas no ambiente têm
precedência; assim, `OPENAI_API_KEY` configurada no `.env` é carregada sem ser
registrada em logs ou enviada ao frontend. Em Docker, as variáveis continuam
sendo fornecidas pelo Compose.

Em outro terminal, execute o frontend:

```bash
cd frontend
npm install
npm run dev
```

Para conectar um número real do WhatsApp durante o desenvolvimento local, execute
também o bridge:

```bash
cd whatsapp-bridge
npm install
npm start
```

O bridge escuta apenas `127.0.0.1:8081`, envia eventos para a API local e salva
as credenciais vinculadas em `whatsapp-bridge/.data/auth`, fora do Git.

Na execução completa por Docker Compose, o serviço `whatsapp-bridge` inicia
automaticamente, escuta `0.0.0.0:8081` dentro do contêiner e se comunica com a
API por `http://api:8080`. API e Worker acessam o bridge pelo DNS interno
`http://whatsapp-bridge:8081`. A autenticação multi-dispositivo é preservada no
volume nomeado `whatsapp_auth_data`, inclusive após recriação do contêiner.

Endereços padrão:

- frontend: `http://localhost:5173`;
- API: `http://localhost:5279`;
- Hangfire Dashboard: `http://localhost:5279/hangfire`;
- PostgreSQL: `localhost:5432`.
- Bridge WhatsApp: `http://127.0.0.1:8081`.

Para encerrar a infraestrutura depois de parar os processos locais:

```bash
docker compose stop postgres
```

Configurações essenciais:

- `ConnectionStrings__ImportDb`: conexão PostgreSQL usada pela API e pelo Worker.
- `Hangfire__Storage__ConnectionString`: conexão opcional específica do
  Hangfire; quando ausente, usa `ConnectionStrings__ImportDb`.
- `Hangfire__Workers__Imports` e `Hangfire__Workers__Default`:
  concorrência por fila do Worker.
- `Hangfire__Dashboard__AllowAnonymous`: libera acesso ao dashboard fora de
  desenvolvimento somente quando configurado explicitamente.
- `Storage__ImportsPath`: caminho compartilhado dos arquivos importados.
- `VITE_API_URL`: base da API incorporada ao build do frontend.
- `WhatsApp__BaseUrl` e `WhatsApp__InstanceName`: endereço do bridge e identidade
  operacional usados pela API e pelo Worker. No Compose, o endereço usa o DNS
  interno `http://whatsapp-bridge:8081`; em execução local, usa
  `http://localhost:8081`.
- `WhatsApp__WebhookSecret`: segredo obrigatório do webhook recebido pela API.
- `WhatsApp__MessageAggregationMilliseconds`: janela de agrupamento de textos do
  mesmo vínculo; o padrão é 1000 ms.
- `WhatsApp__FloodWindowSeconds`, `WhatsApp__FloodMaximumMessages` e
  `WhatsApp__FloodCooldownSeconds`: janela móvel, quantidade admitida e duração
  do bloqueio anti-flood; os padrões são 30 segundos, oito mensagens e 30
  segundos, respectivamente.

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
