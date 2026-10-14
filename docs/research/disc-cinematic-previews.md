# Local disc cinematic previews

Access date: 2026-10-11. Scope: automatically showing presentation media from the user's registered copies.

The earlier preview search only walked a few extracted-folder paths, recognized exact names such as `opening`, and did not expose files inside PS2/Xbox ISOs. AC2 and Last Raven store their openings as abbreviated PS2 MPEG program streams; the tested Xbox ACV copy stores its opening as WMV inside the Xbox game partition. None could be found by that search. The converted cache also wrote an older reader version, forcing unnecessary reads on later selections.

## Implemented path

The Hub now builds a bounded read-only inventory of ISO9660/Joliet or XDVDFS directory extents. Xbox game partitions take precedence over their unrelated ISO9660 DVD video partition. Traversal is limited by depth, file count, total directory bytes and individual directory size; malformed trees, cycles, unsafe names, duplicates and out-of-image extents are rejected.

Recognized opening/intro/title filenames are ranked ahead of menu loops. Console abbreviations such as `AC2OP01.PSS`, `O1.PSS` and `acv_opening.wmv` are supported. Endings, credits, mission, mail and binding-demo movies are excluded. The Hub reads a selected contiguous movie extent through FFmpeg's `subfile` protocol; it does not mount the image or unpack/copy an entire disc or opening movie. Folder scanning prioritizes known movie directories and remains bounded.

The existing verified LGPL decoder generates one muted approximately 20-second MP4, at most 1280×720 and 24 FPS, plus a representative JPEG still. Output remains capped at 64 MiB with a two-minute conversion timeout and cancellation of only its owned child process. Only local `file`, `subfile` and `pipe` protocols are allowed. Reader/cache version 5 now agrees between the menu reader and converted output. Short menu loops start at zero; verified longer AC2/LR/ACV openings skip early splash/grain sequences.

## Actual media verification

| Registered edition | Source inside its original ISO | Preview segment | Output MP4 |
| --- | --- | ---: | ---: |
| AC2 US PS2, SLUS-20014 | `MOVIE/AC2OP01.PSS` | 10–30 seconds | 4.08 MiB |
| Last Raven US PS2, SLUS-21338 | `MOVIE/O1.PSS` | 55–75 seconds | 8.28 MiB |
| ACV US Xbox 360, 4E4D084F | `movie/jp/acv_opening.wmv` | 15–35 seconds | 2.91 MiB |

All three decoded automatically through the actual service in an isolated workspace Hub, with the existing pinned decoder copied from the installed Hub. Source file sizes and modification times stayed unchanged; originals were opened read-only. Representative stills were inspected: AC2 shows its spacecraft/mech opening, Last Raven shows its mech battle, and ACV shows its hangar cinematic. This proves discovery and conversion for these three copies, not all regions or every game format. Private results and generated media stay under ignored work storage; none enter the portable package.

Synthetic regression checks cover valid ISO/Xbox discovery, Xbox-versus-DVD partition priority, cyclic/truncated/unsafe directories, excluded ending/tutorial names, folder discovery and subfile bounds. `--movie-presentation-smoke <liveHubRoot> <catalogDirectory> <targetHubRoot> [edition IDs]` can regenerate those three previews in any Hub root that already contains the pinned decoder; it writes `logs/movie-presentation-smoke.json`.

## Fallback and limits

Explicit user artwork/video retains priority. When no cinematic can be discovered or decoded, added games use a deliberately dim, blurred high-quality cover backdrop; small console menu icons remain small and are not enlarged into backgrounds. Games not added retain the small-cover presentation. The Hub does not claim a video exists when it is showing this fallback.

Current disc enumeration supports contiguous ISO9660/Joliet and XDVDFS files, without decryption. Compressed disc formats and proprietary nested game archives do not yet expose their movies automatically; their cover fallback remains available. Decoders for some discovered console formats may also be unavailable. These are presentation limitations and do not alter the games' launch/readiness state.

## Primary references

- [FFmpeg subfile protocol](https://ffmpeg.org/ffmpeg-protocols.html#subfile): exposes a bounded byte range as a seekable input without extraction. [Protocol allow-list](https://ffmpeg.org/ffmpeg-protocols.html#Protocol-Options) constrains permitted input access.
- [Xenia disc image device](https://github.com/xenia-project/xenia/blob/master/src/xenia/vfs/devices/disc_image_device.cc): identifies Xbox game-partition variants independently of the video partition. The Hub implementation remains its own bounded reader.
- [FFmpeg Bink demuxer](https://github.com/FFmpeg/FFmpeg/blob/n8.0/libavformat/bink.c) and [PlayStation STR demuxer](https://github.com/FFmpeg/FFmpeg/blob/n8.0/libavformat/psxstr.c): establish supported decoder routes for recognizable local movie files. The tested ACV copy used WMV, not Bink.
