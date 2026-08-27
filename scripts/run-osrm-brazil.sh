#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OSRM_DATA_DIR="${OSRM_DATA_DIR:-$PROJECT_ROOT/infra/osrm-brazil}"
OSRM_IMAGE="${OSRM_IMAGE:-ghcr.io/project-osrm/osrm-backend:v5.27.1}"
OSRM_PORT="${OSRM_PORT:-5000}"
OSRM_MAX_TABLE_SIZE="${OSRM_MAX_TABLE_SIZE:-100}"

if [[ ! -f "$OSRM_DATA_DIR/brazil-latest.osrm.partition" ]]; then
  printf 'Mapa não preparado. Execute scripts/prepare-osrm-brazil.sh primeiro.\n' >&2
  exit 1
fi

exec docker run --rm -p "$OSRM_PORT:5000" -v "$OSRM_DATA_DIR:/data:ro" "$OSRM_IMAGE" \
  osrm-routed --algorithm mld --max-table-size "$OSRM_MAX_TABLE_SIZE" /data/brazil-latest.osrm
