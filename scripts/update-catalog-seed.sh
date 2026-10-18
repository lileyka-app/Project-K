#!/usr/bin/env bash
# Refresh the catalogue copy built into the API (ProjectK.Infrastructure/Services/Catalog/Seed).
# The API serves it only when the catalogue service does not answer and there is no copy on disk,
# so it just has to be recent enough; rerun this before a release.
#
#   bash scripts/update-catalog-seed.sh
#   bash scripts/update-catalog-seed.sh https://catalog.example.dev
set -euo pipefail

base_url="${1:-https://catalog.lileyka.app}"
target="$(cd "$(dirname "$0")/.." && pwd)/Backend/ProjectK.Backend/ProjectK.Infrastructure/Services/Catalog/Seed/catalog.json"
temp="$(mktemp)"
trap 'rm -f "$temp"' EXIT

curl -fsS "${base_url%/}/v1/catalog" -o "$temp"

# CatalogClient reads schema 2 only; a copy it cannot parse would leave the fallback empty.
if ! grep -q '"schemaVersion":2[,}]' "$temp"; then
  echo "The catalogue at $base_url does not speak schema 2; the copy is left as it was." >&2
  exit 1
fi

mv "$temp" "$target"
trap - EXIT
echo "Saved $(wc -c < "$target") bytes to $target"
