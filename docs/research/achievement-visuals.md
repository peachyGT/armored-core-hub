# Achievement artwork and locked states

Implemented 2026-10-11. Artwork comes from the game or the linked achievement provider; the Hub does not create achievement definitions, infer prerequisites, or assign PSN grades to Xbox/RetroAchievements records.

## Provider evidence

- [RPCS3's trophy list implementation](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Emu/RSX/Overlays/Trophies/overlay_trophy_list_dialog.cpp) identifies local `TROPCONF.SFM`, `TROPUSR.DAT`, and numbered `TROP000.PNG` artwork. Its `hidden="y"` handling conceals locked trophy names/descriptions. The Hub likewise suppresses hidden artwork and details until the record is unlocked.
- [RPCS3's TRP structures](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Loader/TRP.h) and [loader](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Loader/TRP.cpp) document uncompressed trophy bundles, their big-endian header/entries, and SHA-1 verification for newer archive versions. The Hub's bounded reader accepts only trophy metadata and numbered PNG slots from unpacked games. English `TROP01.SFM` is preferred, then installed/default configuration. No executable content is decoded or modified.
- [Xenia's GPD definitions](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/xdbf/gpd_info.h) identify achievement image IDs, image namespace 2, and the ShowUnachieved/Achieved flags. [Its icon lookup](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/user_tracker.cc) links achievements to those IDs. The Hub reads embedded PNG images by ID and keeps locked/earned descriptions separate; records marked secret are redacted until earned.
- [RetroAchievements' progress endpoint](https://api-docs.retroachievements.org/v1/get-game-info-and-user-progress.html) supplies descriptions, points, unlock dates and `BadgeName`. [RAWeb's badge URL implementation](https://github.com/RetroAchievements/RAWeb/blob/master/app/Models/Achievement.php) supplies the badge path. The Hub accepts only numeric badge IDs and downloads badges from the fixed official media host during the user's explicit account refresh. Existing cached art remains available offline; there is no polling service.

## Behavior

Achievement tiles display the original full-color icon when unlocked. Locked icons retain a dim grayscale impression against black. Each tile also states Locked/Unlocked and its actual grade or points, so color alone does not communicate progress. Missing art uses the existing trophy vector; it is explicitly identified as unavailable artwork rather than a invented game image.

Hover and keyboard focus show the provider's description. This description is the only supplied requirement information; no separate prerequisites are inferred. Secret records have generic titles/details and no game-specific artwork until unlocked. The same protection applies to accessibility text. Escape closes a focused tile's tooltip.

Installed PS3 metadata is sufficient to list locked trophies without `TROPUSR.DAT`. Unpacked games' `TROPDIR` configuration or `TROPHY.TRP` can supply the list before registration. Once an emulator profile exists, its real progress takes precedence over metadata-only records. Direct PS3 ISO trophy-folder enumeration is not added here; launching/registering the set or using a prepared game folder supplies the same metadata. AC4 and For Answer PS3 remain without trophy sets; their Xbox achievements are separate.

A read-only check of the existing ACV QA profile found 50 achievement records and only one embedded GPD image. That profile does not supply original artwork for every achievement, so the missing images use the trophy fallback. No XEX executable decompression or resource extraction was added to fill that gap.

## Storage and bounds

Optional `Icon`, `ProviderIconId` and `Hidden` fields preserve old snapshot compatibility. PNGs are stored by content hash in `achievements/assets`, with relative paths that survive moving the Hub. Paths cannot point outside this cache. Image bytes are capped at 4 MiB and dimensions at 1024×1024; PNG chunk extents are checked before caching. A missing, altered or undecodable image falls back without discarding progress.

TRP input is capped at 16 MiB and 2048 entries, with strict filenames, duplicate detection, bounded non-overlapping extents and supported version/checksum checks. Xbox images use bounded table extents and a 16 MiB aggregate limit. Account refresh downloads at most 512 distinct badges with four concurrent requests and a 16 MiB aggregate budget. Remote redirects are disabled. UI thumbnails decode once at 128 pixels and both color/locked variants share a bounded 128-entry memory cache; no effect runs per frame.

Regression tests exercise original image linkage, metadata-only sets yielding to real progress, secret redaction, hostile paths, oversized/truncated images, TRP checksums/extents/names, malformed GPD image extents, provider badge IDs, and relocation. Native checks verify dim/color pixels, image cache reuse, secret accessibility/art suppression, focused descriptions and small/large layout captures. Synthetic visual fixtures are named as fixtures and are not bundled as game achievements.
