#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

docker compose down --remove-orphans -v

docker compose build app postgres-db

docker compose up -d app postgres-db

docker compose run --rm migration
