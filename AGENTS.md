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
- Para qualquer alteração de código, criar/atualizar testes cobrindo o comportamento alterado.
- No frontend, criar casos de teste para funcionalidades importantes com cenários diferentes (fluxo feliz, bordas e entradas inválidas).
- Não introduzir números mágicos em código de regra de negócio, cálculos, limites, paginação, datas ou timeouts; extrair para constantes nomeadas, configuração ou objetos de política conforme o contexto.
- Antes de finalizar, executar a suíte de testes aplicável (frontend e backend quando houver alterações em ambos) e só encerrar a tarefa com testes passando.

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
- Durante desenvolvimento local, subir no Docker somente a infraestrutura (`postgres` e `redis`); rodar `frontend`, `api` e `worker` localmente pelos comandos próprios de cada projeto.

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
