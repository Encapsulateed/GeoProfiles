#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

docker compose down --remove-orphans -v

docker compose build app postgres-db

docker compose up -d app postgres-db

docker compose build app mock-server

docker compose up -d app mock-server

docker compose run --rm migration
