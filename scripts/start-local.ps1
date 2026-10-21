param(
    [switch]$SkipDoctor,
    [switch]$SkipAzurite,
    [switch]$Open,
    [string]$BackendProfile = "http"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$runRoot = Join-Path $repoRoot ".tmp/local-run"
$logRoot = Join-Path $runRoot "logs"
$backendProject = Join-Path $repoRoot "Backend/ProjectK.Backend/ProjectK.API/ProjectK.API.csproj"
$frontendPath = Join-Path $repoRoot "Frontend/projectk-frontend"

function Resolve-Tool {
    param([string[]]$Names)

    foreach ($name in $Names) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) {
            return $command.Source
        }
    }

    throw "Missing required command: $($Names -join ' or ')"
}

function Start-LoggedProcess {
    param(
        [string]$Name,
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory
    )

    $stdout = Join-Path $logRoot "$Name.log"
    $stderr = Join-Path $logRoot "$Name.err.log"
    New-Item -ItemType File -Force -Path $stdout, $stderr | Out-Null

    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -WindowStyle Hidden `
        -PassThru

    Set-Content -LiteralPath (Join-Path $runRoot "$Name.pid") -Value $process.Id
    Write-Output "Started $Name (PID $($process.Id))"
}

New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

if (-not $SkipDoctor) {
    & (Join-Path $PSScriptRoot "doctor.ps1") -SkipAzurite:$SkipAzurite
}

$dotnet = Resolve-Tool @("dotnet.exe", "dotnet")
$npm = Resolve-Tool @("npm.cmd", "npm")

if (-not $SkipAzurite) {
    $azurite = Resolve-Tool @("azurite.cmd", "azurite")
    Start-LoggedProcess -Name "azurite" -FilePath $azurite -Arguments @("--silent", "--location", (Join-Path $runRoot "azurite")) -WorkingDirectory $repoRoot
}

Start-LoggedProcess `
    -Name "backend" `
    -FilePath $dotnet `
    -Arguments @("run", "--project", $backendProject, "--launch-profile", $BackendProfile) `
    -WorkingDirectory $repoRoot

Start-LoggedProcess `
    -Name "frontend" `
    -FilePath $npm `
    -Arguments @("start") `
    -WorkingDirectory $frontendPath

Write-Host ""
Write-Host "Project-K local stack is starting." -ForegroundColor Green
Write-Host "Frontend: http://localhost:4200"
Write-Host "Backend:  http://localhost:5205"
Write-Host "Swagger:  http://localhost:5205/swagger"
Write-Host "Azurite:  http://127.0.0.1:10000/devstoreaccount1"
Write-Host "Logs:     $logRoot"
Write-Host ""
Write-Host "Stop with: ./scripts/stop-local.ps1"

if ($Open) {
    Start-Process "http://localhost:4200"
}
