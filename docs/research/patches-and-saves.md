# Patch and save research

Accessed 2026-10-09.

[VanLaser's analog-controls project](https://github.com/VanLaser/analog-controls-for-armored-core-ps2) covers PS1, PS2 and PSP variants with actual analog input processing. Its PSP patches target decrypted EBOOT files and break replay compatibility; Formula Front International has a distinct minimum emulator requirement. Confidence high for the author's stated targets/caveats. Redistribution permission and our exact dump hash records remain unresolved, so the Hub records author links rather than redistributing/enabling payloads.

[PCSX2's patch repository](https://github.com/PCSX2/pcsx2_patches) contains title/CRC-bound widescreen, interlacing and rendering modules. [Last Raven JP's entry](https://github.com/PCSX2/pcsx2_patches/blob/main/patches/SLPS-25462_FEBE1992.pnach) distinguishes widescreen, no-interlacing, blur and HUD effects. These are not interchangeable “graphics better” settings. Confidence high for patch existence and targeting; effect quality/performance requires game validation.

[Xenia patch issue 117](https://github.com/xenia-canary/game-patches/issues/117) illustrates why title ID alone can be insufficient for patch matching: AC4 patch metadata lacked exact revision details in the reported snapshot. Confidence high for the issue's historical lesson, not a claim that every current patch remains incomplete.

[LunaMoo's author documentation](https://github.com/LunaMoo/PPSSPP_workarounds/blob/master/README.md) labels older freeze workarounds for AC3/Silent Line/Last Raven. Since upstream emulation has changed, these are research only. Do not enable obsolete hacks globally.

The baseline patch installer accepts licensed local configuration modules, versioned by ID/version, with exact source SHA-256, dependencies/incompatibilities and explicit gameplay opt-in. It does not silently modify images. Binary patching/translation requires a future derived-image adapter and exact source verification.

## Save confidence

Families model known same-generation in-game carryover relationships conservatively; they are not byte converters. Exact per-title import restrictions (parts, money, emblems and special abilities), regional compatibility and PS2/PSP differences still need cited manual-level records plus game tests. Confidence medium on grouping, low on detailed transfer guarantees. The UI explicitly delegates supported transfer to the original game.

PS1 shared cards and PS2 family cards make existing progress visible. Portable, PS3 and Xbox roots remain separate. Backups include source game, backend, family, time, reason and verified file hashes. Restore never consumes the backup.
