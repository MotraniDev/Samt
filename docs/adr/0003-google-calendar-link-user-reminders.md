# Google Calendar link is bi-di for user reminders only on a dedicated calendar

SAMT remains offline-first for prayer calculation; optional network already covers place search and update checks. Users also want personal **user calendar reminders** on phone/work calendars. Full-app cloud backup and multi-device settings sync stay out of scope.

We accept an optional **Google Calendar link**: bidirectional sync of **only** user calendar reminders with **one** dedicated **SAMT Google calendar**, under **one** Google account. Prayer times, special days, locations, and app settings never participate. Same-day delivery repeats and **Enabled** stay local. Conflicts use whole-event last-write-wins plus delete tombstones. First connect pushes local then pulls. Disconnect keeps local data; deleting the cloud calendar is a separate confirm. Inbound imports only single timed non-recurring events; other shapes are skipped and reported on **calendar sync status**. Sync is local-first (queue/retry); controls live in App settings.

**Why not the alternatives:** push-only under-delivers “edit on phone”; bi-di of prayer/special days invents a second authority over computed/catalog facts; syncing an arbitrary user calendar turns SAMT into a general Google client; content-fingerprint merge and multi-account inbox are fragile. Google API/OAuth belong outside pure Core; only reminder + link-state domain concepts live in Core.

See glossary terms in `CONTEXT.md`: User calendar reminder, Google Calendar link, SAMT Google calendar, Calendar sync conflict policy, Google Calendar disconnect, Calendar sync status.
