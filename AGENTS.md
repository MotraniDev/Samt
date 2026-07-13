# SAMT agent notes

## Always use Latin digits

**Rule:** In the app UI and any user-visible string that contains numbers or times, always use **Western/Latin digits `0–9`**. Never Arabic-Indic (`٠–٩`) or Eastern Arabic-Indic (`۰–۹`).

### How to comply

1. Format all times/numbers via `Samt.Core.Formatting.LatinDigits` (`Time`, `Number`, `Date`, `Duration`).
2. Run free-form strings through `LatinDigits.EnsureLatin` before display when they may contain digits.
3. On WinUI controls that show or edit numbers/times, set `Language="en-US"` (use styles `LatinDigitsTextBlock`, `LatinDigitsTextBox`, `LatinDigitsCalendar` in `Themes/SamtTheme.xaml`). This stops XAML digit substitution under Arabic UI language.
4. Keep Arabic **FlowDirection=RightToLeft** for layout; do not change digit language by flipping the whole page to LTR.
5. Prefer `CultureInfo.InvariantCulture` / `LatinDigits.FormatCulture` over the ambient culture for numeric formatting.

### Why

With `ApplicationLanguages` = Arabic, WinUI may still render ASCII `"06:44"` as `"٠٦:٤٤"` unless the control language is forced to a Latin-digit locale.

---

## Agent skills

### Shortlist (use by default)

| Skill | When |
|-------|------|
| **frontend-design** | Visual redesign, Today/overlay chrome, brand tokens, anti-generic UI |
| **rtl-shadcn-support** | Arabic/RTL layout, logical edges, Arabic typefaces, bidi mixed strings (adapt web rules to XAML) |
| **prototype** | Compare UI variants before committing (e.g. DesignLab page) |
| **tdd** | New Core behavior (scheduler, rules, engine) — vertical red→green slices |
| **design** | Large architecture slices (tray, notifications, adhan pipeline) → design doc + PR plan |
| **grill-me** / **grill-with-docs** | Stress-test a plan against domain language before coding |
| **check-work** / **review** | After a phase or non-trivial PR |

Skill packages live on this machine under:

- `~/.grok/skills/` and `~/.grok/bundled/skills/`
- `~/.agents/skills/`

No extra install step is required if those paths exist; this file tells agents **which** to prefer for SAMT.

### Issue tracker

Local markdown under `.scratch/<feature>/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default role strings (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: root `CONTEXT.md` + `docs/adr/`. See `docs/agents/domain.md`.

### Design / RTL for WinUI (adapted)

- Prefer **logical** layout language in code comments and future styles: start/end, not left/right when describing RTL.
- Arabic UI copy from `Strings/ar/Resources.resw`; never hard-code Arabic in Core.
- Fonts (bundled OFL under `Assets/Fonts/`): **Cairo** modern UI (`SamtUiFont`), **Amiri** decorative display / prayer names (`SamtDisplayFont`), **Noto Naskh Arabic** adhkar body (`SamtNaskhFont`), **Cascadia Mono** times via `LatinDigits*` styles. Alert overlay uses `Assets/Textures/islamic-tile.png`. Tokens in `Themes/SamtTheme.xaml`.
- Exit: window close hides to tray; full quit via pane **Exit** button or tray menu **Exit** (`App.RequestExit`).
- Overlay and tray flyouts must respect Reduce motion (PRD).
- Signature visual: **next-prayer countdown in quiet gold on deep navy** — keep it dominant; quiet the rest.

### TDD for this repo

- Test public Core APIs only (`IPrayerEngine`, `NotificationPlanner`, storage, timeline).
- No internet in tests; use fixtures under `testdata/`.
- Inject clocks / time zones for determinism.
