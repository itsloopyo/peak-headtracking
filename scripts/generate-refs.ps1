#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate reference assemblies from Unity game DLLs using JetBrains Refasmer

.DESCRIPTION
    Creates reference-only assemblies (API surface only, no implementation)
    from the game's Unity DLLs. These can be committed to the repo for CI builds.

.PARAMETER GamePath
    Path to Peak installation. Auto-detected if not specified.

.EXAMPLE
    .\generate-refs.ps1
#>

param(
    [string]$GamePath
)

$ErrorActionPreference = "Stop"

Write-Host "Generating Unity Reference Assemblies" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Find game path
if (-not $GamePath) {
    $GamePath = if (Test-Path 'C:/Program Files (x86)/Steam/steamapps/common/Peak') {
        'C:/Program Files (x86)/Steam/steamapps/common/Peak'
    } elseif (Test-Path 'C:/Program Files/Steam/steamapps/common/Peak') {
        'C:/Program Files/Steam/steamapps/common/Peak'
    } elseif ($env:PEAK_GAME_PATH) {
        $env:PEAK_GAME_PATH
    } else {
        Write-Host "ERROR: Could not find Peak installation" -ForegroundColor Red
        exit 1
    }
}

$managedPath = Join-Path $GamePath "PEAK_Data/Managed"
if (-not (Test-Path $managedPath)) {
    Write-Host "ERROR: Managed folder not found at $managedPath" -ForegroundColor Red
    exit 1
}

Write-Host "Game path: $GamePath" -ForegroundColor Gray
Write-Host "Managed: $managedPath" -ForegroundColor Gray
Write-Host ""

# Check/install refasmer
$refasmerInstalled = $null -ne (Get-Command refasmer -ErrorAction SilentlyContinue)
if (-not $refasmerInstalled) {
    Write-Host "Installing JetBrains.Refasmer.CliTool..." -ForegroundColor Yellow
    dotnet tool install -g JetBrains.Refasmer.CliTool
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to install refasmer" -ForegroundColor Red
        exit 1
    }
}

# Create ref directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$refDir = Join-Path $projectRoot "ref"

if (Test-Path $refDir) {
    Remove-Item $refDir -Recurse -Force
}
New-Item -ItemType Directory -Path $refDir | Out-Null

Write-Host "Output directory: $refDir" -ForegroundColor Gray
Write-Host ""

# Unity assemblies we need
$unityDlls = @(
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.UI.dll",
    "UnityEngine.UIModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.Physics2DModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.AnimationModule.dll",
    "Unity.RenderPipelines.Core.Runtime.dll"
)

Write-Host "Generating reference assemblies..." -ForegroundColor Yellow
$success = 0
$failed = 0

foreach ($dll in $unityDlls) {
    $sourcePath = Join-Path $managedPath $dll
    $destPath = Join-Path $refDir $dll

    if (-not (Test-Path $sourcePath)) {
        Write-Host "  [SKIP] $dll (not found)" -ForegroundColor DarkGray
        continue
    }

    # Generate reference assembly (public API only)
    $result = refasmer --omit-non-api-members true -O $refDir $sourcePath 2>&1

    if ($LASTEXITCODE -eq 0 -and (Test-Path $destPath)) {
        $originalSize = (Get-Item $sourcePath).Length
        $refSize = (Get-Item $destPath).Length
        $reduction = [math]::Round((1 - $refSize / $originalSize) * 100, 1)
        Write-Host "  [OK] $dll ($reduction% smaller)" -ForegroundColor Green
        $success++
    } else {
        Write-Host "  [FAIL] $dll" -ForegroundColor Red
        Write-Host "         $result" -ForegroundColor DarkRed
        $failed++
    }
}

Write-Host ""
Write-Host "Complete: $success succeeded, $failed failed" -ForegroundColor Cyan
Write-Host ""
Write-Host "Reference assemblies saved to: $refDir" -ForegroundColor Green
Write-Host "These contain only API signatures (no implementation code)." -ForegroundColor Gray
Write-Host "Commit them to your repo for CI builds." -ForegroundColor Gray
Write-Host ""
