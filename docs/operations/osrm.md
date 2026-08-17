# Operação do OSRM

## Papel no sistema

O OSRM é uma API interna de roteamento. Nesta fase, o Worker consulta o serviço
para obter matrizes direcionais de duração e distância entre o depósito e as
coordenadas municipais das cidades de um único dia. O serviço não otimiza nem
altera rotas.

O conjunto inicial usa o extrato completo do Brasil, perfil veicular `driving`
e algoritmo MLD. Os artefatos gerados ficam fora do Git em
`infra/osrm-brazil`.

## Preparação em infraestrutura

Requisitos: Docker, `curl`, `sha256sum`, espaço em disco e memória compatíveis
com o pré-processamento do mapa completo do Brasil.

```bash
scripts/prepare-osrm-brazil.sh
scripts/run-osrm-brazil.sh
```

Os scripts fixam por padrão a imagem
`ghcr.io/project-osrm/osrm-backend:v5.27.1`, registram URL, checksum, imagem,
perfil, algoritmo e horário de preparação. `OSRM_IMAGE`, `OSRM_DATA_DIR`,
`OSRM_PBF_URL`, `OSRM_PORT` e `OSRM_MAX_TABLE_SIZE` permitem configuração
explícita do ambiente.

O grafo novo deve ser preparado e validado em outro diretório antes da troca do
grafo ativo. Não substitua arquivos enquanto `osrm-routed` estiver usando o
dataset.

## Desenvolvimento local

O ambiente de desenvolvimento continua subindo somente o PostgreSQL no Docker;
API, Worker e frontend são executados localmente. O OSRM não é iniciado pelo
`docker compose` principal. Quando a integração real for necessária,
`Osrm:BaseUrl` deve apontar para uma instância previamente preparada e acessível.
Testes automatizados usam respostas HTTP controladas e não dependem do serviço.

Configuração padrão:

```json
{
  "Osrm": {
    "BaseUrl": "http://localhost:5000",
    "TimeoutSeconds": 30,
    "MatrixBlockSize": 50,
    "MaximumParallelRequests": 2
  }
}
```

`MatrixBlockSize` controla cada dimensão dos blocos `sources × destinations`;
o servidor deve aceitar pelo menos o total de coordenadas distintas enviado por
bloco. `MaximumParallelRequests` limita pressão sobre o serviço.

## Diagnóstico

Depois de cadastrar o depósito, `GET /api/osrm/health` consulta o endpoint
`nearest` com sua coordenada. HTTP 200 indica que o serviço está acessível e
localizou o depósito; HTTP 503 indica depósito ausente, serviço indisponível ou
ponto fora do mapa.

Falha, timeout, matriz incompleta, valor nulo ou ponto inalcançável invalidam a
matriz inteira. Não existe fallback para distância em linha reta.
