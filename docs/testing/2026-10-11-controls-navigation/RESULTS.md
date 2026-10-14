# 0.3.7 controls, runtime detection and navigation verification

Date: 2026-10-11. Existing Windows QA host. This update does not certify complete game playthroughs.

| Check | Result |
|---|---|
| Release build | Zero warnings/errors, self-contained Windows x64, version 0.3.7 |
| Core regression suite | 137/137 pass, including native SDL3 mapping-parser acceptance |
| Clean portable start | 37/37 native UI checks with the package-local .NET runtime |
| Relocated portable start | 37/37 native UI checks; configuration, appearance and save marker preserved |
| Installed runtime recognition | Live For Answer selection without an explicit pin resolves existing RPCS3 0.0.43-20152; media, runtime and firmware Ready. Live state SHA unchanged; no emulator started |
| Archive comparison | Same AC3 source and output hashes: managed extraction 148.69 seconds, guarded native extraction 33.56 seconds (4.43×). Source hashing is excluded from both timings |
| Controls | All catalog schemes have complete sourced action labels; independent presets, named imports, rename/export/remove, legacy migration and edition/layout rejection tested |
| Actual adapter configuration | Community launch-plan tests cover DuckStation, PCSX2, PPSSPP, RPCS3 and Xenia; enabled/disabled analog PNACH options and Xenia custom half-axis mappings tested |
| Native control editor | Action/native-button labels, separate device selections, renamed imports and safe recovery from incompatible game-layout selection checked |
| Achievements | Global navigation and selected-game shortcut checked at 1280, 960 and 820 pixels; synthetic RPCS3 bronze/silver/gold/platinum parsing, unknown grades and per-account progress verified |
| Title logo | Explicit transparent local/manual logo renders for unadded games; PIC0 no longer guessed as a logo; text fallback retained |
| Existing preview playback | Actual locally derived AC4 MP4 opens and advances through native Windows playback |

The native review retained the approved A+C design. The narrow header keeps the logo on one line and collapses secondary navigation text into accessible icons. The binding editor prioritizes combat actions and keeps unused/menu controls accessible below. Achievements use a single progress view; its per-game settings tab handles account/tracking configuration.

The confirmed Community baseline uses LT/RT arm weapons, LB boost/jump, RB quick boost or weapon cycle, left-stick/WASD movement and right-stick/IJKL camera. Each template adapts to the game's declared layout. Selecting a Hub scheme never edits an in-game save. The Community label does not assert universal agreement or new physical-gameplay verification.

Remaining evidence limits: new Community permutations and generated Xenia remaps need physical in-game checks; official For Answer PS3/Xbox Simple manuals disagree about R1/RB and the PS3 label is flagged; Western Another Age pause/map and complete late-PS2 classic Type B presets need additional independent confirmation. RAR-specific fixtures, live RetroAchievements unlocks, real PS3 trophy unlocks and campaign-wide timing/scaling remain open. Prior player-confirmed Default controls and sampled gameplay findings remain in earlier campaign records.

No complete consistent official transparent-logo set was found. Supported `logo.png` or manual Artwork imports remain available, and researched text titles serve the normal fallback. No commercial game assets, saves, firmware, patch payloads or account credentials are included in the release ZIP.

Evidence: `work/test-results/test-results.json`, `work/portable-results.json` and its capture directories; `work/release-0.3.7-verified-build.log`, `work/release-0.3.7-ui-recovery-build.log`; runtime detection smoke records from the read-only live check; the source-preserving archive benchmark described in [archive-performance.md](../../research/archive-performance.md). Primary controls and uncertainty records are in [game-controls.md](../../research/game-controls.md) and [community-controls.md](../../research/community-controls.md); [title-logo-feasibility.md](../../research/title-logo-feasibility.md) records the bounded asset audit.

Program-only deployment to `D:\ArmoredCoreHub-0.3.5-qa20261011` keeps the existing folder name. Changed program files are backed up under `work/upgrade-backup-0.3.7-*`; `work/deployment-0.3.7.json` records the outcome. Configuration/save file hashes are compared before and after the overlay, while games, firmware, runtime builds, artwork and achievement records are excluded from the payload.
