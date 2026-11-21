param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '2.4.0',
    [string]$Destination = (Join-Path $PSScriptRoot '../../.tools/k6')
)
$ErrorActionPreference = 'Stop'
$installPath = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force -Path $installPath | Out-Null
$archiveName = "k6-v$Version-windows-amd64.zip"
$archivePath = Join-Path $installPath $archiveName
$baseUri = "https://github.com/grafana/k6/releases/download/v$Version"
$checksumContent = (Invoke-WebRequest -Uri "$baseUri/k6-v$Version-checksums.txt" -UseBasicParsing).Content
$checksumText = if ($checksumContent -is [byte[]]) { [Text.Encoding]::UTF8.GetString($checksumContent) } else { [string]$checksumContent }
$checksumLine = ($checksumText -split "`n") | Where-Object { $_.TrimEnd().EndsWith($archiveName) } | Select-Object -First 1
if (!$checksumLine) { throw "The official checksum file does not contain $archiveName." }
$expectedHash = ($checksumLine.Trim() -split '\s+')[0]
Write-Host "Downloading official k6 $Version for Windows x64..."
Invoke-WebRequest -Uri "$baseUri/$archiveName" -OutFile $archivePath -UseBasicParsing
$actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
if ($actualHash -ine $expectedHash) { throw 'k6 checksum verification failed. The executable will not be used.' }
Expand-Archive -LiteralPath $archivePath -DestinationPath $installPath -Force
$target = Join-Path $installPath 'k6.exe'
$executable = Join-Path $installPath "k6-v$Version-windows-amd64/k6.exe"
if (!(Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'The verified archive did not contain the expected k6.exe.' }
Copy-Item -LiteralPath $executable -Destination $target -Force
& $target version
if ($LASTEXITCODE -ne 0) { throw 'k6 could not be started.' }
Write-Host "Portable k6 is ready: $target"
