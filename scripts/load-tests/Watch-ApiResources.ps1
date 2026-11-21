param(
    [ValidateRange(1,65535)][int]$ApiPort = 5248,
    [ValidateRange(0,2147483647)][int]$ApiProcessId = 0,
    [ValidateRange(1,86400)][int]$DurationSeconds = 480,
    [ValidateRange(1,30)][int]$IntervalSeconds = 2,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
if ($ApiProcessId -eq 0) {
    $apiCandidates = @(Get-Process -Name EuroleagueQuiz.Api -ErrorAction SilentlyContinue)
    if ($apiCandidates.Count -eq 1) { $ApiProcessId = $apiCandidates[0].Id }
    else {
        try {
            $listeners = @(Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction Stop |
                Select-Object -ExpandProperty OwningProcess -Unique)
            if ($listeners.Count -eq 1) { $ApiProcessId = $listeners[0] }
        } catch { }
    }
}
if ($ApiProcessId -eq 0) {
    throw 'Cannot identify the API process. Start the API, find its PID in Task Manager > Details, then pass -ApiProcessId <PID>.'
}
$apiProcess = Get-Process -Id $ApiProcessId -ErrorAction Stop
$apiProcessName = $apiProcess.ProcessName
$processStartedAt = $apiProcess.StartTime.ToUniversalTime()
$logicalProcessors = [Environment]::ProcessorCount
if (!$OutputDirectory) {
    $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $sampleStamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss-fff')
    $OutputDirectory = Join-Path $repoRoot "load-test-results/$sampleStamp-resources"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$csvPath = Join-Path $OutputDirectory 'api-resources.csv'
$summaryPath = Join-Path $OutputDirectory 'resources-summary.json'
if ((Test-Path -LiteralPath $csvPath) -or (Test-Path -LiteralPath $summaryPath)) {
    throw 'This output directory already contains resource results. Choose a new directory.'
}
$samples = [Collections.Generic.List[object]]::new()
$samplingStartedAt = (Get-Date).ToUniversalTime()
$timer = [Diagnostics.Stopwatch]::StartNew()
$previousElapsed = 0.0
$previousCpu = $apiProcess.TotalProcessorTime.TotalSeconds
$stoppedEarly = $false
Write-Host "Watching $apiProcessName, PID $ApiProcessId, $logicalProcessors logical processors."
Write-Host "CSV: $csvPath"
Write-Host 'Start the load test in another terminal. Keep sampling for 60-120 seconds after it finishes.'
Write-Host 'CPU total is normalized to all logical processors; CPU one-core can exceed 100%.'
try {
    while ($timer.Elapsed.TotalSeconds -lt $DurationSeconds) {
        $remaining = $DurationSeconds - $timer.Elapsed.TotalSeconds
        Start-Sleep -Milliseconds ([int][Math]::Max(1, [Math]::Min($IntervalSeconds, $remaining) * 1000))
        try {
            $apiProcess.Refresh()
            if ($apiProcess.HasExited -or $apiProcess.StartTime.ToUniversalTime() -ne $processStartedAt) {
                $stoppedEarly = $true
                Write-Warning 'The API exited or restarted. Start a new sampler for the new process.'
                break
            }
            $elapsed = $timer.Elapsed.TotalSeconds
            $currentCpu = $apiProcess.TotalProcessorTime.TotalSeconds
            $cpuOneCore = [Math]::Max(0, ($currentCpu - $previousCpu) / ($elapsed - $previousElapsed) * 100)
            $sample = [pscustomobject]@{
                TimeUtc = (Get-Date).ToUniversalTime().ToString('o')
                ElapsedSeconds = [Math]::Round($elapsed, 2)
                ProcessId = $ApiProcessId
                CpuPercentTotal = [Math]::Round($cpuOneCore / $logicalProcessors, 2)
                CpuPercentOneCore = [Math]::Round($cpuOneCore, 2)
                WorkingSetMB = [Math]::Round($apiProcess.WorkingSet64 / 1MB, 2)
                PrivateMemoryMB = [Math]::Round($apiProcess.PrivateMemorySize64 / 1MB, 2)
                Threads = $apiProcess.Threads.Count
            }
        } catch {
            $stoppedEarly = $true
            Write-Warning "Process sampling stopped: $($_.Exception.Message)"
            break
        }
        $sample | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Append -Encoding UTF8
        $samples.Add($sample)
        Write-Host ('{0} | CPU {1,6:N1}% | RAM {2,8:N1} MB | Private {3,8:N1} MB | Threads {4}' -f
            $sample.TimeUtc, $sample.CpuPercentTotal, $sample.WorkingSetMB, $sample.PrivateMemoryMB, $sample.Threads)
        $previousCpu = $currentCpu
        $previousElapsed = $elapsed
    }
} finally {
    $timer.Stop()
    $summary = [ordered]@{
        processId = $ApiProcessId
        processName = $apiProcessName
        processStartedAtUtc = $processStartedAt.ToString('o')
        samplingStartedAtUtc = $samplingStartedAt.ToString('o')
        samplingFinishedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        logicalProcessors = $logicalProcessors
        samples = $samples.Count
        processStoppedEarly = $stoppedEarly
    }
    if ($samples.Count -gt 0) {
        $summary['cpuPercentTotalAverage'] = [Math]::Round(($samples | Measure-Object CpuPercentTotal -Average).Average, 2)
        $summary['cpuPercentTotalMaximum'] = ($samples | Measure-Object CpuPercentTotal -Maximum).Maximum
        $summary['workingSetMBFirst'] = $samples[0].WorkingSetMB
        $summary['workingSetMBLast'] = $samples[$samples.Count - 1].WorkingSetMB
        $summary['workingSetMBMaximum'] = ($samples | Measure-Object WorkingSetMB -Maximum).Maximum
        $summary['privateMemoryMBFirst'] = $samples[0].PrivateMemoryMB
        $summary['privateMemoryMBLast'] = $samples[$samples.Count - 1].PrivateMemoryMB
        $summary['privateMemoryMBMaximum'] = ($samples | Measure-Object PrivateMemoryMB -Maximum).Maximum
    }
    $summary | ConvertTo-Json | Set-Content -LiteralPath $summaryPath -Encoding UTF8
    Write-Host "Resource summary: $summaryPath"
}
