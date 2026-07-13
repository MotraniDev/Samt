# SAMT — personal setup guide

Short guide for the personal v1 install (folder publish, not Microsoft Store).

## Requirements

- Windows 10 22H2+ or Windows 11
- x64 (or matching published RID)
- No separate .NET install when using **self-contained** release output

## Install (Setup.exe — recommended)

From a clone (requires [Inno Setup 6](https://jrsoftware.org/isinfo.php); `winget install JRSoftware.InnoSetup`):

```powershell
.\scripts\installer.ps1 -Platform x64 -Open
```

Or after a normal release:

```powershell
.\scripts\release.ps1 -Platform x64 -Installer
```

Output:

```text
artifacts\installer\SAMT-Setup-<version>-x64.exe
```

- Installs to `%LocalAppData%\Programs\SAMT` (per-user; no admin by default)
- Start Menu shortcut; optional Desktop + Start with Windows
- Uninstall from Windows **Apps & features** or Start Menu

## Install (from release zip)

1. Run `.\scripts\release.ps1 -Platform x64` from a clone (or unpack a pre-built zip).
2. Open `artifacts\release\SAMT-<version>-win-x64\`.
3. Run `Samt.App.exe`.
4. Optional: pin the exe or leave **Start with Windows** enabled (Diagnostics → App options).

Settings live at:

```text
%LocalAppData%\SAMT\settings.json
```

A backup `settings.bak` is written next to it when saves succeed.

## First run

1. Confirm **location** (default Kennadsa) or add coordinates on **Locations**.
2. Check **Today** prayer times against your mosque once.
3. On **Alerts**, confirm pre-alert minutes and channels (toast / overlay / audio).
4. Language: pane footer **العربية** / **English**. Digits always **0–9** (Latin).
5. Close the window to hide to the **tray**; use tray **Exit** to stop notifications.

## Auto-start

- Default: **on**. Registers under the current user’s Run key:
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `SAMT`
- Command: `"…\Samt.App.exe" --autostart` (starts minimized to tray).
- Toggle off on **Diagnostics** if you do not want login launch.

## Missed alerts

If the PC slept through a prayer **start** (or the app started late), SAMT may show a short **missed** toast/balloon. It does **not** replay adhan. Disable under Diagnostics if unwanted.

## Release checklist (human)

- [ ] Clean install from zip on Windows 10 and/or 11
- [ ] Auto-start after sign-in; tray resident
- [ ] Settings survive restart
- [ ] Fajr / Friday / day rollover once each in real use
- [ ] Spot-check working set on Diagnostics after multi-hour tray residence

## Not included

- Store listing, signed MSIX, cloud sync
- Official prayer authority — always verify locally
