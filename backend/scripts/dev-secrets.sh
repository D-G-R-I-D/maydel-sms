#!/usr/bin/env bash
# Stores local secrets for `dotnet run` in user-secrets (outside the repo). Run once per machine:
#   bash backend/scripts/dev-secrets.sh
# Reads POSTGRES_PASSWORD / SEED_ADMIN_* from ../../.env when present, so it matches docker compose.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
api="$root/backend/src/Maydel.Api"

if [ -f "$root/.env" ]; then set -a; . "$root/.env"; set +a; fi
db_password="${POSTGRES_PASSWORD:-maydel_dev_only}"
signing_key="$(head -c 48 /dev/urandom | base64 | tr -d '\n')"

dotnet user-secrets set --project "$api" "ConnectionStrings:Default" "Host=localhost;Port=${POSTGRES_PORT:-5432};Database=maydel;Username=maydel;Password=$db_password" >/dev/null
dotnet user-secrets set --project "$api" "Jwt:SigningKey" "$signing_key" >/dev/null
dotnet user-secrets set --project "$api" "Seed:AdminEmail" "${SEED_ADMIN_EMAIL:-admin@maydel.test}" >/dev/null
dotnet user-secrets set --project "$api" "Seed:AdminPassword" "${SEED_ADMIN_PASSWORD:-Demo#Pass2026}" >/dev/null
if [ -n "${GOOGLE_CLIENT_ID:-}" ]; then
  dotnet user-secrets set --project "$api" "Authentication:Google:ClientId" "$GOOGLE_CLIENT_ID" >/dev/null
fi
echo "User secrets set for Maydel.Api. Start Postgres with 'docker compose up -d db', then 'dotnet run --project backend/src/Maydel.Api'."
