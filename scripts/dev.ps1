#Requires -Version 5.1
<#
.SYNOPSIS
  Project-K dev-container orchestrator (PowerShell).

.DESCRIPTION
  One command drives every environment. The .NET image is environment-agnostic
  (env chosen at runtime via ASPNETCORE_ENVIRONMENT); the Angular config is baked
  per environment. SQL + Azurite run once as shared tooling; each environment's
  app stack attaches to the shared projectk-dev-net network.

  Images are tagged locally only and are never pushed to a registry.

.EXAMPLE
  ./scripts/dev.ps1 tools up
  ./scripts/dev.ps1 up dev
  ./scripts/dev.ps1 up selfhost --build
  ./scripts/dev.ps1 watch dev
  ./scripts/dev.ps1 down e2e -v

  env = dev | e2e | demo | selfhost | tailscale | staging | prod
#>
[CmdletBinding()]
param(
  [Parameter(Position = 0)][string]$Command,
  [Parameter(Position = 1)][string]$Arg1,
  [Parameter(Position = 2, ValueFromRemainingArguments = $true)][string[]]$Rest
)

# 'Continue', not 'Stop': this is a native-command (docker) orchestrator. Under 'Stop',
# a docker call writing to stderr (e.g. an expected "not found") becomes a terminating
# error in Windows PowerShell 5.1. Failures are handled explicitly via $LASTEXITCODE + Die.
$ErrorActionPreference = 'Continue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dockerDir = Join-Path $repoRoot 'docker'
$envDir = Join-Path $dockerDir 'env'
$network = 'projectk-dev-net'

$toolsFile = Join-Path $dockerDir 'compose.tools.yml'
$appFile = Join-Path $dockerDir 'compose.app.yml'
$overrideFile = Join-Path $dockerDir 'compose.dev.override.yml'

$validEnvs = @('dev', 'e2e', 'demo', 'selfhost', 'tailscale', 'staging', 'prod')

function Die($msg) { Write-Error $msg; exit 1 }

function Require-Docker {
  if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { Die 'docker is not installed or not on PATH.' }
  # `docker compose version` succeeds cleanly when v2 is present; only inspect the exit code.
  & docker compose version | Out-Null
  if ($LASTEXITCODE -ne 0) { Die 'docker compose v2 is required.' }
}

function Ensure-Network {
  # Use `network ls` (exits 0 with empty output when absent) rather than `network inspect`
  # (writes to stderr + non-zero exit when absent, which is fatal under -ErrorActionPreference Stop).
  $existing = & docker network ls --filter "name=^$network$" --format '{{.Name}}'
  if ($existing -notcontains $network) {
    & docker network create $network | Out-Null
    if ($LASTEXITCODE -ne 0) { Die "Failed to create docker network '$network'. Is Docker Desktop running?" }
  }
}

function Resolve-EnvFile([string]$env) {
  if ($validEnvs -notcontains $env) { Die "Unknown env '$env'. Valid: $($validEnvs -join ', ')" }
  $file = Join-Path $envDir "$env.env"
  if (Test-Path $file) { return $file }
  $example = Join-Path $envDir "$env.env.example"
  if (Test-Path $example) {
    Die "docker/env/$env.env not found. Copy docker/env/$env.env.example to docker/env/$env.env and set secrets."
  }
  Die "docker/env/$env.env not found."
}

function Invoke-AppCompose {
  param(
    [string]$env,
    [switch]$WithOverride,
    [string[]]$ComposeArgs
  )
  $envFile = Resolve-EnvFile $env
  $files = @('-f', $appFile)
  if ($WithOverride) { $files += @('-f', $overrideFile) }

  # The docs site rides along only where the env file gives it a port (profile "docs").
  $profiles = @()
  if (Select-String -Path $envFile -Pattern '^PROJECTK_DOCS_PORT=\d+' -Quiet) { $profiles = @('--profile', 'docs') }

  $prev = $env:COMPOSE_PROJECT_NAME
  $env:COMPOSE_PROJECT_NAME = "projectk-$env"
  try {
    & docker compose --env-file $envFile @profiles @files @ComposeArgs
    if ($LASTEXITCODE -ne 0) { Die "docker compose exited with code $LASTEXITCODE" }
  }
  finally {
    $env:COMPOSE_PROJECT_NAME = $prev
  }
}

function Cmd-Tools {
  param([string]$Action, [string[]]$Extra)
  if (-not $Action) { $Action = 'up' }
  Ensure-Network
  switch ($Action) {
    'up'   { & docker compose -f $toolsFile up -d }
    'down' { & docker compose -f $toolsFile down @Extra }
    'logs' { & docker compose -f $toolsFile logs -f @Extra }
    'ps'   { & docker compose -f $toolsFile ps }
    default { Die "Unknown tools action '$Action' (up|down|logs|ps)." }
  }
}

function Cmd-Up {
  param([string]$env, [string[]]$Extra)
  if (-not $env) { Die 'Usage: dev.ps1 up <env> [--build|--pull]' }
  Ensure-Network
  Cmd-Tools -Action 'up' | Out-Null
  $option = if ($Extra) { $Extra[0] } else { '' }
  switch ($option) {
    '--build' { Invoke-AppCompose -env $env -ComposeArgs @('up', '-d', '--build') }
    '--pull'  { Invoke-AppCompose -env $env -ComposeArgs @('up', '-d', '--pull', 'always') }
    ''        { Invoke-AppCompose -env $env -ComposeArgs @('up', '-d') }
    default   { Die "Unknown option '$option' (--build|--pull)." }
  }
  Write-Output "Environment '$env' is up. Web: check PROJECTK_WEB_PORT in docker/env/$env.env"
}

function Cmd-Watch {
  param([string]$env)
  if (-not $env) { Die 'Usage: dev.ps1 watch <env>' }
  Ensure-Network
  Cmd-Tools -Action 'up' | Out-Null
  Invoke-AppCompose -env $env -WithOverride -ComposeArgs @('up')
}

Require-Docker

switch ($Command) {
  'tools' { Cmd-Tools -Action $Arg1 -Extra $Rest }
  'up'    { Cmd-Up -env $Arg1 -Extra $Rest }
  'watch' { Cmd-Watch -env $Arg1 }
  'build' { Invoke-AppCompose -env $Arg1 -ComposeArgs @('build') }
  'pull'  { Invoke-AppCompose -env $Arg1 -ComposeArgs @('pull') }
  'down'  { Invoke-AppCompose -env $Arg1 -ComposeArgs (@('down') + $Rest) }
  'logs'  { Invoke-AppCompose -env $Arg1 -ComposeArgs (@('logs', '-f') + $Rest) }
  'ps'    { Invoke-AppCompose -env $Arg1 -ComposeArgs @('ps') }
  { $_ -in @('', '-h', '--help', 'help', $null) } {
    Get-Help $PSCommandPath -Detailed
  }
  default { Die "Unknown command '$Command'. Run 'dev.ps1 --help'." }
}
