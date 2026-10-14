# Baseline design

## Architecture
Three projects: Hub.Core (models/services/adapters), Hub.App (WPF views and thin orchestration), Hub.Tests (deterministic console suite). Versioned JSON catalog is data; atomic JSON state stores user choices. Explicit services handle scanning, downloads/runtime installs, firmware, saves, patches, launch and signed catalog updates. No service container or server.

Storage: config, manifests, runtime, games, saves, firmware, patches, extensions, cache, logs under the portable root. Relative paths are preferred; user-selected external media remains external. Build directories are immutable; user settings and save data are separate from executables. Profile changes are opt-in and preserve prior choices.

## Lifecycle and performance
Network and hashing run asynchronously with progress/cancellation; scanning does not read every multi-gigabyte file merely to list candidates. Fingerprinting occurs on registration. The UI loads cached local catalog/state without network. Downloads stage partial files and publish only after integrity/extraction checks. One game session at a time prevents competing writes to shared save families. Session exit records diagnostics and stops owned helpers only.

## Evidence and limits
Environment inspected: Windows x64, local .NET 10 SDK, no existing source. No game/BIOS assets. Official sources and compatibility reports carry separate confidence/access-date fields. Upstream-recommended configuration is not a locally tested build. No unsigned executable module auto-discovery.

## Rollback
Atomic state replacement with prior version; immutable runtime versions; save snapshot before launch/restore; signed catalog staging and previous catalog retention; patches apply to derived copies or generated configs, never source media.

## Verification
REQ-001: catalog schema/ref validation and resolver tests. REQ-002: synthetic media/firmware fixtures, duplicate and interrupted imports. REQ-003: fake HTTP handler failure matrix and official emulator executable smoke tests. REQ-004: launch plans and harmless child process integration. REQ-005: native app render captures at two sizes and UI-action smoke tests. REQ-006: relocation, rollback, patch integrity, save restore and signature tests. Full gameplay is a separate unfulfilled verification gate, reported as such.
