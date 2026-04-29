#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Automated release workflow for Peak Head Tracking mod.

.DESCRIPTION
    This script:
    1. Updates version in csproj and plugin source
    2. Builds and updates prebuilt DLLs
    3. Commits the changes
    4. Creates and pushes a git tag to trigger CI release

.PARAMETER Version
    The version to release (e.g., "1.0.0", "1.2.3")

.EXAMPLE
    pixi run release 1.0.0

.NOTES
    Run via: pixi run release <version>
#>
param(
    [Parameter(Position=0)]
    [string]$Version = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectDir "src\PeakHeadTracking\PeakHeadTracking.csproj"

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

Write-Host "=== Peak Head Tracking Release ===" -ForegroundColor Cyan
Write-Host ""

$currentVersion = Get-CsprojVersion $csprojPath

# If no version provided, show current and exit
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
    Write-Host $currentVersion -ForegroundColor White
    Write-Host ""
    Write-Host "Usage: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release <major|minor|patch|X.Y.Z>" -ForegroundColor White
    Write-Host ""
    Write-Host "Example: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release patch" -ForegroundColor White
    exit 0
}

# Resolve major/minor/patch into a concrete version (or accept literal X.Y.Z)
try {
    $Version = Resolve-ReleaseVersion -Argument $Version -CurrentVersion $currentVersion
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$tagName = "v$Version"

# Preconditions are the safety net. There is no further confirmation gate;
# `pixi run release ...` is the consent.
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "Error: Must be on 'main' branch to release (currently on '$currentBranch')" -ForegroundColor Red
    exit 1
}

$status = git status --porcelain -- ':!prebuilt/'
if ($status) {
    Write-Host "Error: Working directory has uncommitted changes" -ForegroundColor Red
    Write-Host $status -ForegroundColor Gray
    exit 1
}

$existingTag = git tag -l $tagName
if ($existingTag) {
    Write-Host "Error: Tag '$tagName' already exists" -ForegroundColor Red
    exit 1
}

Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host "New version:     $Version" -ForegroundColor Green
Write-Host ""

# Step 1: Update version in csproj
Write-Host "Updating version to $Version..." -ForegroundColor Cyan
Set-CsprojVersion $csprojPath $Version

# Step 2: Update version in plugin source
$pluginPath = Join-Path $projectDir "src\PeakHeadTracking\PeakHeadTrackingPlugin.cs"
$pluginContent = Get-Content $pluginPath -Raw
$pluginContent = $pluginContent -replace 'PLUGIN_VERSION = "[^"]+"', "PLUGIN_VERSION = `"$Version`""
$pluginContent | Set-Content $pluginPath -NoNewline
Write-Host "  Updated PeakHeadTrackingPlugin.cs" -ForegroundColor Gray

# Step 2b: Update version in manifest.json (Thunderstore)
$manifestPath = Join-Path $projectDir "manifest.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.version_number = $Version
$manifest | ConvertTo-Json -Depth 10 | Set-Content $manifestPath -NoNewline
Write-Host "  Updated manifest.json" -ForegroundColor Gray

# Step 3: Build and update prebuilt DLLs
Write-Host "Building release..." -ForegroundColor Cyan
Push-Location $projectDir
dotnet build src/PeakHeadTracking/PeakHeadTracking.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}

$prebuiltDir = Join-Path $projectDir "prebuilt"
if (-not (Test-Path $prebuiltDir)) {
    New-Item -ItemType Directory -Path $prebuiltDir -Force | Out-Null
}

$buildOutput = "src/PeakHeadTracking/bin/Release/net472"
Copy-Item "$buildOutput/PeakHeadTracking.dll" $prebuiltDir -Force
Copy-Item "$buildOutput/CameraUnlock.Core.dll" $prebuiltDir -Force
Copy-Item "$buildOutput/CameraUnlock.Core.Unity.dll" $prebuiltDir -Force
Write-Host "  Updated prebuilt DLLs" -ForegroundColor Gray
Pop-Location

# Step 4: Generate CHANGELOG
Write-Host "Generating CHANGELOG from commits..." -ForegroundColor Cyan
$changelogPath = Join-Path $projectDir "CHANGELOG.md"
$hasExistingTags = git tag -l 2>$null
if (-not $hasExistingTags) {
    # First release - write a basic changelog entry
    $date = Get-Date -Format 'yyyy-MM-dd'
    $firstEntry = "# Changelog`n`n## [$Version] - $date`n`nFirst release.`n"
    Set-Content $changelogPath $firstEntry
    Write-Host "  First release - wrote initial CHANGELOG entry" -ForegroundColor Gray
} else {
    try {
        $changelogArgs = @{
            ChangelogPath = $changelogPath
            Version = $Version
            ArtifactPaths = @(
                "src/PeakHeadTracking/",
                "cameraunlock-core",
                "scripts/",
                "prebuilt/",
                "manifest.json",
                "assets/",
                "README.md",
                "CHANGELOG.md",
                "LICENSE",
                ".github/"
            )
        }
        New-ChangelogFromCommits @changelogArgs
    } catch {
        # No commits found (e.g. after squash) - write a basic entry
        Write-Host "  No commits in range, writing manual changelog entry" -ForegroundColor Yellow
        $date = Get-Date -Format 'yyyy-MM-dd'
        $entry = "## [$Version] - $date`n`nRelease $Version.`n"
        $existing = if (Test-Path $changelogPath) { Get-Content $changelogPath -Raw } else { "# Changelog`n`n" }
        $existing = $existing -replace '(# Changelog\s*)', "`$1`n$entry`n"
        Set-Content $changelogPath $existing
    }
}

# Step 5: Commit
Write-Host "Committing changes..." -ForegroundColor Cyan
git add $csprojPath
git add $pluginPath
git add $manifestPath
git add "$projectDir/prebuilt"
git add $changelogPath
git commit -m "Release v$Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Commit failed!" -ForegroundColor Red
    exit 1
}

# Step 6: Create tag
Write-Host "Creating tag $tagName..." -ForegroundColor Cyan
git tag -a $tagName -m "Release $tagName"

# Step 7: Push
Write-Host "Pushing to GitHub..." -ForegroundColor Cyan
git push origin main
git push origin $tagName

Write-Host ""
Write-Host "Release $tagName initiated!" -ForegroundColor Green
Write-Host ""
Write-Host "The GitHub Actions release workflow will now:" -ForegroundColor Yellow
Write-Host "  - Build the release" -ForegroundColor White
Write-Host "  - Create GitHub release with artifacts" -ForegroundColor White
Write-Host ""
Write-Host "Watch progress at:" -ForegroundColor Yellow
Write-Host "  https://github.com/itsloopyo/peak-headtracking/actions" -ForegroundColor Cyan
