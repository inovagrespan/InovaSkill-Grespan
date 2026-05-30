# Docker Infra (Postgres + Redis)

Este compose sobe apenas os servicos de infraestrutura para desenvolvimento local:
- Postgres
- Redis

Comandos:

```powershell
cd docker-infra
docker compose up -d
```

Parar mantendo dados:

```powershell
docker compose stop
```

Parar e remover containers da infra:

```powershell
docker compose down
```

Observacao: este arquivo usa o mesmo `name` de projeto do compose principal (`inovaskill-grespan`), entao os mesmos containers e volumes sao reutilizados.
