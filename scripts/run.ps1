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

function Show-SamtLaunchLog {
    $log = Join-Path $env:LOCALAPPDATA 'SAMT\launch.log'
    if (Test-Path $log) {
        Write-SamtInfo "Launch log: $log"
        Get-Content $log -Tail 10 | ForEach-Object { Write-SamtInfo $_ }
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

    # Note existing instance (single-instance will signal it to show, then exit 0).
    $existing = @(Get-Process -Name 'Samt.App' -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0) {
        $ids = ($existing | ForEach-Object Id) -join ', '
        Write-SamtInfo "Already running: $ids - will ask it to show the window"
    }

    $proc = Start-Process -FilePath $exe -WorkingDirectory $dir -PassThru
    Write-SamtOk "Started PID $($proc.Id)."
    Start-Sleep -Seconds 4

    $byName = @(Get-Process -Name 'Samt.App' -ErrorAction SilentlyContinue)

    if ($proc.HasExited) {
        $code = $proc.ExitCode
        if ($code -eq 0 -and $byName.Count -gt 0) {
            # Secondary instance exited after signaling primary (single-instance).
            $ids = ($byName | ForEach-Object Id) -join ', '
            Write-SamtOk "SAMT already running (PID $ids). Primary was asked to show."
            Write-SamtInfo 'If you still see no window: check the tray (overflow chevron), click SAMT -> Open.'
            Show-SamtLaunchLog
            return
        }

        Write-SamtCrashHint -ExitCode $code
        Write-SamtWarn 'If REGDB_E_CLASSNOTREG: rebuild with WindowsAppSDKSelfContained=true (already in csproj).'
        Show-SamtLaunchLog
        throw "App exited early with code $code"
    }

    if ($byName.Count -eq 0) {
        Show-SamtLaunchLog
        throw 'Process finished during smoke check (Samt.App not listed).'
    }

    $ids = ($byName | ForEach-Object Id) -join ', '
    Write-SamtOk "App is still running (PID $ids)."
    Write-SamtInfo 'Close the window to hide to tray. Tray menu: Open / Exit. Shell stop: Stop-Process -Name Samt.App -Force'
    Show-SamtLaunchLog
}
