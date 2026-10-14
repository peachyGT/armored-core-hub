# Validation — Armored Core Hub 0.1.0
Date: 2026-10-09. Windows x64. Local .NET SDK 10.0.401 / bundled runtime 10.0.12.

## Measured results
| Check | Result |
|---|---|
| Release build | Zero warnings and errors |
| Automated service suite | 53 / 53 passed |
| Native UI assertions | 6 / 6 passed |
| Original and relocated self-contained app | Both passed; coreclr.dll loaded from each package folder |
| Configuration and save-marker preservation | Passed across directory move |
| DuckStation | Pinned archive extraction and version command passed |
| PCSX2 | Version command and isolated generated settings initialization passed |
| PPSSPP | Portable window/config startup and selected memory-stick initialization passed |
| RPCS3 | CLI and official firmware installation passed with the documented teardown workaround |
| Xenia Canary | CLI, actual upstream download, SHA-256 verification and offline reuse passed |
| Synthetic executable lifecycle | Successful/nonzero exits, process state, and before/after save bytes verified |

The portable validation measured approximately 2.3 seconds for the entire first self-test process and 1.9 seconds after relocation, including six assertions and multiple render exports. These are not startup benchmarks. The test ran on this machine in isolated folders, not a fresh Windows virtual machine.

## Coverage
Manifest schema/references/exclusions, numeric version comparison, structural ISO/SFO/XEX identification, malformed metadata, CUE dependencies, duplicate imports, managed copies, moved external media, unknown/modified/exact hash states, hash format scope, missing BIOS and missing registered BIOS repair, patch dependencies/incompatibilities, traversal/archives, checksum corruption, interrupted/cancelled download and import, offline cache, immutable builds, save collisions/cancellation/restore verification, signed updates/rollback, launch arguments/resolution, damaged session repair, Windows file locks, controlled process lifecycle and controller-override boundaries.

UI checks cover catalog population, For Answer search, independent PS3/Xbox editions, persisted offline choice with advanced-draft preservation, empty-search recovery and a 960×540 DIP work area. Native captures use WPF RenderTargetBitmap. They do not prove keyboard navigation, screen-reader announcements, physical controller behavior or actual display-scale/theme transitions.

## Limitations
No commercial game, owned BIOS, actual title update or gameplay rendering was exercised. No live multiplayer or original save-transfer semantics are certified. Upstream compatibility claims retain their provenance and uncertainty. The captured RPCS3 build writes complete firmware and a fresh success log, then exits 0xC0000409 during headless teardown; only that narrow case is accepted. Other failures remain failures.

Transient Windows access/sharing errors occurred while moving newly written test directories. The service now uses bounded retries only for those errors when the source remains and the destination does not exist. A held-file regression and the full lifecycle passed afterward.

## Reproduce
Run scripts/build.ps1 -Test -Publish. Run scripts/validate-portable.ps1 for isolated startup/relocation. Run scripts/package.ps1 to build/test/validate and archive a clean output folder. Real-runtime integration requires the exact upstream archives and official Sony PUP described in README.md; no game images are required. Test-only --runtime-fixture exists only in Hub.Tests, not the Hub app.

Machine-readable results and a native preview are delivered beside the ZIP. BLOCKED.md and docs/feature-matrix.md distinguish working behavior from dataset gaps and deferred features.
Archive verification compared all 434 ZIP file entries byte-for-byte (SHA-256) against the published folder and checked that managed user-data directories were absent. The ZIP contains no emulator executables, game images, BIOS or Sony firmware.
