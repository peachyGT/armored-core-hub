# Achievement support and native Hub setup

Checked 2026-10-11. This covers every catalog title/platform combination, including separate PlayStation 3 and Xbox 360 releases. “Enabled” means the emulator integration is configured; it cannot create a missing achievement set or certify an unsupported disc/hash.

## Published sets and platform coverage

The [official Armored Core series index](https://retroachievements.org/hub/4841) lists five published RetroAchievements sets. All fifteen PS1/PS2/PSP title/platform records have verified provider IDs, so an explicit Hub refresh can discover newly published definitions without requiring a binary update. Counts below are the inspected provider listing, not a promise about every region/revision.

| Game | Platform | Provider / ID | Published data at check |
| --- | --- | --- | --- |
| Armored Core | PS1 | [RetroAchievements 11348](https://retroachievements.org/game/11348) | 85 achievements |
| Project Phantasma | PS1 | [RetroAchievements 8081](https://retroachievements.org/game/8081) | 40 |
| Master of Arena | PS1 | [RetroAchievements 8148](https://retroachievements.org/game/8148) | 47; two-disc set |
| Armored Core 2 | PS2 | [RetroAchievements 3316](https://retroachievements.org/game/3316) | 201 |
| Another Age | PS2 | [RetroAchievements 19247](https://retroachievements.org/game/19247) | No published set |
| Armored Core 3 | PS2 | [RetroAchievements 3308](https://retroachievements.org/game/3308) | 104 |
| Silent Line | PS2 | [RetroAchievements 21875](https://retroachievements.org/game/21875) | No published set |
| Nexus | PS2 | [RetroAchievements 19245](https://retroachievements.org/game/19245) | No published set |
| Nine Breaker | PS2 | [RetroAchievements 19246](https://retroachievements.org/game/19246) | No published set |
| Formula Front | PS2 | [RetroAchievements 27005](https://retroachievements.org/game/27005) | No published set |
| Last Raven | PS2 | [RetroAchievements 19244](https://retroachievements.org/game/19244) | No published set |
| Armored Core 3 Portable | PSP | [RetroAchievements 2217](https://retroachievements.org/game/2217) | No published set |
| Silent Line Portable | PSP | [RetroAchievements 2213](https://retroachievements.org/game/2213) | No published set |
| Formula Front Extreme Battle | PSP | [RetroAchievements 2196](https://retroachievements.org/game/2196) | No published set; original/International editions do not get invented separate sets |
| Last Raven Portable | PSP | [RetroAchievements 2201](https://retroachievements.org/game/2201) | No published set |
| Armored Core 4 | PS3 | RPCS3 native trophies | Release has no trophy set |
| For Answer | PS3 | RPCS3 native trophies | Release has no trophy set |
| Armored Core V | PS3 | RPCS3 native trophies | Use actual game-supplied trophy list |
| Verdict Day | PS3 | RPCS3 native trophies | Use actual game-supplied trophy list |
| Armored Core 4 | Xbox 360 | Xenia local achievements | Actual title GPD definitions/unlocks |
| For Answer | Xbox 360 | Xenia local achievements | Actual title GPD definitions/unlocks |
| Armored Core V | Xbox 360 | Xenia local achievements | Actual title GPD definitions/unlocks |
| Verdict Day | Xbox 360 | Xenia local achievements | Actual title GPD definitions/unlocks |

RPCS3 lists come from [TROPUSR](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Loader/TROPUSR.cpp), TROPCONF and game TROPDIR metadata. When the selected unpacked disc identifies set IDs, other games' trophy directories in an imported profile are excluded. Without disc metadata, only the isolated save-family profile is inspected; no independent cross-title verification is claimed. AC4/For Answer's absent trophy metadata remains absent. Xbox [UserTracker](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/user_tracker.cc) supplies GPD achievement records. Hub never converts Gamerscore or RA points into PSN trophy grades/levels.

## Region, revision and patch eligibility

Automatic provider-ID selection is a title/platform association, not an achievement-hash match. The emulator computes RetroAchievements' system-specific hash at launch. It is not the Hub's whole-file SHA-256 or an arbitrary ISO MD5. See [official hashing rules](https://docs.retroachievements.org/developer-docs/game-identification.html) and the provider's per-game supported-hash page. PS2 hashing reads the game's executable; PSP hashing reads PARAM.SFO and EBOOT.BIN. A matching product serial alone cannot establish eligibility.

Primary supported-hash listings directly inspected:

| Set | Supported image | RetroAchievements hash |
| --- | --- | --- |
| [AC2](https://retroachievements.org/game/3316/hashes) | USA stock | `642ffd8e7d5dfbd2ed4dcd4143450b5e` |
| [AC3](https://retroachievements.org/game/3308/hashes) | USA stock | `f92177b18697e5176f89d3bc7582b545` |
| AC3 | USA VanLaser Analog v1.64 | `461cbb0d1361cf24de68509a26fd929e` |
| AC3 | USA VanLaser Inverted Analog v1.64 | `1520cc660ffa2238f38d2525ce9eaef2` |
| [Master of Arena](https://retroachievements.org/game/8148/hashes) | USA Disc 1 | `3a2bb3fd58bd51e415a4b42325c1aaf6` |
| Master of Arena | USA Disc 2 | `33928b871c7ab7240a996d954c9bc4f5` |

AC1/Phantasma supported-hash pages could not be retrieved in this source pass; their published sets are confirmed, while specific supported regions/patch images remain unverified here. No unsupported-region badge is asserted. JP/EU/Asia/Korea editions, reprints, translations, other analog patch versions, and runtime PNACH memory changes must follow the provider's actual identification and eligibility rules. Runtime memory changes can preserve an executable hash while still affecting achievement conditions. Hub defaults to standard tracking, not Hardcore; accepted AC3 analog hashes do not establish universal patch compatibility.

## Native settings and token formats

The Hub's account form is under **Achievements → Achievement account and defaults**; each game also has an Achievements settings tab. One native sign-in configures all three RetroAchievements backends. Global enable/Hardcore defaults and per-edition overrides are stored separately from native credentials. Tracking defaults on; Hardcore defaults off. Explicit later opt-outs survive launches. Offline mode disables native online tracking for that launch and keeps cached progress available. Missing account credentials are explained in Hub; no original emulator settings window is required.

| Backend | Native enable / Hardcore keys | Native credential location and encoding |
| --- | --- | --- |
| DuckStation | `[Cheevos] Enabled`, `ChallengeMode`; `UseRAIntegration=false` uses bundled native client | `Username`, `LoginTimestamp`, and portable encrypted Base64 `Token` in `settings.ini` |
| PCSX2 2.8.2 | `[Achievements] Enabled`, `ChallengeMode` | `Username`/`LoginTimestamp` in `inis/PCSX2.ini`; raw native login `Token` in `inis/secrets.ini` |
| PPSSPP 1.20.4 | `[Achievements] AchievementsEnable`, `AchievementsChallengeMode`; `AchievementsEnableRAIntegration=false` | `AchievementsUserName` in generated INI; raw native token in memory stick `PSP/SYSTEM/ppsspp_retroachievements.dat` |
| RPCS3 | Native trophy handling; no RA enable flag invented | Local `00000001` user/trophy progress; no online sign-in required |
| Xenia Canary | Native local profile achievement handling; no fictitious achievement-enable cvar | Existing player XUID or fresh local Hub player, native Account/content header; saved Hub profile selection survives build changes |

DuckStation portable encryption follows its [official implementation](https://github.com/stenzek/duckstation/blob/master/src/core/achievements.cpp): SHA-256 of UTF-8 username plus 100 further SHA-256 rounds, first 16 digest bytes as AES-128 key, last 16 as CBC IV, zero-padding to a block boundary, Base64 result. Nonportable machine-salted tokens are not assumed portable. [Native settings](https://github.com/stenzek/duckstation/blob/master/src/core/settings.cpp) confirm the enable/mode names.

PCSX2's [2.8.2 QtHost](https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2-qt/QtHost.cpp) loads `secrets.ini` on top of PCSX2.ini; [Achievements](https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/Achievements.cpp) and [Pcsx2Config](https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/Pcsx2Config.cpp) establish account fields and `ChallengeMode`. PPSSPP [Config](https://github.com/hrydgard/ppsspp/blob/v1.20.4/Core/Config.cpp), [RetroAchievements](https://github.com/hrydgard/ppsspp/blob/v1.20.4/Core/RetroAchievements.cpp), and [NativeApp](https://github.com/hrydgard/ppsspp/blob/v1.20.4/UI/NativeApp.cpp) establish its native username/secret-file storage; `AchievementsToken` is not the normal saved INI token field.

Xenia [ProfileManager](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/profile_manager.cc) loads configured identities; current Canary does not automatically create its own default player. Hub prepares a native-format offline player only for fresh managed save storage. Existing/multiple/damaged/orphaned profiles are preserved; the game's Hub Achievements tab selects a valid existing player, while damaged/orphaned imports report repair requirements instead of silently replacing identities. This is local identity setup, not an Xbox Live account. Native Account creation/CON parsing has separate source-verified tests.

## Security, progress and resource handling

Native sign-in uses rcheevos' official [user API](https://github.com/RetroAchievements/rcheevos/blob/master/src/rapi/rc_api_user.c): `login2` with username/password sent as a POST form to the fixed HTTPS `dorequest.php`. Metadata uses the official [runtime API](https://github.com/RetroAchievements/rcheevos/blob/master/src/rapi/rc_api_runtime.c) `patch` request; two `unlocks` requests read standard/Hardcore progress. Hub does not call `startsession` or grant achievements itself. A Web API key is a different credential and is unnecessary; the old credential-bearing GET sync route was removed. The old JSON parser remains compatible with cached/test records.

Passwords are never saved and the native password field is cleared before awaiting network work. The login token is protected with Windows DPAPI in `config/private-achievements/native-ra.bin`, outside state/launch plans/diagnostic JSON. Temporary native formats are written only for an owned game launch, then scrubbed even on prepare/start/cancellation failure. Repair/Open-config-folder preparation does not inject credentials. PPSSPP credentials are excluded from save backups. Sign-out writes a tombstone first, deletes unified and old backend DPAPI files, and scrubs known session/PPSSPP token files so interrupted deletion cannot resurrect a login. A different Windows account must sign in again. Managed/decrypted byte buffers are cleared where feasible; immutable .NET strings and emulator-required native token files are not claimed to resist a process already running as the same Windows user.

HTTP credentials never appear in URLs, redirect requests are disabled, responses are bounded, and transport/server errors are replaced by generic messages without credential-bearing inner exceptions. No background polling/service is added. Explicit refresh and bounded post-session sync update the offline cache. Badge downloads stay on the fixed official media host with existing size/concurrency limits.

Each snapshot is separated by edition/provider/account/set. Earned unlocks and Hardcore flags remain monotonic for that same identity after save restore, retaining original known dates; no cross-account union occurs. Definitions still come from the current provider list and unlock timestamps are not fabricated when the native API supplies IDs only. Null/oversized/duplicate records are skipped without deleting files; each cached record is limited to 4 MiB, an edition read to 16 MiB/10,000 entries/128 files. Local native image validation and secret trophy redaction remain as documented in [achievement visuals](achievement-visuals.md).

## Verification scope

This pass verifies the catalog support matrix, native configuration keys, token formats, protected credential storage, fixed-host POST auth/progress with mock responses, failure/cancellation behavior, restore-unlock/account isolation, cache budgets, and imported multi-game PS3 trophy-set isolation. It includes an independent DuckStation AES fixture and synthetic runtime-start failure cleanup. Native account UI checks cover offline controls, field semantics, persisted opt-outs, and 1280/960 layouts; Xenia has source-verified binary/native-parser fixtures. No actual RetroAchievements credentials were supplied, no real account login was performed, and no game was launched by this achievement verification pass. Gameplay unlock eligibility for each region/patch is not claimed beyond the inspected provider support evidence.
