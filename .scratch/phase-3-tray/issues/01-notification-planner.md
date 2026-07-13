# 01 — NotificationPlanner (Core)

Status: ready-for-agent

## Goal

Pure Core planner turns `PrayerSchedule` + `NotificationRule`s into ordered `PlannedNotification`s (future only).

## Done when

- [x] `NotificationPlanner` in `Samt.Core`
- [x] Unit tests: start, before-offset, Friday suppress Dhuhr, disabled rules
- [x] Display times use Latin digits helper

## Comments

Implemented with TDD skill vertical slice alongside design doc `docs/design/phase-3-tray-notifications.md`.
