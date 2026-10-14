# Local compatibility verification

This campaign uses Universal Modder 0.2.0 for recon, source research, game-window automation and searching existing field notes. It tests the Hub and user-provided console media; it does not modify original downloads, publish notes or distribute game content.

The detailed plan, inventory, result definitions and phase order are in [the campaign plan](docs/testing/2026-10-10/PLAN.md). The isolated test Hub, firmware copies, emulator sessions, saves and private evidence are on `D:\ArmoredCoreHub-QA`. This avoids the nearly full system disk and separates test saves from the user's collection.

First establish the actual Hub default behavior. Then compare supported enhancement profiles and exact-edition patches in separately identified sessions. Never report unimplemented analog patch installation or achievements as working. Controls require observed actions in gameplay; controller enumeration alone cannot verify physical buttons.

One game receives input at a time. Archive work and research may overlap it. Use `scripts/qa/drive-session.py`, which verifies the active QA process and delegates window capture/input to Universal Modder. Never target an unrelated game window or send input after focus changes. Record each scene and outcome in MODLOG and the final matrix.
