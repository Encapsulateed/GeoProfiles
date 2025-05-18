#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

docker compose down --remove-orphans -v
docker compose up -d
docker compose run --rm migration

echo "Scaffolding db context for GeoProfiles..."

dotnet ef dbcontext scaffold \
  "Name=ConnectionStrings:DefaultConnection" \
  Npgsql.EntityFrameworkCore.PostgreSQL \
  --project GeoProfiles/GeoProfiles.csproj \
  --output-dir Model/Generated \
  --namespace "GeoProfiles.Model" \
  --context "GeoProfilesContext" \
  --context-namespace "GeoProfiles" \
  --no-onconfiguring \
  --no-pluralize \
  --no-build \
  --force

echo "Scaffolding db context finished."
