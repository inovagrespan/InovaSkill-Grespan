# Importação de Rotas por Cidades

## Fluxo

`POST /api/route-imports` valida apenas o arquivo, salva o XLSX no storage compartilhado,
cria `imports` e `job_executions` em `QUEUED`, enfileira `ProcessImportJob` no
Hangfire com `ImportId` e `JobExecutionId` e retorna HTTP 202. O job pequeno
segue pela fila `imports`. O Worker Hangfire é o consumidor, resolve
`IDataSourceProcessor` pelo código estável `ROUTES_BY_CITY` e persiste o
resultado no PostgreSQL.

O banco é a fonte histórica. Hangfire não recebe arquivos nem linhas da planilha.
API e Worker devem apontar `Storage__ImportsPath` para a mesma pasta. Sem
configuração, ambos usam `backend/route-imports` em execução local.

Cada upload recebe uma `version` crescente e única dentro da fonte. A linha da
fonte é protegida durante a atribuição da versão, evitando versões duplicadas em
uploads concorrentes. Reprocessar mantém o mesmo import e a mesma versão.
Antes dessa criação, a API identifica automaticamente a fonte pelos cabeçalhos
e estrutura do XLSX; não existe seleção manual no frontend.

`data_sources` também guarda o modo de importação e os ponteiros opcionais
`current_import_id` e `last_successful_import_id`. Para rotas, o modo é
`SNAPSHOT`: somente uma importação `COMPLETED` pode ser publicada, e apenas
quando sua versão é maior que a versão atualmente publicada.

## Responsabilidades e estados

- API: upload, consultas, correções, reprocessamento e publicação.
- Hangfire/PostgreSQL: execução técnica persistente, fila `imports` e retries
  em 5 segundos, 30 segundos e 2 minutos.
- Worker: leitura, validação, normalização e persistência.
- PostgreSQL: imports, erros, execuções, rotas e schema técnico `hangfire`.

Processamento e publicação são etapas diferentes. Enquanto uma nova versão está
em fila, processando, com erros ou falhou, as consultas continuam usando o
snapshot anterior apontado por `current_import_id`.

Import e job têm significados diferentes. Um valor de negócio inválido termina
com import `NEEDS_REVIEW` e job `COMPLETED`. Arquivo estruturalmente inválido
termina com import `FAILED` e job `COMPLETED`. Falha técnica após quatro
execuções termina ambos como `FAILED`.

## Tabelas do pipeline

- `data_sources`: código, chave do processador, modo, contador de versão e
  ponteiros de publicação.
- `imports`: arquivo bruto preservado, versão, estado e contadores.
- `import_errors`: coordenada da célula, valor original e eventual correção.
- `job_executions`: histórico imutável de cada execução.
- `vehicle_types`: capacidade, 2 a 9 eixos e faixa positiva de consumo em km/L;
  Truck (10.300 kg/3 eixos), Toco (7.700 kg/2 eixos) e Acelo (3.300 kg/2 eixos)
  recebem backfill preservando suas faixas atuais.
- `routes`: rota, dia, veículo e import de origem.
- `route_entries`: sequência original, nome, entregas, Média/Dia e observação.
- `route_import_corrections`: ação tipada e valores anterior/novo da correção.
- `route_import_affected_weekdays`: dias recalculados na versão derivada.
- `municipality_aliases`: nome normalizado da fonte vinculado ao município oficial.
- `daily_route_optimization_issues`: causas bloqueantes por resultado, rota e parada.
- `route_cost_snapshots`: versão consolidada por snapshot de rotas e fingerprint.
- `route_cost_items`: custo por rota real ou veículo do cenário otimizado.
- `route_cost_toll_passages`: praças, passagens e tarifa aplicada por item.

## Interpretação e correções

O nome da aba é normalizado sem caixa e acentos e pode conter o dia, como
`SEGUNDA NOVA`. Uma linha com nome na coluna B e `CIDADES DA ROTA` na coluna C
abre um bloco. Linhas seguintes viram entradas; a linha Truck/Toco/Acelo encerra
o bloco e nunca vira entrada. `Acello` é normalizado para `Acelo`; decimais usam
pt-BR. Entradas repetidas são preservadas.

Um campo inválido cria erro genérico por import, aba, linha e campo. Corrigir
preenche `corrected_value`; o XLSX original permanece intocado. Quando todos os
erros estão resolvidos, o reprocessamento cria outro `job_execution` e publica a
mesma mensagem. O parser aplica a correção pela coordenada antes de converter.

### Correção rápida de pendências da otimização

`GET /api/route-optimizations/{resultId}/remediation` entrega todas as causas e
valores atuais agrupados por rota/parada. `municipality-candidates` pesquisa o
catálogo municipal oficial embutido com paginação; a busca reativa no frontend
usa o debounce compartilhado de 300 ms. Diretor, Vendas, Logística, `admin` e
`admin_system` podem usar
`POST /api/route-optimizations/{resultId}/remediations` no snapshot atual.

O POST exige `expectedSnapshotId` e ações discriminadas. Peso recebe três casas
e arredondamento `AwayFromZero`; negativo sempre exige peso positivo. Peso zero
pode receber `EXCLUDE_STOP`, que resolve também município ausente porque a parada
não participa da simulação. Vínculos viram aliases futuros; remapeamento existente
exige confirmação explícita. Coordenada pode vir da base oficial ou de
latitude/longitude manual auditada. Capacidade atualiza o catálogo futuro e é
congelada em cada rota da nova versão.

A operação cria uma versão oficial filha sem sobrescrever o XLSX ou o pai.
`PROCESS_IMPORT` clona o snapshot e aplica correções em transação. Resultados de
dias intactos são copiados integralmente com referência de herança e os dias
afetados são enviados em `weekdays[]` ao `DAILY_ROUTE_OPTIMIZATION`. Status e
retry são expostos por `GET/POST /api/route-optimizations/remediations/{id}` como
uma visão dos mesmos `job_executions`, sem fila ou monitoramento paralelo. Falha
antes da publicação conserva o pai; falha do solver depois da publicação permite
repetir somente os dias afetados.

## Snapshots, idempotência e concorrência

O processador adquire `pg_advisory_xact_lock` derivado do `ImportId`, impedindo
duas execuções simultâneas em processos diferentes. Na mesma transação, remove
somente `routes` daquele import (entries saem por cascade) e recria o conjunto.
O import, o arquivo, correções resolvidas e dados de outros imports são preservados.

Depois de concluir o snapshot, o Worker tenta publicá-lo em uma segunda operação
transacional, protegida por lock da `DataSource`. Se uma versão mais nova já
estiver atual, a versão antiga continua `COMPLETED` no histórico e não altera o
ponteiro.
Quando o snapshot publicado pertence à fonte `CUSTOMERS`, o serviço enfileira o
job operacional `MUNICIPALITY_COORDINATE_ENRICHMENT` para o `ImportId`
publicado. Esse job usa `job_executions`, aparece na Central de Processamentos,
processa apenas municípios de clientes sem coordenada resolvida e não interfere
no estado da importação de clientes.

O enriquecimento opcional de endereço cadastral é iniciado pela Central de
Processamentos com o job `CUSTOMER_REGISTRATION_ADDRESS_ENRICHMENT` e o
snapshot de clientes publicado, ou com um `ImportId` explícito para auditoria.
Somente registros classificados como CNPJ são candidatos. O endereço retornado
pela BrasilAPI é persistido uma única
vez por cliente em `customer_registration_addresses`; novos snapshots preservam
e reutilizam esse dado derivado. CNPJ inválido e não encontrado são resultados
funcionais auditáveis, enquanto indisponibilidade HTTP interrompe a execução
para aproveitar a política de retry do job.

O job posterior `CUSTOMER_ADDRESS_COORDINATE_ENRICHMENT` transforma endereços
cadastrais resolvidos em coordenadas pelo provedor configurado e persiste os
resultados em `customer_address_coordinates`. `Geoapify` é o padrão e
`Nominatim` permanece selecionável; ambos implementam
`ICustomerAddressCoordinateProvider`. A execução com Nominatim público é
sequencial e limitada globalmente a uma chamada por segundo. O cache por
endereço normalizado vale para qualquer provedor. O mapa usa essa coordenada
quando resolvida e recorre a `municipality_coordinates` nos demais casos.
O endereço cadastral persiste separadamente o tipo de logradouro retornado em
`descricao_tipo_de_logradouro`. A consulta combina tipo, logradouro, número,
bairro, cidade, UF e CEP formatado. Resultados que confirmam número, município e
UF são classificados como exatos; fallbacks compatíveis por logradouro, CEP ou
município são aceitos como coordenadas aproximadas e mantêm o nível de precisão
auditável.

Publicar um snapshot de rotas, concluir `DAILY_ROUTE_OPTIMIZATION` ou enriquecer
coordenadas solicita `ROUTE_COST_CONSOLIDATION`; alterações de diesel e tipo de
veículo fazem o mesmo. O job reutiliza `job_executions` e a Central de
Processamentos. Solicitação concorrente marca uma reexecução posterior, e o
fingerprint evita substituir o snapshot quando os insumos são idênticos. Uma
falha técnica preserva integralmente o último resultado válido.

`GET /api/routes` consulta somente o import atual. Para auditoria,
`GET /api/route-imports/{importId}/routes` consulta um snapshot específico.
No frontend, o usuário escolhe snapshots históricos somente pela data. Se
existirem vários uploads no mesmo dia, o mais recente representa aquele dia. O
`ImportId` correspondente é resolvido internamente e a versão permanece um
detalhe de ordenação e proteção contra concorrência.

## Ocupação

Para a planilha atual, cada linha informa em `Média/Dia` a carga destinada à
cidade. Esses valores são somados como peso total da rota. A ocupação por peso é
`peso total / capacidade em kg`. O valor não é limitado a 100%, para preservar
sobrecargas.

O parser lê o valor numérico subjacente da célula. Formatações visuais do Excel
que escondam casas decimais não podem arredondar o dado importado. Cargas usam
três casas decimais, e a soma persistida das entradas deve coincidir com o peso
total da rota.
Cada carga é normalizada por `RouteLoadPolicy` antes da soma, com
`MidpointRounding.AwayFromZero`. A ocupação usa o mesmo total persistido, evitando
diferenças entre detalhe, total e percentual.

As capacidades conhecidas ficam em `LogisticsVehicleCapacityPolicy`: Truck com
10.300 kg, Toco com 7.700 kg e Acelo com 3.300 kg. A política também completa
cadastros antigos desses tipos que ainda estejam sem capacidade. Veículos não
reconhecidos continuam sem capacidade até configuração explícita.
A migration incremental aplica a mesma política e recalcula os snapshots
existentes, evitando exigir um novo upload apenas para disponibilizar a métrica.

O modelo admite peso, volume e paletes. Cada dimensão sem total ou capacidade
válida é ignorada, nunca convertida em 0%. A ocupação geral é o maior valor
entre as dimensões disponíveis. Se nenhuma capacidade estiver configurada, o
status fica `MissingCapacity` e a ocupação geral permanece nula.

O frontend representa a taxa com barra e círculo percentual. Abaixo de 60% a
rota é `Ocioso`; entre 60% e menos de 85%, `Médio`; entre 85% e 95%,
`Saudável`; e acima de 95%, `Crítico`. O cálculo e o texto preservam valores
acima de 100%, inclusive quando a carga informada supera a capacidade. Somente
o preenchimento visual da barra e do círculo é limitado a 100%.

## Sugestões diárias

A tela de Rotas compartilha os filtros de data e dia da semana entre `Rotas
reais` e `Sugestões`. `GET /api/routes?date=&weekday=` filtra as rotas reais
antes da paginação; `GET /api/route-optimizations?date=&weekday=` resolve o
snapshot publicado aplicável à data e retorna apenas os resultados persistidos
do `Weekday` selecionado. A resposta de otimizações informa se ele é o snapshot
atual e inclui a última execução para polling. Snapshots históricos são
consultáveis, mas apenas o atual pode ser recalculado. O índice existente
`routes (ImportId, Weekday, Name)` sustenta a nova combinação de filtros, sem
necessidade de migration adicional.

O job `DAILY_ROUTE_OPTIMIZATION` aceita opcionalmente `weekdays[]`; payload sem
esse campo recalcula todos os dias. Ele agrega `Média/Dia` por município, parcela
somente cidades acima da maior capacidade cadastrada, consulta a matriz OSRM e
executa CP-SAT + Routing Solver no Worker. A sugestão nunca altera `routes` ou
`route_entries`. Somente resultados `Optimized` possuem veículos e paradas;
demais estados preservam a distribuição real e explicam o motivo.
O total proposto conta somente veículos com paradas. Veículos existentes que o
solver libera permanecem na distribuição como `IsIdle`, para auditoria e
visualização, mas não entram em `ProposedVehicleCount`. Da mesma forma, a
quantidade e a capacidade adicionais contam apenas veículos adicionais em uso.
A tela resume essa movimentação com veículos e capacidade liberados,
introduzidos e seus saldos líquidos. Os dados são reconstruídos dos veículos
persistidos na sugestão; não existe pareamento artificial de troca entre dois
veículos, pois a redistribuição ocorre sobre o conjunto inteiro do dia.
Os comparativos superiores destacam visualmente atual, proposto e impacto:
distância e duração incluem variação percentual segura para base zero, veículos
mostram o saldo absoluto e capacidade usa o termo `introduzida` para não sugerir
um crescimento líquido quando houve substituição.

## Consolidação de custos

O cenário atual percorre depósito e clientes que possuam coordenadas cadastrais
exatas; o otimizado percorre depósito e a sequência municipal persistida pelo
solver. Município dividido pode aparecer em mais de um veículo, mas nenhuma
rota sugerida é tratada como substituta 1:1 de uma rota real. O fluxo legado por
`ResultJson` não participa da leitura ou do cálculo.

O Worker persiste distância, duração, combustível mínimo/máximo, pedágios,
passagens e custo total. Litros usam três casas; dinheiro usa duas e o total é a
soma exata das parcelas arredondadas. Falta de diesel, coordenada exata, eixos,
consumo ou otimização gera indisponibilidade com motivo, nunca zero inventado.
O catálogo versionado de pedágios pertence ao backend e é exposto por
`GET /api/logistics/toll-plazas`; vigência desconhecida é nula.

`GET /api/route-costs?date=&weekday=` alimenta o relatório diário/semanal e
separa `actual` de `optimized`; `GET /api/routes/{id}/cost` alimenta o detalhe.
O chat web e o WhatsApp consultam o mesmo snapshot atual por ferramentas somente
leitura e recomendam exclusivamente o último cenário persistido do solver.

## Métricas dos jobs

Os cards consultam `job_executions`: fila é `QUEUED`; processamento soma
`PROCESSING` e `RETRYING`; concluídos e falhos usam as últimas 24 horas. Taxa de
sucesso é `COMPLETED / (COMPLETED + FAILED)`, com zero quando não há base. Tempo
médio usa `finished_at - started_at` apenas para jobs concluídos.

## Nova fonte no futuro

Crie o registro em `data_sources`, implemente um `IDataSourceProcessor` com o
novo `SourceCode`, seu parser/validador e as tabelas específicas da fonte.
Também deve adicionar ao detector uma assinatura estrutural inequívoca, coberta
por teste, para preservar o upload sem escolha manual.
Registre o processador na DI. Reutilize imports, erros, jobs, storage, Hangfire,
Worker e as telas base. Não grave nomes de classes no banco.
