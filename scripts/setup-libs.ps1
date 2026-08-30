#!/usr/bin/env pwsh
# Setup build dependencies: the BepInEx reference assemblies extracted from the
# committed vendor zip, plus the Unity reference stubs compiled from
# cameraunlock-core/csharp/stubs. This matches the CI build exactly - no game
# installation and no network access.
# Pass -UseGameDlls to take the Unity references from a local Peak install
# instead of the stubs. That is a validation opt-in, never the build default.

param(
    [switch]$UseGameDlls
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libPath = Join-Path $projectRoot "lib"
$vendorZip = Join-Path $projectRoot "vendor\bepinex\BepInExPack_PEAK.zip"

if (-not (Test-Path $vendorZip)) {
    throw "Vendored BepInEx not found at $vendorZip. Run 'pixi run update-deps' to fetch it, then commit."
}

New-Item -ItemType Directory -Path $libPath -Force | Out-Null

# Wipe lib/ so a game DLL left behind by -UseGameDlls can't mask CI parity.
# Everything the build needs is regenerated below.
Get-ChildItem -Path $libPath -Force | Remove-Item -Recurse -Force

Write-Host "Setting up build references..." -ForegroundColor Cyan

Add-Type -AssemblyName System.IO.Compression.FileSystem
$tempDir = Join-Path $env:TEMP ("peak-bep-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
try {
    [System.IO.Compression.ZipFile]::ExtractToDirectory($vendorZip, $tempDir)
    foreach ($dll in @('BepInEx.dll', '0Harmony.dll')) {
        $src = Join-Path $tempDir "BepInExPack_PEAK\BepInEx\core\$dll"
        if (-not (Test-Path $src)) { throw "$dll not found in vendor zip at BepInExPack_PEAK\BepInEx\core\" }
        Copy-Item $src (Join-Path $libPath $dll) -Force
        Write-Host "  BepInEx: $dll" -ForegroundColor Gray
    }
} finally {
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

$unityDlls = @(
    'UnityEngine.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.InputLegacyModule.dll',
    'UnityEngine.UI.dll', 'UnityEngine.UIModule.dll', 'UnityEngine.IMGUIModule.dll',
    'UnityEngine.PhysicsModule.dll', 'UnityEngine.TextRenderingModule.dll', 'UnityEngine.AnimationModule.dll'
)

if ($UseGameDlls) {
    Import-Module (Join-Path $projectRoot "cameraunlock-core\powershell\GamePathDetection.psm1") -Force

    $gameId = 'peak'
    $config = Get-GameConfig -GameId $gameId
    $gamePath = Find-GamePath -GameId $gameId
    if (-not $gamePath) {
        Write-GameNotFoundError -GameName 'Peak' -EnvVar $config.EnvVar -SteamFolder $config.SteamFolder
        exit 1
    }
    $managedPath = Get-ChildItem -Path $gamePath -Filter "*_Data" -Directory |
        Select-Object -First 1 |
        ForEach-Object { Join-Path $_.FullName "Managed" }
    if (-not $managedPath -or -not (Test-Path $managedPath)) {
        throw "Could not find the Managed folder under $gamePath"
    }

    Write-Host "  Unity references from: $managedPath" -ForegroundColor Gray
    foreach ($dll in $unityDlls) {
        $src = Join-Path $managedPath $dll
        if (-not (Test-Path $src)) { throw "$dll not found in $managedPath" }
        Copy-Item $src (Join-Path $libPath $dll) -Force
        Write-Host "  Unity: $dll" -ForegroundColor Gray
    }

    Write-Host "Setup complete (game Unity DLLs, vendored BepInEx)" -ForegroundColor Green
    exit 0
}

# Unity reference assemblies, compiled from the shared sources in the core submodule.
# The default -EmptyModule set on net472 is exactly the module list this mod's csproj
# references, so no override is needed here.
$stubBuilder = Join-Path $projectRoot "cameraunlock-core/csharp/stubs/build-unity-stubs.ps1"
if (-not (Test-Path $stubBuilder)) {
    throw "Shared stub builder not found at $stubBuilder. Run 'git submodule update --init'."
}
& $stubBuilder -OutputPath $libPath -TargetFramework net472
if ($LASTEXITCODE -ne 0) { throw "Stub build failed" }

Write-Host "Setup complete (stub assemblies, vendored BepInEx)" -ForegroundColor Green
