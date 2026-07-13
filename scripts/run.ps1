<#
.SYNOPSIS
  Build (optional) and run the SAMT WinUI app.

.DESCRIPTION
  Default path: build, then launch Samt.App.exe directly (stable for personal debug).

  Use -Packaged to launch via `dotnet run` / Windows App SDK packaging (identity,
  location capability testing). Packaged mode has been flaky with some WinUI builds.

.EXAMPLE
  .\scripts\run.ps1

.EXAMPLE
  .\scripts\run.ps1 -NoBuild

.EXAMPLE
  .\scripts\run.ps1 -Packaged
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform,

    [switch]$NoBuild,

    [switch]$SkipRestore,

    [switch]$Packaged
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

function Write-SamtCrashHint {
    param([int]$ExitCode)

    $unsigned = [uint32]$ExitCode
    $hex = '0x{0:X8}' -f $unsigned
    Write-Host "    Exit $ExitCode ($hex)" -ForegroundColor Yellow

    switch ($unsigned) {
        0xC00000FD { Write-SamtWarn 'STATUS_STACK_OVERFLOW — often infinite UI re-entry or recursive property/layout updates.' }
        0xC0000005 { Write-SamtWarn 'STATUS_ACCESS_VIOLATION — native crash; try rebuilding clean.' }
        0xC0000409 { Write-SamtWarn 'STATUS_STACK_BUFFER_OVERRUN — security cookie / corruption.' }
        default { }
    }
}

if (-not $Platform) {
    $Platform = Get-DefaultPlatform
}

$repoRoot = Get-SamtRepoRoot
$rid = Get-RuntimeIdentifier -Platform $Platform
$appProject = Get-AppProjectPath $repoRoot

Write-SamtHeader "SAMT run ($Configuration | $Platform | $rid)"
Assert-DotNetSdk
Write-SamtInfo "Repo: $repoRoot"
Write-SamtInfo 'Mode: unpackaged self-contained (WindowsAppSDKSelfContained=true)'

if (-not $NoBuild) {
    $buildArgs = @{
        Configuration = $Configuration
        Platform      = $Platform
    }
    if ($SkipRestore) {
        $buildArgs['NoRestore'] = $true
    }

    & (Join-Path $PSScriptRoot 'build.ps1') @buildArgs
}

if ($Packaged) {
    Write-SamtHeader 'Launch packaged (dotnet run)'
    $runArgs = @(
        'run',
        '--project', $appProject,
        '-c', $Configuration,
        "-p:Platform=$Platform",
        "-p:RuntimeIdentifier=$rid",
        '--no-build'
    )

    Write-SamtInfo ('dotnet ' + ($runArgs -join ' '))
    Push-Location $repoRoot
    try {
        & dotnet @runArgs
        $code = $LASTEXITCODE
        if ($code -ne 0) {
            Write-SamtCrashHint -ExitCode $code
            throw "App exited with code $code"
        }
    }
    finally {
        Pop-Location
    }
}
else {
    Write-SamtHeader 'Launch Samt.App.exe'
    $exe = Resolve-AppExePath -RepoRoot $repoRoot -Configuration $Configuration -Platform $Platform -RuntimeIdentifier $rid
    if (-not $exe -or -not (Test-Path $exe)) {
        throw "Could not find Samt.App.exe. Build first or pass -Packaged."
    }

    Write-SamtInfo $exe
    $dir = Split-Path -Parent $exe
    $proc = Start-Process -FilePath $exe -WorkingDirectory $dir -PassThru
    Write-SamtOk "Started PID $($proc.Id). Close the window to stop the app."
    # Smoke-check longer than prior REGDB crash window (~4s without self-contained WASDK).
    Start-Sleep -Seconds 6
    if ($proc.HasExited) {
        $code = $proc.ExitCode
        Write-SamtCrashHint -ExitCode $code
        Write-SamtWarn 'If you see REGDB_E_CLASSNOTREG / Class not registered: rebuild with WindowsAppSDKSelfContained=true (already in csproj).'
        throw "App exited early with code $code"
    }

    # Confirm by name — PID alone can be confusing after exit
    $byName = Get-Process -Name 'Samt.App' -ErrorAction SilentlyContinue
    if (-not $byName) {
        throw 'Process finished during smoke check (Samt.App not listed).'
    }

    Write-SamtOk "App is still running (PID $($byName.Id)). Use: Stop-Process -Name Samt.App -Force"
    $log = Join-Path $env:LOCALAPPDATA 'SAMT\launch.log'
    if (Test-Path $log) {
        Write-SamtInfo "Launch log: $log"
        Get-Content $log -Tail 8 | ForEach-Object { Write-SamtInfo $_ }
    }
}
