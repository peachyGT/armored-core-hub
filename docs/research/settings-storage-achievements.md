# Settings, storage and local progress — 2026-10-11

## Reported import failure

The user's `logs/last-scan.json` recorded three free-space failures: For Answer needed about 14.0 GB, AC3 1.7 GB, and ACV 7.3 GB on the Hub's C: drive. Moving the compressed downloads to D: did not change the old extraction destination. The log did not indicate damaged game data.

0.3.6 stores the extraction destination in `config/game-storage.json`. Settings → Game storage selects a dedicated folder for future extracted/copied/prepared/patched games. Existing registrations retain their locations. Space checks refer to the destination drive. The import review shows each failed filename and reason. Original archives are read only; cancelled imports remove only newly generated, unregistered folders.

For this installation, the selected destination is `D:\ArmoredCoreHub-Storage`. All three previously failed archives were successfully unpacked and registered through the actual import implementation; every resulting media path exists. No old C: game copy or player save is deleted.

## Presentation

AC4 EU's prepared PS3 folder contains an icon and overlay but no standard PIC1 background. Its `USRDIR/movie/us/opening.pam` is usable. A missing/older extraction cache now retries. Standard PSP ISO/PBP menu movie slots are extracted with the existing 16 MiB bound. Folder discovery checks only bounded, named movie locations; arbitrary proprietary archives are not unpacked.

The optional decoder is [BtbN's pinned LGPL shared FFmpeg build](https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-10-11-13-04), archive SHA-256 `ba651b56d78b2a09a92d3699fd7751254e8b773dc2c3110081b7f9f4ff3b2c48`. Program files are verified before reuse. A semaphore prevents parallel conversion; each helper has a two-minute timeout and two encoder threads. Network input protocols are disabled. Output is a muted, at-most-20-second, at-most-1280×720/24 FPS MPEG-4 Part 2 clip, limited to 64 MiB, plus a representative JPEG. Helpers exit after conversion. Native Windows playback was verified with the real AC4-derived clip. Still fallback remains available when Windows lacks a codec.

AC4's first movie frames are near-black particles, so its long opening uses the factory sequence starting at 80 seconds. Short menu loops start at zero. PAM/PMF/PSS require explicit MPEG demuxing: automatic `.pam` detection otherwise selects the unrelated Netpbm image format. This was reproduced and fixed.

Movie decoding is not universal. PS1 STR movies, Xbox XMV, compressed PSP CSO movie extraction, arbitrary PS2 movie archives and PSP EBOOT patch preparation are not implemented. Some games contain no useful background/logo. Manual artwork/video selection remains available, without stretching a cover into a backdrop. No retail assets or generated clips are packaged.

## Graphics and controls

Game settings starts with Settings, then Controls, Patches, Achievements, Save Data, Advanced and Details. Preset benefit text changes with the selection; custom scale explicitly supersedes the preset's resolution description. PS3/Xbox Enhanced and High-End now request 2× and 3× rather than duplicating Original. Scaling is optional and remains scene/hardware dependent.

Scale choices map to the [RPCS3 configuration schema](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Emu/system_config.h) and [Xenia options](https://github.com/xenia-canary/xenia-canary/wiki/Options), with existing DuckStation/PCSX2/PPSSPP adapters. Window resolution is applied only to the launched process's window, without changing the display mode, focus or system settings. Larger-than-display windows are clamped. RPCS3/Xenia expose independent caps; older emulators retain game-default timing until a suitable game patch exists. ACV retains its sampled half-vblank behavior: a 60 FPS target uses a 120 cadence and disabled vsync. This is not whole-game timing certification.

Keyboard and controller choices are independent: Default, Community and Custom. The binding editor writes native inputs and retains supported alternate keyboard/XInput devices. Xenia custom controller layouts use an explicitly supplied SDL mapping database, following its [SDL driver](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/hid/sdl/sdl_input_driver.cc) and the [community SDL database](https://github.com/mdqinc/SDL_GameControllerDB). The device GUID must match. No polling/injection helper is added. Generic non-XInput pad alternatives in PPSSPP custom profiles are not certified; Default retains upstream generic mappings.

No reliable evidence establishes one most-common community keyboard mapping for every game. Community entries become selectable when a sourced, edition-matching profile is imported. The Hub does not label its own mapping as community consensus. Profile format:

```json
{
  "name": "Profile name",
  "source": "https://author.example/profile",
  "editions": ["ac2-ps2-slus20014"],
  "keyboard": {"Cross": "X"},
  "controller": {"Cross": "A"}
}
```

Only supported console-button and physical-input names are accepted. Xenia profiles can additionally supply `xeniaMapping`. Imported profiles are user choices, not a bundled compatibility endorsement.

## Local patches

Browse known patches links directly to author documentation, including [VanLaser's analog enhancements](https://github.com/VanLaser/analog-controls-for-armored-core-ps2). Local PNACH and PPF import is enabled independently of permission to redistribute those authors' payloads. The Hub bundles no such bytes.

PPF1/2/3 writes are bounded to the original image. PPF2 size and PPF3 block/undo records are validated; offsets are little-endian. The original [ApplyPPF implementation](https://github.com/meunierd/ppf/blob/master/ppfdev/applyppf_src/applyppf3_linux.c) corrected an endianness discrepancy found in another format reference. PPF1 lacks revision proof; a locally assigned source hash does not add that proof. Only one image patch is active at a time. Disabling it launches the original registered image.

PNACH import requires its eight-digit CRC in the filename and patch instructions. Its copied bytes are hash checked before use. Named sections can be selected separately; the generated [PCSX2 configuration](https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/Patch.cpp) includes `[Cheats]` enable entries, not just the global EnableCheats flag. Editing saves a new version and preserves the old payload. An analog requirement enables the native controller type; DuckStation additionally maps **H** to the analog toggle for titles whose GameDB disables analog at reset. PSP analog patches targeting decrypted EBOOT.BIN are not whole-ISO patches and still require separate preparation. The future mouse extension remains absent.

## Achievements

- [Xenia GPD](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/xdbf/gpd_info.h): bounded XDBF entry parsing, UTF-16 names/descriptions, points, unlock flags and timestamps. The real ACV lab profile yielded 50 achievements and zero unlocked; no progress was invented or edited.
- [RPCS3 trophies](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Loader/TROPUSR.h): bounded trophy table and safe XML parsing, local account separation. AC4/For Answer have no native trophy set. Parser fixtures pass; a real trophy-enabled game unlock has not been exercised in this step.
- [RetroAchievements progress API](https://api-docs.retroachievements.org/v1/get-game-info-and-user-progress.html): explicit username/game ID/key, console/family validation, bounded HTTPS response and offline cached records. The selected set is user-linked, not automatically proven from a dump hash. Hardcore and ordinary unlocks are kept distinct. The API key is not persisted or included in logs. Live authentication/unlock testing requires the user's account and remains unverified.

Native achievement settings use the documented sections in [DuckStation](https://github.com/stenzek/duckstation/blob/master/src/core/achievements.cpp), [PCSX2](https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/Achievements.cpp), and [PPSSPP](https://github.com/hrydgard/ppsspp/blob/master/Core/Config.cpp). The user signs in directly in the emulator. The Hub preserves a filtered, Windows-DPAPI-encrypted achievement section outside launch-plan logging; native secondary secret stores are not scraped. New sessions/builds can require sign-in again. Tests use synthetic tokens only.

The Hub never awards or edits achievements. Patches, cheats and modified images may conflict with a service's set identification or hardcore restrictions; the emulator/service decides what qualifies.

## Verification boundaries

The core suite covers storage relocation/cancellation, separate graphics values, independent bindings, SDL input validation, Windows-account encryption, PPF bounds/revisions, PNACH integrity/options and achievement parsing. Native checks exercise persisted settings, tab order, mapping summaries, small-window layouts and real MP4 decoding. Packaging checks run both fresh and relocated copies. This is implementation evidence, not a repeat of the full gameplay campaign. See STATUS.md for final counts and actual import/native-patch results.

Actual native patch check: PCSX2 2.8.2 launched a separate AC2 session using the imported VanLaser PNACH and logged `Enabled patch: True Analogs`. The test ran for 30 seconds, stopped only its owned process, and kept the original media and lab saves unchanged. This confirms native patch activation; the physical dual-stick gameplay result remains the previously recorded user check.
