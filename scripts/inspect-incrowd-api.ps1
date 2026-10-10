[CmdletBinding()]
param(
    [string]$SeasonCode = 'E2013',
    [string]$TeamCode = 'red',
    [string]$OutputDirectory = '.\api-samples'
)

$ErrorActionPreference = 'Stop'
$baseUrl = 'https://feeds.incrowdsports.com/provider/euroleague-feeds/v2'
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)

New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

$requests = @(
    @{ Name = 'seasons'; Url = "$baseUrl/competitions/E/seasons" },
    @{ Name = "clubs-$SeasonCode"; Url = "$baseUrl/competitions/E/seasons/$SeasonCode/clubs" },
    @{ Name = "people-$SeasonCode-$TeamCode"; Url = "$baseUrl/competitions/E/seasons/$SeasonCode/clubs/$TeamCode/people" }
)

function Get-Shape {
    param(
        [Parameter(Mandatory)]$Value,
        [int]$Depth = 0
    )

    if ($null -eq $Value) { return 'null' }
    if ($Depth -gt 2) { return $Value.GetType().Name }
    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string] -and $Value -isnot [System.Collections.IDictionary]) {
        $items = @($Value)
        if ($items.Count -eq 0) { return 'array[0]' }
        return "array[$($items.Count)] sample=$((Get-Shape -Value $items[0] -Depth ($Depth + 1)))"
    }
    if ($Value -is [pscustomobject]) {
        $properties = @($Value.PSObject.Properties)
        $names = $properties | ForEach-Object { $_.Name }
        return "object{$($names -join ', ')}"
    }
    return $Value.GetType().Name
}

$summary = [System.Collections.Generic.List[string]]::new()
$summary.Add("InCrowd API inspection: $(Get-Date -Format o)")
$summary.Add("Season: $SeasonCode; team: $TeamCode")
$summary.Add('')

foreach ($request in $requests) {
    Write-Host "Requesting $($request.Url)" -ForegroundColor Cyan
    try {
        $response = Invoke-WebRequest -Uri $request.Url -Method Get -Headers @{ 'Accept' = 'application/json' }
        $filePath = Join-Path $resolvedOutput "$($request.Name).json"
        [System.IO.File]::WriteAllText($filePath, $response.Content)

        $parsed = $response.Content | ConvertFrom-Json
        $shape = Get-Shape -Value $parsed
        $summary.Add("$($request.Name): HTTP $($response.StatusCode); $shape; $filePath")
        Write-Host "Saved $filePath" -ForegroundColor Green
        Write-Host "Shape: $shape" -ForegroundColor Yellow
    }
    catch {
        $message = "$($request.Name): FAILED - $($_.Exception.Message)"
        $summary.Add($message)
        Write-Warning $message
    }
}

$summaryPath = Join-Path $resolvedOutput 'summary.txt'
[System.IO.File]::WriteAllLines($summaryPath, $summary)
Write-Host "Summary saved to $summaryPath" -ForegroundColor Green
