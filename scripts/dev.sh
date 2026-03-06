#!/usr/bin/env bash
set -euo pipefail

ACTION="${1:-up}"
COMPOSE_FILE="docker-compose.dev.yml"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$SCRIPT_DIR/../docker"

if [[ ! -f "$DOCKER_DIR/$COMPOSE_FILE" ]]; then
  echo "Compose file not found: $DOCKER_DIR/$COMPOSE_FILE" >&2
  exit 1
fi

cd "$DOCKER_DIR"

case "$ACTION" in
  up)
    docker compose -f "$COMPOSE_FILE" up
    ;;
  down)
    docker compose -f "$COMPOSE_FILE" down
    ;;
  logs)
    docker compose -f "$COMPOSE_FILE" logs -f
    ;;
  restart)
    docker compose -f "$COMPOSE_FILE" down
    docker compose -f "$COMPOSE_FILE" up
    ;;
  *)
    echo "Usage: ./scripts/dev.sh [up|down|logs|restart]" >&2
    exit 1
    ;;
esac
