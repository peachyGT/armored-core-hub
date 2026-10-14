# ADR 004: Explicit settings and local achievement records

Accepted 2026-10-11 for 0.3.6.

The emulator-only achievement sign-in and web API decisions below are superseded by [ADR 005](005-hub-achievement-setup.md) in 0.3.9.

Keep the native WPF shell and five explicit emulator adapters. Store graphics choices, independent keyboard/controller profiles and enabled local patch IDs in the existing per-edition selection. Resolve them into native configuration at launch. A scale changes rendering resolution; a window size changes presentation; an FPS limit is not a universal game-speed patch. Do not replace these with emulator speed multipliers.

Use bounded JSON snapshots for achievement progress, keyed by edition, provider, account and set. Xenia GPD and RPCS3 trophy files remain authoritative and are read only after the emulator exits, or on explicit refresh. For supported older games, use the emulator's RetroAchievements implementation to detect unlocks; the Hub can refresh an explicitly linked account/set through the documented read API. No duplicate achievement engine, injected runtime, account service, backend, database package or background polling process is needed.

The optional web API key stays in memory for one refresh. Native sign-in is performed in the emulator, never in the Hub. Generated settings preserve the native achievement section in a Windows-account-encrypted local record, outside launch plans and diagnostics. Emulator-specific secret stores remain under their native ownership; a different build/session may need sign-in again. Moving to another Windows account requires signing in again. This is preferable to copying unencrypted credentials into portable configuration.

PPF patches create a separate registered image; PNACH files are copied and hash checked. Original media is never overwritten. Patch IDs include source and patch fingerprints, and PNACH named sections have explicit settings. A locally calculated fingerprint establishes which copy the user chose; it is not proof of an author's intended revision. Honor PPF size/block checks and expose provenance limits.

Game storage can be a dedicated external folder while the Hub, configuration and saves stay portable. Absolute external paths retain their meaning; pre-existing relative game paths are not silently redirected. No automatic migration or deletion of existing games accompanies a storage change.

The optional FFmpeg decoder runs once per preview generation, using a pinned LGPL shared build and bounded local-only inputs. Generated muted clips/stills are cached. This supports useful console movies without an always-running helper or bundling commercial assets.
