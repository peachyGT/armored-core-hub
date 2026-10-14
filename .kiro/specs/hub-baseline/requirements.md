# Hub baseline requirements

## REQ-001 — Catalog and resolution
WHEN the Hub loads a manifest, THE SYSTEM SHALL validate schema version, identifiers, references, supported platforms, and excluded native-Windows games before resolving Edition → Profile → Backend → Build → Enhancements/Patches/Extensions → LaunchPlan.
Acceptance: malformed/cyclic/incompatible manifests fail with actionable errors; different editions/builds remain independent.

## REQ-002 — Media and firmware
WHEN a user scans a selected folder, THE SYSTEM SHALL identify supported structured media, fingerprint content, distinguish exact/recognized/unknown/modified states, avoid duplicate registration, and preserve source files.
Acceptance: serial fixtures, CUE companions, extracted PS3 metadata, unknown revision, CHD/CSO caveats, missing firmware and external paths covered by deterministic tests. No invented hash verification.

## REQ-003 — Runtime acquisition and rollback
WHEN provisioning a runtime, THE SYSTEM SHALL use a permitted provider, stage and validate content, install an immutable version, preserve previous versions, and recover cleanly from interruptions.
Acceptance: checksum corruption, fallback, offline cache, traversal and failed extraction tests. Available upstream versions do not silently replace selections.

## REQ-004 — Launch and configuration
WHEN Play is selected, THE SYSTEM SHALL validate prerequisites, generate an isolated configuration, back up saves, launch with argument arrays, monitor exit, and run declared extension lifecycle phases.
Acceptance: explicit adapters for five families; missing media/firmware/build blocked; no shell-concatenated game arguments; logs redact private paths on copy.

## REQ-005 — User experience
WHEN first opened, THE SYSTEM SHALL show the catalog and an optional collection/setup flow. Normal operations SHALL expose Import, Play, Edition, Enhancements, Controls, Saves, Advanced, and diagnostics in plain language.
Acceptance: three reviewed design alternatives; keyboard and resize captures; real service-backed actions, cancellation and error feedback.

## REQ-006 — Safety, saves and updates
WHEN switching builds/profiles or restoring saves, THE SYSTEM SHALL retain rollback information and a prior save backup. Manifest updates SHALL validate trust and retain pinned working selections.
Acceptance: atomic state recovery, backup/restore, relocation, signed manifest rejection, patch hash/dependency checks and offline tests. Never write to unrelated library content.

## Unchanged constraints
No game downloads, fabricated mouse implementation, accounts, telemetry, cloud service, destructive external writes, external publishing, or copyrighted fixture data. Gameplay verification remains pending until actual media exists.
