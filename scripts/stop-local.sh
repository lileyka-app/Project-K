#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
run_root="$repo_root/.tmp/local-run"

if [[ ! -d "$run_root" ]]; then
  echo "No local run state found."
  exit 0
fi

for pid_file in "$run_root"/*.pid; do
  [[ -e "$pid_file" ]] || continue
  name="$(basename "$pid_file" .pid)"
  pid="$(cat "$pid_file" || true)"

  if [[ -n "$pid" ]] && kill -0 "$pid" >/dev/null 2>&1; then
    echo "Stopping $name (PID $pid)"
    kill "$pid" >/dev/null 2>&1 || true
  else
    echo "$name is not running."
  fi

  rm -f "$pid_file"
done

echo "Local Project-K stack stopped."
