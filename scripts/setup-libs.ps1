#!/usr/bin/env pwsh
# Setup build dependencies: BepInEx plus the Unity reference stubs compiled from
# cameraunlock-core/csharp/stubs. This matches the CI build exactly - no game
# installation required.
# Pass -UseGameDlls to copy real DLLs from a local Peak install instead.

param(
    [switch]$UseGameDlls
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libPath = Join-Path $projectRoot "lib"

if ($UseGameDlls) {
    Write-Host "Setting up from game installation..." -ForegroundColor Cyan

    $sharedModulesPath = Join-Path $projectRoot "cameraunlock-core\powershell"
    Import-Module (Join-Path $sharedModulesPath "GamePathDetection.psm1") -Force
    Import-Module (Join-Path $sharedModulesPath "ModLoaderSetup.psm1") -Force

    $gameId = 'Peak'
    $config = Get-GameConfig -GameId $gameId
    $gamePath = Find-GamePath -GameId $gameId

    if (-not $gamePath) {
        Write-GameNotFoundError -GameName 'Peak' -EnvVar $config.EnvVar -SteamFolder $config.SteamFolder
        exit 1
    }

    Write-Host "Found Peak at: $gamePath" -ForegroundColor Green

    $bepinexResult = Install-BepInEx -GamePath $gamePath -Architecture x64 -MajorVersion 5 -EnableConsole $true
    $bepinexPath = Get-BepInExCorePath -GamePath $gamePath

    $managedPath = Get-ChildItem -Path $gamePath -Filter "*_Data" -Directory |
        Select-Object -First 1 |
        ForEach-Object { Join-Path $_.FullName "Managed" }

    if (-not $managedPath -or -not (Test-Path $managedPath)) {
        Write-Host 'ERROR: Could not find Managed folder' -ForegroundColor Red
        exit 1
    }

    if (-not (Test-Path $libPath)) { New-Item -ItemType Directory -Path $libPath | Out-Null }

    foreach ($dll in @('BepInEx.dll', '0Harmony.dll')) {
        $src = Join-Path $bepinexPath $dll
        if (Test-Path $src) { Copy-Item $src $libPath -Force; Write-Host "  $dll" -ForegroundColor Green }
    }

    foreach ($dll in @(
        'UnityEngine.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.InputLegacyModule.dll',
        'UnityEngine.UI.dll', 'UnityEngine.UIModule.dll', 'UnityEngine.IMGUIModule.dll',
        'UnityEngine.PhysicsModule.dll', 'UnityEngine.TextRenderingModule.dll', 'UnityEngine.AnimationModule.dll'
    )) {
        $src = Join-Path $managedPath $dll
        if (Test-Path $src) { Copy-Item $src $libPath -Force; Write-Host "  $dll" -ForegroundColor Green }
    }

    Write-Host "Setup complete (game DLLs)" -ForegroundColor Green
    exit 0
}

# --- Default: vendored/downloaded loader plus the shared Unity reference stubs ---

Write-Host "Setting up build references (stubs + BepInEx)..." -ForegroundColor Cyan

if (-not (Test-Path $libPath)) { New-Item -ItemType Directory -Path $libPath | Out-Null }

# Download BepInEx DLLs if not present
if (-not (Test-Path (Join-Path $libPath "BepInEx.dll"))) {
    Write-Host "  Downloading BepInEx..." -ForegroundColor Gray
    $bepUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.4/BepInEx_win_x64_5.4.23.4.zip"
    $bepZip = Join-Path $env:TEMP "BepInEx_setup.zip"
    Invoke-WebRequest -Uri $bepUrl -OutFile $bepZip -UseBasicParsing
    $bepTemp = Join-Path $env:TEMP "BepInEx_setup"
    if (Test-Path $bepTemp) { Remove-Item -Recurse -Force $bepTemp }
    Expand-Archive -Path $bepZip -DestinationPath $bepTemp -Force
    Copy-Item "$bepTemp/BepInEx/core/BepInEx.dll" $libPath -Force
    Copy-Item "$bepTemp/BepInEx/core/0Harmony.dll" $libPath -Force
    Remove-Item $bepZip -Force -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $bepTemp -ErrorAction SilentlyContinue
    Write-Host "  BepInEx.dll, 0Harmony.dll" -ForegroundColor Green
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

Write-Host "Setup complete (stub assemblies)" -ForegroundColor Green
