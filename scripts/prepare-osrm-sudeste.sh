#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OSRM_DIR="$ROOT_DIR/infra/osrm"
PBF_FILE="$OSRM_DIR/sudeste-latest.osm.pbf"
OSRM_FILE="$OSRM_DIR/sudeste-latest.osrm"
PBF_URL="https://download.geofabrik.de/south-america/brazil/sudeste-latest.osm.pbf"

mkdir -p "$OSRM_DIR"

if [[ ! -f "$PBF_FILE" ]]; then
  curl -L "$PBF_URL" -o "$PBF_FILE"
fi

if [[ ! -f "$OSRM_FILE" ]]; then
  docker run --rm -t -v "$OSRM_DIR:/data" osrm/osrm-backend:latest \
    osrm-extract -p /opt/car.lua /data/sudeste-latest.osm.pbf
  docker run --rm -t -v "$OSRM_DIR:/data" osrm/osrm-backend:latest \
    osrm-partition /data/sudeste-latest.osrm
  docker run --rm -t -v "$OSRM_DIR:/data" osrm/osrm-backend:latest \
    osrm-customize /data/sudeste-latest.osrm
fi
