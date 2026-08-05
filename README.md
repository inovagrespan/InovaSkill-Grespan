# InovaSkill-Grespan

MVP web com frontend React, API e Worker .NET, PostgreSQL e processamento
assíncrono via Hangfire.

## Execução completa com Docker

Pré-requisito: Docker com o plugin Compose.

```bash
docker compose up -d --build
```

Depois que os containers iniciarem, acesse <http://localhost>. A API também fica
disponível em <http://localhost:5279>.

Não é necessário criar um `.env`: o Compose já possui valores demonstrativos
seguros para uso local. Para mudar portas, credenciais locais ou integrar a IA,
copie `.env.example` para `.env` e edite os valores. O `.env` permanece ignorado
pelo Git.

Usuários demonstrativos criados automaticamente:

| Usuário | Senha | Perfil |
| --- | --- | --- |
| `admin` | `admin` | Administrador |
| `admin_system` | `admin_system` | Administrador do sistema |
| `vendas` | `vendas` | Vendas |
| `logistica` | `logistica` | Logística |
| `diretor` | `diretor` | Diretor |

Essas credenciais e os valores padrão do Compose são exclusivos do MVP local e
devem ser substituídos antes de qualquer exposição pública.

O projeto inicia sem chave da OpenAI; nesse caso, apenas os recursos de chat com
IA ficam indisponíveis. Informe `OPENAI_API_KEY` no `.env` para habilitá-los.

A otimização usa distância geográfica por padrão, sem depender de outro
container. O serviço OSRM continua disponível pelo perfil opcional `osrm` para
quem já tiver preparado os artefatos em `infra/osrm`.

Comandos úteis:

```bash
docker compose ps
docker compose logs -f api worker frontend
docker compose down
```

Os dados do PostgreSQL e os arquivos importados permanecem nos volumes Docker.
Para recriar uma instalação demonstrativa do zero, remova os volumes
explicitamente com `docker compose down --volumes`.

## Desenvolvimento local

Conforme a arquitetura do projeto, suba somente a infraestrutura no Docker:

```bash
docker compose up -d postgres
```

Execute frontend, API e Worker localmente com os comandos dos respectivos
projetos.

## Guia para IA e padronização

O repositório possui regras de encoding, arquitetura, implementação e validação
em [AGENTS.md](./AGENTS.md).
