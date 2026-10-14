# Compatibility test campaign — 2026-10-10

Status: active gameplay and repair; see RESULTS.md for current evidence. Baseline commit d2abf1f / Hub 0.3.4. Test data lives in the dedicated D:\ArmoredCoreHub-QA directory; original media and existing Hub saves remain untouched.

## Objective and evidence standard
Test every complete, available edition through the real Hub pipeline, then exercise menus, input, representative gameplay, graphics modes and save recovery. A boot is not a gameplay pass. Passing sampled scenes is not proof of a complete playthrough or the absence of all bugs. Each result records edition, source fingerprint, emulator build, profile, patches, scene, action, expected result, observed result, log and screenshot evidence.

Result states: pass (observed), fail (reproducible), blocked (exact dependency), not implemented, not exercised. Never promote a whole title to certified on a brief smoke test.

## Inventory at start
- AC1 USA rerelease: 7z containing BIN/CUE.
- Project Phantasma USA: 7z containing BIN/CUE.
- AC2 USA: 7z containing ISO.
- AC3 USA: 7z containing ISO.
- Formula Front Extreme Battle USA: PSP ISO in 7z.
- For Answer USA: PS3 extracted-disc folder in 7z.
- AC4 EU: existing PS3 ISO.
- For Answer Regulations 1.40 addon: Xbox 360 save/container layout (465307DA), not compatible with the PS3 base game above.
- ACV Xbox 360: completed during the campaign; imported structurally as title 4E4D084F, with an explicit regional choice.
- Other catalog entries are absent, not assumed tested.

User identified PS1/PS2 BIOS archives in Downloads; validate/import them into the test lab. PS3 firmware and all five pinned emulator archives are available. The user will leave the wireless 8BitDo Ultimate 2C connected; physical button behavior still needs observed exercise.

## Ordered execution
1. Install requested Universal Modder plugin; inspect only relevant skills/tools. Search its existing field notes. Record upstream versions and provenance.
2. Read-only archive listing and size stability check. Exclude partial files/addons from automatic game registration. Extract through MediaArchiveService into the isolated lab; record detected serial/region and format. Investigate any recognition/import error before overriding it.
3. Provision pinned backends through RuntimeService, import available firmware, resolve original profiles. Produce a machine-readable readiness matrix; blocked BIOS-dependent titles still get import/config checks.
4. For each runnable edition: clean startup/no external welcome, intro, title menu, confirm/back/pause, all applicable movement/camera/face/shoulder/trigger inputs, manual combat or arena control, garage/assembly, mission transition, death/retry where reachable, audio and visual inspection. Keep session windows fixed, capture only owned game windows, and inspect logs.
5. Save/reload to a known state, before/after-session backup, restore/rollback, restart and offline reuse. Use separate lab saves. Cheats/imported saves may unlock representative later scenes only after their source, edition and format are established; never overwrite the player's saves or use them in achievement/account sessions.
6. Repeat matched scenes with Original, Enhanced and High-End settings that actually differ. Compare rendering, HUD/effects, frame pacing and control response; record settings that are intentionally identical. Test patch on/off only when an implemented, exact-target module exists. Binary analog mods currently remain research entries; achievement integration is not implemented. These cannot pass by inference.
7. Fix reproducible Hub/configuration defects within scope, add meaningful regression coverage and repeat the affected scene. Record upstream emulator/game limitations separately. Accumulate optional improvements in IMPROVEMENTS.md.
8. Recheck completed downloads, run unaffected automated/native tests, retain reproducible commands, update STATUS/BLOCKED and report coverage and remaining dependencies. Do not publish external field notes or upload game evidence.

## Parallelism
Archive preparation, static validation and research can overlap. Gameplay, visual comparisons and timings are serialized by default to prevent GPU/CPU contention and foreground-input crossover. No helper installs a virtual-controller driver or changes global Windows graphics/security settings. Limit each launch with an owned-process timeout and stop only that exact process tree.

## Tools and permission boundaries
Universal Modder 0.2.0 is installed in Codex. Apply game-recon, game-research-websearch, game-automation and share-field-notes (search/local documentation only). The generic PC-engine detector is supplementary: console disc metadata and emulator build data are authoritative here. Computer Use initialization failed before any UI action (kernel assets path missing); use inspected Universal Modder window tools as the available fallback. Keep tool caches in the test lab. Network is for authoritative dependencies, documentation, authorized patches and test saves; no game/BIOS acquisition or account operations.

## Completion checklist
- [x] Every complete input has an import/identity result.
- [ ] Every runnable edition has scene/input/save evidence and explicit graphics-mode results.
- [ ] Every blocked or absent feature has a precise reason.
- [ ] Fixes have regression checks and targeted gameplay retests.
- [ ] Final matrix, findings, improvements, logs and local field notes are retained.

