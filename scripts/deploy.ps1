#!/usr/bin/env pwsh
# Deploy built mod to Peak BepInEx plugins folder
# Automatically installs BepInEx 5.x if not present

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

# Import shared game detection module
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$modulePath = Join-Path $projectRoot "cameraunlock-core\powershell\GamePathDetection.psm1"
Import-Module $modulePath -Force

$gameId = 'Peak'
$config = Get-GameConfig -GameId $gameId

# BepInEx version and download URL (PEAK-specific pack from Thunderstore)
$BepInExVersion = "5.4.75301"
$BepInExUrl = "https://thunderstore.io/package/download/BepInEx/BepInExPack_PEAK/$BepInExVersion/"
$BepInExSubfolder = "BepInExPack_PEAK"

# Find game installation
$gamePath = Find-GamePath -GameId $gameId

if (-not $gamePath) {
    Write-GameNotFoundError -GameName 'Peak' -EnvVar $config.EnvVar -SteamFolder $config.SteamFolder
    exit 1
}

Write-Host "Found game at: $gamePath" -ForegroundColor Green

# Install BepInEx if missing
$bepinexCorePath = Join-Path $gamePath "BepInEx/core"
if (-not (Test-Path $bepinexCorePath)) {
    Write-Host "BepInEx not found. Installing BepInEx $BepInExVersion..." -ForegroundColor Yellow

    $tempZip = Join-Path $env:TEMP "BepInEx_$BepInExVersion.zip"

    Write-Host "  Downloading from $BepInExUrl..." -ForegroundColor Gray
    Invoke-WebRequest -Uri $BepInExUrl -OutFile $tempZip -UseBasicParsing

    Write-Host "  Extracting to $gamePath..." -ForegroundColor Gray
    $tempExtract = Join-Path $env:TEMP "BepInEx_extract"
    if (Test-Path $tempExtract) { Remove-Item -Recurse -Force $tempExtract }
    Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force
    Copy-Item "$tempExtract/$BepInExSubfolder/*" $gamePath -Recurse -Force
    Remove-Item -Recurse -Force $tempExtract

    Remove-Item $tempZip -Force -ErrorAction SilentlyContinue

    Write-Host "  BepInEx installed successfully!" -ForegroundColor Green
    Write-Host "  NOTE: Run Peak once to let BepInEx initialize before deploying mods." -ForegroundColor Yellow
}

$pluginsPath = Join-Path $gamePath "BepInEx/plugins"
if (-not (Test-Path $pluginsPath)) {
    Write-Host "Creating BepInEx plugins folder..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $pluginsPath -Force | Out-Null
}

$buildPath = "src/PeakHeadTracking/bin/$Configuration/net472"

# Validate build output exists
if (-not (Test-Path $buildPath)) {
    Write-Host "ERROR: Build output not found at $buildPath" -ForegroundColor Red
    Write-Host "Please run 'pixi run build' first" -ForegroundColor Yellow
    exit 1
}

Write-Host "Deploying PeakHeadTracking ($Configuration) to BepInEx..." -ForegroundColor Green
Write-Host "  Source: $buildPath" -ForegroundColor Gray
Write-Host "  Target: $pluginsPath" -ForegroundColor Gray

# Copy DLLs
Copy-Item "$buildPath/PeakHeadTracking.dll" $pluginsPath -Force
Copy-Item "$buildPath/CameraUnlock.Core.dll" $pluginsPath -Force
Copy-Item "$buildPath/CameraUnlock.Core.Unity.dll" $pluginsPath -Force
Write-Host "  Copied: PeakHeadTracking.dll, CameraUnlock.Core.dll, CameraUnlock.Core.Unity.dll" -ForegroundColor Gray

Write-Host '' -ForegroundColor Green
Write-Host "[OK] Deployment complete!" -ForegroundColor Green
Write-Host "DLL location: $pluginsPath/PeakHeadTracking.dll" -ForegroundColor Cyan
Write-Host '' -ForegroundColor Green
Write-Host "Launch Peak to test your changes." -ForegroundColor Yellow
