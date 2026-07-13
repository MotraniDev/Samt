# Adhan / alert audio

## Library (`library/`)

Built-in catalog used by **Alerts → Sound library**:

| Id | File | Use |
|----|------|-----|
| `adhan-alaqsa` | `library/adhan-alaqsa.mp3` | Full adhan (default) |
| `adhan-egypt` | `library/adhan-egypt.mp3` | Full adhan |
| `adhan-abdul-basit` | `library/adhan-abdul-basit.mp3` | Full adhan |
| `adhan-abdul-ghaffar` | `library/adhan-abdul-ghaffar.mp3` | Full adhan |
| `adhan-abdul-hakam` | `library/adhan-abdul-hakam.mp3` | Full adhan |
| `phrase-takbir` | `library/phrase-takbir.wav` | Pre-alert cue (default) |
| `phrase-hayya-alas-salah` | `library/phrase-hayya-alas-salah.wav` | Pre-alert cue |
| `soft-tone` | generated at runtime | Soft beep |
| `silent` | — | No audio |

See `library/SOURCES.md` for origins and licensing notes.

## User sounds

Imported files are copied to `%LocalAppData%\SAMT\sounds\` and listed in settings.

## Legacy

Single-file `adhan.mp3` / `.wav` / `.m4a` in this folder still works as a bundled fallback.
