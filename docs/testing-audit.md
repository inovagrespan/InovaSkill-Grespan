# Auditoria da Filosofia de Testes

Data: 2026-06-01

## Objetivo

Esta auditoria registra lacunas encontradas na suíte atual e direciona a evolução dos testes para encontrar defeitos reais, inconsistências de negócio e regressões.

## Achados

- Há bons testes de unidade para parsing, cálculos e controllers, mas parte da suíte ainda privilegia caminho feliz e presença estrutural de UI.
- Alguns testes de frontend validam strings em arquivos ou marcações, o que ajuda contra regressão visual simples, mas tem baixo poder para detectar bug de negócio.
- Resumos materializados tinham cobertura de idempotência, mas pouca pressão contra contaminação entre arquivos, fronteiras de semana, duplicidade de documentos e soma das partes.
- Parsing monetário tinha um caso ambíguo sem regressão: `1.234` em contexto brasileiro deve ser milhar, enquanto `3.32` pode ser decimal internacional.
- Monitoramento de processamento cobria snapshot básico, mas faltavam invariantes operacionais como progresso limitado a 0..100, jobs sem heartbeat e worker offline.
- Template builder tinha testes de normalização e checklist, mas faltavam entradas inválidas, dados contraditórios, duplicidades e defaults em preview.
- Áreas ainda merecem novas rodadas: concorrência real em filas Redis, idempotência sob reentrega de evento, filtros combinados dos dashboards, timezones em virada de dia e grandes volumes.

## Mudanças Aplicadas

- Adicionados testes de regressão para moeda brasileira ambígua no backend e no frontend.
- Reforçados testes de resumos de vendas com separação por arquivo, dimensões de agregação e domingo pertencendo à semana anterior.
- Reforçados testes de resumos de clientes com documentos repetidos entre clientes, isolamento por arquivo e invariantes de soma entre diário, semanal e mensal.
- Reforçados testes de monitoramento com progresso fora de faixa, job stale, worker offline e cancelamento de job terminal.
- Recriados testes do template builder com cenários de limites, datas impossíveis, valores negativos, defaults, duplicidades e ordem observável das regras enviadas para API.

## Lacunas Restantes

- Criar testes de integração com Redis real ou substituto controlado para reentrega, dead-letter e concorrência entre workers.
- Cobrir filtros combinados dos dashboards com dados pequenos que revelem joins duplicadores.
- Testar imports com planilhas maiores, linhas parcialmente inválidas e múltiplas falhas simultâneas.
- Adicionar regressões para timezone em datas próximas de meia-noite e mudança de mês.
- Reduzir gradualmente testes que inspecionam código-fonte de componentes e substituí-los por testes de comportamento observável.
- Definir invariantes por domínio: faturamento, quantidade, peso, pedidos, progresso e taxas percentuais.

## Critério Para Novos Testes

Novos testes devem começar pela pergunta: "como essa regra pode falhar em produção?". Só depois devem validar o caminho feliz.
