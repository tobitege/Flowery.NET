<#
.SYNOPSIS
    Starts the Flowery.NET.Gallery.Browser WASM application.

.DESCRIPTION
    Runs the pre-compiled Browser project and opens it in the default browser.

.PARAMETER NoBrowser
    If specified, does not automatically open the browser.

.PARAMETER Port
    HTTP port to use (default: 5235).

.EXAMPLE
    pwsh ./scripts/run_browser.ps1
    pwsh ./scripts/run_browser.ps1 -NoBrowser
    pwsh ./scripts/run_browser.ps1 -Port 8080
#>
param(
    [switch]$NoBrowser,
    [int]$Port = 5235
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$browserProject = Join-Path $repoRoot "Flowery.NET.Gallery.Browser"

if (-not (Test-Path $browserProject)) {
    Write-Host "ERROR: Browser project not found at $browserProject" -ForegroundColor Red
    exit 1
}

$url = "http://localhost:$Port"

Write-Host "Starting Flowery.NET.Gallery.Browser..." -ForegroundColor Cyan
Write-Host "  URL: $url" -ForegroundColor Gray
Write-Host "  Press Ctrl+C to stop" -ForegroundColor Gray
Write-Host ""

if (-not $NoBrowser) {
    Start-Job -ScriptBlock {
        param($url)
        Start-Sleep -Seconds 3
        Start-Process $url
    } -ArgumentList $url | Out-Null
}

Push-Location $browserProject
try {
    dotnet run --urls $url
} finally {
    Pop-Location
}
