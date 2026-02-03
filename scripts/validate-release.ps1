#!/usr/bin/env pwsh
# Validate release readiness before tagging

$ErrorActionPreference = "Stop"

Write-Host "Validating release readiness..." -ForegroundColor Cyan

# Read manifest.json to get current version
$manifest = Get-Content "manifest.json" -Raw | ConvertFrom-Json
$version = $manifest.version

Write-Host "Current version in manifest.json: $version" -ForegroundColor Yellow

# Check 1: Verify version format is semantic versioning
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "❌ FAIL: Version '$version' is not valid semantic versioning (x.y.z)" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Version format is valid" -ForegroundColor Green

# Check 2: Verify CHANGELOG.md exists
if (-not (Test-Path "CHANGELOG.md")) {
    Write-Host "❌ FAIL: CHANGELOG.md does not exist" -ForegroundColor Red
    Write-Host "Create a CHANGELOG.md file to track version history" -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ CHANGELOG.md exists" -ForegroundColor Green

# Check 3: Verify CHANGELOG has entry for current version
$changelog = Get-Content "CHANGELOG.md" -Raw
if ($changelog -notmatch "\[?$version\]?") {
    Write-Host "❌ FAIL: CHANGELOG.md missing entry for version $version" -ForegroundColor Red
    Write-Host "Add a changelog entry with heading: ## [$version] - $(Get-Date -Format 'yyyy-MM-dd')" -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ CHANGELOG.md contains entry for v$version" -ForegroundColor Green

# Check 4: Verify tag doesn't already exist
$tagExists = git tag -l "v$version" 2>$null
if ($LASTEXITCODE -eq 0 -and $tagExists) {
    Write-Host "❌ FAIL: Git tag v$version already exists" -ForegroundColor Red
    Write-Host "Bump the version in manifest.json to create a new release" -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ Tag v$version does not yet exist" -ForegroundColor Green

# Check 5: Verify prebuilt DLLs exist
$prebuiltDir = "prebuilt"
$requiredDlls = @(
    "PeakHeadTracking.dll",
    "CameraUnlock.Core.dll",
    "CameraUnlock.Core.Unity.dll"
)

$missingDlls = @()
foreach ($dll in $requiredDlls) {
    $dllPath = Join-Path $prebuiltDir $dll
    if (-not (Test-Path $dllPath)) {
        $missingDlls += $dll
    }
}

if ($missingDlls.Count -gt 0) {
    Write-Host "❌ FAIL: Missing prebuilt DLLs: $($missingDlls -join ', ')" -ForegroundColor Red
    Write-Host "Run 'pixi run update-prebuilt' and commit the results" -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ Prebuilt DLLs present" -ForegroundColor Green

# Check 6: Verify prebuilt DLLs match build output (if build exists)
$buildOutput = "src/PeakHeadTracking/bin/Release/net472"
if (Test-Path $buildOutput) {
    $stale = @()
    foreach ($dll in $requiredDlls) {
        $prebuiltPath = Join-Path $prebuiltDir $dll
        $buildPath = Join-Path $buildOutput $dll
        if (Test-Path $buildPath) {
            $prebuiltHash = (Get-FileHash $prebuiltPath -Algorithm SHA256).Hash
            $buildHash = (Get-FileHash $buildPath -Algorithm SHA256).Hash
            if ($prebuiltHash -ne $buildHash) {
                $stale += $dll
            }
        }
    }

    if ($stale.Count -gt 0) {
        Write-Host "❌ FAIL: Prebuilt DLLs are stale: $($stale -join ', ')" -ForegroundColor Red
        Write-Host "Run 'pixi run update-prebuilt' and commit the results" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "✅ Prebuilt DLLs match build output" -ForegroundColor Green
} else {
    Write-Host "⚠️  Build output not found, skipping staleness check" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🎉 All validation checks passed!" -ForegroundColor Green
Write-Host ""
Write-Host "Ready to release v$version!" -ForegroundColor Cyan
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. git tag v$version" -ForegroundColor White
Write-Host "  2. git push origin main --tags" -ForegroundColor White
Write-Host ""
