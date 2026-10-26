<#
.SYNOPSIS
    Runs the Flowery.NET Gallery on a Linux desktop (XFCE) in Docker and opens it in the browser.

.DESCRIPTION
    Publishes the Desktop Gallery self-contained for Linux to bin/linux-desktop, for the architecture of the Docker host
    (linux-x64 or linux-arm64), builds the image flowery-linux-desktop from docker/linux-desktop when it is missing or
    older than 7 days, then from the current base image and packages for Debian's security updates, and removes the
    image from before (docker/linux-desktop is the same as in Flowery.Uno, which builds the image under the same tag, so
    an existing one is reused). When a rebuild only for the age fails, it keeps the old image. It (re)starts the
    container flowery-desktop with the published app mounted read-only. The desktop is served by noVNC on
    http://localhost:6080 and by VNC on localhost:5901, both on 127.0.0.1 only, so the Flowery.Uno desktop on 6081 and
    5902 can run beside it. The Gallery starts on the desktop; its output is in /tmp/app.log.

    Take a screenshot from outside:
        docker exec flowery-desktop sh -c 'xwd -root -silent | xwdtopnm | pnmtopng' > shot.png
    Stop the desktop:
        docker rm -f flowery-desktop

.PARAMETER Configuration
    Build configuration to publish (default: Release).

.PARAMETER SkipPublish
    Reuse the app in bin/linux-desktop.

.PARAMETER RebuildImage
    Build the image again now, without the Docker build cache and from the current base image and packages, for example
    after a change in docker/linux-desktop or for a CVE. The tag is shared with Flowery.Uno.

.PARAMETER Geometry
    Size of the desktop (default: 1600x1000).

.PARAMETER Culture
    Culture of the app, such as de-DE, passed as LANG (default: the culture of the system; English without one). The
    desktop stays English, as the image has no locales.

.PARAMETER Port
    Local port of noVNC (default: 6080).

.PARAMETER VncPort
    Local port of VNC (default: 5901).

.PARAMETER NoBrowser
    Do not open the browser.

.EXAMPLE
    pwsh ./scripts/run-linux-desktop.ps1

.EXAMPLE
    pwsh ./scripts/run-linux-desktop.ps1 -SkipPublish -Geometry 1280x800 -Culture de-DE
#>
param(
    [string]$Configuration = "Release",
    [switch]$SkipPublish,
    [switch]$RebuildImage,
    [string]$Geometry = "1600x1000",
    [string]$Culture = (Get-Culture).Name,
    [int]$Port = 6080,
    [int]$VncPort = 5901,
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$desktopProject = Join-Path $repoRoot "Flowery.NET.Gallery.Desktop/Flowery.NET.Gallery.Desktop.csproj"
$appDir = Join-Path $repoRoot "bin/linux-desktop"
$appName = "Flowery.NET.Gallery.Desktop"
$image = "flowery-linux-desktop"
$container = "flowery-desktop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: docker was not found. Install Docker Desktop first." -ForegroundColor Red
    exit 1
}

# The architecture of the Linux containers decides the runtime identifier of the app.
$architecture = docker info --format '{{.Architecture}}' 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($architecture)) {
    Write-Host "ERROR: the Docker daemon does not answer. Start Docker Desktop first." -ForegroundColor Red
    exit 1
}
$rid = switch -Regex ($architecture.Trim()) {
    '^(x86_64|amd64)$' { "linux-x64" }
    '^(aarch64|arm64)$' { "linux-arm64" }
    default {
        Write-Host "ERROR: unsupported Docker architecture '$architecture'." -ForegroundColor Red
        exit 1
    }
}

if (-not $SkipPublish) {
    Write-Host "Publishing: $appName for $rid" -ForegroundColor Cyan
    Write-Host "  Configuration: $Configuration" -ForegroundColor Gray
    if (Test-Path $appDir) {
        Remove-Item -Recurse -Force $appDir
    }
    dotnet publish "$desktopProject" -c $Configuration -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$appDir"
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path (Join-Path $appDir $appName))) {
    Write-Host "ERROR: no published app in $appDir (run without -SkipPublish)" -ForegroundColor Red
    exit 1
}

# Debian's security updates reach the image only when it is built anew, so an image older than a week is built again
# from the current base image and packages; -RebuildImage does that at once, for example for a CVE.
$maxImageAgeDays = 7
$oldImage = docker image inspect -f '{{.Id}} {{.Created}}' $image 2>$null
$oldImageId, $imageCreated = if ($LASTEXITCODE -eq 0) { $oldImage -split ' ', 2 } else { $null, $null }
$buildReason = if (-not $oldImageId) {
    "missing"
}
elseif ($RebuildImage) {
    "requested"
}
else {
    # Created is UTC with nanoseconds, which DateTime does not parse; the seconds are enough.
    $created = [DateTime]::ParseExact($imageCreated.Substring(0, 19), "yyyy-MM-ddTHH:mm:ss",
        [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]"AssumeUniversal, AdjustToUniversal")
    $ageDays = ([DateTime]::UtcNow - $created).TotalDays
    if ($ageDays -gt $maxImageAgeDays) { "built $([Math]::Floor($ageDays)) days ago" }
}
if ($buildReason) {
    Write-Host "Building image: $image ($buildReason)" -ForegroundColor Cyan
    docker build --pull --no-cache -t $image (Join-Path $repoRoot "docker/linux-desktop")
    if ($LASTEXITCODE -ne 0) {
        if ($buildReason -in "missing", "requested") {
            exit $LASTEXITCODE
        }
        # Offline or at a Docker Hub rate limit the image from before still runs; the next start tries again.
        Write-Host "WARNING: the image could not be built again; using the one from before." -ForegroundColor Yellow
        $oldImageId = $null
    }
}
else {
    $oldImageId = $null
}

# Only this script's own container is replaced.
docker container inspect $container *> $null
if ($LASTEXITCODE -eq 0) {
    docker rm -f $container | Out-Null
}

# A new build leaves the image from before untagged (about 1 GB). A container of Flowery.Uno that still uses it keeps
# it; then it stays until docker image prune.
if ($oldImageId) {
    docker image rm $oldImageId *> $null
}

# .NET on Linux reads its culture from LANG, such as de_DE.UTF-8.
if ([string]::IsNullOrWhiteSpace($Culture)) {
    $Culture = "en-US"
}
$lang = $Culture.Replace("-", "_") + ".UTF-8"

$runArgs = @(
    "run", "-d", "--name", $container,
    "-p", "127.0.0.1:${Port}:6080", "-p", "127.0.0.1:${VncPort}:5901",
    "-e", "GEOMETRY=$Geometry",
    "-e", "LANG=$lang",
    "-e", "APP_PATH=/opt/app/$appName",
    "-v", "${appDir}:/opt/app:ro",
    "--shm-size=512m"
)
if ($rid -eq "linux-arm64") {
    # libSkiaSharp.so on linux-arm64 calls libuuid and libfreetype without linking them (found in Flowery.Uno).
    $runArgs += @("-e", "LD_PRELOAD=libuuid.so.1 libfreetype.so.6")
}
$runArgs += $image

Write-Host "Starting container: $container" -ForegroundColor Cyan
Write-Host "  Runtime: $rid, culture: $Culture" -ForegroundColor Gray
docker @runArgs | Out-Null
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$ready = $false
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    try {
        Invoke-WebRequest -Uri "http://127.0.0.1:$Port/vnc.html" -UseBasicParsing -TimeoutSec 2 | Out-Null
        $ready = $true
        break
    }
    catch {
        Start-Sleep -Seconds 1
    }
}

if (-not $ready) {
    Write-Host "ERROR: the desktop did not answer on port $Port." -ForegroundColor Red
    Write-Host "  Running: $(docker inspect -f '{{.State.Running}}' $container 2>$null)" -ForegroundColor Gray
    docker logs --tail 20 $container
    exit 1
}

$url = "http://localhost:$Port/vnc.html?autoconnect=true&resize=scale"
Write-Host "Linux desktop: $url" -ForegroundColor Green
if (-not $NoBrowser) {
    Start-Process $url
}
