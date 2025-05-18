#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

docker compose down --remove-orphans -v
docker compose up -d
docker compose run --rm migration

echo "Building GeoProfiles project…"
dotnet build GeoProfiles/GeoProfiles.csproj -c Release

echo "Scaffolding DB context for GeoProfiles…"
pushd GeoProfiles >/dev/null

dotnet ef dbcontext scaffold \
  "Name=ConnectionStrings:DefaultConnection" \
  Npgsql.EntityFrameworkCore.PostgreSQL \
  --output-dir Model/Generated \
  --namespace "GeoProfiles.Model" \
  --context "GeoProfilesContext" \
  --context-namespace "GeoProfiles" \
  --no-onconfiguring \
  --no-pluralize \
  --no-build \
  --force

popd >/dev/null
echo "Scaffolding finished."
