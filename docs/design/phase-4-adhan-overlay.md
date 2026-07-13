# Design: Phase 4 — Adhan audio + prayer overlay

**Status:** Implemented (v1 personal)  
**Stack:** WinUI 3, in-process `NotificationHost`, separate always-on-top overlay window  

## Problem

Toast alone is easy to miss. At prayer start the user needs a strong, dismissible surface (stop adhan) and optional audio. Pre-alerts should stay quieter.

## Goals

1. Fire **overlay** and **audio** channels from the existing notification plan.
2. **Prayer start:** bottom dock card (DesignLab variant B) + adhan audio + Stop.
3. **Pre-alert:** top ribbon (variant A), no audio by default.
4. Respect **Reduce motion** (skip entrance animation when system animations off).
5. Stop audio + dismiss overlay via button or **Esc**.
6. Latin digits on all times; Arabic RTL layout via localization flow direction.

## Non-goals

- Guaranteed adhan after sleep / process death (process must be alive).
- Licensed full adhan recording in-repo (placeholder / system tone / user `LocalFile`).
- Settings UI for rules (JSON / defaults only; editor later).
- Removing Design lab (kept for motion experiments; production path is `OverlayService`).

## Key decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Host | Extend `NotificationHost.FireDue` | One planner, one fire loop |
| Overlay surface | Secondary borderless topmost window | Visible when main is tray-hidden |
| Layout | B bottom dock @ start; A top ribbon @ pre-alert | Matches DesignLab lean |
| Audio API | `Windows.Media.Playback.MediaPlayer` | WinAppSDK-native, no extra package |
| Default audio | `WindowsDefault` short tone | No unlicensed adhan asset |
| Default channels | Start: `All`; Before: `Toast \| Overlay` | Audio only at prayer start |
| Auto-dismiss | After audio end + `PostAudioHold`, or timeout if no audio | Avoid stuck overlay |

## Components

```
NotificationHost.FireDue
  ├─ ToastNotificationService (existing)
  ├─ OverlayService → OverlayWindow (topmost)
  └─ AdhanAudioService → MediaPlayer
        │
        └─ resolve AudioProfile (rule / DefaultAudio)
```

## Reliability

| State | Toast | Overlay | Audio |
|-------|-------|---------|-------|
| Process alive | Yes | Yes | Yes |
| Process dead | Best-effort scheduled toast | No | No |
| Sleep over event | Skip if &gt;2 min late | Same | Same |

## PR plan (single slice for personal v1)

1. Design notes + default channel/audio/overlay profiles.
2. `AdhanAudioService` + path resolution.
3. `OverlayWindow` / `OverlayService` + host wiring + strings.
4. DesignLab “fire real overlay/audio” + README/CONTEXT phase mark.

## Risks

- Unpackaged transparent topmost window chrome quirks.
- MediaPlayer file paths under self-contained publish.
- User without speakers / muted system still needs visual stop path.
