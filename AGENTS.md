# AGENTS.md

Guia local para qualquer IA que edite este repositório.

## Objetivo
- Evitar regressões de texto/encoding (ex.: `Configuração`).
- Manter padrão de arquitetura já existente no projeto.
- Reduzir mudanças fora do escopo.

## Regras Obrigatórias
- Salvar arquivos sempre em UTF-8.
- Não renomear pastas de arquitetura existente sem pedido explícito.
- Não quebrar separação entre `frontend` e `backend`.
- Não mudar contratos de API sem atualizar frontend e backend no mesmo PR.
- Quando a tarefa envolver subir o ambiente em desenvolvimento local, subir apenas `postgres` e `redis` via Docker e executar `frontend`, `api` e `worker` localmente com os comandos próprios de cada projeto.
- Escolha de nomes é parte obrigatória da qualidade do código: nomes de tipos, métodos, variáveis, constantes, rotas e casos de uso devem refletir o domínio com clareza; nome ruim não é detalhe, é defeito de legibilidade.
- Para qualquer alteração de código, criar/atualizar testes cobrindo o comportamento alterado.
- Para qualquer alteração no frontend, criar ou atualizar testes de frontend cobrindo o comportamento alterado.
- Toda métrica, KPI, score, resumo, agregação, comparação, ranking, timeline, previsão ou cálculo exibido em tela ou exposto por API deve ter teste automatizado dedicado, sem exceção.
- É proibido entregar nova métrica ou alterar métrica existente sem cobrir fórmula, filtros, agrupamentos, limites, casos nulos, sinais, arredondamento e consistência entre partes e total.
- No frontend, criar casos de teste para funcionalidades importantes com cenários diferentes (fluxo feliz, bordas e entradas inválidas).
- Sempre que houver qualquer alteração, executar imediatamente os testes aplicáveis da área alterada antes de encerrar a tarefa.
- Não introduzir números mágicos em código de regra de negócio, cálculos, limites, paginação, datas ou timeouts; extrair para constantes nomeadas, configuração ou objetos de política conforme o contexto.
- Antes de finalizar, executar toda a suíte de testes aplicável ao escopo alterado, incluindo testes de frontend quando houver mudanças no frontend e testes de backend quando houver mudanças no backend; só encerrar a tarefa com todos os testes aplicáveis passando.
- Quando for solicitado criar commit, usar mensagens no padrão Conventional Commits.

## Testing Philosophy
- O objetivo principal dos testes é encontrar defeitos, inconsistências, regressões e violações de regra de negócio.
- Testes devem desafiar a implementação, questionar premissas e validar intenção de negócio, não apenas executar código.
- Não basta cobrir linhas: cobertura sem qualidade, sem cenários de risco e sem assertivas de negócio não tem valor.
- Para cada regra relevante, cobrir caminho feliz, limites, entradas inválidas, dados contraditórios, casos reais, casos extremos e regressões conhecidas.
- Edge cases são obrigatórios em regras críticas, especialmente parsing de planilhas, moeda, datas, filtros, agregações, dashboards, filas, workers, progresso e resumos.
- Todo bug corrigido deve ganhar teste de regressão que falhe antes da correção e passe depois.
- Identificar e testar invariantes do domínio, por exemplo: progresso entre 0 e 100, linhas importadas <= linhas lidas, totais coerentes, contagens não negativas e soma das partes igual ao total.
- Para métricas e dashboards, validar obrigatoriamente: fórmula, granularidade, período, filtros, agrupamento, ordenação, totais, subtotais, variação percentual, bases zeradas, valores negativos, ausência de dados, arredondamento e consistência com os dados persistidos.
- Evitar testes que repetem o algoritmo da implementação, validam detalhes internos sem necessidade ou apenas verificam que um método foi chamado.
- Priorizar comportamento observável, resultado esperado, impacto para o usuário e sentido de negócio dos números retornados.
- Preferir dados pequenos, explícitos e calculáveis manualmente; fixtures grandes só quando o volume em si for o comportamento testado.

## Encoding e Idioma
- Textos de UI devem usar PT-BR com acentuação correta.
- Nunca commitar arquivos em UTF-16/ANSI.
- É proibido introduzir ou manter caracteres corrompidos/mojibake (ex.: `Ã`, `��`, `�`) em qualquer arquivo.
- Antes de finalizar, buscar por caracteres corrompidos:
  - `Ã`
  - `��`
  - `�`

Exemplo de validação local:

```powershell
rg "Ã|��|�" -n frontend backend -S
```

## Arquitetura Atual
- `frontend/`: aplicação web (TanStack Router + Vite).
- `backend/`: solução .NET separada em camadas:
  - `InovaSkill.Importer.Api` (HTTP API)
  - `InovaSkill.Importer.Worker` (processamento assíncrono)
  - `InovaSkill.Importer.Application` (casos de uso)
  - `InovaSkill.Importer.Domain` (regras de domínio)
  - `InovaSkill.Importer.Infrastructure` (persistência, filas, integrações)

## Convenções de Implementação
- Frontend:
  - Criar telas novas dentro de `frontend/src/routes`.
  - Reutilizar componentes de `frontend/src/components/ui`.
  - Respeitar o padrão visual já adotado na área de Importações.
- Backend:
  - Endpoints em `Api`.
  - Regra de negócio em `Application/Domain`.
  - Banco, fila e arquivos em `Infrastructure`.
  - Worker deve consumir fila (Redis) e não depender de chamadas internas da API.

## Padrão Para Cálculos e Agregações
- Para cálculos pesados, consolidações, enriquecimento de dados e geração de resumos (diário/semanal/mensal), preferir processamento assíncrono via `InovaSkill.Importer.Worker`.
- A `Api` deve expor consulta e acionamento, mas não executar processamento pesado em request síncrono.
- O `Worker` consome eventos/fila (Redis), executa o pipeline de processamento e persiste resultados nas tabelas de resumo.
- Regras de cálculo, validação e comparação devem ficar em `Application/Domain` (ou serviços de processamento em `Infrastructure` quando envolver integração/persistência), mantendo lógica reutilizável e testável.
- Leitura para dashboards deve consultar dados já consolidados (materializados) sempre que possível, evitando recalcular tudo em cada requisição.
- Sempre que criar novo cálculo:
  - Definir contrato de entrada/saída e granularidade (ex.: diário, semanal, mensal).
  - Implementar processamento no `Worker` + persistência em `Infrastructure`.
  - Expor endpoint de leitura na `Api`.
  - Atualizar frontend para consumir o contrato.
  - Criar testes de unidade para cálculo e testes de integração para filtros/agregações.

## Docker e Infra
- Serviços padrão: `frontend`, `api`, `worker`, `postgres`, `redis`.
- Persistência obrigatória via volumes para Postgres/Redis/uploads.
- Evitar alterações que removam `restart: unless-stopped`.
- Durante desenvolvimento local, subir no Docker somente a infraestrutura (`postgres` e `redis`) com `docker compose up -d postgres redis`; rodar `frontend`, `api` e `worker` localmente pelos comandos próprios de cada projeto.

## Checklist Antes de Entregar
- Build frontend:
```powershell
cd frontend; npm run build
```
- Subir stack:
```powershell
cd ..; docker compose up -d --build
```
- Em desenvolvimento local, subir apenas a infraestrutura:
```powershell
docker compose up -d postgres redis
```
- Verificar containers:
```powershell
docker compose ps
```
- Verificar textos quebrados:
```powershell
rg "Ã|��|�" -n frontend backend -S
```
- Executar testes frontend:
```powershell
cd frontend; npm run test
```
- Executar testes backend:
```powershell
cd backend; dotnet test
```
- Nunca encerrar a tarefa sem rodar os testes aplicáveis ao que foi alterado e confirmar que todos passaram.
