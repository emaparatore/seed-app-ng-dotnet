#!/bin/bash
set -euo pipefail

# =============================================================================
# Database Restore Script
# Restores the database from a pre-migration backup.
# For MANUAL use only — never called automatically.
#
# Usage: bash scripts/restore.sh <backup_file.sql.gz>
# =============================================================================

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.deploy.yml}"

if [ -z "${1:-}" ]; then
  echo "Usage: $0 <backup_file.sql.gz>"
  echo ""
  echo "Available backups:"
  ls -lh /opt/seed-app/backups/seeddb_*.sql.gz 2>/dev/null || echo "  No backups found."
  exit 1
fi

BACKUP_FILE="$1"
if [ ! -f "$BACKUP_FILE" ]; then
  echo "ERROR: Backup file not found: $BACKUP_FILE"
  exit 1
fi

if [ -f .env ]; then
  set -a; source .env; set +a
fi

echo "=== Database Restore ==="
echo "Backup: $BACKUP_FILE"
echo "WARNING: This will DROP and recreate the public schema."
read -p "Continue? (yes/no): " CONFIRM
if [ "$CONFIRM" != "yes" ]; then
  echo "Aborted."
  exit 0
fi

echo "[1/3] Stopping API..."
docker compose -f "$COMPOSE_FILE" stop api

echo "[2/3] Restoring database from $BACKUP_FILE..."
docker compose -f "$COMPOSE_FILE" exec -T postgres \
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"

gunzip -c "$BACKUP_FILE" | docker compose -f "$COMPOSE_FILE" exec -T postgres \
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"

echo "[3/3] Starting API..."
docker compose -f "$COMPOSE_FILE" up -d api

echo "=== Restore complete ==="
