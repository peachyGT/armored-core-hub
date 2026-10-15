# Using Armored Core Hub

## Download and start

Open the [latest release](https://github.com/peachyGT/armored-core-hub/releases/latest). Choose **Windows installer** for the simplest setup, or **Portable ZIP** if you prefer to keep everything in a folder you manage.

The installer runs for your Windows account and adds a Start menu shortcut. Click **Install**, then **Finish** to open the Hub. No administrator access or separate .NET installation is needed. The ZIP must be fully extracted into a writable folder before opening `ArmoredCoreHub.exe`.

Windows 10 or 11 x64 is required. Emulator hardware requirements still apply. The downloads are not code-signed, so Windows may show an unknown-publisher warning. Download from this project's releases; SHA-256 checksums are supplied there.

## Add your games

1. Choose **Add games** and select the folder containing your games.
2. Review recognized editions. Confirm a match only when it is correct.
3. Follow the selected game's checklist to install its emulator and add any required system files.
4. Click **Play**.

Game files stay in place unless you choose to copy them. ZIP, 7z and RAR game archives are supported; recognition depends on their contents. PS1/PS2 need compatible BIOS dumps, including supported ZIP/7z imports. The Hub can download official PS3 system software. Games, BIOS and emulator binaries are not bundled.

## Library and artwork

Group by console or generation, filter your collection and drag the divider to adjust the library. Covers download automatically and remain cached for offline use; automatic downloads can be disabled in Settings.

Added games can supply still images and muted previews from supported game files. Not every game has usable media. Choose **Artwork…** for a custom cover, backdrop, transparent logo or video. Cover colors provide ambient lighting; videos and lighting can be disabled separately. [Artwork sources and credits](artwork-credits.md).

## Settings and controls

**Game settings → Settings** separates graphics presets, rendering scale, window resolution and supported FPS limits. A frame-rate cap does not unlock a game's simulation speed. Advanced graphics exposes supported emulator values. Changes apply on the next launch.

**Controls** lists each game action, its console button and your binding. Edit a keyboard or controller binding directly in its row; the adjacent reset restores that action's Default. Keyboard and controller have separate Default, Community, Custom and named imported profiles. Profiles can be renamed and exported. Community profiles are adaptations rather than a universal community standard; match the selected in-game layout to the actual game settings.

Keyboard defaults are available for every backend. Enter is Start, except PSP where it is Space. Xenia gives a connected Xbox-compatible controller priority; turn it off to return to keyboard input. Recommended RPCS3 builds support keyboard and controllers together; older manually selected builds may choose the device at launch.

**Patches** manages local PNACH/PPF files. Patches must match the exact edition and supported revision. The Hub preserves original media; patch authors' payloads are not bundled. Native mouse aiming is not included.

## Saves, playtime and achievements

Saves are backed up before launches. Use **Save Data** to back up, import or restore progress. [Save and control details](saves-and-controls.md). Playtime includes Hub-launched menus, loading and paused time, with available native history imported once.

**Achievements** in the top menu shows collection progress; the trophy beside Play opens the selected game. Supported tracking is enabled by default. RetroAchievements accounts can be configured in the Hub; PS3/Xbox games use supplied native records where supported. Not every game has achievements. Locked icons are dark; unlocked icons show their supplied colors. Descriptions appear on hover or keyboard focus. [Availability and current limits](compatibility.md).

## Updates, data and uninstalling

Close the Hub and games before updating. The installer reuses its installed location and preserves personal data. For ZIP updates, back up the old folder and replace application files without deleting your data folders. Installed and portable copies have separate libraries unless you move data yourself.

The default installed location is `%LOCALAPPDATA%\Programs\ArmoredCoreHub`. Settings, saves, downloaded runtimes and cached content live beside the app. A different game-storage folder can be chosen in Settings. Uninstalling removes installer-owned application files and shortcuts; personal Hub data and externally stored games remain. Keep a backup before manually deleting those folders.

Closing the Hub warns if a game is running and stops its own emulator/helper processes after protecting saves. It does not stop unrelated applications. Emulator updates remain explicit; see [update behavior](updates.md).

## Help and reporting problems

The setup checklist shows which step is missing for the selected game. If setup or launch fails, check the Hub's logs folder and [known limits](compatibility.md). Search the [issue tracker](https://github.com/peachyGT/armored-core-hub/issues), then use the [bug-report form](https://github.com/peachyGT/armored-core-hub/issues/new?template=bug.yml). Include your Hub version and what happened; logs and screenshots help. Remove private paths or account details before posting them.

Settings → About and legal contains the legal notice and component licenses. Use only files you are legally entitled to use; see [the legal notice](../LEGAL.md).
