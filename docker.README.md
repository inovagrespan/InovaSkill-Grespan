# Docker quickstart

# 1) Build and start all services
# docker compose up -d --build

# 2) Follow logs
# docker compose logs -f api worker frontend postgres

# 3) Stop (without deleting data)
# docker compose down

# 4) Stop and remove data volumes (DANGER: deletes persisted DB/uploads)
# docker compose down -v

# URLs
# Frontend: http://localhost:5173
# API: http://localhost:5279
# PostgreSQL: localhost:5432
