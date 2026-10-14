# Saves and controls

Families are explicit and regional. PS1 titles share a regional card. AC2/Another Age and AC3/Silent Line share their respective families. Nexus/Nine Breaker/Last Raven share a regional family for original in-game import. Portable saves remain separate from PS2. Cross-console/region conversion is not assumed.

Family membership does not promise every item transfers. Parts, credits, emblems, completion data and special abilities follow original game rules. Until sourced title-specific migration records are available, the Hub shares the card/root and the original game performs its supported import.

Each launch makes a timestamped verified backup. Restore verifies hashes, backs up current progress, stages replacements and rolls back a failed transaction. Unrelated files remain: restore overlays a snapshot rather than deleting later progress.

Save import refuses collisions before copying. Expected roots:

- DuckStation: memory-card files, including shared.mcd.
- PCSX2: memory-card files, including shared.ps2; new cards are formatted by the original BIOS/game.
- PPSSPP: a root containing PSP/SAVEDATA.
- RPCS3: a root containing dev_hdd0/home.
- Xenia: its content tree; no resigning or conversion.

PSP backups exclude other memory-stick content; RPCS3 excludes other installed data. Close emulators during import/restore. The Hub mutex prevents competing Hub writers, but cannot govern separately started emulators.

Standard mappings are generated locally. PS1 preserves digital controls unless an enabled analog patch is selected; PCSX2 uses a DualShock2 SDL map; PPSSPP uses native defaults; RPCS3 selects XInput Player 1. XInput detection does not exclude SDL-only controllers.

Controls labels each original console button with its game action and Sony/Xbox equivalent. Select the same in-game control layout you actually use (for example AC4 Type A or Type B). The Hub translates the chosen layout into physical inputs; it does not silently change the game's saved layout. Automatic resolves the original default or a supported active analog patch. Disabling all PNACH instructions also disables its analog-label claim.

Keyboard and controller independently select Default, Community, Custom or any named Imported profile. The Community baseline was confirmed by the user: arm weapons on LT/RT, boost/jump on LB, quick boost or weapon cycle on RB; movement on the left stick/WASD and camera on the right stick/IJKL. Templates adapt this to each game's available actions. Original digital layouts still use digital stick directions. Keyboard and some controller arrangements are inferred, not a claimed community consensus. See [manual research](research/game-controls.md) and [Community evidence](research/community-controls.md).

Import retains a named profile without overwriting Community. Save current bindings creates another personal profile; rename, remove and JSON export support sharing. Profiles are scoped to their edition and declared game layout, and keyboard/controller can select different saved entries. Earlier single-slot imports migrate to the named list. Xenia canonical controller rows generate a per-session XInput SDL mapping; device-specific SDL files remain available in advanced input. Mouse aiming still requires a separate patch.

Controls opens config/controls/<edition>/controller-overrides.json, a section/key-to-string map. DuckStation accepts Controller/InputSources; PCSX2 Pad/InputSources; RPCS3 Player sections; PPSSPP writes a controls INI selected with --controlconfig.

~~~json
{"Controller1/Cross":"SDL-0/A","Controller1/Circle":"SDL-0/B"}
~~~

Overrides merge at launch and survive config regeneration. Use the selected emulator's binding syntax. Mouse injection is not implemented.

The current keyboard summary uses grouped Action / Game button / Key rows rather than a paragraph. Each row names the selected game's action, its original console command and the chosen physical key; switching an edition, scheme or preset updates the rows.
