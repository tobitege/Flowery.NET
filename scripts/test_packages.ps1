# Runs Flowery.NET.PackageTest, an app that references Flowery.NET and Flowery.NET.Skia as NuGet packages only.
# Without -PackageDir or -NuGet it packs both projects first. -Check renders the app headless and checks the live blur.
[CmdletBinding(DefaultParameterSetName = "Pack")]
param(
    [Parameter(Mandatory, ParameterSetName = "Folder")]
    [string]$PackageDir,
    [Parameter(Mandatory, ParameterSetName = "NuGet")]
    [switch]$NuGet,
    [Parameter(ParameterSetName = "Folder")]
    [Parameter(ParameterSetName = "NuGet")]
    [string]$Version = "",
    [switch]$Check,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path "$PSScriptRoot/..").Path
$Properties = @()

if ($PSCmdlet.ParameterSetName -eq "Pack") {
    $Output = dotnet msbuild "$RepoRoot/Flowery.NET/Flowery.NET.csproj" -getProperty:Version
    if ($LASTEXITCODE -ne 0) { throw "Reading the version failed." }
    # The value is the last line; dotnet can print notices before it. A new prerelease version for every run, because
    # NuGet never replaces a package version it extracted once.
    $Version = "$(($Output | Select-Object -Last 1).Trim())-local.$(Get-Date -Format yyyyMMddHHmmss)"
    $PackageDir = "$RepoRoot/obj/Flowery.NET.PackageTest/feed"
    foreach ($Library in "Flowery.NET", "Flowery.NET.Skia") {
        dotnet pack "$RepoRoot/$Library/$Library.csproj" -c $Configuration -o $PackageDir "-p:Version=$Version"
        if ($LASTEXITCODE -ne 0) { throw "Packing $Library failed." }
    }
}
if ($PackageDir) {
    $Properties += "--property:FloweryPackageSource=$((Resolve-Path $PackageDir).Path)"
}
if ($NuGet) {
    # NuGet caches the nuget.org version lists for half an hour, which can hide a version published minutes ago.
    $Properties += "--property:RestoreNoHttpCache=true"
}
if ($Version) {
    $Properties += "--property:FloweryPackageVersion=$Version"
}

$AppArguments = @()
if ($Check) {
    $AppArguments += "--check"
}
$Project = "$RepoRoot/Flowery.NET.PackageTest/Flowery.NET.PackageTest.csproj"
dotnet run --project $Project -c $Configuration @Properties -- @AppArguments
exit $LASTEXITCODE
