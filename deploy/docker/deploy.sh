#!/usr/bin/env bash
set -Eeuo pipefail

usage() {
  cat <<'EOF'
Usage:
  bash deploy.sh [options] [service...]

Options:
  -p, --package PATH   Deployment package path.
                      Default: /opt/agentsprint-deploy/agentsprint-docker-deploy.tgz
  -r, --root PATH      Deployment root.
                      Default: /opt/agentsprint-deploy, or the parent of this script
                      when running from the docker directory.
  --skip-build         Skip docker compose build --no-cache.
  --no-clean           Extract over the existing docker directory instead of replacing it.
  -h, --help           Show this help.

Examples:
  bash /opt/agentsprint-deploy/deploy.sh
  bash /opt/agentsprint-deploy/deploy.sh admin
  bash /opt/agentsprint-deploy/deploy.sh --package /opt/agentsprint-deploy/agentsprint-docker-deploy.tgz
EOF
}

log() {
  printf '[%s] %s\n' "$(date '+%F %T')" "$*"
}

fail() {
  printf '[ERROR] %s\n' "$*" >&2
  exit 1
}

SCRIPT_SOURCE="${BASH_SOURCE[0]}"
SCRIPT_DIR="$(cd "$(dirname "$SCRIPT_SOURCE")" && pwd -P)"

# Re-execute from /tmp so this script can safely replace the docker directory
# that may contain the original deploy.sh while deployment is running.
if [ "${AGENTSPRINT_DEPLOY_REEXECED:-0}" != "1" ]; then
  tmp_script="$(mktemp /tmp/agentsprint-deploy.XXXXXX.sh)"
  cp "$SCRIPT_SOURCE" "$tmp_script"
  chmod +x "$tmp_script"
  export AGENTSPRINT_DEPLOY_REEXECED=1
  export AGENTSPRINT_DEPLOY_ORIGINAL_DIR="$SCRIPT_DIR"
  export AGENTSPRINT_DEPLOY_TMP_SCRIPT="$tmp_script"
  exec "$tmp_script" "$@"
fi

trap 'rm -f "${AGENTSPRINT_DEPLOY_TMP_SCRIPT:-}" 2>/dev/null || true' EXIT

ORIGINAL_DIR="${AGENTSPRINT_DEPLOY_ORIGINAL_DIR:-$SCRIPT_DIR}"
if [ -z "${AGENTSPRINT_DEPLOY_ROOT:-}" ]; then
  if [ "$(basename "$ORIGINAL_DIR")" = "docker" ] && [ -f "$ORIGINAL_DIR/docker-compose.yml" ]; then
    DEPLOY_ROOT="$(cd "$ORIGINAL_DIR/.." && pwd -P)"
  else
    DEPLOY_ROOT="/opt/agentsprint-deploy"
  fi
else
  DEPLOY_ROOT="$AGENTSPRINT_DEPLOY_ROOT"
fi

PACKAGE="${AGENTSPRINT_PACKAGE:-$DEPLOY_ROOT/agentsprint-docker-deploy.tgz}"
SKIP_BUILD=0
CLEAN_EXTRACT=1
SERVICES=()
BACKUP_DIR=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    -p|--package)
      [ "$#" -ge 2 ] || fail "$1 requires a path"
      PACKAGE="$2"
      shift 2
      ;;
    -r|--root)
      [ "$#" -ge 2 ] || fail "$1 requires a path"
      DEPLOY_ROOT="$2"
      shift 2
      ;;
    --skip-build)
      SKIP_BUILD=1
      shift
      ;;
    --no-clean)
      CLEAN_EXTRACT=0
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --)
      shift
      while [ "$#" -gt 0 ]; do
        SERVICES+=("$1")
        shift
      done
      ;;
    -*)
      fail "Unknown option: $1"
      ;;
    *)
      SERVICES+=("$1")
      shift
      ;;
  esac
done

DOCKER_DIR="$DEPLOY_ROOT/docker"
ENV_FILE="$DOCKER_DIR/.env"

on_error() {
  local line="$1"
  printf '[ERROR] Deployment failed near line %s.\n' "$line" >&2
  if [ -n "$BACKUP_DIR" ]; then
    printf '[ERROR] Backup directory: %s\n' "$BACKUP_DIR" >&2
    printf '[ERROR] Manual rollback example:\n' >&2
    printf '  cd %q && mv docker docker.failed.$(date +%%Y%%m%%d%%H%%M%%S) && cp -a %q docker && cd docker && docker compose build --no-cache && docker compose up -d\n' "$DEPLOY_ROOT" "$BACKUP_DIR" >&2
  fi
}
trap 'on_error "$LINENO"' ERR

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "Required command not found: $1"
}

compose() {
  docker compose "$@"
}

wait_for_http() {
  local name="$1"
  local url="$2"
  local expected="$3"
  local attempts="${4:-20}"
  local code=""

  for _ in $(seq 1 "$attempts"); do
    code="$(curl -sS -o /tmp/agentsprint-health.out -w '%{http_code}' --max-time 10 "$url" || true)"
    if [ "$expected" = "any" ] && [ "$code" != "000" ]; then
      log "$name health OK: HTTP $code"
      return 0
    fi
    if [ "$code" = "$expected" ]; then
      log "$name health OK: HTTP $code"
      return 0
    fi
    sleep 2
  done

  printf '[ERROR] %s health failed. Last HTTP code: %s\n' "$name" "${code:-none}" >&2
  if [ -s /tmp/agentsprint-health.out ]; then
    sed -n '1,40p' /tmp/agentsprint-health.out >&2 || true
  fi
  return 1
}

run_health_checks() {
  log "Container status"
  docker ps --filter name=agentsprint --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}'

  log "Image summary"
  docker images --format 'table {{.Repository}}\t{{.ID}}\t{{.Size}}' | grep agentsprint || true

  wait_for_http "admin" "http://127.0.0.1:5999" "200"
  wait_for_http "api" "http://127.0.0.1:5000" "any"
  wait_for_http "mcp" "http://127.0.0.1:5010/mcp" "200"

  if ! grep -q 'agentsprint-mcp' /tmp/agentsprint-health.out 2>/dev/null; then
    fail "MCP /mcp response did not contain agentsprint-mcp"
  fi

  log "Recent API startup logs"
  docker logs --tail 80 agentsprint-api 2>&1 \
    | sed -E 's/(password=)[^;]*/\1***REDACTED***/Ig' \
    | grep -E 'Now listening|Application started|Hosting failed|Unhandled|Access denied|SELECT EXISTS' || true
}

require_command docker
require_command tar
require_command curl
require_command cp
require_command date

[ -f "$PACKAGE" ] || fail "Deployment package not found: $PACKAGE"
mkdir -p "$DEPLOY_ROOT"

log "Deploy root: $DEPLOY_ROOT"
log "Package: $PACKAGE"

env_backup=""
if [ -f "$ENV_FILE" ]; then
  env_backup="$(mktemp /tmp/agentsprint-env.XXXXXX)"
  cp -p "$ENV_FILE" "$env_backup"
fi

if [ -d "$DOCKER_DIR" ]; then
  BACKUP_DIR="$DEPLOY_ROOT/docker.bak.$(date +%Y%m%d%H%M%S)"
  log "Backup current docker directory to $BACKUP_DIR"
  cp -a "$DOCKER_DIR" "$BACKUP_DIR"
fi

if [ "$CLEAN_EXTRACT" -eq 1 ] && [ -d "$DOCKER_DIR" ]; then
  case "$DOCKER_DIR" in
    "$DEPLOY_ROOT"/docker) ;;
    *) fail "Refusing to remove unexpected docker directory: $DOCKER_DIR" ;;
  esac
  log "Remove current docker directory before extraction"
  rm -rf "$DOCKER_DIR"
fi

log "Extract package"
tar -xzf "$PACKAGE" -C "$DEPLOY_ROOT"
[ -d "$DOCKER_DIR" ] || fail "Package did not create docker directory: $DOCKER_DIR"

if [ -f "$DOCKER_DIR/deploy.sh" ]; then
  chmod 755 "$DOCKER_DIR/deploy.sh" || true
  cp "$DOCKER_DIR/deploy.sh" "$DEPLOY_ROOT/deploy.sh"
  chmod 755 "$DEPLOY_ROOT/deploy.sh" || true
fi

if [ -n "$env_backup" ]; then
  log "Restore remote .env"
  cp -p "$env_backup" "$ENV_FILE"
  chmod 600 "$ENV_FILE" || true
fi

if grep -q 'AGENTSPRINT_DB_PASSWORD' "$DOCKER_DIR/docker-compose.yml"; then
  [ -f "$ENV_FILE" ] || fail "$ENV_FILE is required because docker-compose.yml uses AGENTSPRINT_DB_PASSWORD"
  if ! grep -q '^AGENTSPRINT_DB_PASSWORD=' "$ENV_FILE"; then
    fail "$ENV_FILE must define AGENTSPRINT_DB_PASSWORD"
  fi
  chmod 600 "$ENV_FILE" || true
fi

log "Artifact sizes"
du -sh "$DOCKER_DIR"/artifacts/* 2>/dev/null || true

cd "$DOCKER_DIR"
log "Validate docker compose config"
compose config --quiet

if [ "$SKIP_BUILD" -eq 0 ]; then
  if [ "${#SERVICES[@]}" -gt 0 ]; then
    log "Build services with --no-cache: ${SERVICES[*]}"
    compose build --no-cache "${SERVICES[@]}"
  else
    log "Build all services with --no-cache"
    compose build --no-cache
  fi
fi

if [ "${#SERVICES[@]}" -gt 0 ]; then
  log "Start services: ${SERVICES[*]}"
  compose up -d "${SERVICES[@]}"
else
  log "Start all services"
  compose up -d
fi

run_health_checks

log "Deployment finished"
if [ -n "$BACKUP_DIR" ]; then
  log "Backup directory: $BACKUP_DIR"
fi
