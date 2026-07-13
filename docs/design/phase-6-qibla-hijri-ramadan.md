# Design: Phase 6 — Qibla, Hijri, Ramadan, Adhkar

**Status:** Implemented (v1 personal)  
**Stack:** `Samt.Core` (Hijri + Qibla math) + Today / Diagnostics / Adhkar pages (WinUI)

## Problem

After prayer times and alerts work, users still need everyday companions: dual calendar (Hijri + Gregorian), qibla bearing from the active location, Ramadan-aware times (Imsak), and a tiny offline adhkar surface — without cloud, compass hardware, or a full adhkar suite.

## Goals

1. **Hijri date** from civil date + `AppSettings.HijriDayOffset` (tabular `HijriCalendar` + day shift).
2. **Qibla** true-north bearing and great-circle distance from active lat/lon to the Kaaba.
3. **Ramadan mode** when Hijri month is 9: show Imsak (and Midnight), Maghrib labeled as Iftar; badge on Today.
4. **Adhkar page**: short offline morning / evening / after-prayer texts (ar + en). No scheduler, no audio.

## Non-goals

- Full adhkar library, timed dhikr notifications, or audio recitation.
- Live magnetic compass / device heading (bearing is static from coordinates).
- Official moon-sighting calendar; offset is the escape hatch.
- Claiming religious authority for conversion or qibla.

## Key decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Hijri engine | BCL `HijriCalendar` + `AddDays(offset)` | Deterministic offline; `HijriDayOffset` already on settings |
| Qibla | Spherical bearing + haversine km | Standard offline formula; no sensors |
| Kaaba coords | 21.422487° N, 39.826206° E | Common reference used by prayer apps |
| Ramadan UI | Auto from Hijri month 9 | No separate toggle for personal v1 |
| Adhkar | Localization strings + simple page | Avoid Core religious content blobs; full suite is non-goal |

## Components

```
HijriConverter.FromGregorian(date, offset) → HijriDate
QiblaCalculator.Calculate(lat, lon) → QiblaInfo

TodayViewModel
  → HijriText, IsRamadan, QiblaBearingText, expanded row list

DiagnosticsViewModel
  → HijriDayOffset NumberBox → AppSettings.With(hijriDayOffset)

AdhkarPage / AdhkarViewModel
  → static categories from LocalizationService
```

## PR plan (single slice)

1. Design + issue notes.
2. Core Hijri + Qibla + unit tests.
3. Today dual date / qibla / Ramadan rows.
4. Diagnostics offset; Adhkar nav page; strings.
5. README / CONTEXT phase mark.

## Acceptance

- Gregorian → Hijri with offset ±1 shifts the displayed Hijri day.
- Kennadsa qibla bearing is eastward toward Mecca (~90° region), Latin digits.
- When Hijri month is 9 (or forced in tests), Today shows Imsak and Iftar label.
- Adhkar page lists morning / evening / after-prayer in ar and en.
