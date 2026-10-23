#!/usr/bin/env bash
# Project-K dev-container orchestrator.
#
# One command drives every environment. The .NET image is environment-agnostic
# (env chosen at runtime via ASPNETCORE_ENVIRONMENT); the Angular config is baked
# per environment. SQL + Azurite run once as shared tooling; each environment's
# app stack attaches to the shared projectk-dev-net network.
#
# Usage:
#   ./scripts/dev.sh tools up|down|logs                 Manage shared SQL + Azurite
#   ./scripts/dev.sh up        <env> [--build|--pull]   Start app stack (built image)
#   ./scripts/dev.sh watch     <env>                    Start app stack with hot-reload
#   ./scripts/dev.sh build     <env>                    Build images only (no run)
#   ./scripts/dev.sh pull      <env>                    Pull PROJECTK_*_IMAGE images
#   ./scripts/dev.sh down      <env> [-v]               Stop app stack
#   ./scripts/dev.sh logs      <env> [service]          Tail logs
#   ./scripts/dev.sh ps        <env>                    List stack containers
#
#   env = dev | e2e | demo | selfhost | tailscale | staging | prod
#
# Images are tagged locally only and are never pushed to a registry.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
docker_dir="$repo_root/docker"
env_dir="$docker_dir/env"
network="projectk-dev-net"

tools_file="$docker_dir/compose.tools.yml"
app_file="$docker_dir/compose.app.yml"
override_file="$docker_dir/compose.dev.override.yml"

valid_envs="dev e2e demo selfhost tailscale staging prod"

die() { echo "Error: $*" >&2; exit 1; }

require_docker() {
  command -v docker >/dev/null 2>&1 || die "docker is not installed or not on PATH."
  docker compose version >/dev/null 2>&1 || die "docker compose v2 is required."
}

ensure_network() {
  docker network inspect "$network" >/dev/null 2>&1 || docker network create "$network" >/dev/null
}

resolve_env_file() {
  local env="$1"
  case " $valid_envs " in *" $env "*) ;; *) die "Unknown env '$env'. Valid: $valid_envs" ;; esac

  local file="$env_dir/$env.env"
  if [[ -f "$file" ]]; then
    echo "$file"; return
  fi
  # staging/prod ship as .example — dev copies to <env>.env with real secrets.
  if [[ -f "$env_dir/$env.env.example" ]]; then
    die "docker/env/$env.env not found. Copy docker/env/$env.env.example to docker/env/$env.env and set secrets."
  fi
  die "docker/env/$env.env not found."
}

# app_compose <env> [--with-override] -> runs `docker compose` with proper flags
app_compose() {
  local env="$1"; shift
  local with_override="$1"; shift
  local env_file
  env_file="$(resolve_env_file "$env")"

  local files=(-f "$app_file")
  if [[ "$with_override" = "with-override" ]]; then
    files+=(-f "$override_file")
  fi

  # The docs site rides along only where the env file gives it a port (profile "docs").
  local profiles=()
  if grep -qE '^PROJECTK_DOCS_PORT=[0-9]+' "$env_file"; then
    profiles+=(--profile docs)
  fi

  COMPOSE_PROJECT_NAME="projectk-$env" \
    docker compose --env-file "$env_file" "${profiles[@]}" "${files[@]}" "$@"
}

cmd_tools() {
  local action="${1:-up}"
  ensure_network
  case "$action" in
    up)   docker compose -f "$tools_file" up -d ;;
    down) docker compose -f "$tools_file" down "${@:2}" ;;
    logs) docker compose -f "$tools_file" logs -f "${@:2}" ;;
    ps)   docker compose -f "$tools_file" ps ;;
    *)    die "Unknown tools action '$action' (up|down|logs|ps)." ;;
  esac
}

cmd_up() {
  local env="${1:-}"; [[ -n "$env" ]] || die "Usage: dev.sh up <env> [--build|--pull]"
  local extra="${2:-}"
  ensure_network
  cmd_tools up >/dev/null
  case "$extra" in
    --build) app_compose "$env" no-override up -d --build ;;
    --pull)  app_compose "$env" no-override up -d --pull always ;;
    "")      app_compose "$env" no-override up -d ;;
    *)       die "Unknown option '$extra' (--build|--pull)." ;;
  esac
  echo "Environment '$env' is up. Web: check PROJECTK_WEB_PORT in docker/env/$env.env"
}

cmd_watch() {
  local env="${1:-}"; [[ -n "$env" ]] || die "Usage: dev.sh watch <env>"
  ensure_network
  cmd_tools up >/dev/null
  # Hot-reload uses base SDK/Node images driven by command — never build here.
  app_compose "$env" with-override up
}

cmd_build() {
  local env="${1:-}"; [[ -n "$env" ]] || die "Usage: dev.sh build <env>"
  app_compose "$env" no-override build
}

cmd_pull() {
  local env="${1:-}"; [[ -n "$env" ]] || die "Usage: dev.sh pull <env>"
  app_compose "$env" no-override pull
}

cmd_down() {
  local env="${1:-}"; [[ -n "$env" ]] || die "Usage: dev.sh down <env> [-v]"
  app_compose "$env" no-override down "${@:2}"
}

cmd_logs() {
  local env="${1:-}"; [[ -n "$env" ]] || die "Usage: dev.sh logs <env> [service]"
  # Follow only at a terminal: in CI there is no one to press Ctrl+C, and a followed log never
  # ends — the e2e job sat on it until GitHub killed it six hours later.
  local follow=()
  [[ -t 1 ]] && follow=(-f)
  app_compose "$env" no-override logs ${follow[@]+"${follow[@]}"} "${@:2}"
}

cmd_ps() {
  local env="${1:-}"; [[ -n "$env" ]] || die "Usage: dev.sh ps <env>"
  app_compose "$env" no-override ps
}

main() {
  require_docker
  local command="${1:-}"; shift || true
  case "$command" in
    tools) cmd_tools "$@" ;;
    up)    cmd_up "$@" ;;
    watch) cmd_watch "$@" ;;
    build) cmd_build "$@" ;;
    pull)  cmd_pull "$@" ;;
    down)  cmd_down "$@" ;;
    logs)  cmd_logs "$@" ;;
    ps)    cmd_ps "$@" ;;
    ""|-h|--help|help)
      sed -n '2,30p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
      ;;
    *) die "Unknown command '$command'. Run 'dev.sh --help'." ;;
  esac
}

main "$@"
