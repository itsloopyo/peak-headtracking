#!/usr/bin/env pwsh
# Setup build dependencies using Unity stub assemblies.
# This matches the CI build exactly — no game installation required.
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

# --- Default: build stub assemblies (matches CI exactly) ---

Write-Host "Building Unity stub assemblies..." -ForegroundColor Cyan

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

# Build UnityEngine.dll from UnityStubs.cs
$stubsPath = Join-Path $libPath "UnityStubs.cs"
if (-not (Test-Path $stubsPath)) {
    Write-Host "ERROR: lib/UnityStubs.cs not found" -ForegroundColor Red
    exit 1
}

$projContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AssemblyName>UnityEngine</AssemblyName>
    <NoWarn>CS0169;CS0649;CS0067;CS0660;CS0661</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="UnityStubs.cs" />
  </ItemGroup>
</Project>
"@
$projPath = Join-Path $libPath "Stub_UnityEngine.csproj"
$projContent | Out-File -FilePath $projPath -Encoding utf8

dotnet build $projPath -c Release -o $libPath --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Failed to build UnityEngine stub" -ForegroundColor Red; exit 1 }
Write-Host "  UnityEngine.dll (stubs)" -ForegroundColor Green
Remove-Item $projPath -ErrorAction SilentlyContinue

# Build UnityEngine.UI.dll from UnityUIStubs.cs (references UnityEngine.dll)
$uiProjContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AssemblyName>UnityEngine.UI</AssemblyName>
    <NoWarn>CS0169;CS0649;CS0067;CS0660;CS0661</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="UnityUIStubs.cs" />
    <Reference Include="UnityEngine"><HintPath>UnityEngine.dll</HintPath></Reference>
  </ItemGroup>
</Project>
"@
$uiProjPath = Join-Path $libPath "Stub_UnityEngine.UI.csproj"
$uiProjContent | Out-File -FilePath $uiProjPath -Encoding utf8

dotnet build $uiProjPath -c Release -o $libPath --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Failed to build UnityEngine.UI stub" -ForegroundColor Red; exit 1 }
Write-Host "  UnityEngine.UI.dll (stubs)" -ForegroundColor Green
Remove-Item $uiProjPath -ErrorAction SilentlyContinue

# Build empty module stubs
$emptySource = "// Empty stub assembly"
$emptySourcePath = Join-Path $libPath "EmptyStub.cs"
$emptySource | Out-File -FilePath $emptySourcePath -Encoding utf8

$emptyModules = @(
    "UnityEngine.CoreModule", "UnityEngine.IMGUIModule", "UnityEngine.UIModule",
    "UnityEngine.InputLegacyModule", "UnityEngine.TextRenderingModule",
    "UnityEngine.AnimationModule", "UnityEngine.PhysicsModule"
)

foreach ($moduleName in $emptyModules) {
    $emptyProjContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AssemblyName>$moduleName</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="EmptyStub.cs" />
  </ItemGroup>
</Project>
"@
    $emptyProjPath = Join-Path $libPath "Stub_$moduleName.csproj"
    $emptyProjContent | Out-File -FilePath $emptyProjPath -Encoding utf8
    dotnet build $emptyProjPath -c Release -o $libPath --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Failed to build $moduleName stub" -ForegroundColor Red; exit 1 }
    Remove-Item $emptyProjPath -ErrorAction SilentlyContinue
}

# Cleanup temp files
Remove-Item $emptySourcePath -ErrorAction SilentlyContinue
Remove-Item (Join-Path $libPath "*.deps.json") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $libPath "*.pdb") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $libPath "obj") -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Setup complete (stub assemblies)" -ForegroundColor Green
