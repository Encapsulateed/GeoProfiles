#!/usr/bin/env bash
set -e

docker-compose down --remove-orphans -v

docker-compose up -d

docker-compose run --rm migration

dotnet tool restore

echo "Scaffolding db context for GeoProfiles..."

cd GeoProfiles

dotnet ef dbcontext scaffold \
  "Name=ConnectionStrings:DefaultConnection" \
  Npgsql.EntityFrameworkCore.PostgreSQL \
  --project GeoProfiles.csproj \
  --output-dir Model/Generated \
  --namespace "GeoProfiles.Model" \
  --context "GeoProfilesContext" \
  --context-namespace "GeoProfiles" \
  --no-onconfiguring \
  --no-pluralize \
  --no-build \
  --force

echo "Scaffolding db context finished."
