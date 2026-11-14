<#
.SYNOPSIS
  Builds a self-contained EF Core migration bundle (efbundle) for the deploy step.

.DESCRIPTION
  In Production/Staging the app does NOT auto-migrate on startup (see
  API/Hosting/StartupTaskExtensions.cs -> ShouldRunMigrationsOnStartup) so multiple instances can boot without
  racing each other. Apply the schema out-of-band with this bundle before/at rollout:

    scripts/build-migration-bundle.ps1                # -> .\efbundle.exe
    .\efbundle.exe --connection "<connection-string>" # applies pending migrations

  The bundle is a standalone executable: the target host needs no .NET SDK. Copy an
  appsettings.json next to it if you rely on the connection string from configuration
  instead of --connection.

  Requires the EF CLI: dotnet tool install --global dotnet-ef
#>
param(
    [string]$Output
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $root "Backend/ProjectK.Backend"
if (-not $Output) { $Output = Join-Path $root "efbundle.exe" }

dotnet ef migrations bundle `
    --project (Join-Path $backend "ProjectK.Infrastructure/ProjectK.Infrastructure.csproj") `
    --startup-project (Join-Path $backend "ProjectK.API/ProjectK.API.csproj") `
    --configuration Release `
    --output $Output `
    --force

Write-Host "Migration bundle written to: $Output"
Write-Host "Apply with: `"$Output`" --connection `"<connection-string>`""
