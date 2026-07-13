# SAMT — domain context

**سَمت / SAMT** is a personal offline Windows prayer-times app (WinUI 3, C# / .NET).

## Product one-liner

Tray-resident app that calculates prayer times locally from coordinates, reminds the user (toast / overlay / adhan), Arabic-first RTL with English, French, and Spanish, **Latin digits only**. Optional network only for place-name search and update checks — never for prayer calculation.

## Glossary

| Term | Meaning |
|------|---------|
| **LocationProfile** | Named place: lat/lon, timezone id, optional fixed Jumu‘ah |
| **CalculationProfile** | Fajr/Isha/Maghrib rules, Asr madhab, high-latitude rule, minute offsets |
| **PrayerSchedule** | One civil day's computed events for a location |
| **PrayerEvent** | Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha, Imsak, Midnight, Jumuah |
| **NotificationRule** | When/how to notify (before prayer, start, Friday, dhikr) |
| **NotificationPlanner** | Builds fire times for a day from schedule + rules (no UI) |
| **LatinDigits** | Always display 0–9; never ٠–٩ |
| **Kennadsa** | Default seed location (Béchar, Algeria) |
| **Algeria method** | Default calc: Fajr 18°, Isha 17° (not an official claim) |
| **Location acquisition** | Explicit user choice of a `LocationProfile` source: Windows location service, optional free open place-name search, or manual entry; it never shares a location without consent and never requires a paid map API. |
| **Map place lookup** | Optional online place-name search to name a place and derive coordinates; used only when the user asks and network is available; manual entry remains the offline fallback. No paid Google Maps Platform. |
| **Manual location entry** | Offline entry of a named place and its coordinates, used when location services or map place lookup are unavailable or unwanted. |
| **App settings** | The user's preferences hub: display language, theme package, update check, adhkar reminders, and official community links; distinct from diagnostics, which shows technical process status only. |
| **Theme package** | A named, cohesive visual identity covering application surfaces, notifications, and overlays through colours, fonts, ornament, and background treatment. Curated packages only (no free-form theme builder). |
| **Theme customization** | Reserved for later; vNext ships fixed packages only. Readability, accessibility, and Latin-digit rules always apply. |
| **Adhkar collection** | A named set of remembrance texts (morning, evening, after prayer, sleep) bundled offline for reading and optional scheduled reminders. Expanded well-known subset with disclaimer — not a fatwa source. |
| **Adhkar window** | The time range when a collection may be prompted; defaults follow the day's prayer schedule (Sleep uses a local clock default) and may be enabled or disabled per collection in app settings. |
| **Adhkar reader** | A compact dedicated mini-window showing one remembrance at a time with navigation between items in the active collection. |
| **Release manifest** | A versioned JSON document published with GitHub Releases that identifies an available update, its notes, download, and integrity hash. |
| **Update check** | An opt-outable check for a newer release manifest that informs the user but never installs an update without approval; download is verified (SHA-256) before launching the installer. |
| **Display language** | The user-selected application language: Arabic by default, or English, French, or Spanish; it controls UI copy and layout direction without changing the Latin-digit rule. |
| **Installer language** | The language used by setup and update-installation screens; Arabic is the default when a choice is offered. |
| **Publisher identity** | MotraniSoft branding displayed as the application publisher; it uses approved light and dark logo assets and does not replace the SAMT product identity. |
| **Official community link** | A verified publisher-owned external link. GitHub is available now; Facebook and X remain absent from the UI until their official destinations are supplied. |

## Non-goals

Cloud sync, Store listing, mosque search, paid Google Maps, live web scrape of Adhkar, full Hisn-level / fatwa library, bundled Adhkar recitation audio (reader UI may prepare for it), free-form theme builder, silent auto-install of updates, claiming official prayer times.

## Stack

- `Samt.Core` — pure domain + engine + storage interfaces
- `Samt.App` — WinUI 3 shell (Today, Locations, Alerts, Adhkar, Settings, Diagnostics, DesignLab)
- Tests: xUnit, fixtures under `testdata/`

## Phases

0–7 done (shell, engine, today, locations, tray, toast, overlay, adhan, advanced rules + Friday, qibla, Hijri, Ramadan UI, light adhkar, polish & personal delivery).

Post-7 also delivered in-tree: Settings hub, fr/es, theme packages, place-name search, update check, expanded Adhkar suite, publisher branding & bilingual README.

**Phase 8** (see `docs/design/phase-8-ux-polish-onboarding.md`): Adhkar auto-advance, global window transparency (overlay independent), contextual Settings icons, app icon redesign, first-run setup wizard (Skip → smart defaults; hidden on `--autostart`).
