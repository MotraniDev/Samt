# SAMT — domain context

**سَمت / SAMT** is a personal offline Windows prayer-times app (WinUI 3, C# / .NET).

## Product one-liner

Tray-resident app that calculates prayer times locally from coordinates, reminds the user (toast / overlay / adhan), Arabic-first RTL with English, French, and Spanish, **Latin digits only**. Optional network for place-name search, update checks, and opt-in Google Calendar link for user calendar reminders — never for prayer calculation.

## Glossary

| Term | Meaning |
|------|---------|
| **LocationProfile** | Named place: lat/lon, timezone id, optional fixed Jumu‘ah, and optional country code (ISO-style) used as the default calendar country when the user has not overridden it |
| **CalculationProfile** | Fajr/Isha/Maghrib rules, Asr madhab, high-latitude rule, minute offsets |
| **PrayerSchedule** | One civil day's computed events for a location |
| **PrayerEvent** | Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha, Imsak, Midnight, Jumuah |
| **NotificationRule** | When/how to notify (before prayer, start, Friday, dhikr) |
| **NotificationPlanner** | Builds fire times for a day from schedule + rules (no UI) |
| **LatinDigits** | Always display 0–9; never ٠–٩ |
| **Kennadsa** | Default seed location (Béchar, Algeria); Algeria city seeds carry country code `DZ` for calendar-country defaulting |
| **Algeria method** | Default calc: Fajr 18°, Isha 17° (not an official claim) |
| **Location acquisition** | Explicit user choice of a `LocationProfile` source: Windows location service, optional free open place-name search, or manual entry; it never shares a location without consent and never requires a paid map API. |
| **Map place lookup** | Optional online place-name search to name a place and derive coordinates; used only when the user asks and network is available; manual entry remains the offline fallback. No paid Google Maps Platform. |
| **Manual location entry** | Offline entry of a named place and its coordinates, used when location services or map place lookup are unavailable or unwanted. |
| **App settings** | The user's preferences hub: display language, theme package, update check, adhkar reminders, Hijri/calendar preferences (day offset, calendar country override, special-day reminder master/sets/time), **Google Calendar link** controls (connect/disconnect, sync now, last calendar sync status, optional delete SAMT Google calendar), and official community links; distinct from diagnostics, which shows technical process status only. |
| **Theme package** | A named, cohesive visual identity covering application surfaces, notifications, and overlays through colours, fonts, ornament, and background treatment. Curated packages only (no free-form theme builder). |
| **Theme customization** | Reserved for later; vNext ships fixed packages only. Readability, accessibility, and Latin-digit rules always apply. |
| **Adhkar collection** | A named set of remembrance texts (morning, evening, after prayer, sleep) bundled offline for reading and optional scheduled reminders. Expanded well-known subset with disclaimer — not a fatwa source. |
| **Adhkar window** | The time range when a collection may be prompted; defaults follow the day's prayer schedule (Sleep uses a local clock default) and may be enabled or disabled per collection in app settings. |
| **Adhkar reader** | A compact dedicated mini-window showing one remembrance at a time with navigation between items in the active collection. |
| **Release manifest** | A versioned JSON document published with GitHub Releases that identifies an available update, its notes, download, and integrity hash. |
| **Update check** | An opt-outable check for a newer release manifest that informs the user but never installs an update without approval; download is verified (SHA-256) before launching the installer. |
| **Hijri calendar** | Dedicated app surface: a browsable **Hijri-month** grid (primary), each cell dual-labeled with the mapped Gregorian date (Latin digits), special-day highlights, and special-day reminder controls; distinct from the dual-date strip on Today. All Hijri↔civil mapping uses the same `HijriDayOffset` as the rest of the app. Presented with a non-authority disclaimer (tabular Hijri + offset; not official moon-sighting or fatwa). |
| **Calendar day sheet** | Per-day surface opened from a Hijri calendar cell: dual dates, special-day labels with source, optional mute for that day's special-day reminder, and user calendar reminders for that civil day. Not a prayer-times view. |
| **User calendar reminder** | User-authored personal reminder on a civil date: title, optional note, local clock time (Latin digits) interpreted in the **active location** timezone, optional same-day repeat count/interval for local delivery only. Distinct from special-day reminders and from prayer NotificationRules. When the Google Calendar link is enabled, the cloud twin is one timed Google event (title, note, civil date, first time in active location TZ); same-day repeat count/interval do not appear as separate Google events or RRULEs. |
| **Google Calendar link** | Optional, user-initiated connection to Google that bidirectionally synchronizes **only** user calendar reminders with one dedicated SAMT Google calendar under **one** Google account at a time (switch account = disconnect, then connect). Synced fields: title, note, civil date, first local time, delete/tombstone. Not synced: same-day delivery repeats, **Enabled** (local delivery mute only), prayer times, special days, settings, locations. When enabled: push soon after local create/edit/delete; pull on a modest interval while running and on resume from tray; manual “Sync now” always available. Local create/edit/delete always apply immediately; outbound changes queue and retry when offline or when a push fails; failures surface on calendar sync status without blocking the day sheet. Default off. First connect: find-or-create the SAMT Google calendar, push all local user calendar reminders (establish links), then pull. Inbound from Google: only non-recurring timed single events map to user calendar reminders; all-day, multi-instance recurrence, and other unmappable shapes are skipped and reported on last-sync status. Via this link, only reminder title/note/date/time and account auth leave the device — not coordinates, calculation profiles, or prayer schedules. Not full-app cloud backup or multi-device settings sync. |
| **Calendar sync status** | User-visible last-sync outcome for the Google Calendar link: time, success/failure, and counts such as updated/skipped (with reasons for skips). Not a diagnostics dump. |
| **SAMT Google calendar** | The single dedicated Google Calendar (created or reused by SAMT) that is the exclusive cloud twin of user calendar reminders. Events outside this calendar are ignored. App-managed: users should treat it as SAMT’s cloud surface, not a general inbox. |
| **Calendar sync conflict policy** | When the same linked reminder changed on both sides before sync: whole-event last-write-wins by timestamp; deletes use tombstones so a delete on one side is not resurrected by an older edit on the other. |
| **Google Calendar disconnect** | Stops the Google Calendar link: drops account credentials and link metadata, keeps all local user calendar reminders, and leaves the SAMT Google calendar on Google unless the user separately confirms deleting that cloud calendar. |
| **Special day** | A day marked on the Hijri calendar for display/highlight because it is an Islamic observance or a country holiday from the active calendar country package. |
| **Special-day reminder** | An optional toast-only notification on the civil day that maps to a special day (after Hijri day offset), at one global local clock time (default 09:00, Latin digits). Not a prayer NotificationRule, not adhan overlay/audio, and not a user calendar reminder. Planned by a sibling path to NotificationPlanner (not new prayer NotificationEventKind values). Missed while the app was not running may surface in the same spirit as missed-on-resume (summary, not late adhan). |
| **Special-day reminder set** | A bundled group of special days the user can enable or mute together for reminders (e.g. Islamic observances set, calendar country package set), with optional per-day mute. Highlight on the grid is independent of whether a reminder will fire. Reminders default off; a master switch plus per-set enables, then optional per-day mute. |
| **Islamic observance** | A bundled, offline religious or widely shared cultural marker on a Hijri date (or short Hijri window). v1 lean set: 1 Muharram, 10 Muharram, 12 Rabi‘ I, 27 Rajab, 15 Sha‘ban, 1 Ramadan, 27 Ramadan (Qadr window marker only), Eid al-Fitr, ‘Arafah, Eid al-Adha, Ayyam al-Tashreeq (each civil day separate). Not a fatwa; contested days remain visible on the grid (no hide-cultural toggle in v1); mute applies to reminders only. Distinct from every Friday (handled by prayer notification rules). |
| **Calendar country package** | A curated, offline set of country-specific holidays (often Gregorian-anchored) for one nation; v1 ships Algeria only. Distinct from prayer calculation method. When location country is empty or has no package and the user has not overridden calendar country, the Algeria package is the product default. Algeria v1 civil days: 1 Jan New Year, 12 Jan Yennayer, 1 May Labour Day, 5 Jul Independence Day, 1 Nov Revolution Day. Islamic public holidays are not duplicated here — they come from Islamic observances. |
| **Special-day identity** | One special-day instance per civil date after mapping; sources may include Islamic and/or country. UI and reminders collapse duplicates to a single day (one primary label, at most one toast). |
| **Calendar country** | The nation whose calendar country package supplies country holidays on the Hijri calendar. Defaults from the active location’s country when known; the user may override it in settings without changing prayer location. |
| **Display language** | The user-selected application language: Arabic by default, or English, French, or Spanish; it controls UI copy and layout direction without changing the Latin-digit rule. |
| **Installer language** | The language used by setup and update-installation screens; Arabic is the default when a choice is offered. |
| **Publisher identity** | MotraniSoft branding displayed as the application publisher; it uses approved light and dark logo assets and does not replace the SAMT product identity. |
| **Official community link** | A verified publisher-owned external link. GitHub is available now; Facebook and X remain absent from the UI until their official destinations are supplied. |

## Non-goals

Full-app cloud backup / multi-device settings sync, Store listing, mosque search, paid Google Maps, live web scrape of Adhkar, full Hisn-level / fatwa library, bundled Adhkar recitation audio (reader UI may prepare for it), free-form theme builder, silent auto-install of updates, claiming official prayer times, official moon-sighting calendar authority, multi-country holiday CMS, Google sync of prayer times or special days, treating Google as authority for calculation or Hijri catalogs, multi-Google-calendar inbox into SAMT, multi-Google-account merge into one reminder list.

## Stack

- `Samt.Core` — pure domain + engine + storage interfaces
- `Samt.App` — WinUI 3 shell (Today, Locations, Alerts, Adhkar, Hijri calendar, Settings, Diagnostics, DesignLab)
- Tests: xUnit, fixtures under `testdata/`

## Phases

0–7 done (shell, engine, today, locations, tray, toast, overlay, adhan, advanced rules + Friday, qibla, Hijri, Ramadan UI, light adhkar, polish & personal delivery).

Post-7 also delivered in-tree: Settings hub, fr/es, theme packages, place-name search, update check, expanded Adhkar suite, publisher branding & bilingual README.

**Phase 8** (see `docs/design/phase-8-ux-polish-onboarding.md`): Adhkar auto-advance, global window transparency (overlay independent), contextual Settings icons, app icon redesign, first-run setup wizard (Skip → smart defaults; hidden on `--autostart`).

**Phase 9** (see `docs/design/phase-9-hijri-calendar.md`): dedicated Hijri calendar page, Islamic + Algeria special days, special-day reminder sets (toast, sibling planner). **Implemented** (Core catalogs/planner/settings + App calendar page, Settings section, host toasts).
