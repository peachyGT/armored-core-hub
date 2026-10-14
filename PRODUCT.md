# Armored Core Hub
<!-- impeccable:product-schema 1 -->

## Platform
Windows desktop (native WPF; Impeccable's web/iOS/Android taxonomy does not describe this target).

## Stack
Delegated by the user. C# / .NET 10 LTS / WPF, self-contained Windows x64 distribution. See ADR-001.

## Users
Players with legitimate Armored Core collections who want to play the emulation-era games without becoming emulator administrators. Maintainers need traceable compatibility evidence and reversible configuration.

## Product Purpose
Import a collection, identify its editions, provision the appropriate runtime, apply an explicit compatibility profile, and play. The accumulated game-specific knowledge is the product.

## Operating Context
Portable Windows folder, optional external media storage, offline play after setup. Ordinary use is a desktop library with controller-based gameplay. Do not assume a controller is attached. No accounts, servers, telemetry, or registry-dependent installation.

## Capabilities and Constraints
Separate games, editions, media, backends, builds, compatibility, enhancements, patches, extensions, saves, and artwork. Current native Windows releases are excluded. Download providers are for authorized redistributable assets and official firmware; no commercial game or proprietary BIOS discovery/download providers. Future commercial media delivery requires a separate rights-cleared provider and is not in this baseline.

PS1/PS2 BIOS comes from owned hardware. PS3 firmware may come directly from Sony. Modified media remains importable but must never receive an invented exact-dump verification claim. Compatibility recommendations distinguish upstream evidence from local game testing.

## Brand Commitments
Dedicated game library; compact cover grid by default, optional efficient list, and a small on-demand search. Installed state is obvious. Plain language first: Emulators, game actions and missing requirements; product IDs and compatibility internals live under Advanced, projects/licenses under Settings → About. No SaaS dashboard or marketing hero. User selected working prototypes before image concepts.

The user selected A+C on 2026-10-09 and approved its native refinement for 0.3.0. The WPF build places an adjustable library beside the selected-game scene, using warm charcoal and ivory, Cinzel Roman lettering for the wordmark and split main title/subtitle, and Segoe UI controls. Square buttons, vector navigation icons, and an active underline carry the chosen browser review's visual language into native controls. Compact grid/list views support console or generation grouping and intersecting console, generation, added-game, and title-search filters. The library defaults to 36% of the workspace, can collapse to a rail or expand for browsing, and restores its remembered normal width. Grid covers shrink from 108 to 96 DIPs when the library is narrower than 330 DIPs.

The approved shared backdrop supersedes the earlier fitted feathered rectangle: added-game images and muted videos fill one frame across the library and scene, fading at the left edge beneath a translucent library. Cover-derived ambient color supports that atmosphere; lower dark shades protect title and launch controls. Unadded games retain a small cover. A usable local transparent logo can replace the textual identity, but no extracted logo is guaranteed. Game settings opens over the scene. The AC4 / For Answer references establish the photographic atmosphere, not bundled cover-art rights. The earlier Archive engineering preview and browser comparisons are historical; DESIGN.md records the current native system.

Artwork discovery reads supported local PS3/PSP folders, PBP image slots, readable ISO9660 presentation slots, and per-file `.artwork` companions without changing source media. Manual local artwork takes priority. Background videos use available Windows codecs, stay muted, and have pause and still-image failure behavior; PAM/PMF has no decoder in this build. Disabling Windows animations suppresses video in favor of available still artwork, including when the preference changes while the app is open. Artwork and cached presentation assets remain local in the portable Hub and separate from emulator packaging. No fictional game assets, commercial media, or redistributable art set is implied by these features.

The 0.3.2 refinement preserves that composition. A compact three-dot divider has keyboard focus without persistent mouse decoration. Game entries lift slightly on hover using a finite transform; Windows reduced motion, high contrast and software rendering retain an immediate highlight. The user selected the original Hub icon alternative for small-size legibility; the executable and native window/taskbar now all use it. Native caption controls and high-contrast defaults remain available.

A versioned artwork manifest maps every catalog edition to a cover or an explicit same-platform fallback. The Hub downloads selected covers to its hash-verified local cache, retaining source credits and original copyright. Regional and PSP/PS2 art remain distinct. Custom covers take priority. Offline mode and the automatic-download preference control network use; manual retry is available under Artwork. The standard package contains source metadata, not a bundled copyrighted cover set.

## Evidence on Hand
No commercial game media, owned BIOS, gameplay test evidence, or redistributable cover-art set supplied. The user has supplied game/album artwork and UI screenshots as private design references; review prototypes may use those references without adding them to the application package. Local .NET 10.0.401 toolchain present. Research is recorded with access date and confidence in docs/research.

## Product Principles
- Evidence precedes compatibility claims.
- Keep working builds and original saves recoverable.
- A normal setup should not require an emulator settings window.
- Unknown state is visible and actionable.
- External files are read-only unless the user explicitly imports a copy.

## Accessibility & Inclusion
Engineering default: keyboard navigation, visible focus, labeled native controls, scalable text, adequate contrast, no required animation or color-only state.

The 0.3.3 import repair detects PS3 ISOs structurally and confirms recognized games without an edition picker. Unknown copies keep the selected-game context. Scans skip inaccessible entries and keep a local report. PS3 ISO launch is gated by a manifest-declared emulator version floor and readable disc data. The import dialog shares the native caption theme; the disabled library retains its transparent surface.

0.3.4: keyboard-only play is a supported default for every backend and edition. Recommended builds accept controllers connected after launch; backend limitations are explained under Controls. Single-file game archives extract locally before identification, preserving originals and disc companions. A cancelled import discards only unused new extraction data. Archive transport never implies universal payload compatibility.

0.3.5: explicit compatibility repair resolves an edition-specific emulator release and, when necessary, prepares a separate decrypted PS3 ISO9660/Joliet folder. Source media, earlier emulator builds and save families are preserved. Local gameplay remains sampled, not whole-game certification.

## Delivered settings/storage expansion (0.3.6)

Future game extraction can use a dedicated external drive without moving the Hub or saves. Supported local console movies can produce cached previews. Per-game Settings, separate input profiles/editors, local PNACH/PPF patches and per-game achievement records are implemented. Community templates adapt researched priorities to each game’s declared layout, with inferred choices disclosed. Imported personal profiles remain separate and shareable. Frame caps are distinct from game-speed patches. Native RetroAchievements setup needs the user's sign-in, and real service unlocks are not yet validated. Refer to docs/research/settings-storage-achievements.md for the exact supported paths.
