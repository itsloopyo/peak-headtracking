#!/usr/bin/env pwsh
# Update prebuilt DLLs from build output
# Run this after making changes and commit the results before releasing

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$buildOutput = Join-Path $projectRoot "src\PeakHeadTracking\bin\Release\net472"
$prebuiltDir = Join-Path $projectRoot "prebuilt"

Write-Host "Updating prebuilt DLLs..." -ForegroundColor Cyan

if (-not (Test-Path $buildOutput)) {
    Write-Host "ERROR: Build output not found at $buildOutput" -ForegroundColor Red
    Write-Host "Run 'pixi run build' first." -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $prebuiltDir)) {
    New-Item -ItemType Directory -Path $prebuiltDir -Force | Out-Null
}

$dlls = @(
    "PeakHeadTracking.dll",
    "CameraUnlock.Core.dll",
    "CameraUnlock.Core.Unity.dll"
)

foreach ($dll in $dlls) {
    $srcPath = Join-Path $buildOutput $dll
    if (Test-Path $srcPath) {
        Copy-Item $srcPath -Destination $prebuiltDir -Force
        Write-Host "  Updated: $dll" -ForegroundColor Green
    } else {
        Write-Host "ERROR: Missing build output: $dll" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "Prebuilt DLLs updated. Don't forget to commit them:" -ForegroundColor Yellow
Write-Host "  git add prebuilt/" -ForegroundColor White
Write-Host "  git commit -m 'Update prebuilt DLLs'" -ForegroundColor White
