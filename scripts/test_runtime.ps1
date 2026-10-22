<#
.SYNOPSIS
    Runs the runtime UI tests in a real desktop window through the xunit runner.
.DESCRIPTION
    Builds Flowery.NET.RuntimeTests.Runner, which also builds the runtime test host.
    The runner starts Flowery.NET.RuntimeTests.exe, which opens the Gallery application with a test window.
    Results go to TestResults\<Configuration>\<run>\ and the console log to logs\.
.EXAMPLE
    ./scripts/test_runtime.ps1 -Configuration Debug -Monitor 2 -WindowRegion UpperHalf
.EXAMPLE
    ./scripts/test_runtime.ps1 -SkipBuild -TestFilter Given_GalleryPages
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    [string]$TestFilter,
    [ValidateRange(0, 16)]
    [int]$Monitor = 0,
    [ValidateSet('Full', 'UpperHalf', 'LowerHalf')]
    [string]$WindowRegion = 'Full'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$framework = 'net10.0-windows'

if (-not $IsWindows -or $PSVersionTable.PSEdition -ne 'Core' -or $PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this script in native PowerShell 7 on Windows.'
}

$runnerProject = Join-Path $repoRoot 'Flowery.NET.RuntimeTests.Runner\Flowery.NET.RuntimeTests.Runner.csproj'
if (-not $SkipBuild) {
    & dotnet build $runnerProject -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$hostPath = Join-Path $repoRoot "bin\$Configuration\Flowery.NET.RuntimeTests\$framework\Flowery.NET.RuntimeTests.exe"
$runnerPath = Join-Path $repoRoot "bin\$Configuration\Flowery.NET.RuntimeTests.Runner\$framework\Flowery.NET.RuntimeTests.Runner.exe"
foreach ($path in @($hostPath, $runnerPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw ('Required executable is missing. Build without -SkipBuild first: {0}' -f $path)
    }
}

$runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$resultsDirectory = Join-Path $repoRoot "TestResults\$Configuration\$runId"
$logDirectory = Join-Path $repoRoot 'logs'
New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory "runtime-tests-$Configuration-$runId.log"
$reportPath = Join-Path $resultsDirectory 'runtime-tests.xml'
$runnerArguments = @('-trx', (Join-Path $resultsDirectory 'runner.trx'))

$variables = @{
    FLOWERY_RUNTIME_TESTS_EXE_PATH = $hostPath
    FLOWERY_TEST_RESULTS_ROOT = $resultsDirectory
    FLOWERY_RUNTIME_TEST_FILTER = $TestFilter
    FLOWERY_MONITOR_DEVICE = $null
    FLOWERY_MONITOR_REGION = $null
}
if ($Monitor -gt 0) {
    $variables.FLOWERY_MONITOR_DEVICE = '\\.\DISPLAY{0}' -f $Monitor
    $variables.FLOWERY_MONITOR_REGION = switch ($WindowRegion) {
        'UpperHalf' { 'upper-half' }
        'LowerHalf' { 'lower-half' }
        default { 'full' }
    }
    Write-Host ('Window placement: {0}, {1}' -f $variables.FLOWERY_MONITOR_DEVICE, $WindowRegion)
}

$previous = @{}
foreach ($name in $variables.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

try {
    foreach ($name in $variables.Keys) {
        [Environment]::SetEnvironmentVariable($name, $variables[$name], 'Process')
    }
    Write-Host ('Host: {0}' -f $hostPath)
    Write-Host ('Results: {0}' -f $resultsDirectory)
    & $runnerPath @runnerArguments 2>&1 | Tee-Object -FilePath $logPath
    $testExitCode = $LASTEXITCODE
}
finally {
    foreach ($name in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
    }
}

# Inconclusive runtime tests could not set up their scenario here, such as a window without room for it.
# They do not fail the run, but stay visible: as annotations in GitHub Actions, as warnings locally.
if (Test-Path -LiteralPath $reportPath -PathType Leaf) {
    $report = [xml](Get-Content -LiteralPath $reportPath -Raw -Encoding utf8)
    foreach ($test in $report.tests.test) {
        if ($test.outcome -ne 'Inconclusive') {
            continue
        }
        $text = '{0}: {1}' -f $test.name, $test.message
        if ($env:GITHUB_ACTIONS -eq 'true') {
            $escaped = $text.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
            Write-Host ('::warning title=Inconclusive runtime test::{0}' -f $escaped)
        }
        else {
            Write-Warning $text
        }
    }
    Write-Host ('Runtime tests: {0} total, {1} passed, {2} failed, {3} inconclusive' -f `
        $report.tests.total, $report.tests.passed, $report.tests.failed, $report.tests.inconclusive)
}

Write-Host ('Log: {0}' -f $logPath)
exit $testExitCode
