# Run Flowery.NET Gallery on Android Emulator
# Usage: .\run-android.ps1 [-DeviceName "emulator-5554"] [-Configuration "Debug"]

param(
    [string]$DeviceName = "emulator-5554",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path $PSScriptRoot -Parent
$ProjectPath = Join-Path $RepoRoot "Flowery.NET.Gallery.Android"
$ProjectFile = Join-Path $ProjectPath "Flowery.NET.Gallery.Android.csproj"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Flowery.NET Gallery - Android Runner" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Project:       $ProjectPath" -ForegroundColor Gray
Write-Host "Device:        $DeviceName" -ForegroundColor Gray
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host ""

# Check if project exists
if (-not (Test-Path $ProjectPath)) {
    Write-Host "ERROR: Project folder not found at $ProjectPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $ProjectFile)) {
    Write-Host "ERROR: Project file not found at $ProjectFile" -ForegroundColor Red
    exit 1
}

$projectXml = [xml](Get-Content -LiteralPath $ProjectFile -Raw)
$TargetFramework = $projectXml.Project.PropertyGroup.TargetFramework | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($TargetFramework)) {
    Write-Host "ERROR: TargetFramework not found in $ProjectFile" -ForegroundColor Red
    exit 1
}

Write-Host "Target:        $TargetFramework" -ForegroundColor Gray
Write-Host ""

# Check if emulator is running
Write-Host "Checking for connected devices..." -ForegroundColor Yellow
$devices = & adb devices 2>&1 | Out-String
$deviceFound = $devices -like "*$DeviceName*"
if (-not $deviceFound) {
    Write-Host "WARNING: Device '$DeviceName' not found in ADB devices list." -ForegroundColor Yellow
    Write-Host "Available devices:" -ForegroundColor Yellow
    Write-Host $devices -ForegroundColor Gray
    Write-Host ""
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne "y") {
        exit 0
    }
} else {
    Write-Host "Found device: $DeviceName" -ForegroundColor Green
}

Write-Host ""
Write-Host "Building and deploying to $DeviceName..." -ForegroundColor Green
Write-Host ""

# Run the build command
Push-Location $ProjectPath
try {
    dotnet build -t:Run -f $TargetFramework -c $Configuration -p:DeviceName=$DeviceName

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "SUCCESS: App deployed and running!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "ERROR: Build/deploy failed with exit code $LASTEXITCODE" -ForegroundColor Red
    }
}
finally {
    Pop-Location
}
