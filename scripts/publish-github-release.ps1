<#
.SYNOPSIS
  Build the Windows installer and publish a GitHub Release with samt-release.json.

.DESCRIPTION
  Runs installer.ps1, writes artifacts/installer/samt-release.json (ADR 0001),
  then creates a GitHub Release so in-app update checks can discover it at:
  https://github.com/MotraniDev/Samt/releases/latest/download/samt-release.json

.EXAMPLE
  .\scripts\publish-github-release.ps1 -Version 2026.7.22 -SkipTests

.EXAMPLE
  .\scripts\publish-github-release.ps1 -Draft
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform,

    [string]$Version,

    [switch]$SkipTests,

    [switch]$Draft,

    [string]$Notes
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

# Prefer interactive/keyring gh credentials over a limited GITHUB_TOKEN env var.
Remove-Item Env:GITHUB_TOKEN -ErrorAction SilentlyContinue

if (-not $Platform) {
    $Platform = Get-DefaultPlatform
}

$repoRoot = Get-SamtRepoRoot
if (-not $Version) {
    $Version = (Get-Date -Format 'yyyy.M.d')
}

$versionParts = @($Version -split '[^\d]+' | Where-Object { $_ -ne '' })
while ($versionParts.Count -lt 3) {
    $versionParts += '0'
}
$appVersion = ($versionParts[0..2] -join '.')
$archTag = $Platform.ToLowerInvariant()
$tag = "v$appVersion"
$installerOut = Join-Path $repoRoot 'artifacts\installer'
$setupName = "SAMT-Setup-$appVersion-$archTag.exe"
$setupPath = Join-Path $installerOut $setupName
$manifestPath = Join-Path $installerOut 'samt-release.json'
$repoSlug = 'MotraniDev/Samt'

Write-SamtHeader "Publish GitHub Release ($tag | $Platform)"
Assert-DotNetSdk

$gh = Get-Command gh -ErrorAction SilentlyContinue
if (-not $gh) {
    throw 'GitHub CLI (gh) is required. Install: winget install GitHub.cli'
}

Write-SamtHeader 'Build installer'
$installerArgs = @{
    Platform = $Platform
    Version  = $appVersion
}
if ($SkipTests) {
    $installerArgs.SkipTests = $true
}
& (Join-Path $PSScriptRoot 'installer.ps1') @installerArgs

if (-not (Test-Path -LiteralPath $setupPath)) {
    $fallback = Get-ChildItem -Path $installerOut -Filter 'SAMT-Setup-*.exe' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $fallback) {
        throw "Installer not found: $setupPath"
    }
    $setupPath = $fallback.FullName
    $setupName = $fallback.Name
}

Write-SamtHeader 'Write samt-release.json'
$hash = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
$downloadUrl = "https://github.com/$repoSlug/releases/download/$tag/$setupName"
$manifest = [ordered]@{
    version     = $appVersion
    notes       = if ($Notes) { $Notes } else { "SAMT $appVersion" }
    downloadUrl = $downloadUrl
    sha256      = $hash
    minOs       = '10.0.17763'
}
$manifest | ConvertTo-Json -Depth 4 | ForEach-Object {
    [System.IO.File]::WriteAllText($manifestPath, $_ + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}
Write-SamtOk $manifestPath
Write-SamtInfo "SHA-256: $hash"
Write-SamtInfo "Download URL: $downloadUrl"

$ErrorActionPreference = 'Continue'
& gh release view $tag --repo $repoSlug 1>$null 2>$null
$releaseExists = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = 'Stop'
if ($releaseExists) {
    throw "Release $tag already exists. Delete it or choose a new -Version."
}

if (-not $Notes) {
    $Notes = @"
## SAMT $appVersion

Personal Windows release (per-user installer).

### Install
1. Download ``$setupName``
2. Run the setup (installs under ``%LocalAppData%\Programs\SAMT``)
3. Optional: Start with Windows

### In-app updates
This release includes ``samt-release.json`` for Settings → Check for updates.
"@
}

Write-SamtHeader "Create GitHub Release $tag"
$ghArgs = @(
    'release', 'create', $tag
    '--repo', $repoSlug
    '--title', "SAMT $appVersion"
    '--notes', $Notes
    $setupPath
    $manifestPath
)
if ($Draft) {
    $ghArgs += '--draft'
}

& gh @ghArgs
if ($LASTEXITCODE -ne 0) {
    throw "gh release create failed with exit code $LASTEXITCODE"
}

Write-SamtOk "Published: https://github.com/$repoSlug/releases/tag/$tag"
Write-SamtInfo "Manifest: https://github.com/$repoSlug/releases/latest/download/samt-release.json"
