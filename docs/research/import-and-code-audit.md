# Import and code audit — 2026-10-10

Scope: all Core service implementations, native application startup/import/library/presentation/diagnostic paths, manifest generation, and build/portable validation scripts. This is a source review plus focused regression/integration testing, not a guarantee of no vulnerabilities or a complete gameplay certification.

## Findings and repairs

| Priority | Finding | Repair / evidence |
| --- | --- | --- |
| P1 | Import disabled the library, activating the Windows ListBox template's opaque white background. | Owned ListBox template retains its transparent surface. A real modal import regression exercises the disabled owner and subsequent recovery. |
| P1 | PS3 ISO metadata was never read; format filtering hid every PS3 edition. | Read PS3 disc header and PARAM.SFO, cross-check identities, inspect bounded executable/licence prefixes. Catalog declares `ps3-iso`; RPCS3 receives the ISO as one argument. |
| P1 | Scan opened unrelated files looking for Xbox containers and aborted on a Windows Security-blocked DLL. | Probe STFS only for extensionless/`.stfs`/`.000` container candidates during scans; explicit file imports still inspect their signature. Catch errors per entry/folder, continue, and report skipped items. Never disable security software. |
| P1 | Provider redirects were checked only after HttpClient had followed them. | App HTTP client disables automatic redirects; each approved redirect is validated before requesting it. Negative test confirms the unapproved host is never contacted. Hash checks remain mandatory. |
| P1 | RPCS3 custom save mount did not exist on first boot. | Create the managed `dev_hdd0/home/00000001` directory and initial local username before emulator startup; preserve an existing username. Found by the actual AC4 ISO smoke test. |
| P2 | Import forgot the selected-game context and offered an overwhelming edition list. | Detected editions need one confirmation and no dropdown. Unknown copies choose a game, then its compatible editions. A detected AC4 copy overrides a For Answer UI selection. PSP row preferences stay independent. |
| P2 | Encrypted/conflicting/truncated PS3 images could otherwise look ready. | Recognition is independent of readiness. Import blocks unreadable executable data and conflicting IDs with a corrective explanation. No keys are downloaded or guessed. |
| P2 | Folder SFO / CUE metadata had unbounded reads; XEX offset arithmetic could overflow. | Bound metadata size before allocation and use wide offset arithmetic. Tests cover oversized metadata and truncated ISO extents. |
| P2 | Reimport did not repair a corrupt stored firmware object. | Rehash the stored object and replace only with a validated staged copy of the import source. Regression verifies repair. |
| P2 | Cancellation while creating a PS2 card could leave a partial live card. | Stage a complete card, then move atomically; cleanup removes only the stage. Existing cards are untouched. |
| P2 | Helper cleanup errors could leave the session gate acquired. | Nested finally releases the gate and clears running state even after shutdown/logging/cleanup failure. |
| P2 | `NaN` passed numerical scaling comparisons. | Require finite scale values. |
| P2 | Main window and import window had different caption treatments. | Share the native DWM theme helper and original Hub icon. |
| P3 | Diagnostic version strings were fixed at 0.1.0. | Report the built Core assembly version. |

The scanner writes `logs/last-scan.json` with skipped basenames and error codes, avoiding a dump of private folder contents. Existing logs are retained unchanged. Scanning is read-only. Import in place hashes the source and never writes into it.

## Evidence for direct PS3 ISO support

- [RPCS3 System.cpp, pinned commit 304d544b](https://github.com/RPCS3/rpcs3/blob/304d544bc1b3af4f15400a47b34132ff49b1bc69/rpcs3/Emu/System.cpp), accessed 2026-10-10. Boot code detects the ISO path, mounts it, checks disc readability/key status, then resolves its EBOOT internally. **High confidence** for the pinned 0.0.43-20250 build.
- [RPCS3 ISO.cpp at the same commit](https://github.com/RPCS3/rpcs3/blob/304d544bc1b3af4f15400a47b34132ff49b1bc69/rpcs3/Loader/ISO.cpp), accessed 2026-10-10. Readability checks use executable and licence magic bytes independently of identity. Hub performs bounded inspection and delegates emulation to upstream; it does not implement disc decryption. **High confidence**.
- Local user-selected AC4 ISO: structural product identifier BLES00039, PARAM.SFO and SCE executable header readable. The exact file was hashed/imported in place and passed to the pinned RPCS3 executable using the generated Hub configuration. No game bytes, full dump hash, private source path, or emulator device log is committed. This establishes local import/boot-path evidence, not completion/performance/controller certification.
- A second isolated startup after the save-mount repair loaded the ISO, initialized Vulkan and audio, and shut down at the test timeout. The missing-mount error did not recur. Source length and modification time were unchanged. This short run does not establish that a mission, save/load cycle or controller session works.

`minimumBuildsByFormat` is a conservative support floor for this Hub, not a claim that ISO support originated in that version. Older or unparseable builds remain usable for their other supported formats; an ISO asks for the recommended build. The generator preserves this field.

## Review boundaries

Path containment/link rejection, archive traversal/duplicate/size checks, immutable emulator builds, argument-list process execution, source-bound patches, bounded logs, signed RSA-PSS catalog updates, save collision preflight, transactional restore and offline hash caches were reviewed. The existing negative tests remain part of validation.

NuGet advisory queries for App and Core, including transitive dependencies, completed against the official service on 2026-10-10 and returned no reported vulnerable packages. This is advisory coverage at that time, not a proof that every dependency is vulnerability-free.

Remaining: full controller/Narrator/high-contrast certification across Windows versions; long gameplay/save-transfer tests; complete retail reference hashes; encrypted PS3 ISO key import/decryption workflow; UDF-only/nonstandard disc layouts. Unknown or unreadable metadata stays explicit. Game-download sources require established distribution permission; an ownership checkbox is not a source license.

## Native audit assessment

The previous mobile-oriented Impeccable checklist is applied only where meaningful to Windows WPF. No browser detector or mobile HIG score is claimed. The app keeps native caption controls, DPI-independent layout, keyboard focus, reduced-motion fallbacks, and accessible control names. The import's captured 960×540 host state now preserves the accepted A+C composition. The main gap in the previous QA was testing static screens without the real modal/disabled transition; that transition now has a functional regression.

The independent Impeccable finish reviewer returned **ship**, with no material fixes for this import repair. Evidence: `work/audit-confirm-captures/import-disabled-library.png`, `import-ac4.png`, `compact.png` and `small-work-area.png`; 76 core tests and 26 native grouped checks passed. The import capture intentionally shows dialog content rather than native window chrome. These repair captures do not recertify every artwork/video combination.
