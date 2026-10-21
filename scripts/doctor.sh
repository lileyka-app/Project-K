#!/usr/bin/env bash
set -euo pipefail

skip_azurite=false
skip_sql=false

for arg in "$@"; do
  case "$arg" in
    --skip-azurite) skip_azurite=true ;;
    --skip-sql) skip_sql=true ;;
    *) ;;
  esac
done

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
frontend_path="$repo_root/Frontend/projectk-frontend"
failed=0

check() {
  local name="$1"
  local status="$2"
  local detail="$3"

  if [[ "$status" = "0" ]]; then
    printf '[OK] %s: %s\n' "$name" "$detail"
  else
    printf '[FAIL] %s: %s\n' "$name" "$detail"
    failed=1
  fi
}

command_exists() {
  local name="$1"
  command -v "$name" >/dev/null 2>&1
}

port_available() {
  local port="$1"
  if command_exists ss; then
    ! ss -ltn "( sport = :$port )" | grep -q ":$port"
  elif command_exists lsof; then
    ! lsof -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1
  else
    return 0
  fi
}

if command_exists dotnet; then check ".NET SDK" 0 "$(dotnet --version)"; else check ".NET SDK" 1 "Install .NET SDK 10.x"; fi
if command_exists node; then check "Node.js" 0 "$(node --version)"; else check "Node.js" 1 "Install Node.js 22.x"; fi
if command_exists npm; then check "npm" 0 "$(npm --version)"; else check "npm" 1 "Install npm with Node.js"; fi

if [[ -d "$frontend_path/node_modules" ]]; then
  check "Frontend dependencies" 0 "node_modules found"
else
  check "Frontend dependencies" 1 "Run npm install in Frontend/projectk-frontend"
fi

if [[ "$skip_azurite" = false ]]; then
  if command_exists azurite; then check "Azurite" 0 "$(azurite --version)"; else check "Azurite" 1 "Install with: npm install -g azurite"; fi
fi

if [[ "$skip_sql" = false ]]; then
  if command_exists sqlcmd; then check "SQL tooling" 0 "sqlcmd found"; else check "SQL tooling" 1 "Optional: install sqlcmd, or ensure appsettings connection string is valid"; fi
fi

for port in 4200 5205 10000 10001 10002; do
  if port_available "$port"; then check "Port $port" 0 "available"; else check "Port $port" 1 "already in use"; fi
done

if [[ "$failed" -ne 0 ]]; then
  echo
  echo "Some checks failed. Fix them before running scripts/start-local.sh."
  exit 1
fi

echo
echo "All local startup checks passed."
