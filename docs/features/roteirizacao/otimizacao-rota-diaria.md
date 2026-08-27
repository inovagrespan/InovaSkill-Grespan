# Plano vivo da otimização diária de rotas

## Objetivo

Este documento registra a evolução da otimização diária de rotas da Grespan. Ele
é a referência para distinguir o que já existe no sistema, o que está em
desenvolvimento, o que foi removido e o que ainda precisa ser construído.

A otimização futura deverá redistribuir blocos de cidades entre os veículos
disponíveis no mesmo dia, respeitando capacidade e preservando integralmente a
rota atual quando não houver uma solução válida e comprovadamente melhor.

> O PDF `plano_roteirizacao_grespan.pdf` é uma referência de planejamento. As
> ferramentas e fases descritas nele não devem ser consideradas implementadas
> sem confirmação neste documento e no código atual.

**Última atualização:** 15 de agosto de 2026.

## Regras obrigatórias

1. A unidade de processamento é um único dia da semana (`Route.Weekday`) dentro
   do snapshot de rotas selecionado.
2. Cidades, cargas, rotas e veículos de dias diferentes nunca podem ser
   combinados na mesma otimização.
3. Cada cidade e sua carga total em `Média/Dia` formam um bloco indivisível. A
   primeira versão não pode repartir a mesma cidade entre veículos.
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
9. Cidade cuja carga individual exceda a capacidade de todos os veículos
   disponíveis no dia torna o problema inviável para a regra de bloco
   indivisível.
10. A primeira versão será somente uma simulação auditável. Ela não atualizará
    automaticamente `routes`, `route_entries`, vínculos de clientes ou o
    snapshot publicado.
11. Cada execução futura deve identificar, no mínimo, o snapshot de entrada, a
    data de referência e o dia da semana processado.

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
| Coordenada por endereço cadastral | Em desenvolvimento | O job `CUSTOMER_ADDRESS_COORDINATE_ENRICHMENT` e sua persistência existem no workspace atual, mas ainda não estão consolidados no `main`. |
| Apoio à escolha de veículo | Concluído | O detalhe da rota compara a carga com o catálogo de veículos e apresenta alternativas; não altera a rota nem executa otimização global. |
| Otimização legada | Removido | Solver heurístico, matriz geográfica/OSRM, cenários, endpoints, ferramenta de chat, tabelas e job foram removidos intencionalmente. |
| Entrada imutável da otimização diária | Pendente | Ainda deve ser definido e versionado o contrato que reúne o snapshot, o dia, as cidades, as cargas e a frota disponível. |
| Fundação da matriz rodoviária | Concluído | O cliente OSRM calcula duração e distância entre depósito e municípios de um único dia, com divisão em blocos e falha integral; ainda não é consumido por um solver. |
| Dataset OSRM do Brasil | Pendente | Scripts versionados preparam e executam o grafo MLD, mas a instância e o mapa ainda precisam ser provisionados na infraestrutura. |
| Integração OR-Tools VRP | Pendente | A decisão é usar o pacote oficial Google OR-Tools para .NET dentro do Worker; ainda não existe solver ativo. |
| Persistência e apresentação das simulações | Pendente | Não existem atualmente execução, cenário persistido, endpoint ou tela de resultado da nova otimização. |
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

Veículos desconhecidos não recebem capacidade presumida. Uma futura execução de
otimização deve rejeitar ou desconsiderar, de forma auditável, veículos cuja
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

O job `CUSTOMER_ADDRESS_COORDINATE_ENRICHMENT` está implementado nas alterações
atuais do workspace e deverá ser reutilizado pela roteirização depois de
consolidado:

- processa somente endereços cadastrais com estado `RESOLVED`;
- aceita `customerStatus` com `ACTIVE`, `INACTIVE` ou `ALL`;
- aceita `reprocessFailed`, desativado por padrão;
- monta o endereço com tipo e nome do logradouro, número, bairro, município, UF,
  CEP e país;
- consulta o Nominatim sequencialmente, com identificação do cliente HTTP e
  intervalo global mínimo de uma requisição por segundo na instância;
- aceita uma coordenada apenas quando município, UF e número são compatíveis;
- reutiliza coordenadas resolvidas para o mesmo endereço normalizado;
- persiste status, latitude, longitude, identificador do provedor, descrição,
  tentativas e falha auditável em `customer_address_coordinates`;
- registra progresso e resultado no mesmo `job_executions` usado pela Central
  de Processamentos.

Essas coordenadas são mais precisas que o centro do município, mas continuam
dependentes da qualidade do endereço cadastral. Quando não houver coordenada de
endereço válida, a coordenada municipal pode apoiar visualização e diagnóstico;
o uso desse fallback na otimização deverá ser uma decisão explícita do contrato,
sem misturar silenciosamente níveis diferentes de precisão.

## O que foi removido

O sistema já possuiu um subsistema de sugestão global de rotas. Ele incluía
solver heurístico, provedores de matriz geográfica e OSRM, execução assíncrona,
cenários persistidos, endpoints, integração com o assistente e telas de
resultado. A migração `RemoveRouteOptimization` removeu os agendamentos e as
tabelas `route_optimization_runs` e `route_optimization_scenarios`; o código e
os contratos correspondentes também foram retirados.

Esse legado serve apenas como histórico de decisões. Ele não está funcional e
não deve ser reativado por cópia direta. A nova implementação deverá partir das
regras diárias deste documento, reutilizando apenas os padrões arquiteturais
atuais de importação, Worker, Hangfire, `job_executions` e Central de
Processamentos.

## Estado e uso da matriz OSRM

A matriz de duração e distância não é persistida atualmente. O fluxo disponível
é `depósito + municípios do dia → OSRM Table → matriz em memória`. O resultado
será entregue ao futuro solver e descartado ao fim do processamento.

Essa escolha mantém a fundação simples enquanto o contrato da otimização ainda
não existe. Ao implementar o job de otimização, a execução deverá persistir os
dados necessários para auditoria e reprodução: snapshot e dia usados, depósito,
pontos e coordenadas, versão/data/checksum do mapa, configuração do cálculo e
métricas atuais e propostas. A decisão entre persistir a matriz completa ou um
hash acompanhado desses dados será fechada com o contrato do job; até lá não
deve ser criada tabela ou cache paralelo de matrizes.

O OSRM e o solver terão responsabilidades separadas:

1. o OSRM calcula tempo e distância rodoviários entre depósito e cidades;
2. o OR-Tools recebe essa matriz, cargas e capacidades e decide a atribuição e a
   sequência dos blocos;
3. o sistema compara a proposta com a situação atual e mantém as rotas atuais
   quando não houver solução válida e melhor.

## Próxima etapa decidida: OR-Tools VRP

A integração será feita com o pacote oficial Google OR-Tools para .NET dentro do
`InovaSkill.Importer.Worker`. O cálculo será assíncrono, reutilizará Hangfire,
`job_executions`, retries e a Central de Processamentos. A API apenas solicitará
e consultará a execução; não executará o solver durante uma requisição HTTP.

### Entrada do solver

- snapshot de rotas e data de referência;
- um único `Weekday`;
- depósito como início e fim de todos os veículos;
- matriz direcional de duração e distância produzida pelo OSRM;
- blocos municipais indivisíveis com `MunicipalityId` e carga `Média/Dia`;
- veículos disponíveis no dia e suas capacidades válidas;
- distribuição atual, para cálculo e comparação das métricas;
- versão das regras, objetivo e limites de tempo do solver.

### Restrições obrigatórias

- cada cidade aparece exatamente uma vez na solução;
- uma cidade não pode ser dividida entre veículos;
- cidades e veículos não podem atravessar dias;
- nenhum veículo pode superar sua capacidade;
- todos os veículos saem e retornam ao mesmo depósito;
- a soma das cargas e o conjunto de cidades devem permanecer idênticos;
- timeout, inviabilidade ou dados insuficientes nunca geram alteração parcial.

### Saída esperada

Para cada veículo, o resultado informará sequência de cidades, carga, capacidade,
ocupação, distância e duração estimadas. O resumo comparará a distribuição atual
e a proposta por distância total, duração total, veículos utilizados,
sobrecargas e maior ocupação. O contrato usará `Optimized`, `NoImprovement`,
`Infeasible` ou `InsufficientData`; somente `Optimized` poderá carregar uma
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

## Resultado esperado da futura otimização

O contrato ainda será detalhado antes da implementação, mas deverá representar
explicitamente estes resultados:

| Resultado | Significado | Distribuição alternativa |
| --- | --- | --- |
| `Optimized` | Existe uma solução válida e comprovadamente melhor para o dia. | Permitida, somente como simulação na primeira versão. |
| `NoImprovement` | Existem soluções viáveis, mas nenhuma melhora a distribuição atual pelos critérios definidos. | Proibida; manter a rota atual. |
| `Infeasible` | As restrições não podem ser satisfeitas, por exemplo quando um bloco excede toda a frota disponível. | Proibida; manter a rota atual. |
| `InsufficientData` | Faltam capacidade, carga, vínculo, coordenada ou outro dado obrigatório e confiável. | Proibida; manter a rota atual. |

Somente `Optimized` poderá carregar uma distribuição proposta. Os demais
resultados deverão registrar motivos e dados ausentes ou conflitantes, sem
produzir uma alteração parcial.

Antes de codificar o solver, devem ser fixadas as métricas que comprovam a
melhoria, como distância rodoviária total, duração estimada, quantidade de
veículos usados e equilíbrio de ocupação. A ordem de prioridade e os critérios
de desempate também deverão ser versionados e testados.

## Fases seguintes

### 1. Manter e qualificar coordenadas para a evolução porta a porta

- consolidar o job de coordenadas por endereço e sua persistência, sem torná-lo
  pré-requisito da otimização municipal;
- medir cobertura de endereços e coordenadas resolvidos por snapshot;
- identificar endereços cadastrais que não representam pontos de entrega;
- definir quando coordenada municipal pode ser usada e como sua menor precisão
  afeta a confiança da simulação.

### 2. Definir a entrada diária

- criar um snapshot imutável do problema contendo import de origem, data de
  referência, `Weekday`, blocos de cidade, cargas e veículos disponíveis;
- rejeitar mistura de dias e capacidades ausentes;
- preservar a carga total e a identidade de todos os blocos;
- definir métricas, limites, critérios de melhoria e versão das regras.

### 3. Provisionar e validar a matriz rodoviária

- provisionar o mapa completo do Brasil e validar o OSRM Table já integrado para tempo e distância entre depósito e municípios do mesmo dia;
- manter explícita a origem e a versão da matriz;
- manter a falha integral já implementada quando o serviço ou as coordenadas
  forem insuficientes, sem trocar silenciosamente por distância em linha reta;
- preparar infraestrutura própria antes de uso comercial recorrente.

### 4. Implementar o solver no Worker

- adicionar o pacote oficial Google OR-Tools para .NET e resolver o problema de
  veículos com capacidade no Worker;
- registrar o ciclo da execução em `job_executions` e na Central de
  Processamentos;
- manter cada bloco de cidade indivisível;
- restringir movimentos ao mesmo dia;
- retornar `NoImprovement`, `Infeasible` ou `InsufficientData` sem proposta
  alternativa quando aplicável;
- validar por invariantes que nenhuma carga ou cidade foi perdida, duplicada ou
  transferida para outro dia.

### 5. Persistir e apresentar simulações

- executar o cálculo pesado no Worker;
- reutilizar Hangfire, `job_executions`, retries e Central de Processamentos;
- persistir entrada, versão das regras, métricas atuais, proposta, motivos e
  avisos para auditoria;
- expor consulta pela API e uma comparação no frontend;
- manter a aplicação automática fora da primeira versão.

### 6. Avaliar evolução operacional

Somente depois de validar a otimização diária básica, avaliar OSRM Route para
geometria, dados da HERE Traffic ou fonte equivalente, reotimização durante o
dia e aplicativo do motorista. Essas capacidades descritas no PDF não existem
hoje e possuem requisitos próprios de estabilidade, custo, infraestrutura,
status de paradas e funcionamento offline.

## Critérios mínimos de aceitação futura

- a execução processa exatamente um snapshot e um dia por problema;
- nenhuma proposta contém rota, veículo ou cidade de outro dia;
- cada cidade aparece exatamente uma vez e continua indivisível;
- a soma das cargas por dia é preservada exatamente;
- nenhuma capacidade válida é excedida;
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
| 15/08/2026 | Manter a matriz OSRM somente em memória nesta fundação e definir sua auditoria junto ao futuro contrato de otimização. |
| 15/08/2026 | Integrar o pacote oficial Google OR-Tools para .NET no Worker como próxima etapa do VRP. |

## Como manter este documento

Atualize a data, a tabela de situação e o histórico sempre que uma regra, fonte
de dados, job, contrato, solver ou fase mudar de estado. Um item só deve passar
para `Concluído` depois que código, persistência, testes e documentação
arquitetural aplicáveis estiverem consolidados. Propostas do PDF permanecem
como `Pendente` até que sua implementação seja confirmada no projeto.
