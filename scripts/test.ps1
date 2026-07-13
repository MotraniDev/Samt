<#
.SYNOPSIS
  Run SAMT unit tests.

.EXAMPLE
  .\scripts\test.ps1

.EXAMPLE
  .\scripts\test.ps1 -Configuration Release -Filter "PrayerEngine"
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Filter,

    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$repoRoot = Get-SamtRepoRoot
$testProject = Get-TestProjectPath $repoRoot

Write-SamtHeader "SAMT tests ($Configuration)"
Assert-DotNetSdk
Write-SamtInfo "Repo: $repoRoot"

$args = @(
    'test', $testProject,
    '-c', $Configuration,
    '--verbosity', 'minimal'
)

if ($NoBuild) {
    $args += '--no-build'
}

if ($Filter) {
    $args += @('--filter', $Filter)
}

Invoke-DotNet -Arguments $args -WorkingDirectory $repoRoot
Write-SamtOk 'All tests completed.'
