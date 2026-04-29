#!/usr/bin/env pwsh
#Requires -Version 5.1
# Bump vendored BepInExPack_PEAK (Thunderstore) and rewrite
# vendor/bepinex/{LICENSE,README.md}. Manual: dev runs this when they want a
# fresh upstream bump, then commits the result. CI never refreshes.
# See ~/.claude/CLAUDE.md "Vendoring Third-Party Dependencies".
#
# Thunderstore packs are pinned by URL (no version-range filter on the GitHub
# API). To bump, look up the latest BepInExPack_PEAK on
# https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/, edit
# $packVersion below, re-run, retest, commit.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

$module = Join-Path $projectDir 'cameraunlock-core/powershell/ModLoaderSetup.psm1'
if (-not (Test-Path $module)) {
    throw "ModLoaderSetup.psm1 not found at $module. Run 'pixi run sync' to update the cameraunlock-core submodule."
}
Import-Module $module -Force

$packVersion = '5.4.75301'
$out         = Join-Path $projectDir 'vendor/bepinex'
$packUrl     = "https://thunderstore.io/package/download/BepInEx/BepInExPack_PEAK/$packVersion/"

Refresh-VendoredLoader `
    -Name 'bepinex' `
    -OutputDir $out `
    -OutputFileName 'BepInExPack_PEAK.zip' `
    -DirectUrl $packUrl `
    -LicenseUrl 'https://raw.githubusercontent.com/BepInEx/BepInEx/master/LICENSE' | Out-Null

Write-Host ""
Write-Host "vendor/bepinex refreshed (BepInExPack_PEAK $packVersion). Review and commit." -ForegroundColor Green
