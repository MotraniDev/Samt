<#
.SYNOPSIS
  Produce a personal Release publish folder for SAMT.

.DESCRIPTION
  Runs tests (unless -SkipTests), publishes the WinUI app as self-contained
  for the chosen platform, and copies output to artifacts/release/<version>-<rid>/.

  This is a folder-based personal release (not Microsoft Store / signed MSIX yet).

.EXAMPLE
  .\scripts\release.ps1

.EXAMPLE
  .\scripts\release.ps1 -Platform x64 -SkipTests

.EXAMPLE
  .\scripts\release.ps1 -Version 0.2.0 -Open

.EXAMPLE
  .\scripts\release.ps1 -Installer
  # Also builds artifacts\installer\SAMT-Setup-*.exe (requires Inno Setup 6)
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform,

    [string]$Version,

    [switch]$SkipTests,

    [switch]$Open,

    [switch]$FrameworkDependent,

    [switch]$Installer
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

if (-not $Platform) {
    $Platform = Get-DefaultPlatform
}

$repoRoot = Get-SamtRepoRoot
$rid = Get-RuntimeIdentifier -Platform $Platform
$appProject = Get-AppProjectPath $repoRoot
$configuration = 'Release'

if (-not $Version) {
    $Version = (Get-Date -Format 'yyyy.M.d')
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$releaseName = "SAMT-$Version-$rid"
$artifactsRoot = Join-Path $repoRoot 'artifacts\release'
$outDir = Join-Path $artifactsRoot $releaseName

Write-SamtHeader "SAMT release ($configuration | $Platform | $rid | v$Version)"
Assert-DotNetSdk
Write-SamtInfo "Repo: $repoRoot"
Write-SamtInfo "Output: $outDir"

if (-not $SkipTests) {
    Write-SamtHeader 'Test (Release)'
    & (Join-Path $PSScriptRoot 'test.ps1') -Configuration Release
}

if (Test-Path $outDir) {
    Write-SamtWarn "Removing existing output: $outDir"
    Remove-Item -Recurse -Force $outDir
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$selfContained = -not $FrameworkDependent
Write-SamtHeader "Publish (SelfContained=$selfContained)"

Write-SamtInfo 'Restore app for publish RID'
Invoke-DotNet -Arguments @(
    'restore', $appProject,
    '-r', $rid,
    "-p:Platform=$Platform"
) -WorkingDirectory $repoRoot

$publishArgs = @(
    'publish', $appProject,
    '-c', $configuration,
    '-r', $rid,
    "-p:Platform=$Platform",
    "-p:Version=$Version",
    "-p:PublishDir=$outDir\",
    "-p:SelfContained=$($selfContained.ToString().ToLowerInvariant())",
    '-p:PublishSingleFile=false',
    '-p:WindowsAppSDKSelfContained=true',
    # ReadyToRun often fails for WinUI RID publish without extra restore flags.
    '-p:PublishReadyToRun=false',
    '-p:PublishTrimmed=false',
    '--no-restore'
)

Invoke-DotNet -Arguments $publishArgs -WorkingDirectory $repoRoot

# Notes for the personal install (ASCII-safe for PowerShell script encoding)
$readmePath = Join-Path $outDir 'RELEASE-README.txt'
$readmeLines = @(
    'SAMT - personal release'
    "Version:     $Version"
    "Built:       $stamp"
    "Platform:    $Platform ($rid)"
    "Config:      $configuration"
    "SelfContained: $selfContained"
    ''
    'Install'
    '  1. Unzip this folder anywhere (no installer).'
    '  2. Run Samt.App.exe'
    '  3. Close the window to hide to the system tray (Exit from tray quits).'
    '  4. Or use scripts\installer.ps1 to build a Setup.exe'
    ''
    'Auto-start'
    '  Enabled by default (Diagnostics - App options).'
    '  Registers HKCU Run value SAMT with --autostart (starts minimized).'
    ''
    'Settings'
    '  %LocalAppData%\SAMT\settings.json  (settings.bak backup)'
    ''
    'Digits: always Latin 0-9 (never Arabic-Indic).'
    'Not an official prayer timetable. Verify against your mosque.'
    'Full guide in repo: docs/SETUP.md'
)
$readmeLines | Set-Content -Path $readmePath -Encoding UTF8

# Zip for easy copy
$zipPath = Join-Path $artifactsRoot "$releaseName.zip"
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

Write-SamtHeader 'Zip'
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
Write-SamtInfo $zipPath

$exe = Get-ChildItem -Path $outDir -Filter 'Samt.App.exe' -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName

if ($exe) {
    Write-SamtOk "Release ready: $exe"
}
else {
    Write-SamtOk "Release folder ready: $outDir"
    Write-SamtWarn 'Samt.App.exe not found under output (check publish log).'
}

Write-SamtOk "Zip: $zipPath"

if ($Installer) {
    Write-SamtHeader 'Installer (Inno Setup)'
    $installerArgs = @{
        Platform           = $Platform
        Version            = $Version
        SkipPublish        = $true
        UseExistingPublish = $true
        SourceDir          = $outDir
        SkipTests          = $true
    }
    if ($Open) { $installerArgs.Open = $true }
    & (Join-Path $PSScriptRoot 'installer.ps1') @installerArgs
}

if ($Open -and -not $Installer) {
    Start-Process explorer.exe $outDir
}
