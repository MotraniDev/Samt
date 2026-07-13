# 01 — Advanced notification rules (Phase 5)

Status: ready-for-agent

## Goal

General pre-alert + per-prayer exceptions, Friday/Jumu‘ah without double Dhuhr, Alerts settings UI, Friday fields on Locations.

## Done when

- [x] Design notes `docs/design/phase-5-advanced-rules.md`
- [x] Planner: specificity, disabled-exception cancels, Friday Dhuhr→Jumu‘ah remap
- [x] `NotificationRulesComposer` builds rules from UI model
- [x] Tests: Fajr 30 / others 15; Friday no double; fixed Jumu‘ah
- [x] Locations: Friday mode / fixed time / suppress Dhuhr
- [x] Alerts page: general offset, exceptions, channels
- [x] README / CONTEXT phase mark

## Comments

Acceptance from PRD phase 5: Fajr −30 and others −15; no double Dhuhr/Jumu‘ah when Dhuhr suppressed.
Implemented as vertical slice on master for personal v1.
