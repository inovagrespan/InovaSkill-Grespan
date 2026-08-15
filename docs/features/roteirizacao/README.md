# Roteirização — vínculos entre clientes e rotas

## Objetivo

Construir a base confiável cliente → dia → rota antes da futura otimização de trajetos.
Esta documentação acompanha decisões, implementação, pendências e validações da feature.

## Estado encontrado

- Rotas e clientes são importados em snapshots independentes.
- O vínculo anterior era inferido por município, podendo associar um cliente a várias rotas incorretamente.
- O arquivo `Cidade - Rota - atual.xlsx` possui uma aba estruturada `Rotas Atuais ` com
  `Dia`, `Mercado`, `Rota` e `Cidade`: 1.167 linhas completas e 27 incompletas.
- O cadastro usa `Código + Loja` como identidade; o novo arquivo informa apenas o nome do mercado.

## Decisões

- A fonte `CUSTOMER_ROUTE_ASSIGNMENTS` é um snapshot processado pelo Worker e registrado em
  `job_executions`.
- Correspondência automática exige cliente único por nome normalizado e município, e rota única por
  nome normalizado e dia.
- Casos ausentes ou ambíguos ficam em `NeedsReview`; a UI permite selecionar IDs válidos dos snapshots atuais.
- Após a primeira publicação, os vínculos importados substituem a inferência municipal. Cliente ausente
  na planilha aparece sem rota.
- Um cliente pode ter várias associações; a tela mostra cada par dia + rota.

## Fluxo

1. Upload detecta o cabeçalho da nova fonte.
2. Worker lê todas as abas compatíveis e grava linhas resolvidas em `customer_route_mappings`.
3. Pendências são corrigidas na Central de Importações e o mesmo import é reprocessado.
4. A publicação reconstrói `route_customer_assignments` com origem `Imported`.
5. Publicações posteriores de rotas ou clientes reexecutam a sincronização.

## Pontos importantes

- Coordenada municipal não substitui endereço de entrega e ainda não habilita roteirização porta a porta.
- Alterações de nomes de rota exigem nova correspondência ou revisão do snapshot de vínculos.
- Não é realizado matching aproximado automático, evitando associações silenciosamente incorretas.

## Registro de implementação

- [x] Diagnóstico do arquivo e dos dados atuais.
- [x] Fonte, detector, parser, persistência e matching conservador.
- [x] Revisão assistida de cliente e rota.
- [x] Exibição de todas as rotas na listagem de clientes.
- [x] Primeiro processamento local: import `23c5c5b9-1f7d-40c4-9f55-39d5f327a228`,
  1.191 linhas de negócio, 289 resolvidas integralmente e 1.181 pendências de campo.
- [ ] Resolver as pendências do primeiro arquivo e publicar o snapshot.
- [ ] Definir os dados de endereço/parada necessários para a próxima fase da roteirização.

### Pendências do primeiro processamento

- 682 clientes não encontrados por igualdade de nome e cidade.
- 468 rotas não encontradas por igualdade de nome e dia no snapshot atual.
- 18 cidades, 7 dias, 4 rotas e 2 mercados obrigatórios ausentes.

Uma mesma linha pode possuir mais de uma pendência; por isso a quantidade de pendências
é superior à diferença entre linhas lidas e linhas resolvidas.
