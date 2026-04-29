#!/usr/bin/env pwsh
#Requires -Version 5.1
# Custom packaging for Peak Head Tracking.
# Produces two ZIPs:
#   - PeakHeadTracking-v{version}-installer.zip (GitHub Release: install.cmd + plugins/ + docs)
#   - PeakHeadTracking-v{version}-nexus.zip     (Nexus Mods: extract-to-game-folder layout)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

$csprojPath = Join-Path $projectDir "src\PeakHeadTracking\PeakHeadTracking.csproj"
$version = Get-CsprojVersion $csprojPath

$buildOutputDir = Join-Path $projectDir "src\PeakHeadTracking\bin\Release\net472"
$scriptsDir = Join-Path $projectDir "scripts"
$releaseDir = Join-Path $projectDir "release"

$modDlls = @("PeakHeadTracking.dll", "CameraUnlock.Core.dll", "CameraUnlock.Core.Unity.dll")

Write-Host "=== Peak Head Tracking - Package Release ===" -ForegroundColor Magenta
Write-Host ""
Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host ""

# Validate all DLLs exist
foreach ($dll in $modDlls) {
    $dllPath = Join-Path $buildOutputDir $dll
    if (-not (Test-Path $dllPath)) {
        throw "Required DLL not found: $dllPath"
    }
}

# Validate required scripts
foreach ($script in @("install.cmd", "uninstall.cmd")) {
    $scriptPath = Join-Path $scriptsDir $script
    if (-not (Test-Path $scriptPath)) {
        throw "Required script not found: $scriptPath"
    }
}

# Create release directory
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

# Vendoring is install-time source of truth; refresh manually with `pixi run update-deps`.
$vendorBepDir = Join-Path $projectDir "vendor\bepinex"
$vendorBepZip = Join-Path $vendorBepDir "BepInExPack_PEAK.zip"
if (-not (Test-Path $vendorBepZip)) {
    throw "Bundled BepInEx vendor zip missing: $vendorBepZip. Run 'pixi run update-deps' to refresh."
}

# --- GitHub Release ZIP (with installer) ---

Write-Host "--- GitHub Release ZIP ---" -ForegroundColor Yellow
Write-Host ""

$ghStagingDir = Join-Path $releaseDir "staging-github"
if (Test-Path $ghStagingDir) { Remove-Item -Recurse -Force $ghStagingDir }
New-Item -ItemType Directory -Path $ghStagingDir -Force | Out-Null

# Copy install/uninstall scripts
foreach ($script in @("install.cmd", "uninstall.cmd")) {
    Copy-Item (Join-Path $scriptsDir $script) -Destination $ghStagingDir -Force
    Write-Host "  $script" -ForegroundColor Green
}

# Copy mod DLLs to plugins subfolder
$pluginsDir = Join-Path $ghStagingDir "plugins"
New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutputDir $dll) -Destination $pluginsDir -Force
    Write-Host "  plugins/$dll" -ForegroundColor Green
}

# Bundle vendored BepInEx (LGPL-2.1, see THIRD-PARTY-NOTICES.md) as install-time source.
$ghVendorDir = Join-Path $ghStagingDir "vendor\bepinex"
New-Item -ItemType Directory -Path $ghVendorDir -Force | Out-Null
foreach ($vendorFile in @("BepInExPack_PEAK.zip", "LICENSE", "README.md")) {
    $src = Join-Path $vendorBepDir $vendorFile
    if (Test-Path $src) {
        Copy-Item $src -Destination $ghVendorDir -Force
        Write-Host "  vendor/bepinex/$vendorFile" -ForegroundColor Green
    } elseif ($vendorFile -eq "BepInExPack_PEAK.zip") {
        throw "Required vendor file missing: $src. Run 'pixi run update-deps' to refresh."
    }
}

# Bundle the shared detection bundle for install.cmd's shim.
Copy-SharedBundle -StagingDir $ghStagingDir -CoreRoot (Join-Path $projectDir 'cameraunlock-core')

# Copy documentation
$docFiles = @("README.md", "LICENSE", "CHANGELOG.md", "THIRD-PARTY-NOTICES.md")
foreach ($doc in $docFiles) {
    $docPath = Join-Path $projectDir $doc
    if (Test-Path $docPath) {
        Copy-Item $docPath -Destination $ghStagingDir -Force
        Write-Host "  $doc" -ForegroundColor Green
    } elseif ($doc -eq "LICENSE") {
        Write-Host "  WARNING: $doc not found" -ForegroundColor Yellow
    }
}

$ghZipName = "PeakHeadTracking-v$version-installer.zip"
$ghZipPath = Join-Path $releaseDir $ghZipName
if (Test-Path $ghZipPath) { Remove-Item $ghZipPath -Force }

Write-Host ""
Write-Host "Creating GitHub ZIP..." -ForegroundColor Cyan

Push-Location $ghStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $ghZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $ghStagingDir

$ghZipSize = (Get-Item $ghZipPath).Length / 1KB
Write-Host ("  $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green

# --- Nexus Mods ZIP (extract-to-game-folder) ---

Write-Host ""
Write-Host "--- Nexus Mods ZIP ---" -ForegroundColor Yellow
Write-Host ""

$nexusStagingDir = Join-Path $releaseDir "staging-nexus"
if (Test-Path $nexusStagingDir) { Remove-Item -Recurse -Force $nexusStagingDir }

# Mirror game directory structure: BepInEx/plugins/
# Users extract to game root, DLLs land in <game>/BepInEx/plugins/
# Does NOT include BepInEx itself (dependency)
$nexusPluginsDir = Join-Path $nexusStagingDir "BepInEx\plugins"
New-Item -ItemType Directory -Path $nexusPluginsDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutputDir $dll) -Destination $nexusPluginsDir -Force
    Write-Host "  BepInEx/plugins/$dll" -ForegroundColor Green
}

$nexusZipName = "PeakHeadTracking-v$version-nexus.zip"
$nexusZipPath = Join-Path $releaseDir $nexusZipName
if (Test-Path $nexusZipPath) { Remove-Item $nexusZipPath -Force }

Write-Host ""
Write-Host "Creating Nexus ZIP..." -ForegroundColor Cyan

Push-Location $nexusStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $nexusZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $nexusStagingDir

$nexusZipSize = (Get-Item $nexusZipPath).Length / 1KB
Write-Host ("  $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# --- Thunderstore ZIP ---

Write-Host ""
Write-Host "--- Thunderstore ZIP ---" -ForegroundColor Yellow
Write-Host ""

$tsStagingDir = Join-Path $releaseDir "staging-thunderstore"
if (Test-Path $tsStagingDir) { Remove-Item -Recurse -Force $tsStagingDir }
New-Item -ItemType Directory -Path $tsStagingDir -Force | Out-Null

# manifest.json — update version from csproj
$manifestPath = Join-Path $projectDir "manifest.json"
if (-not (Test-Path $manifestPath)) {
    throw "manifest.json not found at project root"
}
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.version_number = $version
$manifest | ConvertTo-Json -Depth 10 | Out-File (Join-Path $tsStagingDir "manifest.json") -Encoding utf8
Write-Host "  manifest.json (v$version)" -ForegroundColor Green

# icon.png — required 256x256
$iconPath = Join-Path $projectDir "assets\icon.png"
if (Test-Path $iconPath) {
    Copy-Item $iconPath -Destination $tsStagingDir -Force
    Write-Host "  icon.png" -ForegroundColor Green
} else {
    Write-Host "  WARNING: assets/icon.png not found - Thunderstore requires a 256x256 icon" -ForegroundColor Yellow
}

# README.md
Copy-Item (Join-Path $projectDir "README.md") -Destination $tsStagingDir -Force
Write-Host "  README.md" -ForegroundColor Green

# LICENSE
$licensePath = Join-Path $projectDir "LICENSE"
if (Test-Path $licensePath) {
    Copy-Item $licensePath -Destination $tsStagingDir -Force
    Write-Host "  LICENSE" -ForegroundColor Green
}

# CHANGELOG.md
$changelogPath = Join-Path $projectDir "CHANGELOG.md"
if (Test-Path $changelogPath) {
    Copy-Item $changelogPath -Destination $tsStagingDir -Force
    Write-Host "  CHANGELOG.md" -ForegroundColor Green
}

# Mod DLLs in plugins subfolder
$tsPluginsDir = Join-Path $tsStagingDir "plugins"
New-Item -ItemType Directory -Path $tsPluginsDir -Force | Out-Null
foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutputDir $dll) -Destination $tsPluginsDir -Force
    Write-Host "  plugins/$dll" -ForegroundColor Green
}

$tsZipName = "PeakHeadTracking-v$version-thunderstore.zip"
$tsZipPath = Join-Path $releaseDir $tsZipName
if (Test-Path $tsZipPath) { Remove-Item $tsZipPath -Force }

Write-Host ""
Write-Host "Creating Thunderstore ZIP..." -ForegroundColor Cyan

Push-Location $tsStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $tsZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $tsStagingDir

$tsZipSize = (Get-Item $tsZipPath).Length / 1KB
Write-Host ("  $tsZipPath ({0:N1} KB)" -f $tsZipSize) -ForegroundColor Green

# --- Summary ---

Write-Host ""
Write-Host "=== Package Complete ===" -ForegroundColor Magenta
Write-Host ""
Write-Host ("GitHub Release:  $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green
Write-Host ("Nexus Mods:      $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green
Write-Host ("Thunderstore:    $tsZipPath ({0:N1} KB)" -f $tsZipSize) -ForegroundColor Green

# Output all zip paths for CI capture (one per line)
Write-Output $ghZipPath
Write-Output $nexusZipPath
Write-Output $tsZipPath
