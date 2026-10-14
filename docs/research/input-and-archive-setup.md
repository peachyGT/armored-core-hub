# Input defaults and archive imports — 0.3.4

Access date: 2026-10-10. Scope: five emulator adapters, all 100 catalog editions, and local archive imports. No universal-modder code or dependencies were fetched.

## Failure and correction

The prior AC4 log reported player one's handler as Null. The generated YAML used `Player 1`, while this RPCS3 build reads `Player 1 Input`; handler-specific settings must be nested under `Config`. The serializer now emits nested nodes correctly. The Hub also writes `main_window/infoBoxEnabledWelcome=false` before launch. Passing `--no-gui` alone does not skip RPCS3's first-run welcome.

Pinned RPCS3 0.0.43-20250 supports native co-pilots. Player one remains Keyboard; four XInput slots use class 40964 (co-pilot for player one). Disconnected slots are retained and polled by the emulator, allowing a wireless controller to appear after launch without removing keyboard input. No helper, process injection or custom emulator build is involved. Older manually selected builds fall back to choosing an attached XInput device or Keyboard at launch; the Controls screen discloses that limitation.

## Adapter behavior

| Backend | Keyboard default | Controller behavior | Routine external UI |
|---|---|---|---|
| DuckStation | Explicit digital PS1 bindings | SDL-0 plus alternative keyboard bindings | Setup wizard/update disabled; batch/no-GUI launch |
| PCSX2 | Explicit buttons and both sticks | SDL-0 plus alternative keyboard bindings | Setup wizard/update disabled; batch/no-GUI launch |
| PPSSPP | Upstream keyboard defaults | Upstream XInput/generic-pad defaults in a dedicated controls file | First run/update disabled; game launched directly |
| RPCS3 recommended | Explicit buttons and both sticks | Keyboard plus four native XInput co-pilots | Welcome, install-success, update and confirmation popups disabled; no-GUI launch |
| Xenia Canary | Gamepad keyboard mode with explicit bindings | SDL controller has priority; disconnect to return to keyboard | Game launched directly |

Keyboard Start is Enter except PPSSPP (Space). Non-PSP bindings: arrows for D-pad; WASD and IJKL for sticks; X/C/Z/V for Cross/Circle/Square/Triangle; Space for Select; Q/E and R/T for shoulders/triggers; F/G for stick clicks. PPSSPP: Z/X/A/S for face buttons, arrows for D-pad, IJKL for analog, Q/W for shoulders, V for Select. The game-specific Controls panel shows its backend's mapping. Original PS1 digital controls are preserved.

Necessary errors, shader compilation and dialogs inside games remain visible. Xenia's first offline-player-profile prompt is an in-game overlay and is not dismissed by fabricating a profile or suppressing errors. A connected 8BitDo must expose an Xbox-compatible/XInput device for RPCS3's automatic slots. Its physical wireless pairing and button path have not been certified here.

## Sources and confidence

All source links are upstream, accessed on the date above. High confidence for configuration parsing and implementation; actual hardware behavior still needs testing.

- [RPCS3 input schema](https://github.com/RPCS3/rpcs3/blob/304d544bc1b3af4f15400a47b34132ff49b1bc69/rpcs3/Emu/Io/pad_config.h): exact player-node names and nested Config.
- [RPCS3 pad types](https://github.com/RPCS3/rpcs3/blob/304d544bc1b3af4f15400a47b34132ff49b1bc69/rpcs3/Emu/Io/pad_types.h) and [pad thread](https://github.com/RPCS3/rpcs3/blob/304d544bc1b3af4f15400a47b34132ff49b1bc69/rpcs3/Input/pad_thread.cpp): co-pilot class and merging into player one.
- [RPCS3 XInput handler](https://github.com/RPCS3/rpcs3/blob/304d544bc1b3af4f15400a47b34132ff49b1bc69/rpcs3/Input/xinput_pad_handler.cpp): disconnected slots remain valid devices and connection state is polled.
- [RPCS3 GUI settings](https://github.com/RPCS3/rpcs3/blob/304d544bc1b3af4f15400a47b34132ff49b1bc69/rpcs3/rpcs3qt/gui_settings.h) and [GUI application](https://github.com/RPCS3/rpcs3/blob/304d544bc1b3af4f15400a47b34132ff49b1bc69/rpcs3/rpcs3qt/gui_application.cpp): welcome preference and startup path.
- [PCSX2 INI reader](https://github.com/PCSX2/pcsx2/blob/v2.8.2/pcsx2/INISettingsInterface.cpp) and [DuckStation INI reader](https://github.com/stenzek/duckstation/blob/master/src/util/ini_settings_interface.cpp): repeated keys provide alternative bindings, not button chords. DuckStation master was inspected because the pinned rolling release source reference is not a stable semver tag.
- [PPSSPP keymap loader](https://github.com/hrydgard/ppsspp/blob/v1.20.4/Core/KeyMap.cpp) and [defaults](https://github.com/hrydgard/ppsspp/blob/v1.20.4/Core/KeyMapDefaults.cpp): restoring Windows keyboard/XInput/generic-pad defaults before applying a controls file.
- [Xenia Canary input system](https://github.com/xenia-canary/xenia-canary/blob/828014b/src/xenia/hid/input_system.cc), [keyboard driver](https://github.com/xenia-canary/xenia-canary/blob/828014b/src/xenia/hid/keyboard/keyboard_input_driver.cc), [bindings](https://github.com/xenia-canary/xenia-canary/blob/828014b/src/xenia/hid/keyboard/keyboard_binding_table.inc), [SDL driver](https://github.com/xenia-canary/xenia-canary/blob/828014b/src/xenia/hid/sdl/sdl_input_driver.cc): keyboard mode, bindings and first-successful-driver priority. Keyboard does not reserve a physical SDL controller slot.

## Archive pipeline

Single-file 7z, ZIP and RAR are transport containers, not formats passed to emulators. The Hub hashes the source, unpacks into a staging directory, checks media inside, and presents the normal detected-edition review. Payloads publish atomically under `games/archives/<archive SHA-256>/files`. The receipt sits outside payload files. Reimport reuses unchanged stored payloads; choosing Copy does not copy managed archive payloads a second time. Cancelling a review removes only newly extracted data with no registered media beneath it.

Limits: 128 GiB expanded game payload, 50,000 entries, free-space preflight and declared-size enforcement. Runtime archives keep their existing 4 GiB limit. Traversal, links, reserved names, duplicate Windows paths and encrypted entries are rejected. Source size/time is checked again after extraction. Corrupt archives become individual scan-log entries, allowing other games to import. Password-protected, nested and split-volume archives are not a supported one-click workflow; unpack those with a trusted archive tool first. RAR uses SharpCompress's existing reader; only 7z and ZIP have dedicated fixtures in this change.

Archive support does not imply every payload is playable: ISO/BIN-CUE/CHD/CSO/PBP and extracted console folders still follow backend/edition checks. Encrypted PS3 discs, unsupported containers and unknown revisions retain their existing corrective steps. Nothing downloads games or runs programs from game archives.

## Verification

- 83 core checks pass, including launch-plan construction for all 100 editions, actual tiny 7z extraction, ZIP PS3 ISO identification, duplicate reuse, source preservation, traversal/duplicate-path rejection, damaged/encrypted archives, size budgets and mid-stream cancellation cleanup. Fixtures contain original synthetic text or structural metadata, never commercial game bytes.
- Actual RPCS3 0.0.43-20250 logs confirm Keyboard on player one and XInput co-pilots on slots two through five; unused slots six/seven remain Null as expected.
- A 90-second isolated AC4 PS3 EU ISO run used window-specific Enter/X key events. Captures show Press Start followed by the game's System/Game Options setup screen. Only game-window titles were observed; no welcome window appeared. Source media remained unchanged. This proves keyboard menu input and startup behavior, not a playthrough, performance certification or physical-controller test.
- Native checks exercise the default keyboard guide, switching to a Portable entry, the modal import, small windows and the retained A+C layout. Searching by the visible Portable title now works.
- Private emulator logs/screenshots stay under ignored work storage and are not included in the package or repository.


Packaging confirmation: all 27 native checks passed in both clean and relocated self-contained copies. Each loaded its bundled .NET runtime; configuration, appearance and the save marker survived. This is an isolated portable-folder test on the current Windows machine, not a fresh Windows VM.
