<#
.SYNOPSIS
  Build SAMT (solution and/or WinUI app).

.DESCRIPTION
  Restores and builds the solution. Optionally runs unit tests.
  Defaults to Debug + current machine architecture (usually x64).

.EXAMPLE
  .\scripts\build.ps1

.EXAMPLE
  .\scripts\build.ps1 -Configuration Release -Platform x64 -Test

.EXAMPLE
  .\scripts\build.ps1 -Configuration Debug -NoRestore
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform,

    [switch]$Test,

    [switch]$NoRestore,

    [switch]$CoreOnly
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

if (-not $Platform) {
    $Platform = Get-DefaultPlatform
}

$repoRoot = Get-SamtRepoRoot
$rid = Get-RuntimeIdentifier -Platform $Platform

Write-SamtHeader "SAMT build ($Configuration | $Platform | $rid)"
Assert-DotNetSdk
Write-SamtInfo "Repo: $repoRoot"

$solution = Get-SolutionPath $repoRoot
$appProject = Get-AppProjectPath $repoRoot
$testProject = Get-TestProjectPath $repoRoot

if (-not $NoRestore) {
    Write-SamtHeader 'Restore'
    Invoke-DotNet -Arguments @('restore', $solution) -WorkingDirectory $repoRoot

    if (-not $CoreOnly) {
        # WinUI app needs a RID-specific restore graph (win-x64 / win-arm64 / …).
        Invoke-DotNet -Arguments @(
            'restore', $appProject,
            '-r', $rid,
            "-p:Platform=$Platform"
        ) -WorkingDirectory $repoRoot
    }
}

Write-SamtHeader 'Build Core'
$coreArgs = @(
    'build', (Join-Path $repoRoot 'src\Samt.Core\Samt.Core.csproj'),
    '-c', $Configuration
)
if (-not $NoRestore) { $coreArgs += '--no-restore' }
Invoke-DotNet -Arguments $coreArgs -WorkingDirectory $repoRoot

if (-not $CoreOnly) {
    Write-SamtHeader 'Build App (WinUI)'
    $appArgs = @(
        'build', $appProject,
        '-c', $Configuration,
        "-p:Platform=$Platform",
        "-p:RuntimeIdentifier=$rid"
    )
    if (-not $NoRestore) { $appArgs += '--no-restore' }
    Invoke-DotNet -Arguments $appArgs -WorkingDirectory $repoRoot
}

if ($Test) {
    Write-SamtHeader 'Test'
    Invoke-DotNet -Arguments @(
        'test', $testProject,
        '-c', $Configuration,
        '--verbosity', 'minimal'
    ) -WorkingDirectory $repoRoot
}

$exe = Resolve-AppExePath -RepoRoot $repoRoot -Configuration $Configuration -Platform $Platform -RuntimeIdentifier $rid
if ($exe) {
    Write-SamtOk "Build finished. App: $exe"
}
else {
    Write-SamtOk 'Build finished.'
    if (-not $CoreOnly) {
        Write-SamtWarn 'Samt.App.exe not found at the usual output path (build may still have succeeded).'
    }
}
