# Runs Flowery.NET.PackageTest, an app that references Flowery.NET and Flowery.NET.Skia as NuGet packages only.
# Without -PackageDir or -NuGet it packs both projects first. -Check renders the app headless and checks the live blur.
param(
    [string]$PackageDir = "",
    [switch]$NuGet,
    [string]$Version = "",
    [switch]$Check,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path "$PSScriptRoot/..").Path
$Properties = @()

if ($NuGet) {
    # NuGet caches the nuget.org version lists for half an hour, which can hide a version published minutes ago.
    $Properties += "--property:RestoreNoHttpCache=true"
}
else {
    if (-not $PackageDir) {
        # A new prerelease version for every run: NuGet never replaces a package version it extracted once.
        $BaseVersion = dotnet msbuild "$RepoRoot/Flowery.NET/Flowery.NET.csproj" -getProperty:Version
        if ($LASTEXITCODE -ne 0) { throw "Reading the version failed." }
        $Version = "$BaseVersion-local.$(Get-Date -Format yyyyMMddHHmmss)"
        $PackageDir = "$RepoRoot/obj/Flowery.NET.PackageTest/feed"
        foreach ($Library in "Flowery.NET", "Flowery.NET.Skia") {
            dotnet pack "$RepoRoot/$Library/$Library.csproj" -c $Configuration -o $PackageDir "-p:Version=$Version"
            if ($LASTEXITCODE -ne 0) { throw "Packing $Library failed." }
        }
    }
    $Properties += "--property:FloweryPackageSource=$((Resolve-Path $PackageDir).Path)"
}
if ($Version) {
    $Properties += "--property:FloweryPackageVersion=$Version"
}

$AppArguments = @()
if ($Check) {
    $AppArguments += "--check"
}
dotnet run --project "$RepoRoot/Flowery.NET.PackageTest/Flowery.NET.PackageTest.csproj" -c $Configuration @Properties -- @AppArguments
exit $LASTEXITCODE
