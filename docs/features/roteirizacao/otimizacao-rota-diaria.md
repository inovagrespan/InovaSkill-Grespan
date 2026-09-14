# Plano vivo da otimização diária de rotas

## Objetivo

Este documento registra a evolução da otimização diária de rotas da Grespan. Ele
é a referência para distinguir o que já existe no sistema, o que está em
desenvolvimento, o que foi removido e o que ainda precisa ser construído.

A otimização implementada redistribui blocos de cidades entre os veículos
disponíveis no mesmo dia, respeitando capacidade e preservando integralmente a
rota atual quando não houver uma solução válida e comprovadamente melhor.

> O PDF `plano_roteirizacao_grespan.pdf` é uma referência de planejamento. As
> ferramentas e fases descritas nele não devem ser consideradas implementadas
> sem confirmação neste documento e no código atual.

**Última atualização:** 31 de agosto de 2026.

## Regras obrigatórias

1. A unidade de processamento é um único dia da semana (`Route.Weekday`) dentro
   do snapshot de rotas selecionado.
2. Cidades, cargas, rotas e veículos de dias diferentes nunca podem ser
   combinados na mesma otimização.
3. Cada cidade e sua carga total em `Média/Dia` formam um bloco. Se a carga
   exceder a maior capacidade cadastrada, o bloco é repartido em parcelas que
   permitem atender a mesma cidade com mais de um veículo no mesmo dia. Blocos
   que cabem em um veículo permanecem indivisíveis.
4. Um bloco pode ser transferido apenas para outra rota ou veículo disponível
   no mesmo dia.
5. Todo veículo considerado deve possuir capacidade válida e a carga proposta
   não pode ultrapassá-la.
6. O conjunto de cidades e a soma das cargas do dia devem ser idênticos antes e
   depois da simulação. Nenhuma cidade ou carga pode desaparecer, ser duplicada
   ou migrar para outro dia.
7. A proposta só pode ser classificada como otimizada quando for viável e
   apresentar melhoria conforme métricas e critérios previamente definidos.
8. Se não houver solução viável, não houver melhoria comprovada ou faltarem
   dados confiáveis, a distribuição atual deve ser preservada integralmente.
   Não será aceita alteração parcial.
9. Cidade cuja carga exceda a capacidade máxima será parcelada, preservando
   exatamente seu peso total e seu dia de origem.
10. A primeira versão será somente uma simulação auditável. Ela não atualizará
    automaticamente `routes`, `route_entries`, vínculos de clientes ou o
    snapshot publicado.
11. Cada execução deve identificar, no mínimo, o snapshot de entrada, a
    data de referência e o dia da semana processado.
12. A frota inicial do problema é formada pelas instâncias de veículos já
    associadas às rotas do mesmo dia. Se ela não comportar todos os blocos sem
    sobrecarga, o solver pode acrescentar instâncias virtuais dos tipos de
    veículo cadastrados que possuam capacidade válida.
13. Ao acrescentar um veículo, deve ser escolhido o menor tipo cuja capacidade
    comporte integralmente o bloco ou o excedente que motivou a ampliação. Por
    exemplo, diante de 12.000 kg e um Truck de 10.300 kg, o complemento deve ser
    um Acelo de 3.300 kg, e não um Toco de 7.700 kg, desde que os blocos
    indivisíveis permitam essa distribuição.
14. A ampliação da frota é parte da simulação e não cria cadastro de veículo nem
    altera as rotas publicadas. O resultado deve distinguir veículos existentes
    de instâncias adicionais propostas.

## Situação atual

| Item | Estado | Registro atual |
| --- | --- | --- |
| Importação versionada de rotas | Concluído | Rotas são snapshots publicados, com histórico e consulta por data de referência. |
| Separação das rotas por dia | Concluído | `Route.Weekday` identifica o dia; a identidade e as consultas de rota preservam esse campo. |
| Cidades e cargas diárias | Concluído | `route_entries` preserva cada cidade e sua `Média/Dia`; a soma normalizada forma `Route.TotalWeightKg`. |
| Tipos e capacidades de veículos | Concluído | Truck: 10.300 kg; Toco: 7.700 kg; Acelo: 3.300 kg. Tipo desconhecido permanece sem capacidade até configuração explícita. |
| Ocupação e sobrecarga | Concluído | A ocupação é calculada com a carga persistida e preserva valores acima de 100%. |
| Vínculos entre clientes e rotas | Concluído | O vínculo importado considera cliente, nome normalizado da rota e dia da semana; pendências continuam sujeitas a revisão. |
| Coordenadas de municípios | Concluído | Oferecem localização aproximada por cidade e continuam disponíveis como fallback. |
| Endereço cadastral por CNPJ | Concluído | O job `CUSTOMER_REGISTRATION_ADDRESS_ENRICHMENT` consulta a BrasilAPI e persiste o endereço cadastral. |
| Coordenada por endereço cadastral | Concluído | O job `CUSTOMER_ADDRESS_COORDINATE_ENRICHMENT`, a seleção explícita de provedor e sua persistência estão consolidados. |
| Apoio à escolha de veículo | Concluído | O detalhe da rota compara a carga com o catálogo de veículos e apresenta alternativas; não altera a rota nem executa otimização global. |
| Otimização legada | Removido | Solver heurístico, matriz geográfica/OSRM, cenários, endpoints, ferramenta de chat, tabelas e job foram removidos intencionalmente. |
| Entrada imutável da otimização diária | Concluído | O contrato reúne snapshot, dia, blocos municipais agregados, frota existente, tipos adicionais e matriz OSRM. |
| Fundação da matriz rodoviária | Concluído | O cliente OSRM calcula duração e distância entre depósito e municípios de um único dia, com divisão em blocos e falha integral; a matriz em memória é consumida pelo solver diário. |
| Dataset OSRM do Brasil | Pendente | Scripts versionados preparam e executam o grafo MLD, mas a instância e o mapa ainda precisam ser provisionados na infraestrutura. |
| Integração OR-Tools VRP | Concluído | CP-SAT dimensiona a frota adicional e o Routing Solver minimiza a distância com capacidades rígidas. |
| Persistência e apresentação das simulações | Concluído | O job substitui o resultado por snapshot/dia, preserva a última sugestão em falha, e a tela alterna `Rotas reais`/`Sugestões` com cards, detalhe, histórico e polling. |
| Trânsito em tempo real e app do motorista | Pendente | Permanecem como fases futuras e não fazem parte da primeira versão. |

## Base de dados já disponível

### Rotas, dias, cidades e capacidade

A fonte de rotas é processada de forma assíncrona e versionada. Cada rota possui
um dia da semana, um tipo de veículo e entradas de cidade. A coluna `Média/Dia`
é normalizada com três casas decimais e somada para formar o peso total da rota.
O cálculo de ocupação usa esse mesmo total persistido.

As capacidades conhecidas são regras explícitas de domínio:

- Truck: 10.300 kg;
- Toco: 7.700 kg;
- Acelo: 3.300 kg.

Veículos desconhecidos não recebem capacidade presumida. A execução de
otimização rejeita, de forma auditável, veículos cuja
capacidade necessária não esteja configurada.

Os vínculos entre clientes e rotas também carregam o dia da semana. Isso evita
associar automaticamente uma relação de segunda-feira a uma rota de outro dia e
deve ser preservado ao montar a entrada do solver.

### Job de endereço cadastral por CNPJ

O job `CUSTOMER_REGISTRATION_ADDRESS_ENRICHMENT` já integra a preparação dos
dados logísticos ao padrão operacional do sistema:

- seleciona clientes CNPJ de um snapshot publicado;
- aceita `customerStatus` com `ACTIVE`, `INACTIVE` ou `ALL`, usando `ACTIVE`
  como padrão;
- consulta a BrasilAPI e persiste um endereço por cliente em
  `customer_registration_addresses`;
- aceita `refreshResolved` para atualizar endereços previamente resolvidos;
- distingue resultados resolvidos, documentos inválidos, não encontrados,
  pendentes e falhas técnicas;
- pode ser iniciado manualmente ou por agendamento;
- registra fila, progresso, resultado, retry e auditoria em `job_executions` e
  aparece na Central de Processamentos.

O endereço retornado pela BrasilAPI é o endereço cadastral do CNPJ. Ele não é,
isoladamente, uma confirmação de que aquele é o endereço efetivo de entrega. A
roteirização deve preservar essa origem e permitir futura validação operacional
do ponto de entrega.

### Job de coordenadas por endereço

O job `CUSTOMER_ADDRESS_COORDINATE_ENRICHMENT` está implementado e consolidado:

- processa somente endereços cadastrais com estado `RESOLVED`;
- aceita `customerStatus` com `ACTIVE`, `INACTIVE` ou `ALL`;
- aceita `reprocessFailed`, desativado por padrão;
- monta o endereço com tipo e nome do logradouro, número, bairro, município, UF,
  CEP e país;
- consulta o Nominatim sequencialmente, com identificação do cliente HTTP e
  intervalo global mínimo de uma requisição por segundo na instância;
- quando o endereço não possui número, consulta primeiro o CEP v2 da BrasilAPI;
  se o CEP não resolver, mantém os fallbacks por logradouro e município no
  Nominatim;
- classifica como exata somente a coordenada com município, UF e número
  compatíveis; logradouro, CEP e município compatíveis são aceitos como níveis
  aproximados auditáveis;
- reutiliza coordenadas resolvidas para o mesmo endereço normalizado;
- persiste status, latitude, longitude, identificador do provedor, descrição,
  tentativas e falha auditável em `customer_address_coordinates`;
- registra progresso e resultado no mesmo `job_executions` usado pela Central
  de Processamentos.

O nível de precisão informa se a coordenada representa número confirmado,
logradouro, CEP ou centro do município e continua dependente da qualidade do
endereço cadastral. Quando não houver coordenada de endereço válida, a coordenada
municipal pode apoiar visualização e diagnóstico; o solver diário usa
explicitamente apenas coordenadas municipais. A evolução porta a porta deverá
definir outro contrato antes de misturar níveis de precisão.

## O que foi removido

O sistema já possuiu um subsistema de sugestão global de rotas. Ele incluía
solver heurístico, provedores de matriz geográfica e OSRM, execução assíncrona,
cenários persistidos, endpoints, integração com o assistente e telas de
resultado. A migração `RemoveRouteOptimization` removeu os agendamentos e as
tabelas `route_optimization_runs` e `route_optimization_scenarios`; o código e
os contratos correspondentes também foram retirados.

Esse legado serve apenas como histórico de decisões. Ele não está funcional e
não deve ser reativado por cópia direta. A implementação atual partiu das regras
diárias deste documento e reutiliza os padrões arquiteturais de importação,
Worker, Hangfire, `job_executions` e Central de Processamentos.

## Estado e uso da matriz OSRM

A matriz de duração e distância não é persistida atualmente. O fluxo disponível
é `depósito + municípios do dia → OSRM Table → matriz em memória`. O resultado
é entregue ao solver diário e descartado ao fim do processamento. Os resultados
persistem snapshot, dia, execução, distribuição e métricas atuais/propostas; não
existe tabela ou cache paralelo de matrizes.

O OSRM e o solver têm responsabilidades separadas:

1. o OSRM calcula tempo e distância rodoviários entre depósito e cidades;
2. o OR-Tools recebe essa matriz, cargas e capacidades e decide a atribuição e a
   sequência dos blocos;
3. o sistema compara a proposta com a situação atual e mantém as rotas atuais
   quando não houver solução válida e melhor.

## OR-Tools VRP implementado

A integração usa o pacote oficial Google OR-Tools para .NET dentro do
`InovaSkill.Importer.Worker`. O cálculo é assíncrono e reutiliza Hangfire,
`job_executions`, retries e a Central de Processamentos. A API apenas solicita
e consulta a execução; não executa o solver durante uma requisição HTTP.

### Entrada do solver

- snapshot de rotas e data de referência;
- um único `Weekday`;
- depósito como início e fim de todos os veículos;
- matriz direcional de duração e distância produzida pelo OSRM;
- blocos municipais indivisíveis com `MunicipalityId` e carga `Média/Dia`;
- veículos disponíveis no dia e suas capacidades válidas;
- tipos de veículo cadastrados com capacidade válida, usados para propor
  instâncias adicionais quando a frota inicial do dia for insuficiente;
- distribuição atual, para cálculo e comparação das métricas;
- versão das regras, objetivo e limites de tempo do solver.

### Restrições obrigatórias

- cada bloco aparece exatamente uma vez na solução;
- uma cidade só é dividida quando sua carga original excede a maior capacidade;
  nesse caso, cada parcela determinística aparece exatamente uma vez;
- cidades e veículos não podem atravessar dias;
- nenhum veículo pode superar sua capacidade;
- quando a frota inicial for insuficiente, veículos adicionais pertencem apenas
  à simulação e devem usar o menor tipo capaz de acomodar integralmente o bloco
  ou excedente correspondente;
- todos os veículos saem e retornam ao mesmo depósito;
- a soma das cargas e o conjunto de cidades devem permanecer idênticos;
- timeout, inviabilidade ou dados insuficientes nunca geram alteração parcial.

### Saída persistida

Para cada veículo, o resultado informa sequência de cidades, carga, capacidade,
ocupação, distância, duração estimada e se a instância já existia no dia ou foi
adicionada pela simulação. O resumo compara a distribuição atual e a proposta
por distância total, duração total, veículos utilizados, capacidade adicional,
sobrecargas e maior ocupação. O contrato usa `Optimized`, `NoImprovement`,
`Infeasible` ou `InsufficientData`; somente `Optimized` pode carregar uma
distribuição alternativa, ainda sem aplicação automática na primeira versão.

### Exemplo operacional ilustrativo

Em uma segunda-feira, o OSRM pode mostrar que Bauru e Jaú formam um eixo
rodoviário próximo, enquanto Assis e Ourinhos formam outro. O OR-Tools avaliará
essas distâncias junto às cargas das quatro cidades e às capacidades de Truck,
Toco e Acelo. Uma combinação que coloque carga acima da capacidade do Acelo será
rejeitada, mesmo que reduza quilômetros. Uma proposta válida poderá agrupar os
eixos em veículos compatíveis e será apresentada somente se melhorar as métricas
definidas. Cidades, cargas, distâncias e resultados desse exemplo são
ilustrativos e não representam dados operacionais confirmados da Grespan.

## Estados do resultado

| Resultado | Significado | Distribuição alternativa |
| --- | --- | --- |
| `Optimized` | Existe uma solução válida e comprovadamente melhor para o dia. | Permitida, somente como simulação na primeira versão. |
| `NoImprovement` | Existem soluções viáveis, mas nenhuma melhora a distribuição atual pelos critérios definidos. | Proibida; manter a rota atual. |
| `Infeasible` | As restrições não podem ser satisfeitas, por exemplo quando um bloco excede toda a frota disponível. | Proibida; manter a rota atual. |
| `InsufficientData` | Faltam capacidade, carga, vínculo, coordenada ou outro dado obrigatório e confiável. | Proibida; manter a rota atual. |

Somente `Optimized` carrega uma distribuição proposta. Os demais
resultados registram motivos e dados ausentes ou conflitantes, sem
produzir uma alteração parcial.

Para a primeira versão, a viabilidade prevalece sobre a otimização rodoviária:
nenhum veículo pode exceder 100%; somente blocos acima da capacidade máxima
podem ser divididos. Entre
soluções viáveis que exigem a mesma quantidade de veículos adicionais, deve ser
preferida a que acrescenta a menor capacidade total; somente depois entram
distância, duração e equilíbrio de ocupação como critérios de comparação. Essa
ordem impede escolher um Toco quando um Acelo já comporta o complemento.

## Componentes concluídos e evolução

### Evolução porta a porta

- consolidar o job de coordenadas por endereço e sua persistência, sem torná-lo
  pré-requisito da otimização municipal;
- medir cobertura de endereços e coordenadas resolvidos por snapshot;
- identificar endereços cadastrais que não representam pontos de entrega;
- definir quando coordenada municipal pode ser usada e como sua menor precisão
  afeta a confiança da simulação.

### Entrada diária concluída

- montar uma entrada imutável do problema contendo import de origem, data de
  referência, `Weekday`, blocos de cidade, cargas e veículos disponíveis;
- rejeitar mistura de dias e capacidades ausentes;
- preservar a carga total e a identidade de todos os blocos;
- definir métricas, limites, critérios de melhoria e versão das regras.

### Matriz rodoviária integrada no código

- provisionar o mapa completo do Brasil e validar o OSRM Table já integrado para tempo e distância entre depósito e municípios do mesmo dia;
- manter explícita a origem e a versão da matriz;
- manter a falha integral já implementada quando o serviço ou as coordenadas
  forem insuficientes, sem trocar silenciosamente por distância em linha reta;
- preparar infraestrutura própria antes de uso comercial recorrente.

### Solver implementado no Worker

- usar o pacote oficial Google OR-Tools para .NET e resolver o problema de
  veículos com capacidade no Worker;
- registrar o ciclo da execução em `job_executions` e na Central de
  Processamentos;
- manter cada bloco de cidade indivisível;
- restringir movimentos ao mesmo dia;
- acrescentar instâncias virtuais quando a frota inicial do dia for insuficiente
  e preferir o menor tipo capaz de acomodar o complemento sem dividir blocos;
- retornar `NoImprovement`, `Infeasible` ou `InsufficientData` sem proposta
  alternativa quando aplicável;
- validar por invariantes que nenhuma carga ou cidade foi perdida, duplicada ou
  transferida para outro dia.

### Persistência e apresentação concluídas

- executar o cálculo pesado no Worker;
- reutilizar Hangfire, `job_executions`, retries e Central de Processamentos;
- persistir entrada, versão das regras, métricas atuais, proposta, motivos e
  avisos para auditoria;
- expor consulta pela API, última execução para polling e comparação no frontend;
- permitir histórico somente leitura e recálculo apenas do snapshot atual;
- manter a aplicação automática fora da primeira versão.

### 6. Avaliar evolução operacional

Somente depois de validar a otimização diária básica, avaliar OSRM Route para
geometria, dados da HERE Traffic ou fonte equivalente, reotimização durante o
dia e aplicativo do motorista. Essas capacidades descritas no PDF não existem
hoje e possuem requisitos próprios de estabilidade, custo, infraestrutura,
status de paradas e funcionamento offline.

## Critérios mínimos de aceitação

- a execução processa exatamente um snapshot e um dia por problema;
- nenhuma proposta contém rota, veículo ou cidade de outro dia;
- cada cidade permanece no mesmo dia; cidades acima da capacidade máxima podem
  aparecer em mais de um veículo;
- a soma das cargas por dia é preservada exatamente;
- nenhuma capacidade válida é excedida;
- veículos adicionais são apenas propostas, usam tipos cadastrados e minimizam
  primeiro a quantidade adicionada e depois a capacidade total acrescentada;
- resultados não otimizados preservam integralmente a distribuição atual;
- métricas, filtros, arredondamentos, casos nulos e totais possuem testes
  automatizados dedicados;
- toda execução aparece na Central de Processamentos e pode ser auditada;
- a simulação nunca altera o snapshot importado.

## Histórico de decisões

| Data | Decisão |
| --- | --- |
| 15/08/2026 | Tratar cada cidade e sua carga diária como bloco indivisível. |
| 15/08/2026 | Executar a otimização separadamente por dia da semana e proibir mistura entre dias. |
| 15/08/2026 | Preservar integralmente a rota atual quando não houver solução válida ou melhoria comprovada. |
| 15/08/2026 | Manter a primeira versão como simulação auditável, sem aplicação automática. |
| 15/08/2026 | Reutilizar os jobs existentes de endereço por CNPJ e coordenada por endereço como preparação dos dados. |
| 15/08/2026 | Usar coordenadas municipais na primeira matriz OSRM; coordenadas de clientes ficam reservadas à futura ordenação porta a porta. |
| 15/08/2026 | Cadastrar o depósito como origem e retorno únicos e preparar o OSRM com o mapa completo do Brasil. |
| 15/08/2026 | Manter a matriz OSRM somente em memória; a auditoria ocorre pelos resultados e pela execução do job. |
| 15/08/2026 | Integrar o pacote oficial Google OR-Tools para .NET no Worker. |
| 18/08/2026 | Otimizar sempre um único dia, mantendo cada bloco cidade + peso no dia de origem. |
| 18/08/2026 | Permitir veículos adicionais virtuais quando a frota diária for insuficiente, minimizando primeiro a quantidade adicionada e depois a capacidade acrescentada. |
| 18/08/2026 | Implementar o job `DAILY_ROUTE_OPTIMIZATION` com CP-SAT, Routing Solver, persistência substituível por snapshot/dia e consulta na tela de Rotas. |
| 18/08/2026 | Permitir que perfis autorizados enfileirem a simulação pelo botão de recálculo, mantendo o cálculo assíncrono no Worker. |
| 18/08/2026 | Alternar rotas reais e sugestões no mesmo espaço da tela, resolver vínculos municipais inequívocos antes da simulação e listar nominalmente os dados ainda insuficientes. |
| 18/08/2026 | Detalhar resultados inviáveis com município, peso agregado do bloco e capacidade máxima disponível. |
| 18/08/2026 | Flexibilizar blocos acima da capacidade máxima, permitindo repartir a carga da cidade entre vários caminhões do mesmo dia. |
| 31/08/2026 | Reutilizar a atribuição viável do CP-SAT como seed do Routing Solver, aplicar quebra de simetria e não descartar a solução em timeout da melhoria rodoviária. |
| 31/08/2026 | Expor snapshot atual, última execução e polling de cinco segundos; histórico é somente leitura e concorrência de recálculo retorna conflito. |
| 31/08/2026 | Apresentar `Rotas reais` e `Sugestões` com o mesmo filtro de data, cards por dia e detalhe municipal, sem inventar entregas. |
| 05/09/2026 | Tornar `Dados insuficientes` acionável: persistir todas as causas, corrigir em versão derivada auditável, herdar dias intactos e recalcular somente dias afetados. |

## Como manter este documento

Atualize a data, a tabela de situação e o histórico sempre que uma regra, fonte
de dados, job, contrato, solver ou fase mudar de estado. Um item só deve passar
para `Concluído` depois que código, persistência, testes e documentação
arquitetural aplicáveis estiverem consolidados. Propostas do PDF permanecem
como `Pendente` até que sua implementação seja confirmada no projeto.
