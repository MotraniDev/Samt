# Kennadsa reference data

## Location

| Field | Value |
|-------|--------|
| Name | القنادسة / Kennadsa, Béchar, Algeria |
| Latitude | 31.5569° N |
| Longitude | 2.4181° W |
| Time zone | `W. Central Africa Standard Time` (UTC+1, no DST) |

## Files

| File | Purpose |
|------|---------|
| `kennadsa-baseline-2025-01.csv` | 31-day **engine regression** baseline (Algeria 18°/17°) |

## Important

- The baseline CSV is generated from `Samt.Core` itself so unit tests catch accidental engine drift.
- It is **not** an official ministry or mosque timetable.
- Before trusting SAMT for personal use, replace or supplement this file with a local table and re-run comparison (see PRD: ≥30 days, record per-prayer minute deltas).
- Prefer adjusting `CalculationProfile` minute offsets over changing solar equations when systematic bias appears.

## CSV format

```text
date,fajr,sunrise,dhuhr,asr,maghrib,isha
2025-01-01,06:12,07:35,13:05,15:52,18:28,19:48
```

Times are local `HH:mm` after nearest-minute rounding.
