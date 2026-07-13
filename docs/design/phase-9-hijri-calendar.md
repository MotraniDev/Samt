# Design: Phase 9 — Hijri calendar, special days, special-day reminders

**Status:** Designed (grill + domain locked; not implemented)  
**Stack:** `Samt.Core` (catalog, resolve, sibling planner, settings) + `Samt.App` (Calendar page, Settings section, host toast wiring)  
**Depends on:** Phase 6 Hijri strip (`HijriConverter`, `HijriDayOffset`, Ramadan Today); Phase 7 missed-on-resume host patterns; Phase 8 Settings hub  
**ADR:** [0002-special-day-reminders-sibling-planner](../adr/0002-special-day-reminders-sibling-planner.md)  
**Domain:** `CONTEXT.md` terms — Hijri calendar, Special day, Islamic observance, Calendar country package, Special-day reminder / set, Calendar day sheet, Special-day identity

## Problem

Phase 6 only surfaces a **single dual date** (and Ramadan mode) on Today. Users still cannot:

1. **Browse** a Hijri month with dual Gregorian labels.
2. **See** fixed Islamic observances and **country** civil holidays on that grid.
3. **Opt in** to morning-style **toast** reminders for those days (without free-form personal notes or adhan channels).

Country holidays and prayer **location** are related but not identical (e.g. pray in Paris, observe Algerian civil holidays). Hijri mapping must stay aligned with the existing tabular converter + day offset — never claim official moon-sighting.

## Goals

1. **Hijri calendar page** — dedicated nav surface: Hijri-month grid, dual Gregorian per cell (Latin digits), prev/next month, “today” affordance.
2. **Special-day highlights** — Islamic lean set + Algeria civil package; one identity per civil date; distinct marks (Islamic / country / both).
3. **Calendar country** — hybrid: default from active `LocationProfile` country code when known; Settings override; empty/unknown package → Algeria product default.
4. **Special-day reminders** — opt-in master + Islamic set + country set + per-day mute; one global local clock time (default 09:00); toast only; missed summary in the spirit of missed-on-resume (no late adhan).
5. **Settings home** for Hijri offset, calendar country, reminder knobs; **day sheet** for dual date + labels + mute.
6. **Country code** on locations (optional ISO-style); Algeria seeds prefilled `DZ`.
7. **Non-authority disclaimer** on calendar Settings / first calendar open.

## Non-goals

- Official moon-sighting calendar or fatwa authority.
- Multi-country holiday CMS (Algeria only in v1; structure allows later packs).
- Free-form personal notes / “dentist on 12 Sha‘ban” scheduler.
- Per-day or per-set custom fire times (one global time only).
- Adhan overlay / audio for special days.
- Gregorian-primary grid or dual-mode toggle (Hijri-month only).
- Hide-cultural toggle for contested Islamic days (mute reminders only).
- Duplicating Islamic public holidays inside the Algeria package.
- Every Friday as a calendar special day (Friday stays prayer `NotificationRule`).
- Full prayer-times / qibla on the day sheet (Today remains that job).
- Live web scrape of holidays.

## Domain alignment

| Term | Phase 9 use |
|------|-------------|
| **Hijri calendar** | New nav page; Hijri-month grid |
| **Calendar day sheet** | Tap cell → dual dates, special labels, mute |
| **Special day / identity** | Resolved catalog rows collapsed per civil date |
| **Islamic observance** | Bundled lean Hijri-anchored set |
| **Calendar country package** | Offline Algeria civil five |
| **Calendar country** | Resolved effective country for package selection |
| **Special-day reminder / set** | Master + two sets + mutes; independent of grid highlight |
| **NotificationRule / Planner** | Unchanged for prayers; **sibling** special-day planner (ADR-0002) |
| **LatinDigits** | All day numbers, years, reminder clock times |
| **LocationProfile** | Optional `CountryCode`; seeds `DZ` |
| **App settings** | Calendar section owns offset + country + reminder prefs |
| **HijriDayOffset** | Single shared mapping for Today + calendar + special days |

## Key decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Product surface | Dedicated Calendar page (not Today-only) | Month browse + day sheet need room |
| Grid orientation | **Hijri-month** primary; Gregorian secondary on cells | Matches “Hijri calendar”; civil holidays still map onto cells |
| Hijri engine | Existing BCL tabular + `HijriDayOffset` | Consistency with Phase 6; offline; no new authority claim |
| Special-day v1 sources | Islamic lean + Algeria civil | Offline, Algeria-first product DNA |
| Country selection | Hybrid default + override | Travel / diaspora without fake prayer locations |
| Empty country / unknown pack | Algeria package | Matches Kennadsa seed; revisit when multi-pack ships |
| Country on location | Optional ISO-style code on `LocationProfile` | Offline after save; place search may prefill later |
| Islamic set (lean) | 1 Muharram, 10 Muharram, 12 Rabi‘ I, 27 Rajab, 15 Sha‘ban, 1 Ramadan, 27 Ramadan (Qadr **window marker** only), 1 Shawwal, 9–13 Dhul-Hijjah (‘Arafah, Eid, Tashreeq each day) | Widely useful without full fatwa catalog |
| Contested days | Always visible; neutral “commonly observed” copy | No hide toggle v1; mute for reminders |
| Algeria civil | 1 Jan, 12 Jan Yennayer, 1 May, 5 Jul, 1 Nov | Fixed Gregorian; no Islamic dupes |
| Dedup | One special day per civil date; primary label + sources flags | Avoid double toast / double badge |
| Reminders default | **Off** (master + sets) | Same politeness as Adhkar reminders |
| Fire model | Single global local time, default **09:00** | Simple “morning of Eid” |
| Channels | **Toast only** | Not prayer start / not adhan |
| Planner boundary | Sibling to `NotificationPlanner` (ADR-0002) | Avoid fake `PrayerEvent`s and adhan coupling |
| Missed | Resume-style **summary** when day still current / scan window | Reuse host idea, not late adhan audio |
| Settings placement | App Settings → Calendar / Hijri; move offset out of Diagnostics as **single home** | Preferences hub; Diagnostics stays technical |
| Day sheet | Lean: dual date, labels, mute; not prayers | Clear job separation |
| Grid marks | Two-tone Islamic vs country (+ combined); today ring separate | Scannable month |
| Multi-day bands | Each civil day is its own special day / mute | Tashreeq / multi-day clarity |
| Schema | Additive `AppSettings` + location field; `SettingsJson.Normalize` defaults | Existing installs safe |

## Islamic observances (v1 catalog)

| Id (stable) | Hijri anchor | Notes |
|-------------|--------------|--------|
| `islamic.new_year` | 1 Muharram | |
| `islamic.ashura` | 10 Muharram | |
| `islamic.mawlid` | 12 Rabi‘ al-Awwal | Contested; “commonly observed” |
| `islamic.isra_miraj` | 27 Rajab | Cultural/widely marked |
| `islamic.mid_shaban` | 15 Sha‘ban | Contested; “commonly observed” |
| `islamic.ramadan_start` | 1 Ramadan | Month tint may still use `IsRamadan` |
| `islamic.laylat_al_qadr_marker` | 27 Ramadan | **Marker only** — not a claim of exact Laylat al-Qadr |
| `islamic.eid_fitr` | 1 Shawwal | |
| `islamic.arafah` | 9 Dhul-Hijjah | |
| `islamic.eid_adha` | 10 Dhul-Hijjah | |
| `islamic.tashreeq_11` … `_13` | 11–13 Dhul-Hijjah | Separate ids per day |

Primary label localization keys: `SpecialDay.{id}` (ar/en/fr/es). Contested helper: `SpecialDay.CommonlyObservedHint`.

## Algeria civil package (v1)

| Id | Gregorian | Label key |
|----|-----------|-----------|
| `dz.new_year` | 01-01 | New Year |
| `dz.yennayer` | 01-12 | Yennayer |
| `dz.labour` | 05-01 | Labour Day |
| `dz.independence` | 07-05 | Independence Day |
| `dz.revolution` | 11-01 | Revolution Day |

Package id: `DZ`. Unknown codes → fall back to `DZ` when no override and no better pack (v1).

## Effective calendar country

```
if Settings.CalendarCountryOverride is non-empty and valid
  → use override
else if ActiveLocation.CountryCode is non-empty
  → use that code
else
  → "DZ"
package = Catalog.GetCountryPackage(code) ?? Catalog.GetCountryPackage("DZ")
```

Override UI: “Use location default” clears override; optional combo shows Algeria only until more packs exist (control can stay visible with one entry).

## Settings model (additive)

```
LocationProfile
  + CountryCode : string?   // ISO 3166-1 alpha-2 style, e.g. "DZ"

AppSettings
  // existing
  HijriDayOffset : int

  // new
  CalendarCountryOverride : string?          // null/empty = follow location / default
  SpecialDayRemindersEnabled : bool = false  // master
  SpecialDayIslamicSetEnabled : bool = false
  SpecialDayCountrySetEnabled : bool = false
  SpecialDayReminderTime : string = "09:00"  // HH:mm Latin; local clock of active location TZ
  SpecialDayMutedIds : IReadOnlyList<string> = []  // stable special-day definition ids
```

**Mute identity:** mute by **catalog definition id** (e.g. `islamic.eid_fitr`), not by civil date — so annual recurrence stays correct across years. Day sheet toggles that id in `SpecialDayMutedIds`.

**Reminder eligibility (fire today):**

```
master on
AND (islamic source ∈ day.sources ⇒ islamic set on) OR (country source ∈ day.sources ⇒ country set on)
AND definition id(s) contributing to the day are not all muted
  (if both sources: fire if any unmuted contributing definition is covered by an enabled set)
AND FireAt = today @ SpecialDayReminderTime in active location timezone
AND FireAt > now for Plan; PlanMissed uses same grace spirit as prayer missed (~2 min)
```

**Highlight** ignores master/set/mute (always show catalog marks).

## Core components

```
Samt.Core/Calendar/   (or SpecialDays/)
  SpecialDaySource        : Islamic | Country
  SpecialDayDefinition    : Id, Source, HijriAnchor? | GregorianMonthDay?, DisplayKey
  ResolvedSpecialDay      : CivilDate, HijriDate, Labels[], Sources, DefinitionIds[]
  IslamicObservanceCatalog.All
  CountryCalendarPackage  : CountryCode, Definitions
  CountryCalendarCatalog.Get("DZ") → Algeria five
  CalendarCountryResolver.Resolve(settings, activeLocation) → code
  SpecialDayResolver
    ForHijriMonth(year, month, offset, countryCode) → cells + resolved days
    ForCivilDate(date, offset, countryCode) → ResolvedSpecialDay?
  SpecialDayReminderPlanner   // ADR-0002 sibling
    Plan(now, settings, locationTz, offset, countryCode) → PlannedSpecialDayReminder[]
    PlanMissed(...) → past-due same day / scan window

HijriConverter
  + ToGregorian(HijriDate, offset) if missing (needed for month grid civil mapping)
  // Keep FromGregorian; tests for invertibility within tabular calendar

AppSettings / SettingsJson / LocationProfile serialization
KnownLocations.* → CountryCode = "DZ"
```

### Planned special-day reminder (Core)

```
PlannedSpecialDayReminder
  Id              // stable e.g. special:{civil:yyyy-MM-dd}:{primaryDefId}
  FireAt          // DateTimeOffset
  CivilDate
  TitleKey / resolved display later in App
  DefinitionIds
  Sources
```

App host: toast builder only; **no** overlay/audio path.

## App components

```
MainWindow nav
  + NavCalendar (Tag=calendar) — after Adhkar or before Settings

Pages/CalendarPage.xaml + CalendarViewModel
  CurrentHijriYear/Month
  Cells: Hijri day, Gregorian subtitle, SpecialMark enum, IsToday
  Prev/Next month; jump to today
  Open day sheet on cell click

CalendarDaySheet (ContentDialog / TeachingTip / side panel — implementer pick; RTL-safe)
  Dual dates (LatinDigits)
  Special labels + source chips
  Mute toggle if any definition ids
  Link text: reminder time lives in Settings

SettingsPage — section "Calendar" / Hijri
  HijriDayOffset (migrate control from Diagnostics; Diagnostics may show read-only or remove)
  Calendar country: Use location default | Algeria (v1)
  Master special-day reminders
  Islamic set / Country set toggles
  Time picker / constrained text for 09:00 (LatinDigits, Language=en-US)
  Short disclaimer string

Locations UI
  Optional country field on edit; seeds show DZ
  Map place lookup: prefill CountryCode when Nominatim returns country code (best-effort)

NotificationHost (or sibling scheduler tick)
  Include SpecialDayReminderPlanner.Plan in daily plan refresh
  On resume / late start: PlanMissed special days → one summary toast if ShowMissedAlertOnResume
    (reuse the boolean or a dedicated flag? v1: reuse ShowMissedAlertOnResume for simplicity)

LocalizationService + resw
  NavCalendar, month names already Hijri.Month.*, SpecialDay.*, SettingsCalendar*, disclaimer
```

### Visual marks (theme tokens)

| Mark | Intent |
|------|--------|
| Islamic | Quiet gold accent (brand countdown gold family) |
| Country | Secondary teal / sage (readable on navy) |
| Both | Combined (dot pair or split bar) |
| Today | Ring / border independent of special mark |
| Ramadan month | Optional soft month tint when `Month == 9` (reuse Today spirit) |

Respect Reduce motion; no heavy animations required.

## PR plan (tracer slices)

| # | Slice | Deliverable |
|---|--------|-------------|
| 1 | Design + domain already in CONTEXT/ADR | this doc |
| 2 | Core catalogs + resolver + Hijri month/civil mapping + tests | pure Core, no UI — **done** (`Samt.Core/Calendar/*`, `HijriConverter.ToGregorian`) |
| 3 | Settings fields + JSON + LocationProfile.CountryCode + DZ seeds + tests | storage round-trip — **done** (`AppSettings` calendar/special-day fields, `LocationProfile.CountryCode`, Algeria seeds `DZ`, `SettingsJson` normalize) |
| 4 | SpecialDayReminderPlanner + PlanMissed + tests | Core |
| 5 | Calendar page grid + day sheet (highlights only) | App UI |
| 6 | Settings Calendar section; move Hijri offset; disclaimer | App UI |
| 7 | Host toast + missed summary wiring | App |
| 8 | Place-search country prefill (optional polish) | App |
| 9 | README / SETUP / CONTEXT phase mark | docs |

Slices 2–4 are TDD-friendly (public Core APIs only, fixtures under `testdata/` if needed).

## Acceptance

- [ ] Nav opens **Hijri calendar**; month is Hijri-primary; cells show Gregorian with **Latin digits**.
- [ ] Changing `HijriDayOffset` in Settings shifts Today **and** calendar special-day cells the same way.
- [ ] Lean Islamic days and Algeria five appear; Eid is not double-listed as a second Algeria row.
- [ ] Empty location country + no override → Algeria marks still show.
- [ ] Override can force Algeria when location later gains another code (future-proof even if only DZ ships).
- [ ] Fresh install: highlights on, **no** special-day toasts until master + set enabled.
- [ ] With master + Islamic set on, at 09:00 local on 1 Shawwal (mapped), one toast; mute `islamic.eid_fitr` suppresses it.
- [ ] Country-only day (e.g. 5 Jul) requires country set (or both) as designed; islamic set alone does not fire.
- [ ] Special-day fire never opens adhan overlay / never uses prayer `NotificationRule`.
- [ ] Missed past 09:00 with resume setting on → summary toast, no audio.
- [ ] Day sheet shows dual date for ordinary days; mute only when special.
- [ ] Algeria seeds persist `CountryCode=DZ`.
- [ ] Disclaimer visible in Settings calendar section.
- [ ] Core unit tests cover resolver dedup, country resolution, planner enable/mute matrix, offset shift.

## Risks & open implementation notes

| Risk | Mitigation |
|------|------------|
| BCL `HijriCalendar` vs local mosque dates | Offset ±3 already; disclaimer; no authority claim |
| WinUI month grid RTL | Logical margins; test ar RTL + en LTR; digits via `LatinDigits*` / `en-US` on number controls |
| Toast spam if many specials same day | Dedup to one toast per civil date |
| `SpecialDayMutedIds` growth | Small fixed catalog; prune unknown ids in Normalize |
| Nominatim country codes | Best-effort; manual field remains source of truth |
| Timezone for 09:00 | Active location `TimeZoneId`, not necessarily system zone (consistent with prayer schedule) |

## Out of scope follow-ups (later phases)

- Additional country packages (MA, TN, FR cultural, …).
- User custom annual Hijri reminders (grill option D).
- Hide-cultural observances toggle.
- Gregorian-primary view toggle.
- Multi-day Eid al-Fitr civil extensions beyond 1 Shawwal.
- Rich day sheet with prayer snippet.

## References

- Grill decisions captured in `CONTEXT.md` (Phase 9 terms).
- ADR-0002: special-day reminders sibling planner.
- Phase 6: `docs/design/phase-6-qibla-hijri-ramadan.md` (thin Hijri strip).
- Phase 7: missed-on-resume toast patterns.
- Agents: Latin digits, RTL logical layout, Core tests only on public APIs.
