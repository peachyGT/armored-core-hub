# Local game verification log

## 2026-10-10 — preparation
- Baseline: Hub 0.3.4, commit d2abf1f.
- Installed Universal Modder 0.2.0 from the user-requested marketplace. Inspected game-recon, game-research-websearch, game-automation and share-field-notes. The local knowledge search for `armored core` returned no notes. No notes or private files were uploaded.
- Prepared a dedicated D-drive QA Hub with the real import/provision/launch services. The opt-in harness is excluded from the ordinary synthetic test run.
- User identified BIOS archives in Downloads and agreed to leave the wireless 8BitDo connected. Firmware validation/import is running against copies in the lab.
- Six complete game archives and one existing PS3 ISO are eligible initially. The Xbox For Answer regulation addon is separate from the PS3 base game; partial ACV download is excluded until complete.
- Computer Use could not initialize its kernel assets. Universal Modder's inspected WinDrive and Windows Graphics Capture are the available fallback. All captures are limited to owned game windows.
- Evidence and source fingerprints remain local under the QA lab; source download paths are not included in committed results.

## AC1 USA rerelease — initial Original session
- DuckStation 0.1-12074-g697599c47, archive SHA-256 beginning 4813f228; source identity SLUS-01323 from disc metadata. BIOS SCPH-5501/5503 verified against known firmware hash.
- Found and fixed a real adapter issue: current DuckStation reads `Pad1`; the Hub supplied only legacy `Controller1`. Both schemas now receive equivalent bindings, including overrides.
- Native log showed Keychron K10 HE as SDL-0 and the wireless XInput controller as SDL-1. Added explicit native XInput alternatives to both PS1/PS2 configurations; physical buttons await hands-on verification. XInput slot 0 enumerated successfully on the interactive desktop.
- Observed title → Scenario Mode → New Game → name confirmation → opening trial. Menu confirm/back and directional selection worked. In combat, forward/turn/boost changed the camera/player position; firing reduced rifle ammo from 200 to 197. Start produced the pause screen. Captures: `combat-before.png`, `combat-actions.png` in the private edition evidence folder.
- Original-resolution HUD and geometry visible; dithering/polygon motion alone is not treated as a rendering bug. No playthrough, later mission, audio or save/reload pass claimed yet.
- Ordinary VK and scan-code input both worked once correct settings were in use and the proper window retained focus. The driver refuses input on focus loss. Added SendInput return-value checking to a lab-only Universal Modder copy and restricted process enumeration to the intended emulator.

## Save cancellation regression
- Reproduced missing after-session restore point when a session was cancelled. Fixed shutdown ordering: close/await owned emulator, drain logs, create final backup with an uncancelled token. The synthetic runtime writes progress before cancellation; the regression verifies both restore points and their contents. Core suite passed 83/83 after this fix.

## Formula Front Extreme Battle USA — Original
- PPSSPP 1.20.4, ULUS10034. New game, sound options, team naming, central menu, League Match, Manual mode and AC selection all responded to default keyboard input.
- In the first league battle, analog movement/boost changed position and reported speed to 120 km/h; Square/fire reduced rifle ammunition from 140 to 132. Start opens the game's Quit confirmation rather than a generic pause menu. Both confirm and cancel worked. Geometry, HUD, opponent and weapon effects were visible in sampled frames.
- Captures: `psp-combat-before.png` and `psp-combat-actions.png`. No audio, match-completion, save/reload or higher-resolution pass claimed yet.
- A later UI click was stopped by the foreground guard when focus changed to another application; no click was sent outside the owned game.

## ACV import and validation checkpoint
- Completed Xbox 360 archive now imports through the Hub. New structural reader found title ID 4E4D084F. Its shared regional ID requires an explicit edition choice; the lab uses the supplied North American copy and retains UnknownRevision confidence.
- ISO fingerprint: e508eec175b31deb7c5cd9d30ca398c582d05f0aed211ae4962a522df75d1076.
- 86/86 core checks and 28/28 native checks passed, including malformed Xbox metadata and narrowed regional import. Existing import rendering was inspected; no visual redesign was made.

## AC2 USA — PCSX2 path repair and physical-controller result
- Actual first launch opened PCSX2 Setup Wizard. The adapter used `-datapath <session>` but PCSX2 2.8.2 appends `/PCSX2`, bypassing the generated `/inis/PCSX2.ini`. Replaced that with explicit portable mode and its marker; retest booted directly into AC2 and used the intended BIOS, memory card and bindings.
- Native game CRC 0D168765 matches SLUS-20014. Upstream GameDB applied EE/FPU clamp, minimum blending, draw buffering and half-pixel fixes. The game's opening trial rendered its mech, arena, HUD and dialogue normally in sampled Original frames.
- Keyboard Start and Cross navigated title/new game into the opening trial. The user then physically tested the wireless 8BitDo: Start, D-pad, face buttons and shoulders. User result: “Everything works perfectly it appears.” This is a real physical-controller pass for this edition/build/original-control scene, not all backends.
- Foreground guards stopped input when other apps were active. A lab-only targeted-window-message experiment avoided focus changes but is not accepted as proof of normal keyboard input. Foreground testing resumed after the user explicitly freed the keyboard.
- Preparing a separate local VanLaser True Analogs test, with matching CRC and immutable patch source. Patch files remain outside the release/repository; original unpatched media is unchanged.

## AC2 Enhanced — local analog patch and shutdown finding
- VanLaser PS2 v1.64, SLUS-20014 CRC 0D168765. PCSX2 confirmed “Found 1 cheats” and “Enabled patch: True Analogs” when the matching PNACH was placed in its session-local cheats folder. An initial custom forward-slash path did not load; this failed attempt is not counted as a pass.
- Enhanced profile uses 2× internal resolution. The user physically verified left-stick movement/strafing and right-stick horizontal/vertical aiming: “Both sticks work correctly.” Capture: `ac2-analog-physical-confirmed.png`. This establishes sampled local integration; the Hub still lacks an installable binary analog module.
- A QA-only F1 state was saved for matched rendering experiments. It contains the active patch and must not be used to claim an unpatched test. No normal in-game save/reload pass yet.
- Native log exposed PCSX2 SDL3 face-button names (`FaceSouth/East/West/North`), different from DuckStation/XInput A/B/X/Y. The adapter now translates SDL bindings only. XInput already passed the physical test.
- Shutdown reported a transient sharing violation reading the memory card after the emulator exited. The existing before-launch backup was retained. Added bounded sharing-lock retries with strict source/copy hash checks and a cancellation regression; gameplay retest remains pending.

## AC4 EU PS3 — Original tutorial
- RPCS3 0.0.43-20250, BLES00039 ISO, native 1280×720 scale and Write Color Buffers. No RPCS3 welcome prompt. The game's own offline regulation/update notice is expected with PSN disconnected and is not hidden.
- Keyboard Start/Cross/directions navigated new game and options. W moved the mech, L turned the camera, Return paused. Tutorial geometry, mech and HUD visible; title FPS approximately 60 in the sampled tutorial, not a formal timing study.
- The user physically verified wireless 8BitDo movement, camera, pause and switching off/on: “All work, including reconnecting.” Native RPCS3 co-pilot configuration retains keyboard input as player one.
- Found a separate fullscreen preference mismatch: the profile's fullscreen setting remained true when the Hub selection was windowed. The resolver now writes that setting from the actual selection; regression covers both states.
- Completed attack, weapon-type, switching and shoulder tutorials (05–08). Rifle ammunition decreased; laser cannon, blade, missiles and their hit effects rendered; L3 targeting and L1/R1 switching worked. Radar drill 09 was entered and left to exercise campaign saving rather than counted complete.
- A 9.56-second process-only recording captured nonzero audio, mean -30.4 dBFS / peak -12.9 dBFS. This is signal presence/no clipping in that sample, not a listening-quality assessment.
- Story video and starter-mech preview rendered. A normal Chapter 1 save produced ACDES/GPROG/OSET/PDATA and metadata/art files in the managed RPCS3 home. Hub shutdown created the after-session snapshot and the next launch created a before-launch snapshot. Fresh-process load passed: same Unknown pilot, GAN01-SUNSHINE-L, 860500 C, Chapter 1 First Strike. Evidence: `ac4-load-confirm.png` and `ac4-restored-campaign.png`.
- Follow-up First Strike briefing stalled after load: frozen 3.64 FPS, no capture frames on two attempts, continuing CPU use. Stopped the owned session after several minutes; save backups retained. Effective log already has Auto frame limit, LLVM PPU/SPU and Write Color Buffers true. Investigate separately from the passing tutorial/control/save results; do not infer a whole-game pass.

## For Answer USA PS3 — Original tutorial and first save
- BLUS30187 / RPCS3 0.0.43-20250. Booted directly; the game's own HDD installation completed in managed storage. Default Simple controls retained.
- Keyboard movement, camera and pause observed. Selected built-in tutorial 10 Battle: advanced toward enemies; rifle ammunition 36→23, shoulder ammunition 48→44, incoming damage AP 35888→35032. Mech, city, HUD and enemies visible. These are sampled actions, not all tutorials or a completed mission.
- Owned-window/process-only 14.20-second recording: mean -27.2 dBFS, peak -12.0 dBFS; signal present without clipping in this sample. Listening assessment remains open.
- Story movie progressed into Chapter 1; GA starter Strayed, pilot Unknown, 120000 C. Normal Save Game created ACDES/GPROG/OSET/PDATA and metadata in the isolated managed RPCS3 save directory. Fresh-process reload follows.
- Fresh-process reload passed: same pilot, Strayed, 120000 C and Attack on Line Ark. Before/after snapshots each contain eight files. Evidence `fa-restored-campaign.png`. The game defaults to Load after a save exists; one Down entered the offline Multiplayer menu instead, which was backed out without connecting. Future scripts must inspect selection rather than assume the new-save menu index.
- First mission briefing completed naturally. Initial Line Ark geometry/HUD/mech rendered (`fa-mission-start.png`), but the subsequent combat sequence stalled. Frozen 27.74 FPS; capture failed twice including after resizing. Native log records RSX FIFO desync at 6:29.98. Retained log: `qa-sessions/acfa-ps3-blus30187-20261011-014812-716`. Controlled stop retained saves; mission is failed/pending workaround, not passed.

## Follow-up regression comparisons
- For Answer 20250 with `Core/RSX FIFO Fetch Accuracy: Ordered & Atomic`: effective log confirms the setting, but mission entry still stopped producing frames. The alternate setting is not promoted. No FIFO error was logged in this second failure, so exact causation remains uncertain.
- Official pre-regression RPCS3 20152 archive verified against its upstream SHA-256 and installed alongside 20250 through RuntimeService. QA can select an alternate installed build for one run without changing the saved Hub selection; first-run compilation completed and the existing save was offered normally.
- ACV original Canary 828014b90: offline local profile, emblem/pilot setup and rendered intro work. Mission loading stopped producing frames twice, including after resize/Start; process remained present. No game-save completion was reached. Upstream #280 documents emulated `protect_zero=false` and GPU readback requirements; isolated comparison prepared.
- Fixed Xenia profile selection being lost when regenerating the TOML. Retain a validated existing local identity or choose the sole local profile; never select arbitrarily among several. Actual ACV restart auto-signed in. Numeric-only identities now remain quoted fixed-width strings, covered by launch-plan regression. Core checks: 88/88.
- For Answer RPCS3 20152 comparison passed sampled first-mission movement, camera, rifle (30→2, reload, then 21), left-arm fire (40→39), pause, mission abandon and retry. Geometry/HUD/effects remained visible for several minutes; no FIFO error in retained log. Two process-only captures were silent; Cubeb initialized at volume 100. Audio remains unresolved, so the older build is a candidate rather than an unconditional recommendation.
- Audio clarification: the user reported that Windows Volume Mixer had been muted. Retain the silent captures as inconclusive; they do not prove an RPCS3 defect. Recheck after unmuting, without changing unrelated audio sessions.
- ACV with only `Memory/protect_zero=false` passed mission loading and reached the Basic Movement tutorial after its opening cinematic. This isolates a useful title-specific compatibility setting. The user returned for physical-controller testing and reported low perceived FPS; automatic input stopped for their exercise.
- ACV physical movement/camera/pause passed. The user reported black mech textures, flickering/rotating terrain and slow gameplay; reconnect was not explicitly confirmed.
- First 30-second PresentMon sample (user-approved UAC, PID-filtered, no input tracking): 898 presents, 29.999 mean FPS, median 33.331 ms, p95 33.919 ms, p99 34.340 ms, one frame over 50 ms. This is the black-mech RTV scene, not all missions or internal simulation timing.
- Found Xenia's implicit config migration undoing an explicit `readback_resolve=fast` override. The Hub now passes its generated file using `--config`, which upstream supports specifically for explicit overrides without a defaults version. Build12: clean compile, 88/88 core tests; actual restarted config retains `fast`. Rendering comparison continues.
- Fast readback: user confirmed terrain flicker gone and audio working, but right hand still black. Full readback: user confirmed hand textured and same movement feel. Corresponding 30-second samples measured 29.992 and 29.998 FPS, respectively, with no frames over 50 ms. The exact USA profile adopts full readback and records only sampled validation on Canary 828014b. Build13: clean compile and 88/88 tests. Higher-rate timing experiments remain isolated and opt-in.
- Full readback with vsync disabled and vertical-blank limiter 120 measured 58.337 FPS in the next 30-second sample (p95 21.050 ms, p99 24.139 ms). The user reports markedly smoother movement and no current major issue, explicitly not validating cutscenes/AI/boss timing. Added a clearly experimental, optional 60-FPS-target enhancement; Original stays default. Build14 passed 88/88 checks, including enabling/removing that timing override while retaining texture fixes. The user continues this private QA run to the first normal save.

- Follow-up: the user confirmed manually closing ACV. Exit 0 at 03:36:42 UTC is a normal closure, not a crash. No campaign save was reached. AC4 comparison extraction succeeded using ISO9660 (9,054 files); the extractor's default UDF attempt reported a header error. The separately registered folder identifies BLES00039 from game data. Source ISO retained unchanged; QA import now supports folders and preserves the selected installed build when multiple builds coexist.

- AC4 EU on official RPCS3 20152 passed First Strike entry and sustained sampled keyboard movement/camera/weapons/pause, then abandoned back to Chapter 1. No FIFO/fatal error in the completed log. Audio signal returned after the user's mixer clarification. Controlled shutdown/final backup passed. Retained original ISO, separately registered derived disc folder, and disabled the temporary keyboard override. Evidence: 20261011-034702-081.

- Project Phantasma Original and Enhanced 2×: native test-arena keyboard controls, rendered combat, pause/quit, normal memory-card save and fresh-process reload passed. Audio signal captured; physical pad/analog/story/High-End not yet checked. Initial automated Start miss resolved with a longer separate key-down/up; no code fix inferred. All 88 core checks passed on build15.

- AC3 Original completed the first trial and recorded one successful sortie. Native memory-card formatting/save followed by a fresh-process reload passed; before/after card hashes agree and controlled shutdown no longer reports a sharing violation. Enhanced 2× with exact local True Analogs responds to keyboard-driven movement/camera axes in the test arena; physical-stick response remains untested. Restored ordinary QA settings after stopping; patch remains outside the Hub release.

- PSP Formula Front: native save/load passed for team 1; 2× and 3× manual first-arena movement, boost, rifle, pause and match exit sampled successfully. Audio signal present. Refocusing the owned client restored initially missed automated title inputs; mappings were already correct. Physical gamepad and complete matches remain open.
- Found QA process-name enumeration selecting an exited PPSSPP PID. The input guard refused it. Verified actual child ownership before continuing, then replaced enumeration with LaunchService.ActiveProcessId. The synthetic child independently reports its PID; launch/error/cancellation checks now verify ownership and cleanup. All 88 core checks passed.
- Advanced profile choices now distinguish sampled gameplay from tested status. Self-contained build16 passed 28 native checks in both fresh and relocated copies, with configuration, appearance and save marker preserved. Build16 predates the subsequent process-ID accessor; no updated release ZIP yet.

- AC1 reprint: original trial controls/combat/audio sampled through normal defeat. A separate cheat-assisted run reached the garage; the armour effect was verified, the enemy-health effect was not. Created a new native card entry, disabled cheats, and loaded successfully in a fresh Enhanced 2× process. Garage restored; three backups have identical card hashes. All sessions closed cleanly. No PS1 analog test yet.
- Shutdown preparation: user estimates a 100-minute timer started at 01:17:33 EDT. Estimated power-off 02:57:33; preserve work by 02:47. Recovery checkpoint is in docs/testing/2026-10-10/RESUME.md; do not change the Windows timer.
- AC1 local v1.53 analog PPF: exact reprint target, native-confirmed sidecar loading, independent keyboard movement/camera and combat/pause at 2×/3×. Removed sidecar/restored ordinary controls afterward; BIN hash still matches import. Physical sticks and later scenes remain open.
- AC2 High-End 3×: controls/rendering sampled with the known analog patch and temporary armour. Did not complete trial, so no native-save pass. Audio capture was silent; cause unresolved. Preserved old state separately, wrote an assisted slot-1 checkpoint, stopped cleanly and removed the armour code. Original analog-only QA settings restored.
- Final-code cached-emulator integration rerun passed all five backends plus PCSX2 portable config, PPSSPP startup and RPCS3 official firmware installation. Package verification in progress.
- Final build passed with no warnings/errors and all 88 core checks. One native run exposed the fixed-200-ms hover assertion; changed only the test wait to require exact endpoints and cleared clocks within a bound. Portable reports now invalidate old successes and persist failures; missing-package regression passed. Rebuilt self-contained checkpoint passed 28/28 native checks in clean and relocated copies, preserving config/appearance/save marker. Final gameplay session ended cleanly at 02:30 EDT, before the estimated 02:57 shutdown.
- Local QA checkpoint archive assembled with 454 files verified against published output. No game/firmware/runtime/user data included; archive and digest remain ignored build artifacts in outputs. The broad compatibility campaign is not complete; remaining work is recorded in RESULTS.md and RESUME.md.
## 2026-10-11 daytime resumption
- AC2 audio silence was confirmed by the user as Windows mixer muting. After unmuting, owned-session status and process capture verified sound; user also confirmed listening quality, then intentionally muted it again. Added a read-only, owned-PID mixer probe to QA tooling; no product dependency or volume mutation.
- AC2 native save/reload completed after explicitly assisted trial progression. Restored author PNACH/analog-only settings before fresh-process native loading; no emulator state used for the reload. Sampled clean 3× test arena, normal damage, controls/rendering and controlled stop. Card SHA-256: 286c3845d3b66cf836d1371794482cd2724e853909e3abb785f025096ba1e71a. Detailed session IDs, evidence and limits are in RESULTS.md.


## 2026-10-11 Phantasma follow-up
- Native fresh loads and exact private PPF arena checks at 2×/3×. Required analog-mode toggle discovered: digital mode causes uncommanded camera rotation even with patch loaded. Toggling corrected it. Documented upstream reason and future setup requirement.
- User confirmed contributing gamepad play to the preceding mission; recordings are not exclusively automated evidence.
- Sidecar removed from active media, QA controls restored, original BIN/card hashes unchanged; sessions stopped cleanly. Full findings in RESULTS.md.
- Optional read-only owned-session audio probe syntax and rejection of inactive/out-of-lab sessions checked; no product dependency or mixer changes.

## 2026-10-11 AC3 3× sample
- Fresh native save load, exact local True Analogs shooting gallery, independent movement/look, ammunition/boost/pause and native abort report observed. Ordinary QA settings restored after stopping. No physical-controller or whole-game claim. See RESULTS.md.

## 2026-10-11 ACV save-test waiver
- Original-timing opening tutorials/road combat sampled with keyboard-only QA override. User explicitly waived native campaign save/reload; remains skipped/unverified.
- Session stopped without error, final backup retained, temporary input override removed and prior experimental lab settings restored. No active owned game process. See RESULTS.md for scope.

## 2026-10-11 compatibility repair, 0.3.5
- Added versioned per-profile build recommendations and known-regression ranges for the two sampled PS3 editions. Explicit repair provisions/reuses 20152 and prepares decrypted ISO9660/Joliet content, preserving original media, saves and prior builds.
- Real-disc comparison exposed lossy primary ISO aliases; preferred Joliet reproduces the previously tested 9,054-file AC4 tree exactly and deduplicates it. For Answer repair also passed. No new dependency or global backend downgrade.
- Added bounded extraction, cancellation/corruption/path/format/transaction tests; 95/95 core checks. Fixed diagnostics inheriting scroll position and put its repair action first; 29 native checks pass in fresh and relocated self-contained copies. Gameplay certification remains limited to observed scenes.
