#!/usr/bin/env bash
set -euo pipefail

# Builds a self-contained EF Core migration bundle (efbundle) for the deploy step.
#
# Why: in Production/Staging the app does NOT auto-migrate on startup (see
# API/Hosting/StartupTaskExtensions.cs -> ShouldRunMigrationsOnStartup) so multiple instances can boot without
# racing each other. Apply the schema out-of-band with this bundle before/at rollout:
#
#   scripts/build-migration-bundle.sh                 # -> ./efbundle (or efbundle.exe on Windows)
#   ./efbundle --connection "<connection-string>"     # applies pending migrations
#
# The bundle is a standalone executable: the target host needs no .NET SDK. Copy an
# appsettings.json next to it if you rely on the connection string from configuration
# instead of --connection.
#
# Requires the EF CLI: dotnet tool install --global dotnet-ef

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND="$ROOT/Backend/ProjectK.Backend"
OUTPUT="${1:-$ROOT/efbundle}"

dotnet ef migrations bundle \
  --project "$BACKEND/ProjectK.Infrastructure/ProjectK.Infrastructure.csproj" \
  --startup-project "$BACKEND/ProjectK.API/ProjectK.API.csproj" \
  --configuration Release \
  --output "$OUTPUT" \
  --force

echo "Migration bundle written to: $OUTPUT"
echo "Apply with: \"$OUTPUT\" --connection \"<connection-string>\""
