# Refresh the catalogue copy built into the API (ProjectK.Infrastructure/Services/Catalog/Seed).
# The API serves it only when the catalogue service does not answer and there is no copy on disk,
# so it just has to be recent enough; rerun this before a release.
#
#   pwsh scripts/update-catalog-seed.ps1
#   pwsh scripts/update-catalog-seed.ps1 -BaseUrl https://catalog.example.dev
param([string]$BaseUrl = "https://catalog.lileyka.app")
$ErrorActionPreference = "Stop"

$target = Join-Path $PSScriptRoot "../Backend/ProjectK.Backend/ProjectK.Infrastructure/Services/Catalog/Seed/catalog.json"
$temp = New-TemporaryFile
try {
    Invoke-WebRequest -Uri "$($BaseUrl.TrimEnd('/'))/v1/catalog" -OutFile $temp -UseBasicParsing

    # CatalogClient reads schema 2 only; a copy it cannot parse would leave the fallback empty.
    if (-not (Select-String -Path $temp -Pattern '"schemaVersion":2[,}]' -Quiet)) {
        throw "The catalogue at $BaseUrl does not speak schema 2; the copy is left as it was."
    }

    Move-Item -Force $temp $target
    Write-Host "Saved $((Get-Item $target).Length) bytes to $((Resolve-Path $target).Path)"
}
finally {
    if (Test-Path $temp) { Remove-Item $temp }
}
