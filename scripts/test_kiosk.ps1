<#
.SYNOPSIS
    Runs the smoke run of the Kiosk app in its real desktop window and checks its report.
.DESCRIPTION
    Builds Flowery.NET.Kiosk.Desktop (or takes a published executable), starts it with
    --smoke-test <report>, waits for it to exit and reads the XML report it wrote.
    The report, its log and the screenshots go to TestResults/Kiosk/<Configuration>/<run>/, the console output to logs/.
    Runs on Windows, Linux and macOS; Linux needs a display, such as xvfb-run.
.EXAMPLE
    ./scripts/test_kiosk.ps1 -Configuration Release
.EXAMPLE
    ./scripts/test_kiosk.ps1 -ExecutablePath ./publish-kiosk/Flowery.NET.Kiosk.Desktop.exe
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    # A published Kiosk; the script then builds nothing.
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core' -or $PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this script in PowerShell 7.'
}
$repoRoot = Split-Path -Parent $PSScriptRoot
if ($ExecutablePath) {
    $executable = (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
}
else {
    $project = Join-Path $repoRoot 'Flowery.NET.Kiosk.Desktop' 'Flowery.NET.Kiosk.Desktop.csproj'
    if (-not $SkipBuild) {
        & dotnet build $project -c $Configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    # The projects build net10.0-windows10.0.19041.0 on Windows and net10.0 elsewhere.
    $framework = if ($IsWindows) { 'net10.0-windows10.0.19041.0' } else { 'net10.0' }
    $name = if ($IsWindows) { 'Flowery.NET.Kiosk.Desktop.exe' } else { 'Flowery.NET.Kiosk.Desktop' }
    $executable = Join-Path $repoRoot 'bin' $Configuration 'Flowery.NET.Kiosk.Desktop' $framework $name
}
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw ('Kiosk executable is missing. Build without -SkipBuild first: {0}' -f $executable)
}

$runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$resultsRoot = Join-Path $repoRoot 'TestResults' 'Kiosk' $Configuration $runId
$logRoot = Join-Path $repoRoot 'logs'
New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$reportPath = Join-Path $resultsRoot 'kiosk-smoke.xml'
$logPath = Join-Path $logRoot ('kiosk-tests-{0}-{1}.log' -f $Configuration, $runId)

$startInfo = [Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $executable
$startInfo.WorkingDirectory = Split-Path -Parent $executable
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.ArgumentList.Add('--smoke-test')
$startInfo.ArgumentList.Add($reportPath)
$startInfo.Environment['FLOWERY_STATE_ROOT'] = Join-Path $resultsRoot 'state'
$process = [Diagnostics.Process]::Start($startInfo)
$processId = $process.Id
Write-Host ('Kiosk process: {0}; report: {1}' -f $processId, $reportPath)
try {
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    # The run takes about two minutes: six slides, the scaling checkpoints and one automatic cycle of 48 seconds.
    $timedOut = -not $process.WaitForExit(240000)
    if ($timedOut) {
        $process.Kill($true)
        $process.WaitForExit()
    }
    else {
        $exitCode = $process.ExitCode
    }
    # The output goes to the log also after a timeout, as far as the app wrote it; its pipes close when it exits.
    [void][Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdoutTask, $stderrTask), 10000)
    $output = @($stdoutTask, $stderrTask | Where-Object IsCompletedSuccessfully | ForEach-Object Result) -join ''
    $output | Set-Content -LiteralPath $logPath -Encoding utf8
}
finally { $process.Dispose() }
if ($timedOut) {
    throw ('Kiosk smoke test timed out; process {0} was stopped. Log: {1}' -f $processId, $logPath)
}

if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    throw ('Kiosk did not write its report; exit code {0}. Log: {1}' -f $exitCode, $logPath)
}
[xml]$report = Get-Content -LiteralPath $reportPath -Raw -Encoding utf8
$cases = @($report.tests.test)
$total = [int]$report.tests.total
$passed = [int]$report.tests.passed
$failed = [int]$report.tests.failed
$inconclusive = [int]$report.tests.inconclusive
if ($total -lt 6 -or $cases.Count -ne $total -or $passed + $failed + $inconclusive -ne $total) { throw 'Kiosk smoke report has invalid counts.' }
$names = @($cases | ForEach-Object { [string]$_.name })
if (@($names | Sort-Object -Unique).Count -ne $total -or @($names | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
    throw 'Kiosk smoke report has invalid case names.'
}
$failedCases = @($cases | Where-Object { $_.outcome -ne 'Passed' -and $_.outcome -ne 'Inconclusive' })
$inconclusiveCases = @($cases | Where-Object { $_.outcome -eq 'Inconclusive' })
Write-Host ('Kiosk: {0} passed, {1} failed, {2} inconclusive. Report: {3}' -f $passed, $failed, $inconclusive, $reportPath)
$inconclusiveCases | ForEach-Object { Write-Warning ('{0}: {1}' -f $_.name, ($_.message -split "`n")[0]) }
if ($exitCode -ne 0 -or $failed -ne 0 -or $failedCases.Count -gt 0 -or $passed + $inconclusive -ne $total -or $inconclusiveCases.Count -ne $inconclusive) {
    $failedCases | ForEach-Object { Write-Host ('{0}: {1}' -f $_.name, $_.message) }
    exit 1
}
exit 0
