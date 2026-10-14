# Community control research — 2026-10-11

The bundled Community choices adapt the user's confirmed baseline into optional control layouts. They are not changes to a game's internal control configuration, and selecting one must use the same in-game scheme that the binding editor displays. Imports remain separately named user profiles.

## Findings and confidence

- [VanLaser's patch documentation](https://github.com/VanLaser/analog-controls-for-armored-core-ps2) confirms that the earlier PS1/PS2 games can gain actual left-stick movement and right-stick camera processing. Digital weapon assignments remain configurable. The author documents exact supported editions, single-player limitations and broken arena replays. PSP requires a patched decrypted executable plus suitable right-stick bindings; assigning a second stick alone does not create support. This is primary author documentation, not proof that an arbitrary user patch is installed correctly.
- [For Answer players' layout discussion](https://gamefaqs.gamespot.com/boards/944263-armored-core-for-answer/46957723) repeatedly favors Type B because firing and camera movement can occur together. It also describes alternatives that place weapons on triggers and boosting on shoulders. [A second player discussion](https://gamefaqs.gamespot.com/boards/944263-armored-core-for-answer/45899429) gives that four-shoulder arrangement explicitly. These are first-hand preferences from a small sample, not a poll or universal consensus.
- [obliviondoll's Verdict Day piloting guide](https://gamefaqs.gamespot.com/ps3/704812-armored-core-verdict-day/faqs/69136) recommends moving important actions away from face buttons, describes its author's layout, and notes other mobility-on-shoulder variants. The guide covers the identical physical layout on PS3 and Xbox 360 and is grounded in the author's experience with V and Verdict Day. The Hub's modern alternative is an inference from those priorities, rather than copying an entire guide or asserting one accepted competitive layout.
- [PPSSPP control documentation](https://www.ppsspp.org/docs/settings/controls/) explains native keyboard/gamepad remapping. No PSP patch, mouse aim, automatic in-game remapping or purge preservation should be inferred merely from choosing a profile.

## Confirmed baseline and translation

Movement uses the left stick or WASD. Camera uses the right stick or IJKL. Where the game exposes arm weapons, controller presets favor left/right triggers; boost and quick boost or weapon cycling use shoulders when possible. Each original scheme is translated separately. Keyboard layouts are explicitly inferred, since no reliable cross-game keyboard consensus was found. The editor must retain pause, menu, weapon selection, purging and game-specific secondary functions.

For original early digital schemes, assigning movement and camera buttons to stick directions offers modern positioning but remains digital. Installing a validated analog patch is what makes that movement genuinely analog. Emulator remapping can also move menu confirmation/navigation when the same console button serves both menu and combat actions; the UI must explain that tradeoff.

Existing physical-controller confirmations cover the previous Default layouts for AC2, patched AC2, AC4 and ACV. New Community templates need an additional hands-on confirmation before being described as verified. They must remain optional and must not overwrite existing Default or Custom selections.

## Xenia controller translation

[SDL2's mapping API](https://wiki.libsdl.org/SDL2/SDL_GameControllerAddMapping) defines the reserved `xinput` device identifier for all Windows XInput controllers. The Hub generates a per-session mapping from its canonical controller rows using [SDL2's native XInput joystick order](https://github.com/libsdl-org/SDL/blob/SDL2/src/joystick/windows/SDL_xinputjoystick.c), including signed stick half-axes. The child emulator's environment selects the corresponding SDL XInput driver so a HID or raw-input mapping cannot silently take precedence. This changes no global controller settings and adds no background polling. Existing device-specific SDL imports retain their original driver choice. Canonical generation covers XInput-compatible pads; other devices still need their own imported SDL mapping.

[Xenia Canary's input-driver factory](https://github.com/xenia-canary/xenia-canary/blob/master/src/xenia/app/xenia_main.cc) retains its keyboard driver alongside a selected SDL controller driver. The live controller/mapping behavior still needs an in-game check on the selected build; parser acceptance and generated configuration are narrower evidence.

## Research tooling

Universal Modder's local knowledge search was attempted. Its CLI lacks PyYAML in the current Python runtime, so the installed knowledge folder was searched directly; no Armored Core-specific field note was present. Public primary author documentation and first-hand control-layout reports were then reviewed. No game images, executables, saves or commercial downloads were fetched during this research.
