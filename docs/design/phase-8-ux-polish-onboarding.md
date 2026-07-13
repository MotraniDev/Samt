# Design: Phase 8 — UX polish, window chrome & first-run onboarding

**Status:** Implemented  

**Stack:** `Samt.Core` (settings schema) + `Samt.App` (Settings, Adhkar reader, shell windows, assets)  
**Depends on:** Phases 0–7 complete (engine, locations, alerts, overlay, adhkar suite, settings hub)

## Problem

Personal v1 is functionally complete, but first-time experience and everyday polish still lag:

1. **Adhkar reader** finishes a counter and stays on the item — users who want azkar.me-style flow must tap Next every time; there is no setting.
2. **App icon** is a generic placeholder (not “Night Mosque” / SAMT identity).
3. **Surfaces** lean on text and hairlines; section hierarchy and empty states lack a consistent icon language.
4. **Transparency** exists only for the **adhan overlay** (`OverlayProfile.Opacity` + layered HWND). Main shell and Adhkar reader have no user-facing opacity control.
5. **First run** is a short checklist in `docs/SETUP.md` — no in-app wizard. New installs land on Today with Kennadsa defaults and no guided location / prayer / adhkar setup.

## Goals

1. **Adhkar auto-advance** — Settings toggle; when enabled, completing the current item’s target count (or Mark done) advances to the next item after a short, reducible confirmation beat.
2. **App icon redesign** — Multi-size `.ico` + Store/logo PNGs aligned with navy + quiet gold aesthetic; tray and window share the same mark.
3. **Contextual icons** — Sparse, expressive glyphs (and rare ornaments) on nav, settings sections, and key empty/hint states — no sticker-book clutter.
4. **Window transparency** — Global opacity for shell-class windows; windows with a **dedicated** opacity control (adhan overlay today) **ignore** the global value.
5. **First-run setup wizard** — Modal/full-flow on first interactive launch: location → prayer times confirm → adhkar schedule → short product tour; **Skip** applies smart defaults. **Final implementation slice** of this phase.

## Non-goals

- Paid **Google Maps** Platform (domain: Windows GPS + free Nominatim place search + manual / known list only).
- Free-form theme builder or per-control color alpha.
- Full glass / Acrylic redesign of every page (optional Mica/backdrop remains theme-package territory).
- Silent auto-install of location without consent; GPS only when the user chooses Detect.
- Store listing / MSIX icon certification packages beyond existing unpackaged asset set.
- Multi-step “edit wizard” for returning users (Settings remains the permanent editor).
- Bundled Adhkar recitation audio.

## Domain alignment

| Term | Phase 8 use |
|------|-------------|
| **Location acquisition** | Wizard Step 1 reuses GPS / place search / manual — never invents a third map vendor |
| **Map place lookup** | Nominatim only when user searches |
| **App settings** | New toggles and sliders live on Settings; wizard writes the same `AppSettings` |
| **Adhkar window / reader** | Auto-advance is a reader behavior gated by settings |
| **Theme package** | Icon and ornaments respect curated packages; no new package required |
| **Display language** | Wizard UI follows current language (default Arabic); digits always Latin |
| **LatinDigits** | All times, percentages, step numbers via `LatinDigits` |

## Key decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Auto-advance default | **On** (`true`) | Matches common adhkar-web UX; easy to disable; power users still use arrows |
| Auto-advance trigger | Target count reached **or** Mark done | Both complete the item; same navigation path |
| Auto-advance timing | ~400 ms hold on checkmark (0 if Reduce motion) | User sees completion before page flip |
| Global opacity storage | `AppSettings.WindowOpacity` (0.30–1.0, default **1.0**) | Opaque by default for readability; slider for aesthetic use |
| Per-window override | Only surfaces with an explicit control use their own value | Overlay keeps `DefaultOverlay.Opacity`; future Adhkar-only slider can set `AdhkarReaderOpacity` nullable = use global |
| Opacity application | Layered HWND alpha helper shared with overlay (Main + Adhkar reader) | WinUI root `Opacity` blurs hit-testing less predictably; overlay already uses this path |
| Icon motif | Stylized crescent + geometric dome / compass ring on deep navy, single gold accent | Readable at 16px tray; spiritual without calligraphy that muddies at small size |
| Wizard gate | `AppSettings.SetupWizardCompleted` (bool, default `false` for new installs) | Survives restart; set true on Finish **or** Skip |
| Wizard vs `--autostart` | **Do not** show wizard when launched with `--autostart` | Login tray start must stay silent; first **interactive** show of MainWindow runs the gate |
| Wizard location skip defaults | Device timezone + keep seed **Kennadsa** if no GPS/search choice; if GPS succeeds once, use that profile | Offline-safe; GPS only when user picks it or we attempt once on Skip only if Windows location already authorized (optional: Skip never prompts GPS — prefer timezone + Kennadsa) |
| Skip GPS policy | **Skip does not request GPS** | Privacy; timezone + Kennadsa + Algeria method |
| Wizard prayer step | Confirm calc method + madhab + optional minute offsets; show computed times for today | Does not hand-edit five clock faces as “source of truth” |
| Wizard maps wording | UI copy: “place search” / “map lookup” — never “Google Maps” | Domain non-goal |
| Icons pack | Segoe MDL2 / Fluent **FontIcon** + existing theme ornaments | No new binary image pack unless a section truly needs it |
| Schema | Bump only if needed; prefer additive JSON fields with defaults in `SettingsJson.Normalize` | Existing installs get safe defaults without migration ceremony |

## Components

```
AppSettings
  + AdhkarAutoAdvanceEnabled : bool = true
  + WindowOpacity            : double = 1.0   // 0.30–1.0
  + SetupWizardCompleted     : bool = false
  // Optional later:
  // + AdhkarReaderOpacity   : double? = null  // null → WindowOpacity

SettingsJson.Normalize / CreateDefault / With(...)
SettingsStoreTests — round-trip new fields

AdhkarReaderWindow
  IncrementCount / MarkDone → if complete && auto-advance && not last → delay → Next
  Apply opacity via WindowChromeOpacity.Apply(this, resolved)

WindowChromeOpacity (new helper)
  Apply(Window, double opacity)  // layered HWND, clamp 0.30–1.0
  Subscribe AppState.SettingsChanged → re-apply open windows

SettingsPage
  Adhkar section: ToggleSwitch Auto-advance
  Appearance / Window section: Slider global transparency + live %
  Hint: “Adhan overlay uses its own opacity under Alerts / Design lab”

OverlayProfile.Opacity
  Unchanged; OverlayService continues to use rule/default overlay only
  Document as the per-window override for adhan overlay

Assets
  AppIcon.ico (multi-size), Square44/150, StoreLogo, Splash optional refresh
  AppIconHelper — no API change if path stays Assets/AppIcon.ico

SetupWizardWindow (or ContentDialog host on MainWindow)
  Steps 1–4 + Skip / Back / Next / Finish
  Writes LocationProfile, CalculationProfile ids, Adhkar* settings, SetupWizardCompleted

App.OnLaunched / MainWindow.Activated
  if !SetupWizardCompleted && !autostart → show wizard once

Localization
  ar + en-US (+ fr/es keys) for all new strings
```

## Feature specs

### 1. Auto-advance to next Adhkar

**Settings (Adhkar section)**  
- Toggle: `Settings.AdhkarAutoAdvance` / `Settings.AdhkarAutoAdvanceHint`  
- Visible immediately under master Adhkar reminders block (or above schedule rows) — reader UX, not only reminders.

**Runtime**  
```
on item becomes complete:
  ShowItem()  // checkmark visible
  if settings.AdhkarAutoAdvanceEnabled
     && index < count - 1
     && !reduceMotion → await 400ms (cancellable if user presses Prev/Next)
     → index++
     → ShowItem()
  if last item → stay; optional future: completion toast (out of scope)
```

**Tests**  
- Core: none required if pure UI (session already tracks complete).  
- Optional: small pure helper `AdhkarNav.ShouldAutoAdvance(enabled, complete, index, count)` unit-tested.

### 2. Application icon redesign

**Visual brief**  
- Field: `#0B1F33` / `#071525`  
- Accent: `#C4A35A` quiet gold  
- Motif: simple crescent + thin dome silhouette **or** compass ring with gold tick toward qibla metaphor — **one** mark, not a miniature mosque scene  
- Avoid dense Arabic letterforms below 32px  
- Light/dark taskbar: prefer gold mark that reads on both; keep navy fill in the icon plate  

**Deliverables**  
| Asset | Role |
|-------|------|
| `Assets/AppIcon.ico` | Taskbar, window, tray (`AppIconHelper`) |
| `Square44x44Logo*.png`, `Square150x150Logo*.png`, `StoreLogo.png` | Package / branding |
| Optional `SplashScreen` refresh | Match plate |

**Process**  
1. Generate 1024 master (design tool / Imagine).  
2. Export 16 / 24 / 32 / 48 / 64 / 256 into ICO.  
3. Spot-check tray at 16px and Start pin.  
4. Update packaging if Inno uses a separate icon path (`Samt.iss`).

### 3. Contextual icons and images

**Principles**  
- Prefer **FontIcon** with gold accent only on the selected / section-primary control.  
- Settings section headers: small glyph + title (language, theme, app options, updates, adhkar, appearance, about).  
- Adhkar library groups: keep catalog emoji **or** replace with consistent FontIcons — pick one system; do not mix heavily.  
- Today / Locations: reinforce existing ornaments; add empty-state illustration only if copy is otherwise sparse.  
- Reuse `islamic-tile.png` only on overlay / ceremonial surfaces (already).  
- Reduce motion: no decorative animation required for icons.

**Out of scope clutter**  
Full-bleed photos, random stock mosques, animated Lottie packs.

### 4. Window transparency

| Layer | Control | Default | Notes |
|-------|---------|---------|--------|
| **Global** | Settings slider 30%–100% → `WindowOpacity` | 100% | MainWindow, Adhkar reader, Setup wizard shell |
| **Adhan overlay** | Existing `DefaultOverlay.Opacity` (+ Design lab / future Alerts UI) | 0.94 | **Excluded** from global |
| **Future per-window** | Nullable override fields | null = global | Document pattern only if needed in phase |

**UX**  
- Settings card **Appearance / الشفافية** with slider + Latin `%` label.  
- Live preview: apply to MainWindow while dragging (debounce save).  
- Accessibility: floor at 30% so text remains usable; do not allow 0.

**Tech**  
Extract alpha helpers from `OverlayWindow` into `Helpers/WindowChromeOpacity.cs` (or extend `CustomWindowChrome`) so Main and Adhkar share one path. On theme change / settings reload, re-apply.

### 5. First-run setup wizard (final slice)

**Trigger**  
```
interactive launch (not --autostart)
&& !settings.SetupWizardCompleted
→ show wizard before or over Today
```

**Steps**

| Step | Title (ar concept) | Content | Persist |
|------|--------------------|---------|---------|
| 1 | الموقع | Choose: **Detect location** (Windows GPS), **Search place** (Nominatim), **Manual** lat/lon + name, or **Known list / timezone** (reuse Locations patterns). Brief privacy line. | Active `LocationProfile` |
| 2 | المواقيت | Show today’s Fajr…Isha (Latin digits) for active location + method combo (Algeria default) + Asr madhab. Optional minute adjustments advanced expander. | `ActiveCalculationProfileId`, `AsrMadhab`, `MinuteAdjustments` |
| 3 | جدول الأذكار | Master enable optional; Morning / Evening / Sleep times; After-prayer delay; recommended defaults pre-filled. | Existing Adhkar* fields |
| 4 | نظرة عامة | 3–4 cards: tray close, Today countdown, Alerts, Adhkar reader. No interactive tutorial engine. | — |

**Chrome**  
- Clean card on navy shell; gold progress dots (1–4); **Skip setup** always visible; **Back** from step ≥2; **Next** / **Finish**.  
- RTL-native; Cairo / Amiri per theme tokens.  
- Illustrations: simple line icons per step — not full screenshots.

**Skip**  
- Set `SetupWizardCompleted = true`.  
- Language/theme: leave defaults (Arabic, system theme).  
- Location: Kennadsa seed + device timezone id if we can map it without GPS; else keep seed timezone.  
- Prayer: Algeria method + standard Asr.  
- Adhkar: current defaults (reminders off unless product chooses on — **keep reminders default false** to avoid surprise prompts).  
- Do not open network or location permission dialogs on Skip.

**Finish**  
- Same flag; save settings once atomically; optionally navigate to Today.

**Re-entry**  
- No “run wizard again” required in v1; all fields exist under Locations / Settings / Alerts. Optional later: Settings link “Run setup again” clearing only the flag.

## PR plan (ordered slices)

### PR 1 — Adhkar auto-advance  
**Depends on:** —  
**Files:** `NotificationModels` / `SettingsJson`, `SettingsPage` (+ .cs), `AdhkarReaderWindow`, strings ar/en(/fr/es), `SettingsStoreTests`  
**Done when:** Toggle persists; complete counter or Mark done advances when on; stays put when off; Reduce motion skips delay.

### PR 2 — Global window transparency  
**Depends on:** — (can parallel PR 1)  
**Files:** `AppSettings.WindowOpacity`, `WindowChromeOpacity` helper, `MainWindow`, `AdhkarReaderWindow`, Settings UI, docs note that overlay opacity is independent  
**Done when:** Slider 30–100% applies to main + reader; overlay still uses `DefaultOverlay.Opacity` only; restart restores value.

### PR 3 — Contextual icons  
**Depends on:** —  
**Files:** `SettingsPage.xaml`, nav/pages empty states, optional `SamtTheme` styles for section headers  
**Done when:** Each Settings section has a clear glyph; hierarchy improved without new noise on Today hero.

### PR 4 — Application icon redesign  
**Depends on:** —  
**Files:** `Assets/*` icon set, `packaging/Samt.iss` if needed, visual spot-check notes  
**Done when:** Tray, taskbar, and installer shortcut show the new mark; still resolves via `AppIconHelper`.

### PR 5 — First-run setup wizard (**final**)  
**Depends on:** PR 1 optional (wizard can omit auto-advance); ideally after PR 2–3 for chrome polish  
**Files:** `SetupWizardWindow` (or page host), `App`/`MainWindow` gate, settings flag, localization, `docs/SETUP.md` first-run section, CONTEXT/README phase mark  
**Done when:** Fresh settings → wizard; Skip → defaults + no GPS prompt; Finish persists location/method/adhkar; `--autostart` never shows wizard; second launch skips wizard.

## Implementation notes

- **Immutability:** keep `AppSettings` / `With(...)` pattern; extend `With` parameters carefully.  
- **Tests:** public Core only — settings normalize/round-trip; optional pure auto-advance predicate; no UI tests required.  
- **Latin digits:** percentages, step index, prayer times, adhkar clocks.  
- **A11y:** Automation names on wizard primary buttons and transparency slider.  
- **Single instance:** wizard on the primary MainWindow instance only.

## Acceptance (phase-level)

- [ ] Auto-advance setting visible under Adhkar settings; on/off behavior verified in reader.  
- [ ] Global transparency slider affects main + adhkar reader; overlay opacity independent.  
- [ ] Settings and major chrome use consistent icons within Night Mosque tokens.  
- [ ] New ICO readable at 16px; tray matches window.  
- [ ] First interactive launch runs wizard; Skip and Finish both set completed flag; autostart silent.  
- [ ] No Google Maps dependency; GPS/search only on explicit user action (except documented optional detect).  
- [ ] `.\scripts\build.ps1 -Test` green; SETUP.md first-run updated.

## Open questions

None blocking — product defaults above match domain docs. Optional product tweaks:

1. Default auto-advance **on** vs **off** (plan: on).  
2. Whether Skip should attempt silent timezone-only refinement of Kennadsa (plan: timezone id only, keep Kennadsa coords).  
3. Whether Alerts page should expose overlay opacity next to global slider (nice-to-have; Design lab already prototypes overlay opacity).

## References

- `CONTEXT.md` — location acquisition, map place lookup, adhkar reader, theme packages  
- `docs/SETUP.md` — current manual first-run checklist  
- `OverlayWindow` / `OverlayService` — layered opacity precedent  
- `AdhkarReaderWindow.IncrementCount` — stub comment for auto-advance  
- `LocationsPage` / `WindowsGeolocationService` / `NominatimPlaceSearchService` — wizard Step 1 reuse  
- `Agents.md` — Latin digits, RTL, gold-on-navy signature  
