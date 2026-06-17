-- Corrige Crescimento12M, Crescimento6M e Crescimento3M que estavam como 100
-- quando o período anterior era zero (sem receita).
-- Agora devem ser NULL (sem base de comparação).
-- Execute APÓS deploy da correção no código (CalcularVariacao retorna null em vez de 100).

UPDATE "ClienteIndicadores"
SET
    "Crescimento12M" = NULL,
    "Crescimento6M"  = NULL,
    "Crescimento3M"  = NULL
WHERE
    "Crescimento12M" = 100 OR
    "Crescimento6M"  = 100 OR
    "Crescimento3M"  = 100;
