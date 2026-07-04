# Importação de Rotas por Cidades

## Fluxo

`POST /api/route-imports` valida apenas o arquivo, salva o XLSX no storage compartilhado,
cria `imports` e `job_executions` em `QUEUED`, publica `ProcessImport { ImportId,
JobExecutionId }` e retorna HTTP 202. A mensagem pequena segue pelo stream
`route-imports` do Redis. O Worker Wolverine é o único consumidor, resolve
`IDataSourceProcessor` pelo código estável `ROUTES_BY_CITY` e persiste o resultado
no PostgreSQL.

O banco é a fonte histórica. Redis não recebe arquivos nem linhas da planilha.
API e Worker devem apontar `Storage__ImportsPath` para a mesma pasta. Sem
configuração, ambos usam `backend/route-imports` em execução local.

## Responsabilidades e estados

- API: upload, consultas, correções, reprocessamento e publicação.
- Redis/Wolverine: transporte durável e retries em 5 segundos, 30 segundos e 2 minutos.
- Worker: leitura, validação, normalização e persistência.
- PostgreSQL: imports, erros, execuções e rotas.

Import e job têm significados diferentes. Um valor de negócio inválido termina
com import `NEEDS_REVIEW` e job `COMPLETED`. Arquivo estruturalmente inválido
termina com import `FAILED` e job `COMPLETED`. Falha técnica após quatro
execuções termina ambos como `FAILED`.

## Sete tabelas

- `data_sources`: código, nome e tipo da fonte.
- `imports`: arquivo, estado e contadores.
- `import_errors`: coordenada da célula, valor original e eventual correção.
- `job_executions`: histórico imutável de cada execução.
- `vehicle_types`: Truck (10.300 kg), Toco (7.700 kg) e Acelo (3.300 kg).
- `routes`: rota, dia, veículo e import de origem.
- `route_entries`: sequência original, nome, entregas, Média/Dia e observação.

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

## Idempotência e concorrência

O processador adquire `pg_advisory_xact_lock` derivado do `ImportId`, impedindo
duas execuções simultâneas em processos diferentes. Na mesma transação, remove
somente `routes` daquele import (entries saem por cascade) e recria o conjunto.
O import, o arquivo, correções resolvidas e dados de outros imports são preservados.

## Métricas dos jobs

Os cards consultam `job_executions`: fila é `QUEUED`; processamento soma
`PROCESSING` e `RETRYING`; concluídos e falhos usam as últimas 24 horas. Taxa de
sucesso é `COMPLETED / (COMPLETED + FAILED)`, com zero quando não há base. Tempo
médio usa `finished_at - started_at` apenas para jobs concluídos.

## Nova fonte no futuro

Crie o registro em `data_sources`, implemente um `IDataSourceProcessor` com o
novo `SourceCode`, seu parser/validador e as tabelas específicas da fonte.
Registre o processador na DI. Reutilize imports, erros, jobs, storage, Wolverine,
Redis, Worker e as telas base. Não grave nomes de classes no banco.
