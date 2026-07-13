# Design: Phase 5 — Advanced notification rules

**Status:** Implemented (v1 personal)  
**Stack:** `NotificationPlanner` (Core) + Alerts page (WinUI) + Friday fields on Locations  

## Problem

Defaults are one global pre-alert (15 min) and one start rule for the five prayers. Users need:

1. A **general** pre-alert offset with **per-prayer exceptions** (e.g. Fajr 30 min, others 15).
2. **Friday / Jumu‘ah** that does not double-fire with Dhuhr.
3. Clear **priority** when rules overlap, and a settings UI (no raw JSON).

## Goals

1. General BeforePrayer rule + optional per-prayer exception rules.
2. More specific rule wins; a **disabled** specific rule cancels that prayer (does not fall back to general).
3. On Friday with suppress-Dhuhr: remap Dhuhr targets → Jumu‘ah (no double midday toast).
4. Independent fixed Jumu‘ah time on `LocationProfile` (engine already supports it) — editable in Locations.
5. Alerts page: edit general offset, exceptions, channel toggles, save to `AppSettings.NotificationRules`.

## Non-goals

- Free-form multi-rule list editor / drag-drop priority.
- Dhikr / Friday-only event kinds in the planner (still reserved).
- Per-prayer audio file picker (global `DefaultAudio` remains).

## Priority algorithm

For each `(Kind, Prayer)`:

1. Candidate rules: same `Kind`, and prayer ∈ expanded targets (empty targets = notifiable set).
2. Specificity = `TargetPrayers.Count` (empty → least specific).
3. Lowest count wins. If that rule is **disabled**, skip the prayer entirely.
4. Deduplicate output by `(Kind, Prayer)` after expansion (Friday remap).

## Friday

| Setting | Where | Effect |
|---------|--------|--------|
| `FridayTimeMode` / `FixedFridayLocalTime` | Location | Engine Jumu‘ah clock |
| `SuppressDhuhrNotificationsOnFriday` | Location | Planner remaps Dhuhr → Jumu‘ah |

## Components

```
AlertsPage / AlertsViewModel
  → builds NotificationRule[] via NotificationRulesComposer
  → AppState.UpdateAsync

LocationsPage
  → Friday mode / fixed time / suppress Dhuhr

NotificationHost.RebuildPlan
  → planner.Plan(..., location.SuppressDhuhrNotificationsOnFriday)
```

## PR plan

1. Design + issue notes.
2. Core planner + composer + tests (TDD).
3. Locations Friday UI.
4. Alerts page + nav + strings.
5. README / CONTEXT phase mark.

## Acceptance

- Fajr before 30 + other prayers before 15 schedules correctly.
- Friday with suppress: Jumu‘ah fires, Dhuhr does not (no double).
- Fixed Friday 13:30 appears on schedule and plan.
