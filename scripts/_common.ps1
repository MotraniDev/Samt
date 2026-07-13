# Shared helpers for SAMT utility scripts. Dot-source only; do not run directly.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-SamtRepoRoot {
    $scriptsDir = $PSScriptRoot
    if (-not $scriptsDir) {
        $scriptsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    return (Resolve-Path (Join-Path $scriptsDir '..')).Path
}

function Write-SamtHeader {
    param([Parameter(Mandatory)][string]$Title)
    Write-Host ''
    Write-Host "==> $Title" -ForegroundColor Cyan
}

function Write-SamtOk {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "OK  $Message" -ForegroundColor Green
}

function Write-SamtInfo {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "    $Message" -ForegroundColor DarkGray
}

function Write-SamtWarn {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "!!  $Message" -ForegroundColor Yellow
}

function Assert-DotNetSdk {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet SDK not found on PATH. Install .NET 9 SDK and retry.'
    }

    $version = (& dotnet --version).Trim()
    Write-SamtInfo "dotnet SDK $version"
}

function Get-DefaultPlatform {
    switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        'X64' { 'x64' }
        'X86' { 'x86' }
        'Arm64' { 'ARM64' }
        default { 'x64' }
    }
}

function Get-RuntimeIdentifier {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('x64', 'x86', 'ARM64')]
        [string]$Platform
    )

    switch ($Platform) {
        'x64' { 'win-x64' }
        'x86' { 'win-x86' }
        'ARM64' { 'win-arm64' }
    }
}

function Get-AppProjectPath {
    param([Parameter(Mandatory)][string]$RepoRoot)
    Join-Path $RepoRoot 'src\Samt.App\Samt.App.csproj'
}

function Get-TestProjectPath {
    param([Parameter(Mandatory)][string]$RepoRoot)
    Join-Path $RepoRoot 'tests\Samt.Core.Tests\Samt.Core.Tests.csproj'
}

function Get-SolutionPath {
    param([Parameter(Mandatory)][string]$RepoRoot)
    Join-Path $RepoRoot 'Samt.sln'
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [string]$WorkingDirectory
    )

    $display = 'dotnet ' + ($Arguments -join ' ')
    Write-SamtInfo $display

    if ($WorkingDirectory) {
        Push-Location $WorkingDirectory
    }

    try {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet failed with exit code $LASTEXITCODE`nCommand: $display"
        }
    }
    finally {
        if ($WorkingDirectory) {
            Pop-Location
        }
    }
}

function Resolve-AppExePath {
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$Configuration,
        [Parameter(Mandatory)][string]$Platform,
        [Parameter(Mandatory)][string]$RuntimeIdentifier
    )

    $tfm = 'net9.0-windows10.0.26100.0'
    $candidates = @(
        (Join-Path $RepoRoot "src\Samt.App\bin\$Platform\$Configuration\$tfm\$RuntimeIdentifier\Samt.App.exe"),
        (Join-Path $RepoRoot "src\Samt.App\bin\$Configuration\$tfm\$RuntimeIdentifier\Samt.App.exe")
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}
