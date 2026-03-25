#!/bin/bash
set -euo pipefail

# =============================================================================
# Application Seeding Script
# Runs idempotent bootstrap data initialization (roles, permissions, settings,
# and initial SuperAdmin) using the API image, without starting the HTTP server.
#
# Usage: bash scripts/seed.sh
# =============================================================================

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.deploy.yml}"

# Load environment variables from Docker-style .env without shell-evaluating values.
if [ -f .env ]; then
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line%$'\r'}"

    case "$line" in
      ''|\#*)
        continue
        ;;
    esac

    key="${line%%=*}"
    value="${line#*=}"
    export "$key=$value"
  done < .env
fi

# Fallback for environments where the full connection string is not explicitly set.
if [ -z "${ConnectionStrings__DefaultConnection:-}" ] \
  && [ -n "${POSTGRES_DB:-}" ] \
  && [ -n "${POSTGRES_USER:-}" ] \
  && [ -n "${POSTGRES_PASSWORD:-}" ]; then
  ConnectionStrings__DefaultConnection="Host=postgres;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
fi

echo "=== Application Seeding Script ==="

echo "[1/3] Ensuring postgres is running..."
docker compose -f "$COMPOSE_FILE" up -d postgres
echo "  Waiting for postgres to be ready..."
for i in $(seq 1 30); do
  if docker compose -f "$COMPOSE_FILE" exec -T postgres \
    pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB" > /dev/null 2>&1; then
    echo "  Postgres is ready."
    break
  fi
  if [ "$i" -eq 30 ]; then
    echo "ERROR: Postgres did not become ready after 30 attempts."
    exit 1
  fi
  sleep 2
done

echo "[2/3] Running application seeding..."
docker compose -f "$COMPOSE_FILE" run --rm --no-deps \
  -e ConnectionStrings__DefaultConnection="$ConnectionStrings__DefaultConnection" \
  api \
  --seed

echo "[3/3] Seeding completed successfully."
