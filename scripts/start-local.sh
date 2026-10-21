#!/usr/bin/env bash
set -euo pipefail

skip_doctor=false
skip_azurite=false
open_browser=false
backend_profile="http"

for arg in "$@"; do
  case "$arg" in
    --skip-doctor) skip_doctor=true ;;
    --skip-azurite) skip_azurite=true ;;
    --open) open_browser=true ;;
    --backend-profile=*) backend_profile="${arg#*=}" ;;
    *) ;;
  esac
done

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
run_root="$repo_root/.tmp/local-run"
log_root="$run_root/logs"
backend_project="$repo_root/Backend/ProjectK.Backend/ProjectK.API/ProjectK.API.csproj"
frontend_path="$repo_root/Frontend/projectk-frontend"

mkdir -p "$log_root"

if [[ "$skip_doctor" = false ]]; then
  doctor_args=()
  if [[ "$skip_azurite" = true ]]; then doctor_args+=(--skip-azurite); fi
  "$repo_root/scripts/doctor.sh" "${doctor_args[@]}"
fi

start_process() {
  local name="$1"
  local workdir="$2"
  shift 2

  (
    cd "$workdir"
    "$@"
  ) >"$log_root/$name.log" 2>"$log_root/$name.err.log" &

  echo "$!" > "$run_root/$name.pid"
  echo "Started $name (PID $!)"
}

if [[ "$skip_azurite" = false ]]; then
  mkdir -p "$run_root/azurite"
  start_process "azurite" "$repo_root" azurite --silent --location "$run_root/azurite"
fi

start_process "backend" "$repo_root" env ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$backend_project" --launch-profile "$backend_profile"
start_process "frontend" "$frontend_path" npm start

echo
echo "Project-K local stack is starting."
echo "Frontend: http://localhost:4200"
echo "Backend:  http://localhost:5205"
echo "Swagger:  http://localhost:5205/swagger"
echo "Azurite:  http://127.0.0.1:10000/devstoreaccount1"
echo "Logs:     $log_root"
echo
echo "Stop with: ./scripts/stop-local.sh"

if [[ "$open_browser" = true ]]; then
  if command -v xdg-open >/dev/null 2>&1; then xdg-open http://localhost:4200 >/dev/null 2>&1 || true; elif command -v open >/dev/null 2>&1; then open http://localhost:4200 >/dev/null 2>&1 || true; fi
fi
