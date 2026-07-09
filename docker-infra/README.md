# Docker Infra (Postgres)

Este compose sobe apenas o servico de infraestrutura para desenvolvimento local:
- Postgres

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
