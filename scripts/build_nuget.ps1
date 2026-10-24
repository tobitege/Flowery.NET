# Build the NuGet packages for Flowery.NET and Flowery.NET.Skia
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "./nupkg"
)

$ErrorActionPreference = "Stop"
$Projects = @(
    "$PSScriptRoot/../Flowery.NET/Flowery.NET.csproj",
    "$PSScriptRoot/../Flowery.NET.Skia/Flowery.NET.Skia.csproj"
)

Write-Host "Building NuGet packages..." -ForegroundColor Cyan
foreach ($ProjectPath in $Projects) {
    dotnet pack $ProjectPath -c $Configuration -o $OutputDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed: $ProjectPath" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Packages created in $OutputDir" -ForegroundColor Green
Get-ChildItem $OutputDir -Filter "*.nupkg" | ForEach-Object { Write-Host "  $_" }
