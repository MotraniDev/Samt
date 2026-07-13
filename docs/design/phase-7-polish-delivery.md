# Design: Phase 7 — Polish & personal delivery

**Status:** Implemented (v1 personal)  
**Stack:** `Samt.App` (auto-start, resume, diagnostics, a11y) + Core missed-plan API + release docs

## Problem

Phases 0–6 deliver calculation, tray, toasts, overlay, rules, and companions. Personal v1 still needs: Windows login auto-start wired to settings, honest “missed while asleep” feedback, English/RTL polish hooks, long-run status visibility, and a short install path without Store/MSIX ceremony.

## Goals

1. **Auto-start** — honor `AppSettings.AutoStartEnabled` via HKCU Run (unpackaged); start minimized with `--autostart`.
2. **Missed on resume** — when `ShowMissedAlertOnResume` is true, surface a summary toast/balloon for prayer-start events skipped while the machine slept or the app was late.
3. **Diagnostics controls** — toggles for auto-start and missed-resume; process working-set + planned count for tray longevity checks.
4. **A11y basics** — `AutomationProperties.Name` on nav chrome and primary actions.
5. **Delivery** — `docs/SETUP.md`, richer release folder README; mark Phase 7 in README/CONTEXT.

## Non-goals

- Microsoft Store / signed MSIX (folder self-contained zip remains personal delivery).
- Full screen-reader certification or automated UI tests.
- Two-week manual soak (checklist only).
- Cloud, mosque search, full adhkar suite.

## Key decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Auto-start mechanism | HKCU `...\Run` + `--autostart` | Works unpackaged; no elevated install |
| Minimize on login | Hide main window after show when `--autostart` | Notifications stay resident; no desktop flash forever |
| Missed window | Prayer **start** events with `FireAt` in (day start, now − 2 min] | Matches host skip threshold; avoid replaying pre-alerts as “missed adhan” |
| Missed channel | Toast/balloon only | No late adhan (PRD: no guarantee of late audio) |
| Status surface | Diagnostics | Keeps Today clean; power users monitor memory here |

## Components

```
NotificationPlanner.PlanMissed(...)  → Core list of past starts
AutoStartService.Apply(enabled)      → Registry Run
App.OnLaunched                       → Apply auto-start; hide if --autostart
NotificationHost                     → PowerMode resume + startup miss scan
DiagnosticsViewModel                 → toggles + ProcessStatusText
docs/SETUP.md + release RELEASE-README
```

## PR plan (single slice)

1. Design + issue notes.
2. Core `PlanMissed` + tests.
3. AutoStartService + App wiring.
4. Host resume/missed summary.
5. Diagnostics UI + strings + a11y names.
6. SETUP + release notes; README/CONTEXT phase mark.

## Acceptance

- Enabling auto-start writes a Run key; disabling removes it. Login launch can use `--autostart` and land in tray.
- After simulated “late” now, with the setting on, a missed-start summary is shown once (no adhan).
- Diagnostics shows memory + remaining plan count; toggles persist.
- `.\scripts\release.ps1` still produces a runnable folder + zip; SETUP.md documents install and Latin digits.
