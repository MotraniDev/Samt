# سَمت / SAMT

**English** | [العربية](#العربية)

Local Windows prayer-times app. Offline calculation, Arabic-first RTL UI, languages: **Arabic** (default), English, French, Spanish. **Latin digits only (0–9).**

**Not an official timetable.** Verify against your mosque.

**Publisher:** MotraniSoft · **Repo:** [github.com/MotraniDev/Samt](https://github.com/MotraniDev/Samt)

---

## Features

| Area | What you get |
|------|----------------|
| **Prayer times** | Offline astronomical calculation (Algeria method default + other profiles) |
| **Locations** | Saved places, **Windows GPS**, **free place-name search** (OpenStreetMap Nominatim), manual coordinates |
| **Alerts** | Toast, overlay, adhan audio, pre-alerts, Friday / Jumu‘ah rules |
| **Themes** | Curated packages: System, Light, Dark, Ramadan, Algeria, Morocco |
| **Adhkar** | Offline Morning / Evening / After-prayer / Sleep collections; mini reader with auto-advance option; optional scheduled reminders |
| **First run** | Optional setup wizard (location, prayer method, Adhkar schedule, quick tour); Skip uses smart defaults |
| **Transparency** | Global window opacity in Settings; Adhan overlay keeps its own opacity |
| **Updates** | Optional check against a GitHub Releases **JSON manifest**; download only after approval; SHA-256 verify |
| **Languages** | ar / en / fr / es — choose in **Settings** (not on the main chrome) |
| **Tray** | Close hides to tray; Exit from pane or tray menu |

## Screenshots / video

Add screenshots under `docs/media/` (or link releases). Video walkthrough welcome when available.

## Solution layout

| Project | Role |
|---------|------|
| `src/Samt.Core` | Domain model + offline prayer engine + Adhkar catalog |
| `src/Samt.App` | WinUI 3 shell (Today, Locations, Alerts, Adhkar, **Settings**, Diagnostics, Design lab) |
| `tests/Samt.Core.Tests` | Unit tests |
| `tools/GenerateBaseline` | Kennadsa regression CSV |
| `testdata/kennadsa` | Comparison fixtures |

## Requirements

- Windows 10 22H2+ or Windows 11
- .NET 9 SDK
- Windows App SDK runtime (restored via NuGet with the app)

## Scripts (PowerShell)

| Script | Purpose |
|--------|---------|
| `.\scripts\build.ps1` | Restore + build (optional `-Test`) |
| `.\scripts\test.ps1` | Unit tests |
| `.\scripts\run.ps1` | Build and launch |
| `.\scripts\release.ps1` | Publish zip under `artifacts/release/` |
| `.\scripts\release.ps1 -Installer` | Publish + Setup.exe (Inno Setup 6) |
| `.\scripts\installer.ps1` | Installer only |

```powershell
.\scripts\build.ps1 -Test
.\scripts\run.ps1
.\scripts\installer.ps1 -Platform x64 -Open
```

## Release update manifest

Publish `samt-release.json` on the GitHub Release (see ADR `docs/adr/0001-github-releases-update-distribution.md`):

```json
{
  "version": "2026.7.14",
  "notes": "Theme packages and Settings hub",
  "downloadUrl": "https://github.com/MotraniDev/Samt/releases/download/v2026.7.14/SAMT-Setup-2026.7.14-x64.exe",
  "sha256": "lowercase-hex-of-installer"
}
```

Default app URL:  
`https://github.com/MotraniDev/Samt/releases/latest/download/samt-release.json`

## Privacy

- Prayer calculation is fully offline.
- GPS and place search are optional and user-initiated; coordinates stay on device.
- Update check only contacts the configured release manifest URL when enabled.

---

<a id="العربية"></a>

## العربية

تطبيق مواقيت صلاة لـ Windows. الحساب **أوفلاين**، الواجهة **عربية أولاً** (RTL)، اللغات: **العربية** (افتراضي)، الإنجليزية، الفرنسية، الإسبانية. **الأرقام لاتينية 0–9 فقط.**

**ليس جدولاً رسمياً.** راجع مسجدك.

**الناشر:** MotraniSoft · **المستودع:** [github.com/MotraniDev/Samt](https://github.com/MotraniDev/Samt)

### المزايا

- مواقيت أذان محلية مع طرق حساب متعددة (افتراضي: طريقة الجزائر).
- مواقع محفوظة، **GPS**، **بحث بالاسم** (بيانات مفتوحة Nominatim)، وإدخال يدوي.
- تنبيهات: إشعار Windows، نافذة عائمة، صوت الأذان، تنبيه مسبق، الجمعة.
- **حزم مظهر:** تلقائي، فاتح، داكن، رمضان، الجزائر، المغرب.
- **أذكار** أوفلاين (صباح / مساء / بعد الصلاة / نوم) مع قارئ مصغّر وتذكير اختياري.
- **تحديثات اختيارية** عبر ملف JSON على GitHub Releases بموافقة المستخدم.
- **الإعدادات** للغة والمظهر والتحديثات والأذكار والروابط (GitHub).

### البناء

انظر أوامر PowerShell أعلاه. المثبّت يدعم العربية (افتراضي) والإنجليزية والفرنسية والإسبانية.
