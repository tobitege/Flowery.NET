<#
Builds desktop-relevant Flowery projects with correct per-project settings.

Usage:
  pwsh ./scripts/build-desktop.ps1
  pwsh ./scripts/build-desktop.ps1 -Configuration Release
  pwsh ./scripts/build-desktop.ps1 -IncludeTests
  pwsh ./scripts/build-desktop.ps1 -NoRestore
#>
param(
    [string]$Configuration = "Debug",
    [switch]$IncludeTests,
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
$script:buildResults = @()
$script:startTime = Get-Date

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$Command
    )

    Write-Host $Title -ForegroundColor Cyan
    Write-Host "  $Command"
    $stepStart = Get-Date
    Invoke-Expression $Command
    $stepDuration = (Get-Date) - $stepStart

    if ($LASTEXITCODE -ne 0) {
        $script:buildResults += [PSCustomObject]@{ Project = $Title; Status = "FAILED"; Duration = $stepDuration }
        Write-Host "FAILED: $Title" -ForegroundColor Red
        exit 1
    }
    $script:buildResults += [PSCustomObject]@{ Project = $Title; Status = "OK"; Duration = $stepDuration }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

$captureProject = Join-Path $repoRoot "Flowery.Capture.NET/Flowery.Capture.NET.csproj"
$floweryProject = Join-Path $repoRoot "Flowery.NET/Flowery.NET.csproj"
$galleryProject = Join-Path $repoRoot "Flowery.NET.Gallery/Flowery.NET.Gallery.csproj"
$desktopProject = Join-Path $repoRoot "Flowery.NET.Gallery.Desktop/Flowery.NET.Gallery.Desktop.csproj"
$testsProject = Join-Path $repoRoot "Flowery.NET.Tests/Flowery.NET.Tests.csproj"

$noRestoreArg = if ($NoRestore) { " --no-restore" } else { "" }

Invoke-Step "Build: Flowery.Capture.NET" "dotnet build `"$captureProject`" -c $Configuration$noRestoreArg"
Invoke-Step "Build: Flowery.NET" "dotnet build `"$floweryProject`" -c $Configuration$noRestoreArg"
Invoke-Step "Build: Flowery.NET.Gallery" "dotnet build `"$galleryProject`" -c $Configuration$noRestoreArg"
Invoke-Step "Build: Flowery.NET.Gallery.Desktop" "dotnet build `"$desktopProject`" -c $Configuration$noRestoreArg"

if ($IncludeTests) {
    Invoke-Step "Build: Flowery.NET.Tests" "dotnet build `"$testsProject`" -c $Configuration$noRestoreArg"
}

$totalDuration = (Get-Date) - $script:startTime

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host " BUILD SUMMARY" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
foreach ($result in $script:buildResults) {
    $statusColor = if ($result.Status -eq "OK") { "Green" } else { "Red" }
    $duration = $result.Duration.ToString("mm\:ss\.ff")
    Write-Host ("  [{0}] {1,-40} {2}" -f $result.Status, $result.Project, $duration) -ForegroundColor $statusColor
}
Write-Host ""
Write-Host ("  Total time: {0:mm\:ss\.ff}" -f $totalDuration) -ForegroundColor Cyan
Write-Host ("  Projects built: {0}" -f $script:buildResults.Count) -ForegroundColor Cyan
Write-Host ""
Write-Host "All builds completed successfully." -ForegroundColor Green


