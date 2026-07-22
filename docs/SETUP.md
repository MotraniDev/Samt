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

### Publish to GitHub Releases (in-app updates)

Builds the installer, writes `samt-release.json` (version + download URL + SHA-256), and creates a GitHub Release so Settings → **Check for updates** can find it:

```powershell
.\scripts\publish-github-release.ps1 -Version 2026.7.22 -SkipTests
```

Requires [GitHub CLI](https://cli.github.com/) authenticated with `repo` scope. Manifest URL used by the app:

```text
https://github.com/MotraniDev/Samt/releases/latest/download/samt-release.json
```

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

On the first **interactive** launch (not `--autostart`), SAMT opens the **first-run setup wizard**:

1. **Location** — Windows GPS, place-name search (Nominatim), or a default Algerian city seed.
2. **Prayer times** — confirm calculation method and Asr madhab; preview today’s times.
3. **Adhkar schedule** — optional reminders and default times.
4. **Quick tour** — tray, Today, alerts, Adhkar reader.

You can **Skip** at any time. Skip keeps smart defaults (Kennadsa seed, Algeria method, Arabic UI, Adhkar reminders off) and does **not** request GPS. Login auto-start never shows the wizard.

Afterwards (or after Skip):

1. Re-check **Today** times against your mosque once.
2. On **Alerts**, confirm pre-alert minutes and channels (toast / overlay / audio).
3. Language and theme: **Settings**. Digits always **0–9** (Latin).
4. Close the window to hide to the **tray**; use tray **Exit** to stop notifications.
5. Optional: **Settings → Transparency** for shell window opacity (Adhan overlay stays independent).
6. **Calendar** (nav): Hijri-month grid with dual Gregorian labels. Special-day highlights are always shown; **toast reminders** default off — enable under **Settings → Calendar & Hijri** (master + Islamic set and/or country set). Hijri day offset lives there too (shared with Today). Diagnostics shows the offset read-only.
7. Optional **Google Calendar link** (user personal reminders only): see below.

## Google Calendar link (optional)

Bidirectional sync of **user calendar reminders** only with a dedicated Google calendar named **SAMT**. Prayer times and special days are **not** synced. Default off.

### One-time OAuth client (publisher / self-host)

1. In [Google Cloud Console](https://console.cloud.google.com/), create a project (or use MotraniSoft’s).
2. Enable **Google Calendar API**.
3. Create OAuth consent screen (External or Internal).
4. Create credentials → **OAuth client ID** → application type **Desktop app**.
5. Download the JSON (or copy client id + secret).

Place either:

- `%LocalAppData%\SAMT\google-oauth-client.json` with shape:
  ```json
  { "client_id": "….apps.googleusercontent.com", "client_secret": "…" }
  ```
  (Google’s downloaded `installed` wrapper is also accepted), or
- Environment variables `SAMT_GOOGLE_CLIENT_ID` and `SAMT_GOOGLE_CLIENT_SECRET`.

Then in the app: **Settings → Calendar & Hijri → Connect Google**. Tokens are stored under `%LocalAppData%\SAMT\google-tokens\` (DPAPI-protected).

Disconnect keeps local reminders; **Disconnect & delete cloud calendar** removes the SAMT calendar on Google.

## Auto-start

- Default: **on**. Registers under the current user’s Run key:
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `SAMT`
- Command: `"…\Samt.App.exe" --autostart` (starts minimized to tray).
- Toggle off on **Diagnostics** if you do not want login launch.

## Missed alerts

If the PC slept through a prayer **start** (or the app started late), SAMT may show a short **missed** toast/balloon. It does **not** replay adhan. Disable under Diagnostics if unwanted.

If special-day reminders are enabled and the morning fire was missed while asleep, a separate **special-day missed** summary toast may appear (still no audio). Same resume toggle governs both.

## Release checklist (human)

- [ ] Clean install from zip on Windows 10 and/or 11
- [ ] Auto-start after sign-in; tray resident
- [ ] Settings survive restart
- [ ] Fajr / Friday / day rollover once each in real use
- [ ] Spot-check working set on Diagnostics after multi-hour tray residence

## Not included

- Store listing, signed MSIX, full-app cloud backup / multi-device settings sync
- Official prayer authority — always verify locally
- Google sync of prayer times or special days (user reminders only when linked)
