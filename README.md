# سَمت / SAMT

Local Windows prayer-times app (personal v1). Offline calculation, Arabic-first RTL UI, English supported.

**Not an official timetable.** Verify against your mosque.

## Solution layout

| Project | Role |
|---------|------|
| `src/Samt.Core` | Domain model + offline prayer engine |
| `src/Samt.App` | WinUI 3 shell (Today, Locations, Diagnostics, Design lab) |
| `tests/Samt.Core.Tests` | Unit tests (engine, rounding, fixtures) |
| `tools/GenerateBaseline` | Regenerates Kennadsa regression CSV |
| `testdata/kennadsa` | Comparison fixtures + source notes |

## Requirements

- Windows 10 22H2+ or Windows 11 (template min version is 1809; personal target is newer)
- .NET 9 SDK
- Windows App SDK runtime (restored via NuGet with the app)

## Scripts (PowerShell)

From the repo root:

| Script | Purpose |
|--------|---------|
| `.\scripts\build.ps1` | Restore + build (optional `-Test`) |
| `.\scripts\test.ps1` | Run unit tests |
| `.\scripts\run.ps1` | Build (unless `-NoBuild`) and launch the app |
| `.\scripts\release.ps1` | Release publish → `artifacts/release/` + zip |
| `.\scripts\clean.ps1` | Remove `bin`/`obj` (add `-Artifacts` for release output) |

Examples:

```powershell
# Debug build + tests
.\scripts\build.ps1 -Test

# Run the app
.\scripts\run.ps1

# Personal release (x64, self-contained)
.\scripts\release.ps1 -Platform x64 -Open

# Tests only
.\scripts\test.ps1 -Configuration Release
```

If script execution is blocked:

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

## Build & test (raw dotnet)

```powershell
dotnet test tests\Samt.Core.Tests\Samt.Core.Tests.csproj -c Release
dotnet build src\Samt.App\Samt.App.csproj -c Debug -p:Platform=x64
dotnet run --project src\Samt.App\Samt.App.csproj -p:Platform=x64
```

## Phase status

- [x] **Phase 0** — solution, domain models, ar/en strings, light/dark/system theme, RTL/LTR
- [x] **Phase 1** — pure prayer engine, methods, Asr madhab, high-latitude rules, diagnostics UI, Kennadsa baseline fixture
- [x] **Phase 2** — daily home + countdown, locations (manual/GPS), offline JSON settings, Latin digits (0–9)
- [x] **Phase 3** — tray icon, single-instance, notification host, prayer toasts (close hides to tray)
- [ ] Phase 4 — adhan audio + overlay window (see Design lab prototypes A/B/C)

## Agent skills (shortlist)

Configured in `AGENTS.md` + `docs/agents/`. Prefer: **frontend-design**, **rtl** concepts, **prototype**, **tdd**, **design**, **grill-me**, **check-work**.

## Settings path

`%LocalAppData%\SAMT\settings.json` (with `settings.bak` backup)

## Display digits

**Rule: always Latin digits.** UI language may be Arabic (RTL), but times and numbers always use `0–9`, never Arabic-Indic (`٠–٩`).

Implementation:

- Format via `Samt.Core.Formatting.LatinDigits`
- Numeric WinUI controls use `Language="en-US"` styles in `Themes/SamtTheme.xaml` (blocks XAML digit substitution)
- See `AGENTS.md`

## Default calculation

Algeria-style preset: **Fajr 18° / Isha 17°**, Maghrib = sunset, Asr standard (factor 1). Seed location: **Kennadsa**.

## License notes

Personal project. Adhan audio and branding assets must be licensed before any public release.
