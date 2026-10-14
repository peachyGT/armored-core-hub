# Playtime, legal notice and shutdown verification — 0.3.10

Date: 2026-10-13. Existing Windows QA host. No commercial game was launched or modified in this pass.

## Changes

- Each selected edition displays time played beside its readiness status. A portable `config/playtime/<edition>.json` ledger imports the highest available native total once, then records Hub session deltas. Copied histories do not add together. Records remain outside save rollback.
- DuckStation and PCSX2 native history stores seconds; RPCS3's persistent Playtime section stores milliseconds. Imports match the edition's exact product serial and bound file/line/value sizes. Other backends use the same Hub session clock. Menus/loading/pauses count; setup and final backup/sync do not. A checkpoint runs every 30 seconds only while a game is running and is stopped/committed when it ends. A forced exit can lose the last interval.
- Closing during a game defaults to keeping it open. Confirming cancels the owned session, waits asynchronously for the emulator/helpers, commits playtime and protects saves before the Hub window closes. Background artwork work is cancelled and joined. No new online achievement refresh is started for a cancelled session.
- Windows job objects own launched emulator/helper processes, preview conversion and native extraction. Closing their last job handle terminates associated children, including when the owner exits unexpectedly. No process is discovered/terminated by executable name; unrelated applications are not owned by this mechanism.
- Valid installed emulator releases are reused before accessing download/cache services. Concurrent same-release installs serialize and return one installation. The Emulators page omits Install for an already available backend. Separate intentionally selected compatibility versions and per-game settings/saves remain supported.
- Settings → About and legal opens a Legal tab with an anti-piracy disclosure and separate Licenses/About tabs. Historical third-party game/BIOS download-site references were removed from shipped documentation. Local imports, official emulator/system-update sources and required third-party credits remain. No unauthorized game-download provider was implemented.

Read-only inspection of the installed D: Hub confirmed that AC4 and For Answer's executables and the pristine RPCS3 executable are hard links to one file. Their native histories contain 168,789 and 2,623,056 milliseconds respectively at inspection. The importer reads them without changing native records. This is history evidence, not new gameplay certification.

## Verification

- Core suite: **209/209** pass with the real BIOS ZIP, SDL parser and MP4 fixtures enabled. New regressions cover native units/serials/bounds, copied-history deduplication, idempotent deltas, stopped timers, atomic recovery, portable relocation, installed reuse without network/cache, concurrent installation, owned-process cancellation and unexpected-owner cleanup.
- An unrelated synthetic process stayed running after the owned job closed. A separate synthetic parent's forced exit killed its child. Utility cancellation waited for exit and released its pipes.
- Native app verification covers playtime/Legal layouts, redundant install actions and the real running-session close path using a synthetic runtime. Declining retains the session; confirming checks process exit, final save snapshot and playtime before the window closes. These are application fixtures, not physical controller or emulator gameplay tests.
- Published resource checks pass: 80 small/8 large artwork replacements, 12 closed-window collections, 24 real MP4 source switches and 12 malformed preview shutdowns. Video handles: 2759 → 2762 → 2762 → 2763, settling at 2757. Resource budgets exclude actively displayed visuals and native/OS pooling; this is finite coverage, not a guarantee against every possible leak.

An initial relocation fixture hit a transient Windows directory lock and now uses the existing bounded managed-move retry. The first native close fixture exceeded Windows' working-directory limit; shortening its private path fixes that test setup. Automated fixture errors are reported without modal dialogs. Required validation still fails on errors/timeouts rather than leaving a stale successful receipt.

Fresh and relocated self-contained copies each pass **50/50** native checks, using the package-local .NET runtime. Configuration, appearance and the save marker survive relocation. All five published resource checks pass; twelve closed windows collect and window-batch handles stay 2496 → 2496 → 2496. No Hub/synthetic-runtime process remained after these checks.

One batched visual review inspected playtime, the Legal notice and licenses at 1280/960 and the shared-emulator page at 1280. The existing charcoal/ivory styling, readable status row and native scrolling remain; no extra visual correction cycle was needed.

Evidence: `work/release-0.3.10-shutdown-build.log`, `work/test-results/test-results.json`, `work/release-0.3.10-final-portable.log`, `work/portable-results.json`, `work/review-0.3.10.jpg`, and the resource root named in `work/resource-root-0.3.10.txt`. Package/deployment receipts remain in ignored work storage. User media, BIOS, native save/achievement records and runtime selections are not changed by the program-file overlay.

Primary format/lifetime references: [DuckStation game list](https://github.com/stenzek/duckstation/blob/master/src/core/game_list.cpp), [PCSX2 2.8.2 game list](https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/GameList.cpp), [RPCS3 persistent settings](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/rpcs3qt/persistent_settings.cpp), [Microsoft job objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects).
