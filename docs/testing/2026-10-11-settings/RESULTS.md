# 0.3.6 settings/storage verification

Date: 2026-10-11. Windows host used for the existing QA campaign. No physical-controller or full-playthrough retest is implied.

| Check | Result |
|---|---|
| Release build | Zero warnings/errors; app/core version 0.3.6 |
| Core regressions | 112/112 pass |
| Fresh self-contained package | 33/33 native checks, package-local .NET runtime |
| Relocated self-contained package | 33/33 native checks; state, appearance and save marker preserved |
| Actual failed archive retries | For Answer US PS3, AC3 US PS2, ACV US Xbox all registered under D:\ArmoredCoreHub-Storage; all paths exist |
| Original archives/player saves | Retained; no relocation/deletion of existing data |
| AC4 EU presentation | Local PAM opening converted; native MP4 opens and timeline advances; image/video cached in user's existing Hub |
| Real native patch activation | Isolated AC2 + PCSX2 2.8.2, local VanLaser PNACH imported through LocalPatchService; emulator log reports `Enabled patch: True Analogs` |
| Local ACV achievement set | Real Xenia profile: 50 entries, zero unlocked; no progress edited |
| Live RetroAchievements credentials/unlocks | Not tested; no user credentials supplied/requested |
| Real PS3 trophy unlock | Not tested; bounded parser fixtures pass |

Native layout review covered Settings at 1280×800 and 960×540, Controls, Patches and Achievements. The existing suite also covers 820/960/1280/1600 artwork layouts, drag handle, hover/reduced motion, modal import transparency, Help, edition switching and codec failure fallback. Seven tabs remain reachable and Details is last. This is the approved A+C visual system, not a redesign. Covers in isolated offline fixtures are intentional placeholders; the user's cached cover catalog is preserved.

New core regressions cover destination-drive persistence and cancellation cleanup; supported scale/cap translation and invalid values; distinct preset descriptions; independent control profiles; native RPCS3/PPSSPP/Xenia mappings; SDL validation; encrypted native achievement-section preservation; damaged-cache isolation; local PNACH CRC/integrity/options and enable/disable plans; PPF1 copy preservation, PPF2 little-endian size and PPF3 block/undo bounds; embedded PBP menu movies; GPD unlocks, PS3 trophy bounds and RetroAchievements hardcore/console validation.

Evidence retained locally (ignored generated data): `work/test-results/test-results.json`, `work/final-core-tests.txt`, `work/portable-results.json`, native captures beneath its validation root, `work/actual-presentation-smoke.json`, and the owned AC2 session logs under `work/settings-patch-native`. User installation logs retain `logs/last-scan.json` and add `logs/storage-repair.json` for the repair outcome. Commercial game files/previews/patch payloads and native credentials are excluded from the source checkpoint and portable ZIP.

Remaining limits: unsupported proprietary movie containers; generic non-XInput PPSSPP custom layouts; Xenia custom SDL hardware mapping exercise; per-title community consensus; PSP EBOOT patch rebuilding; real-service sign-in across emulator build switches; additional-monitor window sizing and assistive-technology certification. Window-size controls never change system display modes. Existing gameplay findings and the explicitly waived ACV campaign-save check remain in the October 10 campaign record.
