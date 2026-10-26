<#
.SYNOPSIS
    Runs the Flowery.NET Gallery on a Linux desktop (XFCE) in Docker and opens it in the browser.

.DESCRIPTION
    Publishes the Desktop Gallery self-contained for linux-x64 to bin/linux-desktop, builds the image
    flowery-linux-desktop from docker/linux-desktop when it is missing, and (re)starts the container flowery-desktop
    with the published app mounted read-only. The desktop is served by noVNC on http://localhost:6080 and by VNC on
    localhost:5901, both on 127.0.0.1 only. The Gallery starts on the desktop; its output is in /tmp/app.log.

    Take a screenshot from outside:
        docker exec flowery-desktop sh -c 'xwd -root -silent | xwdtopnm | pnmtopng' > shot.png
    Stop the desktop:
        docker rm -f flowery-desktop

.PARAMETER Configuration
    Build configuration to publish (default: Release).

.PARAMETER SkipPublish
    Reuse the app in bin/linux-desktop.

.PARAMETER RebuildImage
    Build the image again, for example after a change in docker/linux-desktop.

.PARAMETER Geometry
    Size of the desktop (default: 1600x1000).

.PARAMETER NoBrowser
    Do not open the browser.

.EXAMPLE
    pwsh ./scripts/run-linux-desktop.ps1
    pwsh ./scripts/run-linux-desktop.ps1 -SkipPublish -Geometry 1280x800
#>
param(
    [string]$Configuration = "Release",
    [switch]$SkipPublish,
    [switch]$RebuildImage,
    [string]$Geometry = "1600x1000",
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$desktopProject = Join-Path $repoRoot "Flowery.NET.Gallery.Desktop/Flowery.NET.Gallery.Desktop.csproj"
$appDir = Join-Path $repoRoot "bin/linux-desktop"
$image = "flowery-linux-desktop"
$container = "flowery-desktop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: docker was not found. Start Docker Desktop first." -ForegroundColor Red
    exit 1
}

if (-not $SkipPublish) {
    Write-Host "Publishing: Flowery.NET.Gallery.Desktop for linux-x64" -ForegroundColor Cyan
    Write-Host "  Configuration: $Configuration" -ForegroundColor Gray
    dotnet publish "$desktopProject" -c $Configuration -r linux-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$appDir"
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path (Join-Path $appDir "Flowery.NET.Gallery.Desktop"))) {
    Write-Host "ERROR: no published app in $appDir (run without -SkipPublish)" -ForegroundColor Red
    exit 1
}

docker image inspect $image *> $null
if ($RebuildImage -or $LASTEXITCODE -ne 0) {
    Write-Host "Building image: $image" -ForegroundColor Cyan
    docker build -t $image (Join-Path $repoRoot "docker/linux-desktop")
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

# Only this script's own container is replaced.
docker container inspect $container *> $null
if ($LASTEXITCODE -eq 0) {
    docker rm -f $container | Out-Null
}

Write-Host "Starting container: $container" -ForegroundColor Cyan
docker run -d --name $container `
    -p 127.0.0.1:6080:6080 -p 127.0.0.1:5901:5901 `
    -e "GEOMETRY=$Geometry" `
    -v "${appDir}:/opt/app:ro" `
    --shm-size=512m `
    $image | Out-Null
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$url = "http://localhost:6080/vnc.html?autoconnect=true&resize=scale"
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    try {
        Invoke-WebRequest -Uri "http://127.0.0.1:6080/vnc.html" -UseBasicParsing -TimeoutSec 2 | Out-Null
        break
    }
    catch {
        Start-Sleep -Seconds 1
    }
}

Write-Host "Linux desktop: $url" -ForegroundColor Green
if (-not $NoBrowser) {
    Start-Process $url
}
