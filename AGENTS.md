# AGENTS.md

Guia local para qualquer IA que edite este repositório.

## Objetivo
- Evitar regressões de texto/encoding (ex.: `Configura��o`).
- Manter padrão de arquitetura já existente no projeto.
- Reduzir mudanças fora do escopo.

## Regras Obrigatórias
- Salvar arquivos sempre em UTF-8.
- Não renomear pastas de arquitetura existente sem pedido explícito.
- Não quebrar separação entre `frontend` e `backend`.
- Não mudar contratos de API sem atualizar frontend e backend no mesmo PR.

## Encoding e Idioma
- Textos de UI devem usar PT-BR com acentuação correta.
- Nunca commitar arquivos em UTF-16/ANSI.
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

## Docker e Infra
- Serviços padrão: `frontend`, `api`, `worker`, `postgres`, `redis`.
- Persistência obrigatória via volumes para Postgres/Redis/uploads.
- Evitar alterações que removam `restart: unless-stopped`.

## Checklist Antes de Entregar
- Build frontend:
```powershell
cd frontend; npm run build
```
- Subir stack:
```powershell
cd ..; docker compose up -d --build
```
- Verificar containers:
```powershell
docker compose ps
```
- Verificar textos quebrados:
```powershell
rg "Ã|��|�" -n frontend backend -S
```
