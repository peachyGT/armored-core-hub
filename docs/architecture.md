# Architecture

One native Windows Hub process, plus its emulator and explicitly selected helper processes. No server, webview, generic plugin loader or DI container.

~~~mermaid
flowchart LR
 G[Game] --> E[Selected edition]
 E --> M[Media fingerprint]
 E --> P[Compatibility profile]
 P --> B[Backend]
 B --> R[Installed immutable build]
 P --> H[Enhancement and patch modules]
 M --> L[Launch plan]
 R --> L
 H --> L
 F[Independent firmware] --> L
 I[Exact-match input extensions] --> L
 L --> S[Save backup]
 S --> C[Local generated configuration]
 C --> X[PreLaunch / emulator / PostLaunch / runtime / shutdown]
~~~

## Boundaries

Game is a title; Edition is platform/region/disc/product revision. Media is a file set. Backend is the emulator family; Build is an archive identity. Compatibility profiles reference editions/backends and identify tested builds independently of release pins. Enhancements, patches and input extensions remain separate. Save families are explicit and platform/region-specific. Asset providers are independent of games.

Schema 1 JSON contains separate collections in one file for atomic updates. Models.cs defines records. Startup validates references, platforms, duplicate IDs, patch dependency cycles and provider kinds.

## Storage

~~~text
ArmoredCoreHub.exe
config/state.json
config/appearance.json
config/controls/<edition>/controller-overrides.json
artwork/index.json
artwork/<sha256>.<image-or-video-extension>
artwork/extracted/<media-id>/assets.json
manifests/catalog.json
runtime/<backend>/<build-id>/
runtime/sessions/<edition>/<build-id>/
games/<fingerprint>/
saves/<family>/<backend>/live/
saves/<family>/<backend>/backups/<timestamp-id>/
firmware/<class>/<sha256>.bin|pup
patches/<module>/<version>/module.json
extensions/<module>/
cache/objects/<sha256>
logs/
~~~

Persisted internal paths are relative. External paths are explicit. Emulator configs contain the absolute paths they require and regenerate after relocation. A named mutex prevents simultaneous writers to one Hub root.

UI preferences are independent of launch configuration. The native library presents a console entry plus a separate optional Portable entry where editions exist; filtering/grouping never selects a hard-coded emulator. `LibraryArtwork` resolves independent local managed cover/background/logo/video references, caches bounded bitmap decodes, and samples a small hue histogram for a low-opacity ambient wash. Artwork imports are copied by content hash; original image files stay intact. Path/link guards apply to artwork storage. Missing or unreadable images use typographic fallback entries. Local review crops are display metadata, not rewritten source images. Release packaging rejects an output directory containing artwork or other user data.

JSON writes flush then atomically replace with a previous copy. Damaged state can recover. Archive installation stages content and rejects traversal, links, reserved names, duplicate paths and expansion limits. Solid 7z extraction is sequential.

Archive cache is content-addressed; version directories remain. Session assets use hardlinks when possible, otherwise copies. Generated config destinations are unlinked before rewriting. Executables are hashed before launch. New releases never remove old builds.

## Lifecycle

Import fingerprints without changing sources. CUE dependencies form one file set. ISO SYSTEM.CNF, PSP/PS3 SFO, XEX and STFS provide product recognition. Shared identifiers remain ambiguous. Only sourced reference hashes establish verified dumps; filenames do not establish identity.

The resolver rejects missing prerequisites, wrong platforms/formats, incompatible or hash-mismatched patches, missing extension dependencies and unsafe settings. Explicit backend adapters generate configs and argument arrays.

Launch backs up saves, prepares config, starts PreLaunch helpers, starts the emulator, then PostLaunch/RuntimeExtension helpers. Logs are bounded. Cancellation closes then terminates only owned processes. Prior saves/builds remain.

InputExtension is a declarative contract containing capabilities, exact media/build gates, dependencies, phase, executable/hash, arguments and options. Future sensitivity/raw input/cursor capture belong to real extensions. No mouse-to-stick substitute is implemented.

No automatic commercial media acquisition, disc ripping, save conversion or multiplayer configuration. Signed catalog updating exists without a fabricated service or trust key. Scans and archive decompression run off the UI thread.

Reference hashes can specify container format and byte size. Hash algorithms compare case-insensitively; an ISO reference is not used to classify a CHD. Registration can be removed without deleting media or saves, and separate imported copies can be selected per edition.

Session preparation records the source installation identity. A repaired installation refreshes program assets, and a damaged session executable is replaced from the verified original even when the immutable build ID is unchanged. Generated configuration remains local.
Staged directory commits retry bounded Windows sharing/access errors only while the source exists and destination is absent. Paths are rechecked as managed descendants; conflicts and permanent failures are not overwritten.

`GameAssetReader` reads bounded standard PSP/PS3 menu slots, PBP offsets and ISO9660 assets off the UI thread. It scans explicitly named per-game companions rather than crawling a shared folder for arbitrary images. Copied assets are hashed; cancellation cannot publish a partial index. Selection revisions prevent a late result from changing another game's scene. Explicit local artwork overrides automatic discovery. Covers never become backgrounds implicitly. Two-line display titles are optional catalog metadata; old catalogs retain a full-title fallback.

An image-backed Windows Media Foundation reader shares the background layout and left mask with still images. One worker owns a muted decoder, reuses its pixel buffer and synchronously copies one frame at a time to a `WriteableBitmap`; stale source/seek callbacks cannot publish. Playback pauses on focus/visibility/settings changes, reuses the worker when changing clips and closes on exit. Native COM pointers, source locks and byte streams are released explicitly. Source sizes, dimensions and scanline extents are bounded; codec failures fall back to still artwork. No extra media package or service is shipped. High contrast suppresses decoration; reduced motion suppresses video. Portrait cover fallbacks use a centered viewport crop. Unsupported packed movies and universal title-logo extraction retain still/text fallbacks.

Game archive import uses MediaArchiveService as a transport step before MediaService registration. It stages 7z/ZIP/RAR payloads, publishes to games/archives/<archive-hash>/files, and keeps the receipt outside payload files. MediaImportBatch retains accepted registrations and removes unused newly extracted roots on cancellation. No helper process is needed. InputDefaults supplies emulator-native keyboard/controller mappings; the launch planner serializes nested RPCS3 YAML and alternative INI bindings. No mouse injection or input-forwarding service is introduced. See docs/research/input-and-archive-setup.md.

Compatibility profiles may carry a BuildRecommendation: immutable ReleaseAsset, supported media formats, optional preparation adapter and bounded affected-build ranges. Diagnostics exposes known regressions; only an explicit setup/repair action applies the recommendation. Backend-wide defaults remain available for other editions. Catalog changes never silently rewrite a saved selection. This field requires Hub 0.3.5 for enforcement; older binaries ignore it.

CompatibilityRepair validates/reuses an installed release or provisions it, then prepares unsupported source media before committing the selected build/media together. Ps3DiscPreparation reads an already decrypted ISO9660/Joliet image into a unique managed stage. Joliet is preferred because the primary ISO view can rename or omit assets. Limits: 128 GiB output, 50,000 records, depth 32, 64 MiB directory metadata; safe names, unique paths, finite extents, cancellation and free space are checked. Multi-extent/interleaved/extended-attribute layouts require a supplied folder. No mounting, decryption or executable extraction helper. The source hash is checked before/after, derived content is fingerprinted independently, duplicates are reused, and a receipt permits verified offline reuse. Original media/build selections remain available under Advanced and save families do not change.

## 0.3.6 explicit adapters

GraphicsSettings and ControlProfiles translate per-edition choices into existing LaunchPlanner configuration. LocalPatchService manages hash-bound copied payloads and derived images; it never writes the original. AchievementService reads native progress and writes bounded offline JSON snapshots. EmulatorAccounts preserves filtered native achievement settings outside logged launch plans using Windows account encryption. MediaPreviewService runs a verified one-shot decoder with a fixed concurrency/timeout/output budget. HubPaths.GameStorageRoot separates game payloads from the portable configuration/save root; legacy relative paths remain meaningful. See ADR 004.


## 0.3.7 detection, archives and input catalogs

Readiness and launch share a side-effect-free installed-build resolver. Deliberate pins remain authoritative; otherwise the edition recommendation is preferred among installed usable builds, with compatibility/format diagnostics retained. The app pins the resolved build when Play is chosen.

For supported 7z codecs, a bundled hash-verified 7zr decoder runs after managed metadata preflight and inside empty owned staging. The source/tool are held immutable; live and final output checks retain entry/size/link bounds, and cancellation kills only that child. Other codecs/formats keep the managed path. Archive hash, extraction and scan phases have separate progress and timing records.

GameControlCatalog labels the declared per-game in-game layout from primary manuals. ControlProfiles stores independent keyboard/controller presets and named imported profiles. A launch-only clone resolves active analog patches without rewriting preferences; CommunityControlPresets derives optional modern bindings and states its inference. XeniaControllerMappings emits SDL’s reserved Windows XInput mapping with child-only driver hints; it does not install hooks or change Windows input settings. Achievement JSON adds an optional PS3 grade and the collection browser reads cached records without polling services.

## 0.3.8 setup and presentation

SetupChecklist derives selected-game requirements from the same readiness planner used by Play. FirmwareService validates individual components and bounded ZIP/7z candidates, reads PS2 ROMDIR/ROMVER metadata, and shares deterministic compatible-region selection with launch planning. Import publishes one selected firmware object and removes temporary extraction; PS3 imported updates and installed sessions have distinct states.

DiscMovieReader inventories bounded ISO9660/Joliet/XDVDFS extents; GameMovieDiscovery ranks non-spoiler opening/menu movies. MediaPreviewService gives the verified decoder a local byte-range input, caches a muted short MP4 and still under reader version 5, and preserves manual artwork priority. Added games can use a dim/blurred high-quality cover backdrop while no real cinematic is available. No large disc unpacking or game executable execution is required for these previews.

Achievement entries add optional managed icon/provider-ID/secret metadata without changing old snapshots. Bounded PNG objects are cached by content hash; the native UI caches a dim grayscale variant for locked entries and uses the original for unlocked ones. Hover and keyboard focus expose only provider-supplied visible requirements. Unsupported/missing art retains the trophy fallback; no service polling or invented achievement sets are added. Keyboard summaries are grouped native rows rather than concatenated text.

## 0.3.9 inline input, native achievement setup and application review

Keyboard and controller action rows edit the resolved device map directly. Editing snapshots the current preset into that device's Custom map, retains other actions and imported profiles, and saves atomically through the existing state store. Per-row resets resolve the backend's Default input. Stale detached rows cannot update a different selected edition. Portrait cover fallback crops explicitly around the source center and reuses its crop until the viewport rectangle changes.

AchievementSupport records checked provider IDs and availability for every title/platform combination. AchievementPreferences resolves global and edition tracking choices; native integration defaults on and Hardcore off. NativeRetroClient uses bounded fixed-host POST requests for the official native login/progress protocol. NativeAchievementAccounts holds Windows-protected credentials outside launch plans and creates the exact backend-specific temporary secret format only for an owned launch. A repair or configuration-folder action does not inject credentials. Secrets are scrubbed on exit/failure/sign-out and excluded from backups. Earned cache history merges only within one edition/provider/account/set; hidden details and foreign accounts remain separate. See ADR 005.

XeniaProfiles creates a native offline Account/content header only for fresh managed storage, preserves existing accounts, and records explicit player selection when ambiguous. The official Canary parser accepted a generated player with no game loaded. Snapshot restore rewinds a complete native profile and its selection marker together rather than leaving a marker referencing a removed identity.

The application review adds finite metadata validation, exact scoped save restoration with transactional rollback/hash recheck, duplicate-patch preservation, complete firmware predicates and mutable runtime file isolation. WPF item sources are materialized to avoid dispatcher-held closures retaining a window. Status descriptors, timers, publisher callbacks, preference callbacks and image/asset resources detach or cancel on close. Decoded artwork cache ownership is limited to 48 objects / 64 MiB; active visuals and WPF/GPU allocations are separate. Preview players initialize lazily and remain shared across selection changes. There is no new shipped package or background service.

## Playtime and owned shutdown (0.3.10)

PlaytimeService stores bounded per-edition ledgers in config/playtime, independent of save snapshots. Native totals seed history once; monotonic session deltas checkpoint every 30 seconds only while an emulator is running. Setup and final backup/sync are outside that clock. Damaged records preserve their bytes and can recover the prior atomic file.

MainWindow close confirmation cancels and awaits current work and presentation reads before saving preferences and closing. LaunchService stops owned processes asynchronously, commits time, scrubs credentials and protects saves. Windows jobs cover emulator/extension, conversion and extraction children; unrelated application processes are not adopted. RuntimeService serializes installation and verifies an existing release before fetching its archive; per-game configuration remains separate from the shared immutable build. The Legal tab displays LEGAL.md's anti-piracy policy and preserves license credits.
