# Native A+C adoption — 2026-10-09

The user selected the hybrid and requested a narrower adjustable library, smaller fitted artwork with softened edges, and gentle cover-derived ambient lighting. This is native WPF adoption, version 0.2.0. The older browser comparison remains a historical design artifact.

## Delivered behavior

- Default 36% library / 64% selected-game scene. The divider supports pointer dragging and keyboard arrows; double-click resets it. Collapse uses a 48 DIP rail. Expand uses 68% where the 320 DIP scene minimum allows it. Restore keeps the normal split. The library minimum is 280 DIP.
- Grid/list, console or generation grouping, console/generation/search/added filters. The catalog remains 15 games and 100 editions; four optional PSP entries give 19 library entries.
- Local cover/background import with content-hash filenames, preserved originals, cached bounded decodes and path guards. Fit-to-scene artwork and edge masks preserve more of the supplied image. Low-opacity ambient tint comes from a small hue histogram excluding very dark and near-white pixels. Settings can disable it; high contrast disables decorative imagery and tint.
- Cinzel wordmark and game titles, Segoe UI controls, charcoal and ivory. Game settings use the existing native services. About/licenses are under Settings. No native trailer player was added; local-video playback remains a browser-prototype capability.

## Verification

`scripts/build.ps1 -Offline -Test -Publish` compiled with zero warnings/errors and passed all 53 core tests. The app's isolated `--self-test` exercises twelve native assertions including edition filters, split restoration/persistence, compact settings space, ambient warm/cool sampling, disabled lighting, invalid preferences, artwork import/deduplication/traversal protection, and advanced-draft preservation. Fixtures contain no commercial game bytes.

Actual WPF client renders are in `.impeccable/review/native-hybrid/`. Outer window sizes tested are 1320×880, 1280×720 and 960×540 DIP; PNG dimensions exclude the Windows frame. Required review captures: hybrid-final, hybrid-1280, hybrid-list-960, hybrid-filters-960, hybrid-advanced-960, library-collapsed and library-expanded. These show supplied private reference artwork and real “Not added” states.

The first independent finish review requested three fixes: clear artwork lettering from behind the live title, give Artwork a stable dark background, and replace the pending-choice product statement. Corrections were implemented and recaptured in one batch. An additional artwork test exposed a missing InvalidDataException fallback catch; that exception is now handled and the full native checks pass.

The native web detector was skipped because it does not parse WPF. Fresh visual review used Windows conventions. No third-party screenshot automation or synthetic copyrighted media was used. Existing native rendering checks run inside the Hub. This is not Narrator, physical controller, pointer-drag hardware, high-contrast-theme or gameplay certification.

`scripts/validate-portable.ps1` checks clean and relocated copies, requires their bundled .NET runtime, and compares state, appearance and save markers. The clean redistributable excludes user artwork, games, firmware and emulator downloads. The separate local review folder includes only the user-supplied artwork, with original provenance metadata retained; the provenance scan found three rasters and zero missing records.

Final package verification passed: 53 core tests, twelve native assertions in each clean/relocated copy, and SHA-256 comparison of all 441 ZIP files against the published directory. State, appearance preferences and the synthetic save marker survived relocation. This was an isolated copy on the current Windows machine, not a fresh Windows VM.

## Review disposition

**ship**, at the correction-verdict scope. The reviewer scored all three findings resolved, validated all seven refreshed captures, and observed no regressions from those corrections. This verdict is not a claim of whole-application accessibility or gameplay certification.
