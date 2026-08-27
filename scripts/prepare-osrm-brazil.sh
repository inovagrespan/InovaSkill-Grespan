#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OSRM_DATA_DIR="${OSRM_DATA_DIR:-$PROJECT_ROOT/infra/osrm-brazil}"
OSRM_IMAGE="${OSRM_IMAGE:-ghcr.io/project-osrm/osrm-backend:v5.27.1}"
OSRM_PBF_URL="${OSRM_PBF_URL:-https://download.geofabrik.de/south-america/brazil-latest.osm.pbf}"
OSRM_PBF="$OSRM_DATA_DIR/brazil-latest.osm.pbf"
OSRM_BASE="$OSRM_DATA_DIR/brazil-latest.osrm"

mkdir -p "$OSRM_DATA_DIR"

if [[ ! -f "$OSRM_PBF" ]]; then
  curl --fail --location "$OSRM_PBF_URL" --output "$OSRM_PBF"
fi

sha256sum "$OSRM_PBF" > "$OSRM_DATA_DIR/brazil-latest.osm.pbf.sha256"

if [[ ! -f "$OSRM_BASE.partition" ]]; then
  docker run --rm -t -v "$OSRM_DATA_DIR:/data" "$OSRM_IMAGE" \
    osrm-extract -p /opt/car.lua /data/brazil-latest.osm.pbf
  docker run --rm -t -v "$OSRM_DATA_DIR:/data" "$OSRM_IMAGE" \
    osrm-partition /data/brazil-latest.osrm
  docker run --rm -t -v "$OSRM_DATA_DIR:/data" "$OSRM_IMAGE" \
    osrm-customize /data/brazil-latest.osrm
fi

printf 'image=%s\npbf_url=%s\nprepared_at_utc=%s\nalgorithm=MLD\nprofile=driving\n' \
  "$OSRM_IMAGE" "$OSRM_PBF_URL" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  > "$OSRM_DATA_DIR/dataset-metadata.txt"

printf 'Mapa preparado em %s\n' "$OSRM_BASE"
