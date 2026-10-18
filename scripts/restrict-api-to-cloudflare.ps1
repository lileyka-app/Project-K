# SEC-4.1, the Azure half: let only the Cloudflare edge reach the production API.
#
# Why it matters. The API trusts `CF-Connecting-IP` (`Security:ClientIp:Header` in
# appsettings.Production.json) to know who is calling. While the App Service answers everyone, the
# default `*.azurewebsites.net` hostname is an open door: a request sent straight there can carry
# any `CF-Connecting-IP` it likes, and with it walk past the login rate limit, the geo block and
# the address written into the audit log.
#
#   .\scripts\restrict-api-to-cloudflare.ps1          # close the perimeter
#   .\scripts\restrict-api-to-cloudflare.ps1 -Undo    # open it again
#
# The same thing as restrict-api-to-cloudflare.sh, for a PowerShell prompt. Cloudflare publishes
# its ranges at cloudflare.com/ips-v4 and /ips-v6 and changes them rarely; rerun after such a
# change. Staging is deliberately not touched: its frontend calls the App Service hostname
# directly, with no Cloudflare in front, so the same rules would cut it off.
[CmdletBinding()]
param([switch]$Undo)

$ErrorActionPreference = 'Stop'

$subscription = 'projectk-prod-sub'
$group        = 'rg-projectk-prod-paid'
$app          = 'api-projectk-prod-new'
$hostName     = 'api.lileyka.app'

function Invoke-Az {
    param([string[]]$Arguments, [string]$What)
    $output = & az @Arguments
    if ($LASTEXITCODE -ne 0) { throw "az failed while $What" }
    return $output
}

$defaultHost = Invoke-Az @('webapp', 'show', '--subscription', $subscription, '-g', $group, '-n', $app,
    '--query', 'defaultHostName', '-o', 'tsv') 'reading the app'

if ($Undo) {
    Write-Host '== removing the Cloudflare rules'
    $rules = Invoke-Az @('webapp', 'config', 'access-restriction', 'show', '--subscription', $subscription,
        '-g', $group, '-n', $app,
        '--query', "ipSecurityRestrictions[?starts_with(name, 'cloudflare-')].name", '-o', 'tsv') 'listing the rules'
    foreach ($rule in @($rules)) {
        if ([string]::IsNullOrWhiteSpace($rule)) { continue }
        Invoke-Az @('webapp', 'config', 'access-restriction', 'remove', '--subscription', $subscription,
            '-g', $group, '-n', $app, '--rule-name', $rule, '-o', 'none') "removing $rule" | Out-Null
        Write-Host "   removed $rule"
    }
    Write-Host '== done; the app answers everyone again'
    return
}

Write-Host '== checks before touching anything'

# Deployment goes through the SCM site. If it inherited these rules, the next release would have
# nowhere to land, so refuse rather than discover that during a release.
$scmUsesMain = Invoke-Az @('webapp', 'config', 'access-restriction', 'show', '--subscription', $subscription,
    '-g', $group, '-n', $app, '--query', 'scmIpSecurityRestrictionsUseMain', '-o', 'tsv') 'reading the SCM rules'
if ("$scmUsesMain".Trim() -eq 'true') {
    throw 'SCM inherits the main rules: closing the perimeter would lock deployments out'
}
Write-Host '   deployments unaffected (SCM keeps its own rules)'

# The whole plan rests on Cloudflare actually being in front of the custom domain. Its answer says
# so itself: only the edge sets CF-RAY.
$probe = Invoke-WebRequest -Uri "https://$hostName/health" -Method Get -UseBasicParsing
if (-not $probe.Headers.ContainsKey('CF-RAY')) {
    throw "https://$hostName is not answered by Cloudflare - closing the perimeter would cut the app off"
}
Write-Host "   $hostName is served through Cloudflare"

$ranges = @()
foreach ($list in @('https://www.cloudflare.com/ips-v4', 'https://www.cloudflare.com/ips-v6')) {
    $body = (Invoke-WebRequest -Uri $list -UseBasicParsing).Content
    $ranges += $body -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -like '*/*' }
}
if ($ranges.Count -lt 15) {
    throw "only $($ranges.Count) ranges came back from cloudflare.com, expected about 22 - stopping"
}
Write-Host "   $($ranges.Count) Cloudflare ranges"

Write-Host '== allowing them'
# The first Allow rule turns the implicit default into Deny, so between the first and the last rule
# a request from a range not yet added is refused. It lasts under a minute; run it off-peak.
$priority = 100
foreach ($cidr in $ranges) {
    if ($cidr.Contains(':')) { $name = "cloudflare-v6-$priority" } else { $name = "cloudflare-v4-$priority" }
    Invoke-Az @('webapp', 'config', 'access-restriction', 'add', '--subscription', $subscription,
        '-g', $group, '-n', $app, '--rule-name', $name, '--action', 'Allow', '--ip-address', $cidr,
        '--priority', "$priority", '--description', 'Cloudflare edge', '-o', 'none') "adding $cidr" | Out-Null
    $priority++
}
Write-Host "   $($ranges.Count) rules in place"

Write-Host '== verifying'
Start-Sleep -Seconds 10

function Get-Status {
    param([string]$Url)
    try {
        return (Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing).StatusCode
    } catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        throw
    }
}

$throughEdge = Get-Status "https://$hostName/health"
$direct      = Get-Status "https://$defaultHost/health"
Write-Host "   through Cloudflare: $throughEdge (want 200)"
Write-Host "   straight at the App Service: $direct (want 403)"

if ($throughEdge -ne 200) {
    throw 'the app stopped answering through Cloudflare; undo with: .\scripts\restrict-api-to-cloudflare.ps1 -Undo'
}
if ($direct -ne 403) {
    throw 'the App Service still answers directly; check the rules in the portal'
}
Write-Host '== done: only the Cloudflare edge reaches the API'
