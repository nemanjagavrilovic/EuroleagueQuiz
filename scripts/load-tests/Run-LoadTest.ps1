param(
    [ValidateSet('smoke','load','stress','production-smoke')][string]$Profile = 'smoke',
    [ValidateSet('read','guest','practice','admin')][string]$Workload = 'read',
    [ValidateSet('all','grid','team-history','missing-club','career-order','mystery-player','five-player')][string]$Game = 'all',
    [uri]$BaseUrl = 'http://localhost:5248/api',
    [ValidateRange(1,100)][int]$MaxVus = 20,
    [switch]$ConfirmTestDatabase,
    [switch]$AllowProduction,
    [string]$K6Path
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$K6Path) {
    $portable = Join-Path $repoRoot '.tools/k6/k6.exe'
    if (Test-Path -LiteralPath $portable) { $K6Path = $portable }
    elseif (Get-Command k6 -ErrorAction SilentlyContinue) { $K6Path = (Get-Command k6).Source }
    else { throw 'Install portable k6 first: .\scripts\load-tests\Install-K6.ps1' }
}
if ($BaseUrl.UserInfo -or $BaseUrl.Query -or $BaseUrl.Fragment) { throw 'Use an API base URL without credentials, query or fragment.' }
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss-fff')
$resultsPath = Join-Path $repoRoot "load-test-results/$stamp-$Profile-$Workload"
New-Item -ItemType Directory -Force -Path $resultsPath | Out-Null
$summary = Join-Path $resultsPath 'summary.json'
$arguments = @('run', '--no-usage-report', '--env', "BASE_URL=$($BaseUrl.AbsoluteUri.TrimEnd('/'))",
    '--env', "PROFILE=$Profile", '--env', "WORKLOAD=$Workload", '--env', "GAME=$Game",
    '--env', "MAX_VUS=$MaxVus", '--env', "RESULTS_FILE=$summary",
    '--env', "CONFIRM_TEST_DATABASE=$($ConfirmTestDatabase.IsPresent.ToString().ToLowerInvariant())",
    '--env', "ALLOW_PRODUCTION=$($AllowProduction.IsPresent.ToString().ToLowerInvariant())",
    (Join-Path $PSScriptRoot 'api-load.js'))
Write-Host "Target: $BaseUrl | Profile: $Profile | Workload: $Workload | Game: $Game"
& $K6Path @arguments
$exitCode = $LASTEXITCODE
Write-Host "Report: $summary"
if ($exitCode -ne 0) { throw "k6 failed with exit code $exitCode. Inspect the checks, thresholds and API logs before increasing load." }
