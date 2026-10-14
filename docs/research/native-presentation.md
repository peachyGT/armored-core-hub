# Native presentation assets and visual parity

Access date: 2026-10-09. Release: 0.3.0 engineering preview.

**0.3.8 update, 2026-10-11:** the original cover policy below is superseded for added games only: high-quality covers now provide an explicitly dim/blurred backdrop when no real cinematic/background is available. Tiny menu icons are not promoted, and unadded games still use small covers. Direct PS2/Xbox disc movie discovery now prepares cinematic stills/videos for the tested AC2, Last Raven and ACV copies; see [disc-cinematic-previews.md](disc-cinematic-previews.md). Manual artwork remains authoritative.

## Decisions

The user's seven comparison screenshots are the visual authority for this refinement. The approved A+C split remains. Native controls now use drawn vector icons, square button borders and underlined navigation. Catalog display-title/display-subtitle fields provide the two-line Cinzel identity independently of edition/backend resolution.

Unadded titles show a small cover. A cover is never implicitly promoted to a scene background. Registered media can supply its own background; manual choices take precedence. One cropped background plane spans the library and action scene, with its soft left edge underneath the translucent library. There is no inset rectangular frame. Images and videos share that geometry. The library remains adjustable and defaults narrower than the action scene.

The sampled ambient alpha increased from 35/255 to 57/255 at the radial origin. A separate 32/255 library tint preserves quiet color under controls. High contrast suppresses decorative layers. Windows reduced-motion preferences suppress background video, and a user setting can disable it independently.

## Source evidence

| Source | Conclusion | Confidence |
|---|---|---|
| [PPSSPP GameInfoCache.cpp](https://raw.githubusercontent.com/hrydgard/ppsspp/master/UI/GameInfoCache.cpp) | PSP menu artwork uses ICON0.PNG, PIC0.PNG and PIC1.PNG in game folders/discs; ICON1.PMF is a separate movie asset. | High for slot meanings, not title-specific availability |
| [PPSSPP PBPReader.cpp](https://raw.githubusercontent.com/hrydgard/ppsspp/master/Core/ELF/PBPReader.cpp) and [header](https://raw.githubusercontent.com/hrydgard/ppsspp/master/Core/ELF/PBPReader.h) | PBP has a fixed header and indexed data offsets. Extract only bounded image sections; never execute content. | High |
| [RPCS3 game_list_base.cpp](https://raw.githubusercontent.com/RPCS3/rpcs3/master/rpcs3/rpcs3qt/game_list_base.cpp) | RPCS3 treats ICON0 as menu imagery with its own aspect ratio; a menu icon is not a full-screen background. | High |
| [pop-fe author documentation](https://github.com/sahlberg/pop-fe) | Its PS3 packaging distinguishes cover/ICON0, foreground PIC0, and background PIC1. This supports treating presentation roles separately. | High for that project's output; medium for arbitrary retail editions |
| [Microsoft WPF multimedia overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/multimedia-overview) | Native MediaElement supports composited playback using available Windows codecs. Manual playback permits pause, loop, close and error handling without a webview/helper process. | High |

## Implemented scope

- Read known image slots from PS3/PSP extracted directories, readable ISO9660 containers, and PBP image sections. Named local cover/background/fanart/logo files are also supported.
- Read named Windows-compatible clips from extracted folders and per-file companion directories. A companion for `game.chd` is `game.chd.artwork`; a shared library's unrelated images are not borrowed.
- Copy assets into shared `artwork/<sha256>.<extension>` objects and retain a per-media index under `artwork/extracted/<media-id>/assets.json`. Retain originals, cache the result, and allow a manual rescan. Limit image extraction to 16 MiB and movie copies to 256 MiB. UI decoding is dimension-bounded.
- Validate logo candidates as wide images with actual transparency. This is a geometric check, not proof that an overlay contains the correct wordmark. User-supplied transparent logos are supported; no official logo set is bundled.
- Autoplay compatible videos muted. Pause when the window is inactive/minimized, while utility/settings pages cover the scene, or on user request. Unplayable clips leave the still background visible with a plain explanation.

## Explicit limits

No commercial disc was available to verify which assets each Armored Core edition contains. Standard slots may be missing, low resolution, or contain descriptions rather than logos. Packed textures, encrypted containers, proprietary PS1/PS2/Xbox movie formats, and compressed-container asset extraction are not implemented. PS3 PAM and PSP PMF playback require a separate decoder and remain still-image fallbacks. MP4/AVI/etc. are containers, not codec guarantees. No universal trailer or exact-logo extraction is claimed.

Game-derived files remain local to the user's Hub. They are not inserted into emulator packages and are excluded from redistributable builds. The supplied reference artwork is used only in a separate local preview and test captures.

## Verification

Core fixtures cover PS3 slot extraction, PBP section boundaries, malformed offsets, ISO9660 traversal, duplicate hash copies, missing/corrupt caches, companion isolation and cancellation. WPF checks include 1600/1280/960/820 widths, quiet unadded state, title hierarchy, managed asset paths, native generated-AVI playback/pause, and a real corrupt-clip failure. These fixtures contain no commercial game bytes and do not establish gameplay compatibility.
