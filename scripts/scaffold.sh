#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

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