<#
.SYNOPSIS
  Remove build outputs, obj folders, and optional artifacts.

.EXAMPLE
  .\scripts\clean.ps1

.EXAMPLE
  .\scripts\clean.ps1 -Artifacts
#>
[CmdletBinding()]
param(
    [switch]$Artifacts,

    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$repoRoot = Get-SamtRepoRoot
Write-SamtHeader 'SAMT clean'
Write-SamtInfo "Repo: $repoRoot"

$patterns = @('bin', 'obj')
$removed = 0

Get-ChildItem -Path $repoRoot -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $patterns -contains $_.Name } |
    ForEach-Object {
        Write-SamtInfo "Remove $($_.FullName)"
        if (-not $WhatIf) {
            Remove-Item -Recurse -Force $_.FullName -ErrorAction SilentlyContinue
        }
        $removed++
    }

if ($Artifacts) {
    $artifacts = Join-Path $repoRoot 'artifacts'
    if (Test-Path $artifacts) {
        Write-SamtInfo "Remove $artifacts"
        if (-not $WhatIf) {
            Remove-Item -Recurse -Force $artifacts
        }
        $removed++
    }
}

Write-SamtOk "Clean finished ($removed paths)."
