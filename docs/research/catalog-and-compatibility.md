# Catalog and compatibility evidence

Accessed 2026-10-09 throughout.

## PlayStation

Primary: [DuckStation game database](https://github.com/stenzek/duckstation/blob/master/data/resources/gamedb.yaml) and [project documentation](https://github.com/stenzek/duckstation).

| Game | Conclusion | Confidence |
|---|---|---|
| Armored Core | Separate regional serials; preserve original digital controls and upstream automatic compatibility settings | High identity; gameplay untested here |
| Project Phantasma | Separate title, regional identity and shared first-generation card family | High identity; save details provisional |
| Master of Arena | Separate disc records and regional serials; preserve both discs and shared card | High identity/disc metadata |

No global PGXP, widescreen or analogue gameplay patch is enabled. Internal scaling is opt-in and does not increase source texture detail.

## PlayStation 2

Primary: [PCSX2 GameIndex](https://github.com/PCSX2/pcsx2/blob/master/bin/resources/GameIndex.yaml). These entries determine serials and the upstream runtime's automatic fixes. Do not duplicate their patch bytes ad hoc.

| Game | Conservative decision | Confidence |
|---|---|---|
| Armored Core 2 | Automatic game fixes; native default, optional scaling; upstream clamping/blending requirements matter | High source, untested gameplay |
| Another Age | Independent edition/profile; preserve automatic fixes and its own regional card identity | High identity |
| Armored Core 3 | Console and Portable editions remain separate; native default, opt-in scaling | High identity |
| Silent Line | Separate regional records; no copied obsolete global patch preset | High identity |
| Nexus | Evolution/Revolution disc records; upstream handles rendering-specific fixes; scaling tiers stay native pending validation | High identity, medium enhancement decision |
| Nine Breaker | Independent title; conservative rendering, automatic fixes | High identity |
| Formula Front | PS2 remains a distinct edition, including Japanese/Asian identity; do not substitute PSP | High identity |
| Last Raven | Preserve effects and automatic fixes; native scaling default because post-processing changes need game-specific testing | High source, medium enhancement decision |

The GameIndex includes renderer fixes involving blend accuracy, mipmaps and sprite processing in this group. The Hub leaves the runtime's serial-based database enabled instead of freezing old internal fixes into every regional profile.

## PSP variants

Identity sources: [Libretro serial metadata](https://github.com/libretro/libretro-database/blob/master/metadat/serial/Sony%20-%20PlayStation%20Portable.dat), [patch author's targeted editions](https://github.com/VanLaser/analog-controls-for-armored-core-ps2), and [PPSSPP reports](https://report.ppsspp.org/games?compat=playable).

AC3 Portable, Silent Line Portable and Last Raven Portable keep their own content, controls and saves. Formula Front Extreme Battle/International PSP variants remain separate from PS2 and from each other. Native controls and no frameskip form the baseline. Confidence: high on listed product identities, medium on cross-version gameplay.

[PPSSPP issue 10734](https://github.com/hrydgard/ppsspp/issues/10734) describes old Silent Line/Last Raven hangs and is closed. Do not silently install an old workaround. [Recent Silent Line reports](https://report.ppsspp.org/game/NPUH10025_1.00) include differing OS/GPU/build conditions; an Android report is not a Windows certification.

## PS3 / Xbox 360

Primary: [RPCS3 compatibility API filtered to Armored Core](https://rpcs3.net/compatibility?api=v1&g=Armored), [RPCS3 wiki](https://wiki.rpcs3.net/), and [Xenia compatibility issues](https://github.com/xenia-project/game-compatibility).

| Game | PS3 evidence | Xbox evidence / decision |
|---|---|---|
| AC4 | BLUS30027/BLES00039 classified Playable by upstream | [465307D6 report](https://github.com/xenia-project/game-compatibility/issues/279); keep independent experimental option |
| For Answer | BLUS30187/BLES00370 classified Playable | [465307DA report](https://github.com/xenia-project/game-compatibility/issues/654); corrected title ID, no permanent winner assumed |
| V | BLUS30516/BLES01440 classified Ingame, not Playable | 4E4D084F appears in [settings maintainer's catalog](https://github.com/xenia-manager/optimized-settings); [Asia ID 4E4D0854](https://github.com/xenia-project/game-compatibility/issues/1317) demonstrates regional variance |
| Verdict Day | BLUS31194/BLES01898 Ingame; rendering risks remain | 4E4D0864 in maintainer catalog; experimental independent edition |

Confidence: high for reported PS3 categories/IDs, medium for Xbox identity reports, low for unsupplied region-specific Xbox variants. Shared IDs cannot establish region; import requires manual confirmation. The current US/JP placeholders retain this warning rather than claiming exact region verification.

[RPCS3 issue 19555](https://github.com/RPCS3/rpcs3/issues/19555) reports Verdict Day booster-light corruption. Historical [11984](https://github.com/RPCS3/rpcs3/issues/11984) is closed; do not turn its old crash workaround into today's universal pin. Linux-only [18061](https://github.com/RPCS3/rpcs3/issues/18061) does not establish Windows behavior. Confidence: medium, issue-specific.

All later-generation profiles remain native-resolution by default. Required regulations/title updates, GPU-specific fixes and multiplayer readiness are not claimed. Original PSN/Xbox service availability and RPCN/community support differ; network setup is outside baseline automation. Consult the current [RPCN compatibility documentation](https://wiki.rpcs3.net/index.php?title=Help:Netplay) before making any online claim.

## Hash representation follow-up
On 2026-10-09, direct access to redump.org through the research tool returned an internal fetch error. Search indexes surfaced alternate-domain listings and PS3 encrypted-disc metadata; these were not promoted into a trusted whole-file hash feed. An encrypted PS3 ISO hash cannot verify an extracted decrypted folder, and an ISO hash cannot verify its CHD representation. Reference records now support an explicit format and byte size, and fixtures cover these distinctions. This is a dataset/representation gap, not a requirement for copyright permission to record individual factual hashes. Confidence: high for the implemented comparison boundary; retail verification coverage remains incomplete.
