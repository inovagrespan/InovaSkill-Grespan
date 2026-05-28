# Arquivos de teste manual

## Válidos
- customers-valid.csv
- products-valid.csv
- orders-valid.csv

## Com erro
- customers-invalid.csv
- products-invalid.csv
- orders-invalid.csv

Use estes arquivos para validar os dois cenários do pipeline:
1. `ValidationFailed` com erros em `ImportError`.
2. `ReadyToImport` -> `Completed` para arquivos sem erro.
