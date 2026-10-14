# Armored Core Hub

A portable Windows library and compatibility manager for the earlier Armored Core generations.

Time played is shown beside each selected edition's status. Available DuckStation, PCSX2 and RPCS3 history seeds its portable ledger once; all five backends then track Hub-launched emulator sessions. Setup is excluded; menus/loading/pauses are included. A 30-second checkpoint runs only during play and stops with the session. Native histories copied between builds are not counted twice.

Closing the Hub during a game asks for confirmation, then stops the owned emulator/helpers, protects saves and commits playtime before the window closes. Windows jobs also clean up owned child processes on unexpected owner exit. Installed emulator builds are shared; game settings/saves remain isolated, and explicitly selected compatibility versions stay pinned. A valid installed build is reused without a download or cached archive.

Settings → About and legal opens the anti-piracy notice and separate license tabs. Game/BIOS imports use local files; see [legal notice](../LEGAL.md).

**Baseline 0.3.10:** a working native application with a cinematic library, local imports, edition selection, immutable emulator installation, generated configurations, saves, diagnostics and signed-catalog updates. This is an engineering preview, not a claim that every game is certified playable.

Game covers download automatically and remain cached for offline use. Edition choices select regional/platform artwork; 17 uncommon or digital editions use documented fallbacks. Artwork provides source credits and retry, and Settings can disable automatic downloads. The ZIP contains cover metadata, not the copyrighted image set. See [cover sources and limits](research/cover-art.md).

After import, **Help** shows the selected game's setup checklist. Green checks identify available game files, its emulator and its BIOS/system software; the next action points to the remaining requirement. **Import system files…** accepts ZIP and 7z archives, validates their contents and selects one compatible BIOS for the game's region. Other-console firmware never completes this game's requirement. PS3 updates distinguish imported files from installation pending on first Play.

Added games use their discovered cinematic still/video, or a dim, softened high-quality cover backdrop while no cinematic is available. Portrait fallback covers are cropped around their center and stay centered as the window resizes. AC2, Last Raven and Xbox ACV openings can be read directly from their ISO file extents without unpacking the discs. Small menu icons stay small. Generated previews remain local and are never bundled. See [disc previews](research/disc-cinematic-previews.md).

## Start

1. Extract the Windows x64 package into a writable folder.
2. Run ArmoredCoreHub.exe. The published package needs no separate .NET installation.
3. Choose **Add games**, select a folder, and review the detected editions. Dropping files/folders and individual file import are also supported.
4. Select a game and edition. Its primary action identifies the next missing component.
5. Install the indicated emulator. Add owned PS1/PS2 BIOS dumps or download official PS3 system software. PSP and Xbox 360 need no separate BIOS.
6. Click **Play** when prerequisites are ready.

Recognized copies arrive with the game, console and region selected. PS3 ISOs with readable metadata and executable data can launch directly on supported RPCS3 builds. For the sampled AC4 EU and For Answer US editions, **Repair setup** selects the older build that passed opening-mission testing; it prepares a separate game folder when needed, keeping the ISO, saves and previous emulator build. Encrypted, conflicting or incomplete images show a corrective step instead of being registered as ready. Scans skip inaccessible files and record them in `logs/last-scan.json`.

Single-file **7z, ZIP and RAR** imports unpack automatically into the selected game-storage folder (Settings → Game storage), then identify the game files inside. Originals remain unchanged; cancelled reviews remove unused new extractions. Reimport reuses the stored copy. Password-protected, nested and split-volume archives need separate extraction. 7z and ZIP have dedicated automated fixtures; RAR uses the library reader and still needs a dedicated fixture. Expanded archives are limited to 128 GiB and 50,000 entries, with a free-space check.

Ordinary game files stay at their original locations by default. The import dialog can copy them into hash-addressed portable storage. Moving the Hub preserves managed paths. External drives must remain accessible at their registered paths.

Advanced settings lets you select another registered copy or forget a mistaken registration. Forgetting preserves the files and saves; import it again to correct the selected edition.

The library starts at 36% of the window. Drag its three-dot divider handle to resize it, or use the arrows above the library to expand, collapse and restore it. Focus the divider and use the arrow keys for keyboard resizing; double-click it to reset. Width, view, grouping and selected game are remembered in portable storage. Group by console or generation; **Filter** includes console, generation, search and added-only choices. Game hover lifts the entry slightly with a brief highlight fade; Windows reduced-motion settings and software rendering use an immediate highlight without movement.

Added games can supply menu images automatically from supported game folders, ISO9660 discs and PBP files. Choose **Artwork…** for a custom cover, background, transparent logo or muted video. Unadded titles keep a small cover; added backgrounds fill the scene and blend behind the translucent library. Cover colors provide ambient lighting, which can be switched off under **Settings**. Compatible videos pause with the window or settings, and can be disabled independently. A verified optional FFmpeg tool generates local muted previews from supported PAM/PMF/PSS movies; PSP menu slots in readable ISO/PBP containers are supported. Not every game includes usable artwork or a supported movie. See [native presentation](research/native-presentation.md) for exact format limits. Windows high contrast removes decorative images and lighting. The ZIP contains cover metadata and documentation captures, rather than a reusable cover-art collection. Displayed game artwork retains its owners' rights. About and licenses are under **Settings**.

Windows 10/11 x64 is the target. On Windows 11 the native title bar uses the app's charcoal/ivory colors and retains the standard window controls; high contrast restores OS defaults. The executable includes an original Hub icon. The same original Hub icon is used for the title bar, taskbar and executable. Emulator CPU/GPU requirements still apply. Start with Original enhancements. The Hub never changes system drivers, services or registry configuration.

## Controls

Every backend now provides keyboard defaults. Open **Game settings → Controls** for the mapping. **Enter** is Start, except PSP where it is **Space**. Xbox-compatible controllers can connect after launch on the recommended builds; RPCS3 keeps keyboard input active alongside four XInput slots. Xenia gives a connected controller priority, so turn it off to return to keyboard input. Older manually selected RPCS3 builds choose keyboard or controller at launch.

Keyboard and controller bindings list grouped movement, camera, weapon, mobility and menu actions. Change the input selector directly beside an action; the adjacent reset button restores just that action's Default input. Changes save as Custom for that device and apply next launch. Other bindings and imported source profiles are preserved. Achievements use provider artwork when available: locked icons are dark and grayscale, unlocked icons retain their original color. Hover or keyboard focus shows supplied descriptions; secrets stay hidden until unlocked and missing artwork uses a trophy symbol.

Routine emulator welcome/setup/update windows are disabled in local generated configuration. Necessary errors and in-game dialogs remain visible. AC4 PS3 EU keyboard input was exercised from Press Start into the game setup menu; the user previously verified physical 8BitDo input and reconnect. Complete gameplay remains uncertified. See [input and archive evidence](research/input-and-archive-setup.md).

## Per-game settings and progress

**Settings** separates graphics preset, rendering scale, window resolution and supported FPS targets. Advanced graphics exposes profile values. Settings change on the next launch; a frame-rate cap alone does not unlock a game's simulation speed. Details is the last tab.

**Controls** names the selected game’s actual actions beside its native button and your binding. Match the in-game layout selector to the layout used in the game. Keyboard and controller independently choose Default, Community, Custom or named Imported profiles; personal profiles can be renamed and exported as JSON to share. Community templates use the confirmed modern baseline, with researched priorities and inferred details disclosed. Xenia custom layouts support Xbox-compatible controllers; other devices can import native SDL mappings. **Patches** accepts PNACH/PPF files, enables options and saves edited PNACH files as new versions. Analog patches still need the matching edition; a custom source fingerprint is not author revision verification.

**Achievements** in the top menu browses collection progress; the trophy button beside Play opens the selected game. Local PS3 records retain bronze, silver, gold and platinum grades, while Xbox/RetroAchievements retain their own points and progress. Supported tracking is enabled by default. **Achievement account** provides RetroAchievements sign-in, global defaults and optional Hardcore mode entirely in the Hub; per-edition choices stay under Game settings. No web API key or manual emulator sign-in is required. Native tokens are protected by your Windows account and supplied only to the selected runtime session. Offline records remain available, and previously recorded unlocks survive restoring an older save for the same account/set.

Achievement availability is explicit: five early title/platform combinations currently have published RetroAchievements sets; the other PS1/PS2/PSP provider pages have no published set as checked. PS3 AC4 and For Answer have no trophies. A published set does not prove that every region or patched copy matches its supported hashes; the native emulator identifies those at launch. Live account authentication and real unlocks remain unverified. See [support and setup evidence](research/achievement-support-and-setup.md).

## Included

- Fifteen game records; console and Portable editions remain distinct. No AC VI or Windows Mobile Archives releases.
- One hundred regional/disc/product records, product-ID recognition and explicit unknown/modified states.
- DuckStation, PCSX2, PPSSPP, RPCS3 and Xenia Canary adapters.
- Pinned SHA-256-verified upstream archives and independent per-edition build choices.
- Per-edition compatibility, enhancements, settings and persistent controller overrides.
- Save-family storage, timestamped verified backups, conservative import and restore.
- Hash-bound local PNACH/PPF patches, original-preserving derived images, named patch options and separate Patches tab.
- Future input-helper lifecycle with exact media/build gates; no mouse injection.
- Offline cache, cancellable downloads, bounded logs, repair and redacted diagnostics.
- Signed catalog import and rollback. No fabricated hosted update service or signing secret.

Emulator binaries, commercial games, BIOS dumps and Sony firmware are **not bundled**. DuckStation downloads are unmodified; generated configuration is local and separate. Upstream license notices stay with downloaded archives.

## Important limits

AC4 PS3 EU import, direct-ISO boot and keyboard menu input were exercised with user-provided media. Full gameplay remains **not certified**. Recognition is not exact-dump verification. The catalog contains no invented retail hashes. CHD/CSO/PBP revisions without reference hashes need manual edition confirmation. Xbox regions sharing product IDs remain provisional.

Arbitrary media/save-format conversion, PSP executable-patch preparation, commercial title-update acquisition and network multiplayer setup are not shipped. Community templates are explicitly inferred adaptations, not a claim of universal consensus; real gameplay verification of every new layout remains open. See BLOCKED.md, STATUS.md and docs/feature-matrix.md.

The captured RPCS3 build installs firmware successfully but encounters a headless teardown assertion on this machine. The narrow workaround requires a fresh success log and installed system files; other failures block setup.

## Build and test

Install the Microsoft .NET 10 SDK, then:

~~~powershell
.\scripts\build.ps1 -Test -Publish
~~~

The script uses work/sdk/dotnet.exe when present, otherwise dotnet on PATH. Restore contacts NuGet. -Offline requires a previous restore/cache.

~~~powershell
dotnet run --project tests/Hub.Tests -c Release -- work/test-results
~~~

Tests generate synthetic media only. Optional real-runtime integration tests expect the pinned upstream archives under work/emulator-smoke and official Sony PS3UPDAT.PUP under work:

~~~powershell
dotnet run --project tests/Hub.Tests -c Release -- --integration .
~~~

Isolated native UI verification:

~~~powershell
.\outputs\ArmoredCoreHub-0.3.10\ArmoredCoreHub.exe --root work\clean-root --self-test --capture work\ui-captures
~~~

Do not use self-test against production Hub storage.

Run scripts/validate-portable.ps1 after publishing to exercise two isolated copies, including relocation and package-local .NET loading. scripts/package.ps1 builds, tests, validates and writes the portable ZIP plus SHA-256. It refuses to package a previously used Hub folder. The optional --download-smoke . test mode checks a real upstream Xenia download and offline reuse.

Core services are in src/Hub.Core; native UI in src/Hub.App; the versioned catalog is manifests/catalog.json. PRODUCT.md and DESIGN.md are living authorities. See CONTRIBUTING.md, docs/architecture.md, docs/updates.md and THIRD-PARTY-NOTICES.md.

Independent project, unaffiliated with FromSoftware, Bandai Namco, Sony, Microsoft or emulator authors. Game names identify supported titles; original typographic artwork avoids redistributing game covers.
