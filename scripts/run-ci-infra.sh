#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

docker compose down --remove-orphans -v
docker compose up -d
docker compose run --rm migration

