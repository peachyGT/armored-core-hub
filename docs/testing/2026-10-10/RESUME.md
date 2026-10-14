# QA recovery checkpoint — 2026-10-11, 13:33 EDT

Daytime testing resumed after the shutdown. Last night's timer is historical; do not assume another shutdown is pending or alter a timer. User authorizes uninterrupted owned-window keyboard testing while watching video. Audio is intentionally muted again after the user confirmed AC2 sounds good. User contributed to the earlier Phantasma mission, then left the menus alone.

## Latest state
- No active owned gameplay session. ACV session 20261011-164639-952 stopped by request at 17:06:04 UTC without error; final backup 20261011-170604-097-2d3ceb. The user explicitly waived the difficult campaign-save test: SKIPPED / UNVERIFIED, not passed.
- ACV keyboard-only override removed to lab/tools/acv-keyboard-only-20261011.json. Prior experimental QA settings restored from lab/tools/acv-experimental-qa-settings-20261011.json (full readback, vsync=false, frame limit=120). Disable that lab-only override before a future Original comparison. Normal controller defaults restored, not physically retested afterward.
- AC3 High-End comparison complete (session 20261011-163129-028): native load, shooting gallery controls/rendering and abort report observed. Ordinary QA settings restored; temporary analog module disabled.
- AC2 native card save and fresh-process 3× load/test now pass. Progression was separately cheat-assisted; those progression codes were removed. Exact True Analogs remains enabled as before. Card SHA-256 `286c3845d3b66cf836d1371794482cd2724e853909e3abb785f025096ba1e71a`. Native save is independent of the older assisted emulator state.
- AC2 audio passed after user unmuted its mixer; same-process capture and owned-session mixer report retained. Optional QA probe is scripts/qa/audio-status.py; pycaw/psutil live in lab/tools/audio-libs, not the product.
- Phantasma exact PS1 v1.53 sidecar passed sampled independent movement/camera/fire/pause at 3× and 2× after analog mode was activated. Digital mode caused camera rotation; explicit H toggle resolved it. This startup dependency must be handled before shipping an analog module. Author replay/multiplayer limits remain. Last abort capture was a black transition, so patched arena return is not certified.
- Phantasma sidecar is INACTIVE: moved to lab/patch-research/pp-SLUS00670-tested.ppf. Ordinary QA controls restored from lab/tools/pp-original-qa-extra.json. Source BIN and shared card hashes unchanged. No private binary patch added to Git.
- 0.3.5 RPCS3 repair is implemented and validated. AC4 EU and For Answer US lab selections now use 20152; ISO and prior builds retained. New harness: work/qa-build18/Hub.Tests.dll. Final app: outputs/ArmoredCoreHub-0.3.5-qa20261011. 95 core checks; 29 native checks in both fresh/relocated copies. No active owned game. ACV campaign save/reload is explicitly waived by the user. See RESULTS.md and IMPROVEMENTS.md for coverage limits.

## Current work

- Code checkpoints: `6fd2919` (folder import/build selection and AC4/PP/AC3 findings), `b4d6a24` (owned-process identity, accurate sampled-profile label and PSP findings), followed by the shutdown evidence/validation checkpoint. Latest synthetic harness: `work/qa-build17/Hub.Tests.dll`; current tests/Hub.Tests/bin/Release/net10.0/Hub.Tests.dll also passed 88/88. Final self-contained folder `outputs/ArmoredCoreHub-0.3.4-qa20261011` includes the process-ID accessor and bounded hover check. It passed 28 native checks in both clean and relocated copies by 02:39 EDT. Use this folder instead of the earlier work/qa-app16.
- Private lab: `D:/ArmoredCoreHub-QA`. Original downloads and existing user Hub saves are unchanged. Runtime sessions, captures, logs, imported media and disposable saves are inside this lab. Do not include them in a release or Git commit.
- AC4/For Answer official RPCS3 20152 passed sampled first missions where 20250 stalled. AC4 used a separately extracted ISO9660 folder. The 0.3.5 explicit repair now automates this preparation/selection for these two editions; original media and saves remain. Direct ISO still needs the newer build. Older RPCS3 selects keyboard/controller at launch.
- ACV full readback fixes the sampled rendering defects. Optional higher-rate profile measured 58.3 FPS and the user reported improved movement. Later timing, campaign save and reconnect are not verified. The lab still has an experimental `qa-settings/acv-x360-4e4d084f-us.json`; remove/disable it before claiming an Original comparison.
- Final-code backend integration rerun passed at 02:31 EDT: five official release integrity/startup checks, PCSX2 portable configuration, PPSSPP portable startup and Sony/RPCS3 firmware installation. Report: project/work/integration-results.json. Final native report: work/portable-results.json, validation root work/portable-check-0fb26c74d60f4b6980e9b2cd9a3f5963. A failed earlier run was retained; reporting now invalidates old passes before each run.
- Local checkpoint archive: outputs/ArmoredCoreHub-0.3.4-qa20261011-win-x64.zip. Its adjacent sha256.json records the digest and byte-for-byte file verification. No game media, firmware, emulator installations, user configuration or saves are included. This is a local engineering checkpoint, not an external release.

## AC1 latest evidence

- Edition `ac1-ps1-slus01323`, DuckStation 0.1-12074 (`duckstation-4813f22823e6aa26`). Unmodified opening-trial controls/rendering/audio sampled. The pilot died normally after damage; this was not an emulator crash.
- Separate assisted session `20261011-053755-198` enabled two temporary GameShark groups. Native log confirms both; stable AP 7168 verifies the armour effect. The enemy-health shortcut's effect is **not established**. The trial eventually reached the garage between observations. This is assisted progression, not an unassisted mission-completion pass.
- Native System → Save Data → Slot 1 → New → Yes wrote a changed shared memory card. Fresh-process native load subsequently reported **Load completed** and restored the starter mech in the garage. Card SHA-256 and three successive after/before/after backup hashes agree: `54cca544d489b73077e822ad4ddf23f4063cdce3c5a56ecaf6683c627aa1f73c`.
- Temporary game cheat INI was moved out of the active gamesettings directory to `lab/tools/ac1-cheat-gameini.txt`. The `.cht` remains in the isolated session but is disabled. No cheat is enabled in the fresh Enhanced run.
- Fresh Enhanced session `20261011-055455-471`, PID 40928, stopped cleanly at 06:01:13 UTC; native log confirms 2× scale. No lab processes remained on the 06:02 check. Check `lab/hub/logs/qa-session.json` again before any input; never trust a PID across a reboot.
- AC1 exact PS1 analog sidecar was subsequently tested successfully with keyboard-driven independent axes in 2×/3× test-arena runs. **Sidecar removed and normal QA settings restored at 02:17 EDT.** Source BIN hash still matches import. Tested PPF retained at lab/patch-research/ac1-SLUS01323-tested.ppf. Physical analog response and later scenes remain open; PP analog was subsequently sampled as recorded above. Do not alter DuckStation's hardlinked upstream game database or original game bytes.

## Historical AC2 shutdown checkpoint (superseded by daytime results above)

- Session 20261011-061917-883 stopped cleanly at 06:30:14 UTC. 3×, exact True Analogs and a temporary armour code; controls/rendering sampled, no native campaign save reached. Audio capture silent, cause unresolved.
- Native state slot 1 is now the **assisted** trial checkpoint (AP 9999). Earlier analog-only state copied to lab/tools/ac2-original-analog-trial.p2s. Do not confuse emulator states with native card saves or use this checkpoint to claim unassisted behavior.
- Armour code removed; original analog PNACH and analog-only qa-extra restored from lab/tools/ac2-analog-original.pnach and ac2-analog-original-qa-extra.json. Future Original control tests must disable that remaining analog module or start fresh with ordinary settings.

## Safe resumption

1. Read this file, STATUS.md, RESULTS.md and MODLOG.md; inspect Git status and current lab session metadata.
2. Confirm no owned test process survived; read the last final session and backup before restarting. Use the current harness's actual child PID, never process-name discovery.
3. RPCS3 repair is complete; continue remaining product fixes and expanded game evidence. AC3 High-End is complete; ACV campaign save/reload was explicitly skipped by the user. AC1/PP patch and AC2 save follow-ups are recorded above. Record absent achievements and binary-patch installation as not implemented.
4. Backend integration and native/relocation checks now pass for this checkpoint. Rerun affected checks after future code changes, not merely on resumption. Keep certifications limited to observed scenes.
5. User previously authorized unattended keyboard use. Recheck idle/foreground ownership; no unrelated system settings, files, accounts or online sessions.


## ACV follow-up navigation evidence
Original timing was used for the stopped run; native existing offline profile selected automatically. First road combat reached after movement, boost/jump and attack tutorials. Pilot displayed `dsad`; no campaign save yet. Use scanmode on for each future input-driver call; ordinary virtual-key arrows sometimes missed. Do not resume this waived save attempt unless the user requests it.

## 0.3.5 repair checkpoint
- Real AC4 extraction initially exposed the lossy primary ISO tree. Corrected Joliet extraction exactly matches all 9,054 files/fingerprint of the previously tested folder; no duplicate retained. The rejected generated primary-view copy was removed after identity/path checks. Source ISO, prior folder and saves remain untouched.
- QA repair ran for AC4 and For Answer; per-edition receipts in lab/hub/logs/qa-compatibility-repair-<edition>.json. No new gameplay pass claimed from preparation alone.
- Full build has zero warnings/errors; 95/95 core checks. Final portable validation: work/portable-check-4bb6313be28a4f9d8316bc2ab34ac6f9, 29/29 each phase. Initial framework-dependent self-test did not start without DOTNET_ROOT; bounded owned process stopped, then correct local runtime run passed. Self-contained copies use their bundled runtime.
- Native scroll-position defect fixed and confirmed at 960/1280. No new artwork or dependency. Impeccable's sidecar/update notices and unsupported WPF platform taxonomy are tooling follow-ups; the approved native visual system was preserved.
