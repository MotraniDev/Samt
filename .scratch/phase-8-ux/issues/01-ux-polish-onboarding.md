# 01 — UX polish, window chrome & first-run onboarding (Phase 8)

Status: ready-for-human

## Goal

Ship five related polish slices: Adhkar auto-advance, app icon redesign, contextual icons, global window transparency (with overlay override), and a **first-run setup wizard** as the final slice.

## Design

`docs/design/phase-8-ux-polish-onboarding.md`

## Done when

- [x] Design notes accepted
- [x] **PR 1** Adhkar auto-advance setting + reader behavior
- [x] **PR 2** Global `WindowOpacity` + layered apply; overlay independent
- [x] **PR 3** Contextual icons on Settings / chrome
- [x] **PR 4** App icon ICO + package logos
- [x] **PR 5** First-run wizard (Skip + smart defaults; no show on `--autostart`)
- [x] SETUP.md / CONTEXT / README phase mark
- [x] `.\scripts\build.ps1 -Test` green (76 tests)

## Comments

- No Google Maps — GPS + Nominatim + manual only (domain).
- Wizard is **last** implementation slice so chrome/settings fields it depends on already exist.
- Default auto-advance **on**; default global opacity **100%**.
