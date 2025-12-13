<#
Builds all Flowery projects with correct per-project settings.

Usage:
  pwsh ./scripts/build_all.ps1
  pwsh ./scripts/build_all.ps1 -AndroidSdkDirectory "C:\Users\YOURUSER\AppData\Local\Android\Sdk"
  pwsh ./scripts/build_all.ps1 -RestoreWorkloads
#>
param(
    [string]$Configuration = "Debug",
    [string]$AndroidSdkDirectory = "",
    [switch]$RestoreWorkloads
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

if ($RestoreWorkloads) {
    Push-Location $repoRoot
    try {
        Invoke-Step "Workloads: restore" "dotnet workload restore"
    } finally {
        Pop-Location
    }
}

if ([string]::IsNullOrWhiteSpace($AndroidSdkDirectory)) {
    if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_SDK_ROOT)) {
        $AndroidSdkDirectory = $env:ANDROID_SDK_ROOT
    } elseif (-not [string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
        $AndroidSdkDirectory = $env:ANDROID_HOME
    } elseif (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $AndroidSdkDirectory = Join-Path $env:LOCALAPPDATA "Android\Sdk"
    }
}

if ([string]::IsNullOrWhiteSpace($AndroidSdkDirectory)) {
    throw "AndroidSdkDirectory not set. Pass -AndroidSdkDirectory or set ANDROID_SDK_ROOT / ANDROID_HOME."
}

$captureProject = Join-Path $repoRoot "Flowery.Capture.NET/Flowery.Capture.NET.csproj"
$floweryProject = Join-Path $repoRoot "Flowery.NET/Flowery.NET.csproj"
$galleryProject = Join-Path $repoRoot "Flowery.NET.Gallery/Flowery.NET.Gallery.csproj"
$desktopProject = Join-Path $repoRoot "Flowery.NET.Gallery.Desktop/Flowery.NET.Gallery.Desktop.csproj"
$browserProject = Join-Path $repoRoot "Flowery.NET.Gallery.Browser/Flowery.NET.Gallery.Browser.csproj"
$testsProject = Join-Path $repoRoot "Flowery.NET.Tests/Flowery.NET.Tests.csproj"
$androidProject = Join-Path $repoRoot "Flowery.NET.Gallery.Android/Flowery.NET.Gallery.Android.csproj"

Invoke-Step "Build: Flowery.Capture.NET" "dotnet build `"$captureProject`" -c $Configuration"
Invoke-Step "Build: Flowery.NET" "dotnet build `"$floweryProject`" -c $Configuration"
Invoke-Step "Build: Flowery.NET.Gallery" "dotnet build `"$galleryProject`" -c $Configuration"
Invoke-Step "Build: Flowery.NET.Gallery.Desktop" "dotnet build `"$desktopProject`" -c $Configuration"
Invoke-Step "Build: Flowery.NET.Gallery.Browser" "dotnet build `"$browserProject`" -c $Configuration"
Invoke-Step "Build: Flowery.NET.Tests" "dotnet build `"$testsProject`" -c $Configuration"

Invoke-Step "Android: InstallAndroidDependencies" "dotnet build `"$androidProject`" -c $Configuration -f net9.0-android -t:InstallAndroidDependencies -p:AndroidSdkDirectory=`"$AndroidSdkDirectory`""
Invoke-Step "Android: Build" "dotnet build `"$androidProject`" -c $Configuration -f net9.0-android -p:AndroidSdkDirectory=`"$AndroidSdkDirectory`""

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
