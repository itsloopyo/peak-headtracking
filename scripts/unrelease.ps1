#!/usr/bin/env pwsh
# Unrelease script - safely revert a release
# Usage: unrelease.ps1 <version>
# Example: unrelease.ps1 1.0.1

param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

Write-Host "======================================" -ForegroundColor Red
Write-Host "   Unreleasing v$Version" -ForegroundColor Red
Write-Host "======================================" -ForegroundColor Red
Write-Host ""

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "❌ ERROR: Version '$Version' is not valid semantic versioning" -ForegroundColor Red
    Write-Host "Use format: X.Y.Z (e.g., 1.0.1)" -ForegroundColor Yellow
    exit 1
}

Write-Host "This will:" -ForegroundColor Yellow
Write-Host "  1. Delete the local git tag v$Version" -ForegroundColor White
Write-Host "  2. Delete the remote git tag v$Version from GitHub" -ForegroundColor White
Write-Host "  3. Revert the version bump commit (if it's the last commit) and force-push main" -ForegroundColor White
Write-Host ""
Write-Host "NOTE: This does NOT delete the GitHub Release." -ForegroundColor Yellow
Write-Host "  Delete it manually at:" -ForegroundColor Yellow
Write-Host "  https://github.com/itsloopyo/peak-headtracking/releases/tag/v$Version" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/4] Checking local tag..." -ForegroundColor Cyan
$localTag = git tag -l "v$Version" 2>$null
if ($LASTEXITCODE -ne 0 -or -not $localTag) {
    Write-Host "⚠️  Local tag v$Version does not exist" -ForegroundColor Yellow
} else {
    Write-Host "✅ Found local tag v$Version" -ForegroundColor Green
}
Write-Host ""

Write-Host "[2/4] Checking remote tag..." -ForegroundColor Cyan
$remoteTag = git ls-remote --tags origin "refs/tags/v$Version" 2>$null
if ($LASTEXITCODE -ne 0 -or -not $remoteTag) {
    Write-Host "⚠️  Remote tag v$Version does not exist" -ForegroundColor Yellow
} else {
    Write-Host "✅ Found remote tag v$Version" -ForegroundColor Green
}
Write-Host ""

if ($remoteTag) {
    Write-Host "[3/4] Deleting remote tag..." -ForegroundColor Cyan

    $prevErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "SilentlyContinue"

    git push origin --delete "v$Version" *>&1 | Out-Null
    $pushExitCode = $LASTEXITCODE

    $ErrorActionPreference = $prevErrorActionPreference

    if ($pushExitCode -eq 0) {
        Write-Host "✅ Remote tag v$Version deleted from GitHub" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Failed to delete remote tag (may not exist or no permissions)" -ForegroundColor Yellow
    }
} else {
    Write-Host "[3/4] Skipping remote tag deletion (doesn't exist)" -ForegroundColor Cyan
}
Write-Host ""

if ($localTag) {
    Write-Host "[4/4] Deleting local tag..." -ForegroundColor Cyan
    git tag -d "v$Version"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Local tag v$Version deleted" -ForegroundColor Green
    } else {
        Write-Host "❌ Failed to delete local tag" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[4/4] Skipping local tag deletion (doesn't exist)" -ForegroundColor Cyan
}
Write-Host ""

Write-Host "Checking for version bump commit..." -ForegroundColor Cyan
$lastCommit = git log -1 --pretty=format:"%s"
if ($lastCommit -match "^Release v$Version$" -or $lastCommit -match "chore: bump version to $Version") {
    Write-Host "Found version bump commit: $lastCommit" -ForegroundColor Yellow
    Write-Host "Reverting last commit..." -ForegroundColor Cyan
    git reset --hard HEAD~1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to revert commit" -ForegroundColor Red
        exit 1
    }
    Write-Host "Version bump commit reverted" -ForegroundColor Green

    Write-Host "Force-pushing main..." -ForegroundColor Cyan
    git push origin main --force
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Force push failed. Run manually: git push origin main --force" -ForegroundColor Red
        exit 1
    }
    Write-Host "Force push completed" -ForegroundColor Green
} else {
    Write-Host "Last commit is not a version bump for v$Version - leaving HEAD intact" -ForegroundColor Yellow
    Write-Host "  Last commit: $lastCommit" -ForegroundColor White
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "   Unrelease Complete" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Manually delete the GitHub Release at:" -ForegroundColor White
Write-Host "     https://github.com/itsloopyo/peak-headtracking/releases/tag/v$Version" -ForegroundColor Cyan
Write-Host "  2. Verify manifest.json has the correct version" -ForegroundColor White
Write-Host "  3. Verify CHANGELOG.md is up to date" -ForegroundColor White
Write-Host ""
