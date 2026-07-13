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
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform,

    [string]$Version,

    [switch]$SkipTests,

    [switch]$Open,

    [switch]$FrameworkDependent
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
    "-p:PublishDir=$outDir\",
    "-p:SelfContained=$($selfContained.ToString().ToLowerInvariant())",
    '-p:PublishSingleFile=false',
    '-p:WindowsAppSDKSelfContained=true',
    '--no-restore'
)

Invoke-DotNet -Arguments $publishArgs -WorkingDirectory $repoRoot

# Notes for the personal install
$readmePath = Join-Path $outDir 'RELEASE-README.txt'
@(
    "SAMT / سَمت — personal release"
    "Version:     $Version"
    "Built:       $stamp"
    "Platform:    $Platform ($rid)"
    "Config:      $configuration"
    "SelfContained: $selfContained"
    ""
    "Run:  Samt.App.exe"
    ""
    "Settings are stored at:"
    "  %LocalAppData%\SAMT\settings.json"
    ""
    "Not an official prayer timetable. Verify against your mosque."
    "Arabic UI uses Latin digits (0-9) for times and numbers."
) | Set-Content -Path $readmePath -Encoding UTF8

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

if ($Open) {
    Start-Process explorer.exe $outDir
}
