<#
Publishes ABI-specific Flowery.NET Gallery APKs.

Usage:
  pwsh ./scripts/build_android_apks.ps1
  pwsh ./scripts/build_android_apks.ps1 -RuntimeIdentifiers android-arm64,android-x64
  pwsh ./scripts/build_android_apks.ps1 -AndroidSdkDirectory "C:\Users\YOURUSER\AppData\Local\Android\Sdk"
#>
param(
    [string]$Configuration = "Release",
    [string]$AndroidSdkDirectory = "",
    [string[]]$RuntimeIdentifiers = @("android-arm", "android-arm64", "android-x86", "android-x64")
)

$ErrorActionPreference = "Stop"
$buildResults = @()
$startTime = Get-Date

function Resolve-AndroidSdkDirectory {
    param(
        [string]$ConfiguredPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        return $ConfiguredPath
    }

    if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_SDK_ROOT)) {
        return $env:ANDROID_SDK_ROOT
    }

    if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
        return $env:ANDROID_HOME
    }

    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        return Join-Path $env:LOCALAPPDATA "Android\Sdk"
    }

    throw "AndroidSdkDirectory not set. Pass -AndroidSdkDirectory or set ANDROID_SDK_ROOT / ANDROID_HOME."
}

function Invoke-DotNetPublish {
    param(
        [string]$Title,
        [string[]]$Arguments
    )

    Write-Host $Title -ForegroundColor Cyan
    Write-Host ("  dotnet {0}" -f ($Arguments -join " "))
    $stepStart = Get-Date
    & dotnet @Arguments
    $stepDuration = (Get-Date) - $stepStart

    if ($LASTEXITCODE -ne 0) {
        $script:buildResults += [PSCustomObject]@{ Project = $Title; Status = "FAILED"; Duration = $stepDuration }
        Write-Host "FAILED: $Title" -ForegroundColor Red
        exit 1
    }

    $script:buildResults += [PSCustomObject]@{ Project = $Title; Status = "OK"; Duration = $stepDuration }
}

function Get-AndroidApk {
    param(
        [string]$PublishDirectory
    )

    $apk = Get-ChildItem -LiteralPath $PublishDirectory -Filter "*-Signed.apk" -Recurse | Select-Object -First 1
    if ($null -eq $apk) {
        $apk = Get-ChildItem -LiteralPath $PublishDirectory -Filter "*.apk" -Recurse | Select-Object -First 1
    }

    if ($null -eq $apk) {
        throw "No Android APK was produced in $PublishDirectory."
    }

    return $apk
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$androidProject = Join-Path $repoRoot "Flowery.NET.Gallery.Android/Flowery.NET.Gallery.Android.csproj"

if (-not (Test-Path -LiteralPath $androidProject)) {
    throw "Project file not found at $androidProject."
}

$AndroidSdkDirectory = Resolve-AndroidSdkDirectory $AndroidSdkDirectory
$projectXml = [xml](Get-Content -LiteralPath $androidProject -Raw -Encoding utf8)
$targetFramework = $projectXml.Project.PropertyGroup.TargetFramework | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($targetFramework)) {
    throw "TargetFramework not found in $androidProject."
}

$artifactRoot = Join-Path $repoRoot "artifacts/android"
$publishRoot = Join-Path $artifactRoot "publish"
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Flowery.NET Gallery - Android APKs" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Project:       $androidProject" -ForegroundColor Gray
Write-Host "Target:        $targetFramework" -ForegroundColor Gray
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Android SDK:   $AndroidSdkDirectory" -ForegroundColor Gray
Write-Host "Output:        $artifactRoot" -ForegroundColor Gray
Write-Host ""

foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    if ([string]::IsNullOrWhiteSpace($runtimeIdentifier)) {
        throw "RuntimeIdentifiers must not contain empty values."
    }

    $publishDirectory = Join-Path $publishRoot $runtimeIdentifier
    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

    $arguments = @(
        "publish",
        $androidProject,
        "-c",
        $Configuration,
        "-f",
        $targetFramework,
        "-r",
        $runtimeIdentifier,
        "-p:AndroidPackageFormat=apk",
        "-p:AndroidSdkDirectory=$AndroidSdkDirectory",
        "-o",
        $publishDirectory
    )

    Invoke-DotNetPublish "Android: Publish $runtimeIdentifier" $arguments

    $apk = Get-AndroidApk $publishDirectory
    $apkSuffix = $runtimeIdentifier -replace "^android-", ""
    $destination = Join-Path $artifactRoot "Flowery.Gallery-Android-$apkSuffix.apk"
    Copy-Item -LiteralPath $apk.FullName -Destination $destination -Force
    Write-Host "APK: $destination" -ForegroundColor Green
    Write-Host ""
}

$totalDuration = (Get-Date) - $startTime

Write-Host "========================================" -ForegroundColor Green
Write-Host " APK BUILD SUMMARY" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
foreach ($result in $buildResults) {
    $statusColor = if ($result.Status -eq "OK") { "Green" } else { "Red" }
    $duration = $result.Duration.ToString("mm\:ss\.ff")
    Write-Host ("  [{0}] {1,-36} {2}" -f $result.Status, $result.Project, $duration) -ForegroundColor $statusColor
}
Write-Host ""
Write-Host ("  Total time: {0:mm\:ss\.ff}" -f $totalDuration) -ForegroundColor Cyan
Write-Host ""
Write-Host "APKs written to $artifactRoot" -ForegroundColor Green
