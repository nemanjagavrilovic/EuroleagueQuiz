#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_DIR"

NODE_BUILD_MEMORY_MB="${NODE_BUILD_MEMORY_MB:-768}"
BUILD_CACHE_MAX_AGE="${BUILD_CACHE_MAX_AGE:-168h}"
PRUNE_AFTER_DEPLOY="${PRUNE_AFTER_DEPLOY:-1}"
PULL_BASE_IMAGES="${PULL_BASE_IMAGES:-1}"
UPDATE_NGINX="${UPDATE_NGINX:-1}"
NGINX_SOURCE="${NGINX_SOURCE:-$REPO_DIR/docker/pickandroll.io.nginx.conf}"
NGINX_TARGET="${NGINX_TARGET:-/etc/nginx/sites-available/pickandroll.io}"
NGINX_ENABLED="${NGINX_ENABLED:-/etc/nginx/sites-enabled/pickandroll.io}"
export COMPOSE_PARALLEL_LIMIT=1

log() { printf '\n\033[1;36m==> %s\033[0m\n' "$1"; }
fail() { printf '\n\033[1;31mDeployment failed: %s\033[0m\n' "$1" >&2; exit 1; }
trap 'fail "command failed on line $LINENO"' ERR

command -v git >/dev/null 2>&1 || fail "git is not installed."
command -v docker >/dev/null 2>&1 || fail "Docker is not installed."
docker compose version >/dev/null 2>&1 || fail "Docker Compose v2 is not available."
[[ -f .env ]] || fail ".env was not found in $REPO_DIR."

if command -v flock >/dev/null 2>&1; then
  exec 9>"${TMPDIR:-/tmp}/pickandroll-deploy.lock"
  flock -n 9 || fail "another deployment is already running."
fi

if ! git diff --quiet || ! git diff --cached --quiet; then
  fail "the production checkout has uncommitted changes. Commit or restore them before deploying."
fi

BRANCH="${DEPLOY_BRANCH:-$(git branch --show-current)}"
[[ -n "$BRANCH" ]] || fail "the repository is in detached HEAD state. Set DEPLOY_BRANCH explicitly."

log "Pulling the latest $BRANCH code"
git fetch --prune origin "$BRANCH"
git pull --ff-only origin "$BRANCH"
REVISION="$(git rev-parse --short HEAD)"

log "Docker disk usage before deployment"
docker system df

PULL_ARGS=()
if [[ "$PULL_BASE_IMAGES" == "1" ]]; then PULL_ARGS+=(--pull); fi

# Build one service at a time. Building both Compose services in parallel can
# exhaust RAM because the Angular and .NET SDK stages run simultaneously.
log "Building API image for $REVISION"
docker compose build "${PULL_ARGS[@]}" api

log "Building web image with a ${NODE_BUILD_MEMORY_MB} MB Node heap limit"
docker compose build "${PULL_ARGS[@]}" \
  --build-arg "NODE_OPTIONS=--max-old-space-size=${NODE_BUILD_MEMORY_MB}" \
  web

# The runtime API image intentionally contains only the ASP.NET runtime, so EF
# migrations run in a short-lived SDK container. NuGet packages are cached in
# an ignored host folder; no migration image is retained after this command.
log "Applying pending EF Core migrations"
mkdir -p .deploy-cache/nuget
docker run --rm \
  --env-file "$REPO_DIR/.env" \
  --user "$(id -u):$(id -g)" \
  --env DOTNET_CLI_HOME=/tmp/dotnet-cli \
  --env NUGET_PACKAGES=/cache/nuget \
  --mount "type=bind,src=$REPO_DIR,dst=/src" \
  --mount "type=bind,src=$REPO_DIR/.deploy-cache/nuget,dst=/cache/nuget" \
  --workdir /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  bash -lc '
    set -euo pipefail
    if [[ -z "${POSTGRES_CONNECTION_STRING:-}" ]]; then
      echo "POSTGRES_CONNECTION_STRING is missing from .env." >&2
      exit 1
    fi
    export ConnectionStrings__Postgres="$POSTGRES_CONNECTION_STRING"
    dotnet tool install --tool-path /tmp/dotnet-tools dotnet-ef --version 8.0.11 >/dev/null
    dotnet restore EuroleagueQuiz.Api/EuroleagueQuiz.Api.csproj
    /tmp/dotnet-tools/dotnet-ef database update \
      --project EuroleagueQuiz.Infrastructure \
      --startup-project EuroleagueQuiz.Api \
      --configuration Release
  '

log "Starting the new containers"
docker compose up -d --no-build --remove-orphans
sleep 5

for service in $(docker compose config --services); do
  container_id="$(docker compose ps -q "$service")"
  [[ -n "$container_id" ]] || fail "$service did not create a container."
  [[ "$(docker inspect --format '{{.State.Running}}' "$container_id")" == "true" ]] || {
    docker compose logs --tail=100 "$service"
    fail "$service is not running."
  }
done

docker compose ps

if [[ "$UPDATE_NGINX" == "1" ]]; then
  log "Updating and validating host Nginx configuration"
  [[ -f "$NGINX_SOURCE" ]] || fail "Nginx source configuration was not found at $NGINX_SOURCE."

  if [[ "$EUID" -eq 0 ]]; then
    SUDO=()
  else
    command -v sudo >/dev/null 2>&1 || fail "sudo is required to update the host Nginx configuration."
    SUDO=(sudo)
  fi

  NGINX_BIN="${NGINX_BIN:-/usr/sbin/nginx}"
  [[ -x "$NGINX_BIN" ]] || fail "Nginx was not found at $NGINX_BIN. Set NGINX_BIN if it is installed elsewhere."
  command -v systemctl >/dev/null 2>&1 || fail "systemctl is required to reload Nginx."

  nginx_backup="$(mktemp)"
  had_nginx_config=0
  created_nginx_link=0
  if "${SUDO[@]}" test -f "$NGINX_TARGET"; then
    "${SUDO[@]}" cat "$NGINX_TARGET" > "$nginx_backup"
    had_nginx_config=1
  fi

  "${SUDO[@]}" mkdir -p "$(dirname "$NGINX_TARGET")" "$(dirname "$NGINX_ENABLED")" /var/www/certbot
  "${SUDO[@]}" install -m 0644 "$NGINX_SOURCE" "$NGINX_TARGET"
  if ! "${SUDO[@]}" test -e "$NGINX_ENABLED" && ! "${SUDO[@]}" test -L "$NGINX_ENABLED"; then
    "${SUDO[@]}" ln -s "$NGINX_TARGET" "$NGINX_ENABLED"
    created_nginx_link=1
  fi

  if ! "${SUDO[@]}" "$NGINX_BIN" -t; then
    printf '\nNginx validation failed; restoring the previous configuration.\n' >&2
    if [[ "$had_nginx_config" == "1" ]]; then
      "${SUDO[@]}" install -m 0644 "$nginx_backup" "$NGINX_TARGET"
    else
      "${SUDO[@]}" rm -f "$NGINX_TARGET"
    fi
    if [[ "$created_nginx_link" == "1" ]]; then "${SUDO[@]}" rm -f "$NGINX_ENABLED"; fi
    "${SUDO[@]}" "$NGINX_BIN" -t || true
    rm -f "$nginx_backup"
    fail "the new Nginx configuration was rejected; the previous configuration was restored."
  fi

  "${SUDO[@]}" systemctl reload nginx
  rm -f "$nginx_backup"
fi

if [[ "$PRUNE_AFTER_DEPLOY" == "1" ]]; then
  log "Removing superseded images and BuildKit cache older than $BUILD_CACHE_MAX_AGE"
  # Without -a, image prune removes only dangling images left by this and other
  # completed builds. It does not remove images used by running containers.
  docker image prune --force
  docker builder prune --force --filter "until=$BUILD_CACHE_MAX_AGE"
fi

log "Docker disk usage after deployment"
docker system df
printf '\n\033[1;32mDeployment %s completed successfully.\033[0m\n' "$REVISION"
