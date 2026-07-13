# سَمت / SAMT

Local Windows prayer-times app (personal v1). Offline calculation, Arabic-first RTL UI, English supported.

**Not an official timetable.** Verify against your mosque.

## Solution layout

| Project | Role |
|---------|------|
| `src/Samt.Core` | Domain model + offline prayer engine |
| `src/Samt.App` | WinUI 3 shell + diagnostics screen (phase 0–1) |
| `tests/Samt.Core.Tests` | Unit tests (engine, rounding, fixtures) |
| `tools/GenerateBaseline` | Regenerates Kennadsa regression CSV |
| `testdata/kennadsa` | Comparison fixtures + source notes |

## Requirements

- Windows 10 22H2+ or Windows 11 (template min version is 1809; personal target is newer)
- .NET 9 SDK
- Windows App SDK runtime (restored via NuGet with the app)

## Build & test

```powershell
dotnet test tests\Samt.Core.Tests\Samt.Core.Tests.csproj -c Release
dotnet build src\Samt.App\Samt.App.csproj -c Debug -p:Platform=x64
```

Run the UI (from Visual Studio or):

```powershell
dotnet run --project src\Samt.App\Samt.App.csproj -p:Platform=x64
```

## Phase status

- [x] **Phase 0** — solution, domain models, ar/en strings, light/dark/system theme, RTL/LTR
- [x] **Phase 1** — pure prayer engine, methods, Asr madhab, high-latitude rules, diagnostics UI, Kennadsa baseline fixture
- [x] **Phase 2** — daily home + countdown, locations (manual/GPS), offline JSON settings, Latin digits (0–9)
- [ ] Phase 3 — tray + scheduling + toast
- [ ] Phase 4 — adhan audio + overlay window

## Settings path

`%LocalAppData%\SAMT\settings.json` (with `settings.bak` backup)

## Display digits

UI language may be Arabic (RTL), but **all times and numbers use Latin digits** (`0–9`), never Arabic-Indic (`٠–٩`).

## Default calculation

Algeria-style preset: **Fajr 18° / Isha 17°**, Maghrib = sunset, Asr standard (factor 1). Seed location: **Kennadsa**.

## License notes

Personal project. Adhan audio and branding assets must be licensed before any public release.
