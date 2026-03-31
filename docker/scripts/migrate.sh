#!/bin/bash
set -euo pipefail

# =============================================================================
# Database Migration Script
# Runs backup + EF Core migration bundle before API restart.
# Called automatically by deploy.yml or manually on the VPS.
#
# Usage: bash scripts/migrate.sh
# =============================================================================

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.deploy.yml}"
BACKUP_DIR="${BACKUP_DIR:-/opt/seed-app/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"

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

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/seeddb_${TIMESTAMP}.sql.gz"

echo "=== Database Migration Script ==="

# Step 1: Ensure postgres is running
echo "[1/5] Ensuring postgres is running..."
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

# Step 2: Backup the database (skip on first deploy when DB has no tables)
echo "[2/5] Backing up database..."
HAS_TABLES=$(docker compose -f "$COMPOSE_FILE" exec -T postgres \
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc \
  "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public';" 2>/dev/null || echo "0")

if [ "$HAS_TABLES" -gt 0 ] 2>/dev/null; then
  mkdir -p "$BACKUP_DIR"
  docker compose -f "$COMPOSE_FILE" exec -T postgres \
    pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
    --no-owner --no-privileges \
    | gzip > "$BACKUP_FILE"

  if [ -s "$BACKUP_FILE" ]; then
    BACKUP_SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
    echo "  Backup saved: $BACKUP_FILE ($BACKUP_SIZE)"
  else
    echo "  WARNING: Backup file is empty, removing it."
    rm -f "$BACKUP_FILE"
  fi
else
  echo "  No tables found (first deploy). Skipping backup."
fi

# Step 3: Apply migrations using EF Core bundle
echo "[3/5] Applying migrations..."
docker compose -f "$COMPOSE_FILE" run --rm \
  -e ConnectionStrings__DefaultConnection="$ConnectionStrings__DefaultConnection" \
  -e DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/.net \
  --entrypoint /app/efbundle \
  api \
  --connection "$ConnectionStrings__DefaultConnection"

# Step 4: Verify database health
echo "[4/5] Verifying database health..."
docker compose -f "$COMPOSE_FILE" exec -T postgres \
  pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"

# Step 5: Clean up old backups
echo "[5/5] Cleaning up backups older than ${RETENTION_DAYS} days..."
find "$BACKUP_DIR" -name "seeddb_*.sql.gz" -mtime +"$RETENTION_DAYS" -delete 2>/dev/null || true

echo "=== Migration complete ==="
