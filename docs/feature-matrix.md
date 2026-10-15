# Features and current limits

For a short explanation of what has and has not been checked, see [test coverage](compatibility.md). The README's gameplay report refers to the titles exercised in our harnesses, not every console/region edition or completed playthrough.

| Requirement | Result |
|---|---|
| Portable native application and library | Implemented, self-contained WPF x64 |
| First run / selected folder and file import | Implemented, honest empty state |
| Archive import | Single-file 7z/ZIP/RAR → bounded native 7zr for supported 7z, managed fallback → existing identification; phase progress/timing logs; one AC3 extraction sample 4.43× faster with matching hashes. RAR reader needs dedicated verification |
| Default input | Keyboard defaults for all five backends; sampled keyboard combat on PS1/PS2/PSP/PS3. User verified AC2 original/patched dual sticks and AC4 controller/reconnect. ACV keyboard/menu and user-reported controller movement/camera/pause checks pass; unsampled layouts and remaining reconnect checks stay open |
| Edition identity | 15 games, 100 product/disc/region records |
| Exact dump verification | Mechanism tested; production reference hashes incomplete |
| Provisioning and build selection | Five upstream adapters, pinned hashes, retained versions; installed builds automatically recognized across editions, explicit pins retained, per-edition recommendations/regression repair |
| Alternate backend selection | Profile model supports it; baseline has one family per console, PS3/360 are separate editions |
| System-file management | Selected-game checklist; bounded ZIP/7z import chooses one validated compatible BIOS using ROMDIR/ROMVER/region/hash metadata; PS1 reviewed hashes, official PS3 update download with imported/installed distinction |
| Configuration and launch | Detailed real-media campaign records cover eight editions. AC4/For Answer tutorial and save/reload checks pass; first-mission 20250 freezes have a sampled 20152 fallback; explicit repair prepares ISO/Joliet folders and keeps originals. Fresh managed Xenia storage creates a native offline player; first-story save/reload and unsampled editions remain separate verification tasks |
| Graphics | Distinct presets, supported independent scale/window size/FPS limit, advanced profile values; gameplay-dependent timing/scaling remains unverified |
| Patch manager | Local PNACH with named options/version editing; bounded PPF1/2/3 creates a separate image; source/payload fingerprints and original-preserving disable. Author bytes not bundled |
| Achievement tracking | Default supported integration; in-Hub RetroAchievements sign-in/defaults/edition overrides with protected native token and no web API key. All 23 title/platform combinations have explicit support; five early sets published, PS3 AC4/FA trophy-less. Native Xenia local profile creation/selection, bounded account/set records and restore-safe unlock history; locked/color provider art. Missing supplied art remains a trophy fallback. Live RA/real unlocks unverified |
| Control profiles | Grouped editable action/native-button/input rows for keyboard and controller with per-action Default resets, independent presets and imported-source preservation; explicit game layouts, sourced/inferred Community templates, named imports with rename/export/remove. SDL parser/launch generation verified; new physical Community layouts remain unverified |
| Saves | Backup, import, exact snapshot restore with rollback and original hash recheck; preserve installed game data, exclude achievement credentials, explicit families; no format conversion |
| Input extensions | Exact-match process lifecycle; no mouse injection |
| Updates | Signed catalog import/rollback, runtime update selection; no hosted Hub updater |
| Offline / diagnostics / repair | Implemented and failure-tested |
| Conversion / multiplayer / title updates | Bounded PS3 ISO/Joliet preparation delivered for the recommended build; other conversion, multiplayer and title-update adapters remain deferred |
| Artwork | Versioned edition cover sources, automatic hash-verified local downloads/offline cache, 17 explicit fallback mappings; separate cover/background/logo/video roles, local game-asset discovery, shared scene/ambient color; Windows-codec video with still fallback. Transparent local/manual logos work; no guessed PIC0 logo or complete bundled title-logo set |
| Adopted A+C library | Adjustable 36/64 split, collapse/expand/restore, console/generation grouping and filters, grid/list, portable preferences |

This distinguishes delivered behavior, architectural seams and research blocks. Compilation never substitutes for gameplay certification.
