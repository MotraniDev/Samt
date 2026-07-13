# 01 — Adhan audio + prayer overlay

Status: ready-for-agent

## Goal

At prayer start show bottom dock overlay + play adhan/alert audio; pre-alert shows top ribbon. Wire through existing `NotificationHost`.

## Done when

- [x] Design notes `docs/design/phase-4-adhan-overlay.md`
- [x] `AdhanAudioService` (WindowsDefault tone / Bundled / LocalFile)
- [x] `OverlayWindow` + `OverlayService` (stop / Esc, reduce motion)
- [x] Default channels: Start=All, Before=Toast|Overlay; legacy rule upgrade
- [x] Design lab preview buttons
- [ ] Manual smoke: Design lab → Fire overlay+audio

## Comments

Implemented as vertical slice on master for personal v1. Core tests 41/41 green; app build Debug|x64 clean.
