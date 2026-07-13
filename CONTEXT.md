# SAMT — domain context

**سَمت / SAMT** is a personal offline Windows prayer-times app (WinUI 3, C# / .NET).

## Product one-liner

Tray-resident app that calculates prayer times locally from coordinates, reminds the user (toast / overlay / adhan later), Arabic-first RTL with full English, **Latin digits only**.

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

## Non-goals (v1 personal)

Cloud sync, Store listing, mosque search, full adhkar suite, claiming official times.

## Stack

- `Samt.Core` — pure domain + engine + storage interfaces
- `Samt.App` — WinUI 3 shell (Today, Locations, Diagnostics, DesignLab)
- Tests: xUnit, fixtures in `testdata/`

## Phases

0–7 done (shell, engine, today, locations, tray, toast, overlay, adhan, advanced rules + Friday, qibla, Hijri, Ramadan UI, light adhkar, polish & personal delivery).
