param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
if (-not [OperatingSystem]::IsWindows()) { throw 'The native review checks require Windows.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'Flowery.NET.Tests.Native\Flowery.NET.Tests.Native.csproj'
$output = Join-Path ([IO.Path]::GetTempPath()) ('flowery-review-native-' + [Guid]::NewGuid().ToString('N'))
$buildArgs = @('build', $project, '-c', $Configuration)
if (Get-Command rtk -ErrorAction SilentlyContinue) { & rtk proxy dotnet @buildArgs }
else { & dotnet @buildArgs }
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$runArgs = @('run', '--project', $project, '-c', $Configuration, '--no-build', '--', $output)
if (Get-Command rtk -ErrorAction SilentlyContinue) { & rtk proxy dotnet @runArgs }
else { & dotnet @runArgs }
$runExit = $LASTEXITCODE
Write-Host ('Native evidence: {0}' -f $output)
exit $runExit
