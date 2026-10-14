# 0.3.8 setup, presentation and achievement verification

Date: 2026-10-11. Existing Windows QA host. This is interface/import/presentation verification, not a new whole-game or physical-controller certification.

| Check | Result |
|---|---|
| Release build | Zero warnings/errors; self-contained Windows x64, 0.3.8 |
| Core suite | 159/159 pass, including real user BIOS ZIP, native 7z, SDL parser, disc extents and achievement artwork/security regressions |
| Clean portable start | 44/44 native checks with package-local .NET runtime |
| Relocated portable start | 44/44 native checks; configuration, appearance and save marker preserved |
| Current-game setup | Missing → game/emulator available but BIOS needed → three completed checks. Unrelated PS3 update excluded; PS3 imported versus installed distinguished |
| BIOS ZIP | Four validated PS2 candidates; one US v1.60 BIOS selected, ROMVER `0160AC20020207`. Source ZIP SHA unchanged; no temporary staging left |
| BIOS identity | Structural ROMDIR/ROMVER recognition. No reviewed PS2 hash reference is bundled, and a console model is not inferred from the filename |
| AC2 US preview | `MOVIE/AC2OP01.PSS`, 10–30-second spacecraft/mech segment; direct ISO range, MP4 + JPEG |
| Last Raven US preview | `MOVIE/O1.PSS`, 55–75-second mech battle segment; direct ISO range, MP4 + JPEG |
| ACV US Xbox preview | `movie/jp/acv_opening.wmv`, 15–35-second hangar segment; direct Xbox game-partition range, MP4 + JPEG |
| Real native video playback | All three new MP4s opened and advanced in WPF; shared scene checked at 1280/960 pixels |
| Source media | Original ISO sizes/timestamps unchanged; source streams read-only, no whole-disc/movie extraction |
| Cover fallback | Added high-quality covers supply dim/blurred backdrops; tiny menu icons and unadded games stay small |
| Keyboard summary | Grouped action/native-button/key rows replace the paragraph; preset/edition changes and 1280/960 layouts checked |
| Achievement visuals | Cached color versus dim grayscale pixels, secret art/accessibility suppression, focused provider descriptions, missing-art fallback and 1280/960 layout checked |

The bounded UI review found that the main setup action and achievement icons could fall below the small-window viewport. The fix places the setup action beneath the heading, compacts the achievement toolbar/progress text and keeps native scrolling for the full lists. A single confirmation round checked those corrections and the actual keyboard rows. No further visual polishing cycle was run.

Real-media smoke outputs are private under `work/movie-presentation-smoke`; no game movies, extracted images or BIOS bytes enter the repository/release ZIP. The selected BIOS SHA256 is `f4c948e61a291d4b3f92a141e550cf8357204287a31ff784caccbedaef910c9d`. Package/deployment receipts are in ignored work storage, with changed program files backed up before the D: overlay. The live BIOS import is intentional and logged separately in `logs/bios-setup-import.json`; library registrations are retained. Live cinematic preparation records `logs/movie-presentation-smoke.json`.

Evidence: `work/test-results/test-results.json`, `work/portable-results.json` and its capture directory, `work/release-0.3.8-build.log`, `work/release-0.3.8-confirmation-build.log`. Research and supported-source limits: [bios-selection.md](../../research/bios-selection.md), [disc-cinematic-previews.md](../../research/disc-cinematic-previews.md), [achievement-visuals.md](../../research/achievement-visuals.md).

Remaining limits: proprietary/nested or compressed disc movie containers; provider achievement art absent from local data; direct PS3 ISO trophy enumeration and Xbox XEX/SPA resource decompression; live RetroAchievements authentication/unlocks and real PS3 unlocks. The inspected ACV profile contains 50 records but only one embedded image, so most entries correctly retain the trophy fallback until suitable art is supplied. No trophy sets or prerequisites are invented.
