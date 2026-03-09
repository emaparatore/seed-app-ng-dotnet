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

# Load environment variables
if [ -f .env ]; then
  set -a; source .env; set +a
fi

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/seeddb_${TIMESTAMP}.sql.gz"

echo "=== Database Migration Script ==="

# Step 1: Create backup directory
mkdir -p "$BACKUP_DIR"

# Step 2: Backup the database
echo "[1/4] Backing up database..."
docker compose -f "$COMPOSE_FILE" exec -T postgres \
  pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  --no-owner --no-privileges \
  | gzip > "$BACKUP_FILE"

if [ ! -s "$BACKUP_FILE" ]; then
  echo "ERROR: Backup file is empty. Aborting migration."
  rm -f "$BACKUP_FILE"
  exit 1
fi

BACKUP_SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
echo "  Backup saved: $BACKUP_FILE ($BACKUP_SIZE)"

# Step 3: Apply migrations using EF Core bundle
echo "[2/4] Applying migrations..."
docker compose -f "$COMPOSE_FILE" run --rm --no-deps \
  -e ConnectionStrings__DefaultConnection="$ConnectionStrings__DefaultConnection" \
  --entrypoint /app/efbundle \
  api \
  --connection "$ConnectionStrings__DefaultConnection"

# Step 4: Verify database health
echo "[3/4] Verifying database health..."
docker compose -f "$COMPOSE_FILE" exec -T postgres \
  pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"

# Step 5: Clean up old backups
echo "[4/4] Cleaning up backups older than ${RETENTION_DAYS} days..."
find "$BACKUP_DIR" -name "seeddb_*.sql.gz" -mtime +"$RETENTION_DAYS" -delete

echo "=== Migration complete ==="
