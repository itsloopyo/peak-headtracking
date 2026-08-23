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

# Stamp launcher-manifest.json with the real release version and copy it
# into the installer ZIP root. The launcher reads this file to decide how
# to stage the mod (native manifest deployment, with install.cmd as the
# legacy fallback).
$manifestSource = Join-Path $projectDir "launcher-manifest.json"
if (-not (Test-Path $manifestSource)) {
    throw "launcher-manifest.json not found at repo root ($manifestSource)"
}
$manifestJson = Get-Content $manifestSource -Raw | ConvertFrom-Json
$manifestJson.mod_info.version = $version
$manifestDest = Join-Path $ghStagingDir "launcher-manifest.json"
# Set-Content -Encoding UTF8 on Windows PowerShell 5.1 writes a BOM
# (EF BB BF) which serde_json rejects with "expected value at line 1
# column 1". Write through the .NET API with an explicit no-BOM encoder.
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText(
    $manifestDest,
    ($manifestJson | ConvertTo-Json -Depth 10),
    $utf8NoBom
)
Write-Host "  launcher-manifest.json (v$version)" -ForegroundColor Green

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
# The LGPL-2.1 licence has to travel with the binary we redistribute, so a
# missing LICENSE is a compliance failure, not something to skip quietly.
foreach ($vendorFile in @("BepInExPack_PEAK.zip", "LICENSE", "README.md")) {
    $src = Join-Path $vendorBepDir $vendorFile
    if (-not (Test-Path $src)) {
        throw "Required vendor file missing: $src. Run 'pixi run update-deps' to refresh."
    }
    Copy-Item $src -Destination $ghVendorDir -Force
    Write-Host "  vendor/bepinex/$vendorFile" -ForegroundColor Green
}

# Bundle the shared detection bundle for install.cmd's shim.
Copy-SharedBundle -StagingDir $ghStagingDir -CoreRoot (Join-Path $projectDir 'cameraunlock-core')

# Copy documentation. LICENSE and THIRD-PARTY-NOTICES.md carry the notices that
# every licence here requires to accompany the binaries, so a missing one fails
# the build rather than shipping a ZIP without them.
$docFiles = @("README.md", "LICENSE", "CHANGELOG.md", "THIRD-PARTY-NOTICES.md")
foreach ($doc in $docFiles) {
    $docPath = Join-Path $projectDir $doc
    if (-not (Test-Path $docPath)) {
        throw "Required document not found: $doc. Every published ZIP is a binary distribution and must carry it."
    }
    Copy-Item $docPath -Destination $ghStagingDir -Force
    Write-Host "  $doc" -ForegroundColor Green
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

# The Nexus ZIP is a binary distribution too: the licences of everything
# compiled into or bundled with the payload require their notices to travel
# with it, so LICENSE and THIRD-PARTY-NOTICES.md ship at its root.
foreach ($noticeDoc in @('LICENSE', 'THIRD-PARTY-NOTICES.md', 'README.md')) {
    $noticeSrc = Join-Path $projectDir $noticeDoc
    if (-not (Test-Path $noticeSrc)) {
        throw "Required notice file not found: $noticeDoc. Every published ZIP is a binary distribution and must carry it."
    }
    Copy-Item $noticeSrc -Destination $nexusStagingDir -Force
    Write-Host "  $noticeDoc" -ForegroundColor Green
}
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

# manifest.json - update version from csproj
$manifestPath = Join-Path $projectDir "manifest.json"
if (-not (Test-Path $manifestPath)) {
    throw "manifest.json not found at project root"
}
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.version_number = $version
$manifest | ConvertTo-Json -Depth 10 | Out-File (Join-Path $tsStagingDir "manifest.json") -Encoding utf8
Write-Host "  manifest.json (v$version)" -ForegroundColor Green

# icon.png - Thunderstore requires a 256x256 package icon. It is PEAK gameplay
# footage; see the "PEAK footage and screenshots" section of THIRD-PARTY-NOTICES.md,
# which is why that file has to ship in this ZIP too.
$iconPath = Join-Path $projectDir "assets\icon.png"
if (-not (Test-Path $iconPath)) {
    throw "assets/icon.png not found. Thunderstore requires a 256x256 package icon."
}
Copy-Item $iconPath -Destination $tsStagingDir -Force
Write-Host "  icon.png" -ForegroundColor Green

# The Thunderstore ZIP carries the same mod DLLs as the other two, so it is a
# binary distribution and the notices for everything compiled into or shipped
# beside them must travel with it.
foreach ($doc in @("README.md", "LICENSE", "CHANGELOG.md", "THIRD-PARTY-NOTICES.md")) {
    $docPath = Join-Path $projectDir $doc
    if (-not (Test-Path $docPath)) {
        throw "Required document not found: $doc. Every published ZIP is a binary distribution and must carry it."
    }
    Copy-Item $docPath -Destination $tsStagingDir -Force
    Write-Host "  $doc" -ForegroundColor Green
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
