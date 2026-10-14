# 0.3.9 application review

Date: 2026-10-11. Scope: the Hub application, configuration, storage, UI, native preview playback and release portability. No guest game was launched for this review and no new physical-controller or gameplay certification is claimed.

## Requested changes

- Added-game portrait cover fallbacks crop around their actual pixel center at every supported window size. Cinematic images/videos retain the shared scene behind both panes; tiny icons and unadded games keep the small-cover treatment.
- Keyboard and controller rows are editable in place, with a Default reset beside each binding. Editing snapshots the resolved layout as Custom for that device, preserves every other action and the other device, and leaves named imports and Community templates unchanged. Stale rows cannot write a different edition. Selection and focus remain in the list.
- Achievement tracking defaults on wherever supported; Hardcore defaults off. Hub-native account setup configures DuckStation, PCSX2 and PPSSPP, and native local profiles handle RPCS3/Xenia. Explicit global/per-edition opt-outs persist. There is no requirement to edit an emulator's account settings manually.

Every catalog game/platform combination is covered by the [support matrix](../../research/achievement-support-and-setup.md). Five early releases have published RetroAchievements sets at the check date; the other PS1/PS2/PSP provider pages have no published set. PS3 AC4 and For Answer have no trophies. PS3 V/Verdict Day and the Xbox releases use actual game-supplied native data. Missing sets, art, prerequisites or grades are never invented. The native emulator must still recognize a supported region/revision/hash. Live RetroAchievements sign-in and actual unlocks remain unverified because no real credentials or guest game were used.

## Review findings addressed

The independent Core review covers save restore, import/archive boundaries, patch reimport, runtime isolation, firmware readiness, state recovery, malformed metadata and process ownership. Material fixes include exact save replacement without removing installed PS3 data, a staged hash check against a restore race, normalized-path checks, atomic configuration-module validation, bounded metadata reads and actionable partial-firmware handling. [Core review and evidence](../../research/core-app-review.md).

The [UI/resource review](../../research/native-ui-resource-review.md) covers subscriptions, timers, callbacks after close/reselection, decoded artwork ownership, deferred collection retention, settings drafts and native preview lifetime. Closed windows now detach publishers/descriptors/timers; binding sources are materialized so a deferred iterator cannot retain the window. Decoded artwork has bounded dimensions and a 48-entry/64-MiB cache budget, excluding currently displayed visuals. Advanced graphics edits survive refresh and require save/discard rather than silently disappearing.

The old WPF media player leaked native handles both when unused and during clip changes. The replacement uses Windows Media Foundation with one decoder worker, reusable frame storage and bounded synchronous UI copies. COM/source/byte-stream ownership is explicit; source/seek revisions reject stale frames and loop callbacks. Windows codec failures retain the still fallback. No new shipped package, process, service or external binary is added for playback. [Decoder ownership and proof](../../research/video-preview-lifetime.md).

Achievement review covers fixed-host POST authentication, protected credentials, native token formats, credential cleanup after launch failure, cache/read budgets, account/set isolation and secret redaction. Passwords are not saved; temporary emulator secrets are limited to owned launches and removed afterward. Cached unlocks remain monotonic only within the same edition/provider/account/set. Fresh Xenia storage receives a source-verified local player; existing, ambiguous or damaged identities are preserved. The optional native Xenia parser check runs with isolated storage and no guest title. [Profile evidence](../../research/xenia-local-profile.md).

## Verification

| Check | Result |
| --- | --- |
| Release build/publish | 0 warnings/errors; self-contained Windows x64, 0.3.9 |
| Core regressions | 199/199 pass; real BIOS ZIP/native SDL/MP4 fixtures enabled |
| Failed native decoder opens | Warmed handles 440 → 440 → 440 across two 12-failure batches; source leases released |
| Valid decoder | Scaled top-down frames, reused pixel buffer, seek/end/loop, cancellation, separate-thread rejection and 24 open/dispose cycles pass |
| Fresh portable app | 46/46 checks with package-local .NET runtime |
| Relocated portable app | 46/46; state, appearance and save marker preserved |
| Published resource suite | Four checks pass: graphics drafts, artwork budget, 12 closed windows and 24 real MP4 source changes |
| Closed-window native handles | 2524 → 2524 → 2524; all 12 fixture windows collect |
| Published MP4 source changes | 2786 → 2790 → 2791 → 2791, then 2785 after idle; one player/worker reused |
| Malformed adapter previews | Twelve distinct invalid MP4s shut down and use the cover fallback; source-build handles 2786 → 2664 → 2664 → 2664 |
| Core dependency advisory query | No known vulnerable NuGet packages, including transitive dependencies |

One batched native visual review inspected 24 representative captures across library, Help, emulators, import, graphics, patches, achievements and inline bindings at 1600/1280/960/820 widths. It found the small-window achievement sign-in action below the viewport. The correction moves the form before tracking defaults/secondary explanations and compacts account notices. A first behavioral layout assertion caught the action still below the fold; its repair places Sign in entirely inside the 960×540 viewport. The final confirmation inspected the account at 960/1280 and passed all 46 native checks again in fresh and relocated copies. Native scrolling remains available for long lists. No additional visual redesign/polishing loop was performed.

Build/test evidence: `work/release-0.3.9-final-build.log`, `work/test-results/test-results.json`, `work/release-0.3.9-layout-repair-build.log`, `work/release-0.3.9-final-portable.log`, `work/portable-results.json` and its captures. Published resource receipts are `work/ui-resource-published/logs/`; the extra malformed-adapter fixture used source build 21 in `work/ui-resource-native`. The final app includes that fixture; the later production change affects only account-form ordering. Finite resource metrics and final release receipts are retained in ignored work storage.

The current official NuGet advisory query reported no vulnerable managed packages, including transitive dependencies. Repository auditing stays enabled. This result does not certify native Windows codecs, downloaded emulator code or the absence of all vulnerabilities. Evidence: `work/dependency-audit-0.3.9.json`.

The review uses targeted regressions and finite resource fixtures, not a penetration test or a guarantee that all possible leaks/defects are absent. Real-media inputs are opened read-only; BIOS/game/artwork bytes and account credentials are excluded from the release ZIP. Prior gameplay coverage and known compatibility limits remain in STATUS.md and the earlier test records.
