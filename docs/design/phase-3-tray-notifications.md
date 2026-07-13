# Design: Phase 3 — Tray, scheduling, Windows toast

**Status:** Implemented (v1 personal) — tray + host + toast; polish deferred  
**Stack:** WinUI 3, Windows App SDK, single-process tray host  

## Problem

Users need prayer reminders even when the main window is hidden. The process must stay resident, re-plan events when date/location/rules change, and show a system toast as a visual channel. Audio + custom overlay remain Phase 4; process-must-run for those is already accepted.

## Goals

1. Single-instance process with tray icon.
2. Build today's remaining notification fire times offline.
3. Fire toast at prayer start / pre-alert per rules.
4. Reschedule on: login, resume, midnight, settings change, system time change.
5. Close-to-tray; Exit stops scheduling after confirm.

## Non-goals

- Guaranteed adhan after sleep (document only).
- Push / cloud notifications.
- Elevated process.

## Key decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Process model | Single-instance `AppInstance` | Avoid double adhan/toast |
| Timer host | In-process dispatcher + optional scheduled toast backup | Toast may work if process dies; audio will not (Phase 4) |
| Plan engine | Pure `NotificationPlanner` in Core | TDD-friendly, no WinUI in Core |
| Tray | H.NotifyIcon or WinUIEx (spike) | No first-party WinUI tray API |
| Data | Existing `AppSettings` JSON | No new store |

## Components

```
AppState / Settings
       │
       ▼
PrayerEngine → PrayerSchedule
       │
       ▼
NotificationPlanner → PlannedNotification[]
       │
       ▼
NotificationHost (App)
  ├─ DispatcherTimer / due queue
  ├─ AppNotification (toast)
  └─ TrayIcon (tooltip = next prayer)
```

## PlannedNotification (Core)

- `Id` (stable for dedupe)
- `FireAt` (DateTimeOffset)
- `Kind` (BeforePrayer | PrayerStart | …)
- `PrayerEvent`
- `Channels` (Toast | …)
- `TitleKey` / payload for localization in App layer

## Reliability

| State | Toast | Overlay/Audio |
|-------|-------|----------------|
| Process alive | Yes | Phase 4 |
| Process dead | Best-effort scheduled toast if API allows | No |
| Sleep over event | Missed; optional “missed” on resume | No |

## Open questions (product)

1. Default pre-alert minutes (suggest 15).
2. Toast only vs toast+tray balloon for personal v1.

## PR plan

1. **Core planner + tests** — `NotificationPlanner` (this repo slice starts here).  
2. **Host wiring** — rebuild plan on settings/day change.  
3. **Tray spike** — icon + next-prayer tooltip + Exit.  
4. **Toast channel** — show at fire time; click opens Today.  
5. **Lifecycle** — single-instance, resume, midnight rebuild.

## Risks

- Tray packaging with MSIX  
- WinAppSDK scheduled toast gaps  
- Digit substitution on toast text → format with `LatinDigits` before show  
