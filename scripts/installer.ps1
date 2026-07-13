<#
.SYNOPSIS
  Build a Windows Setup.exe installer for SAMT (Inno Setup).

.DESCRIPTION
  1. Publishes a self-contained Release build (unless -SkipPublish / -UseExistingPublish).
  2. Compiles packaging\Samt.iss with ISCC.exe into artifacts\installer\.

  Requires Inno Setup 6 (ISCC.exe).

.EXAMPLE
  .\scripts\installer.ps1

.EXAMPLE
  .\scripts\installer.ps1 -Platform x64 -Version 0.7.0 -SkipTests -Open

.EXAMPLE
  .\scripts\installer.ps1 -UseExistingPublish -SourceDir .\artifacts\release\SAMT-2026.7.13-win-x64
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform,

    [string]$Version,

    [switch]$SkipTests,

    [switch]$SkipPublish,

    [switch]$UseExistingPublish,

    [string]$SourceDir,

    [switch]$FrameworkDependent,

    [switch]$Open,

    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

if (-not $Platform) {
    $Platform = Get-DefaultPlatform
}

$repoRoot = Get-SamtRepoRoot
$rid = Get-RuntimeIdentifier -Platform $Platform

if (-not $Version) {
    $Version = (Get-Date -Format 'yyyy.M.d')
}

# Normalize to x.y.z for Inno VersionInfo
$versionParts = @($Version -split '[^\d]+' | Where-Object { $_ -ne '' })
while ($versionParts.Count -lt 3) {
    $versionParts += '0'
}
$appVersion = ($versionParts[0..2] -join '.')

$artifactsRoot = Join-Path $repoRoot 'artifacts\release'
$installerOut = Join-Path $repoRoot 'artifacts\installer'
$issPath = Join-Path $repoRoot 'packaging\Samt.iss'
$iconPath = Join-Path $repoRoot 'src\Samt.App\Assets\AppIcon.ico'
$archTag = $Platform.ToLowerInvariant()

Write-SamtHeader "SAMT installer (v$appVersion | $Platform | $rid)"
Assert-DotNetSdk
Write-SamtInfo "Repo: $repoRoot"

# --- Locate / produce publish folder ---
if ($SourceDir) {
    $publishDir = (Resolve-Path -LiteralPath $SourceDir).Path
}
elseif ($UseExistingPublish -or $SkipPublish) {
    $pattern = "SAMT-*-$rid"
    $candidate = Get-ChildItem -Path $artifactsRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like $pattern } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $candidate) {
        throw "No existing publish folder matching $pattern under $artifactsRoot. Run without -SkipPublish."
    }
    $publishDir = $candidate.FullName
    Write-SamtInfo "Reusing publish: $publishDir"
}
else {
    Write-SamtHeader 'Publish (via release.ps1)'
    $releaseScript = Join-Path $PSScriptRoot 'release.ps1'
    if ($SkipTests -and $FrameworkDependent) {
        & $releaseScript -Platform $Platform -Version $appVersion -SkipTests -FrameworkDependent
    }
    elseif ($SkipTests) {
        & $releaseScript -Platform $Platform -Version $appVersion -SkipTests
    }
    elseif ($FrameworkDependent) {
        & $releaseScript -Platform $Platform -Version $appVersion -FrameworkDependent
    }
    else {
        & $releaseScript -Platform $Platform -Version $appVersion
    }
    $publishDir = Join-Path $artifactsRoot "SAMT-$appVersion-$rid"
    if (-not (Test-Path -LiteralPath $publishDir)) {
        throw "Publish folder not found after release.ps1: $publishDir"
    }
}

$exe = Join-Path $publishDir 'Samt.App.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    $found = Get-ChildItem -Path $publishDir -Filter 'Samt.App.exe' -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($found) {
        $exe = $found.FullName
        $publishDir = $found.DirectoryName
    }
    else {
        throw "Samt.App.exe not found under publish folder: $publishDir"
    }
}

Write-SamtOk "App: $exe"
Write-SamtInfo "SourceDir: $publishDir"

if (-not (Test-Path -LiteralPath $issPath)) {
    throw "Missing Inno script: $issPath"
}

if (-not (Test-Path -LiteralPath $iconPath)) {
    Write-SamtWarn "AppIcon.ico missing at $iconPath - installer may use a default icon."
}

function Find-Iscc {
    param([string]$Explicit)

    if ($Explicit -and (Test-Path -LiteralPath $Explicit)) {
        return (Resolve-Path -LiteralPath $Explicit).Path
    }

    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $pf86 = ${env:ProgramFiles(x86)}
    $pf = $env:ProgramFiles
    $local = $env:LocalAppData

    $candidates = @(
        (Join-Path $pf86 'Inno Setup 6\ISCC.exe'),
        (Join-Path $pf 'Inno Setup 6\ISCC.exe'),
        (Join-Path $local 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path $pf86 'Inno Setup 5\ISCC.exe')
    )

    foreach ($p in $candidates) {
        if ($p -and (Test-Path -LiteralPath $p)) {
            return $p
        }
    }

    return $null
}

$iscc = Find-Iscc -Explicit $IsccPath
if (-not $iscc) {
    Write-SamtWarn 'Inno Setup 6 (ISCC.exe) not found.'
    Write-Host ''
    Write-Host 'Install options:' -ForegroundColor Yellow
    Write-Host '  winget install --id JRSoftware.InnoSetup -e'
    Write-Host '  or download https://jrsoftware.org/isinfo.php'
    Write-Host ''
    Write-Host 'Then re-run:  .\scripts\installer.ps1' -ForegroundColor Cyan
    throw 'ISCC.exe required to build the Setup installer.'
}

Write-SamtInfo "ISCC: $iscc"
New-Item -ItemType Directory -Force -Path $installerOut | Out-Null

Write-SamtHeader 'Compile Setup.exe'
$isccArgs = @(
    $issPath
    "/DAppVersion=$appVersion"
    "/DSourceDir=$publishDir"
    "/DOutputDir=$installerOut"
    "/DArch=$archTag"
    "/DAppIcon=$iconPath"
)

Write-SamtInfo ("ISCC " + ($isccArgs -join ' '))
& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE"
}

$expectedName = "SAMT-Setup-$appVersion-$archTag.exe"
$setup = Get-ChildItem -Path $installerOut -Filter $expectedName -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $setup) {
    $setup = Get-ChildItem -Path $installerOut -Filter 'SAMT-Setup-*.exe' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

if (-not $setup) {
    throw "Setup.exe not found under $installerOut"
}

$notes = Join-Path $installerOut "SAMT-Setup-$appVersion-$archTag-README.txt"
$noteLines = @(
    'SAMT / SAMT - Windows installer'
    "Version:  $appVersion"
    "Platform: $Platform ($rid)"
    "Built:    $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ''
    "Installer: $($setup.Name)"
    ''
    'What it does'
    '  - Installs under %LocalAppData%\Programs\SAMT (per-user, no admin by default)'
    '  - Start Menu shortcut'
    '  - Optional Desktop shortcut'
    '  - Optional Start with Windows (--autostart)'
    '  - Uninstall via Windows Apps and features or Start Menu'
    ''
    'Settings remain at:'
    '  %LocalAppData%\SAMT\settings.json'
    ''
    'Not an official prayer timetable. Verify against your mosque.'
    'Latin digits only (0-9).'
)
$noteLines | Set-Content -Path $notes -Encoding UTF8

Write-SamtOk "Installer: $($setup.FullName)"
Write-SamtInfo ("Size: {0:N1} MB" -f ($setup.Length / 1MB))
Write-SamtInfo "Notes: $notes"

if ($Open) {
    Start-Process explorer.exe "/select,$($setup.FullName)"
}

Write-Host ''
Write-SamtOk 'Done. Distribute the Setup.exe to install SAMT.'
