# Improvements from compatibility testing

Only reproduced defects become fixes during the test pass. Optional work stays here.

- Show patch entries explicitly as research-only when no installable binary module exists; avoid implying analog controls are already delivered.
- Keep achievement support visibly absent until an actual backend/account integration is designed. Cheats and imported saves need a separate testing mode if achievements are later supported.
- Detect addon/save containers separately from bootable games, including console mismatches (For Answer PS3 versus Xbox 360 regulation data).
- Offer an external managed extraction location when the portable Hub drive is low on space.
- Explain when graphics presets resolve to identical safe settings (currently PS3/Xbox); avoid suggesting an unvalidated upgrade.
- Add explicit generic SDL device selection: analog keyboards can claim index zero. Native XInput works around this for the tested wireless controller.
- Handle Xenia's first offline player-profile creation inside setup; preserve its existing local identity when regenerating settings. The preservation fix is implemented and verified on restart, but initial creation and the upstream quickstart dialog still need handling.
- Warn about a proven emulator regression before promoting a newer build. RPCS3 0.0.43-20250 passes AC4/For Answer tutorials but stalls in their first missions; tutorial-only checks would have missed it.
- Validate ACV higher-rate timing against mission timers, cutscenes, boss patterns and AI before recommending it. The optional target measured 58.3 FPS and felt smoother, but neither 60-FPS correctness throughout the game nor 120+ FPS support is established. Compare more accurate pacing settings after a normal save avoids repeating setup for each trial.
- Advanced now distinguishes sampled gameplay from tested status. Keep this distinction in future compatibility summaries and update recommendations.
- Keep automated title navigation synchronized with actual transitions. Short fixed waits entered AC1's Assembly screen instead of the test arena; inspection caught this before any equipment change. A visible-screen state check should precede each destructive/save action.
- Preserve temporary cheat/patch provenance and cleanup receipts in the QA workflow. Assisted emulator states must be distinct from native campaign saves and ordinary compatibility samples.
- Fixed portable-validation reports retaining an older success after a failed run. Reports now start invalidated and explicitly record failures, package path and validation root. The missing-package failure regression verifies this behavior.

- PS1 analog modules must verify the emulated controller is actually in analog mode, not just that the PPF loaded. Phantasma rotated its camera in digital mode; the explicit toggle resolved it. Preserve upstream compatibility settings and files when implementing this.
- Keep a scoped read-only Windows mixer probe in QA: intentional session muting caused a false audio concern. `scripts/qa/audio-status.py` checks the owned PID without changing volumes or collecting unrelated sessions.

- Implemented in 0.3.5: per-edition regression detection and transactional recommended-build repair for the two sampled PS3 editions, with complete Joliet extraction. Core and actual media tests validate it; later gameplay remains open.
