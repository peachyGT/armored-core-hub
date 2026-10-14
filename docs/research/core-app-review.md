# Core application review — 2026-10-11

Scope: application behavior and local data safety, not gameplay. Reviewed storage, state recovery, game and BIOS import, archive extraction, source-bound patch installation, runtime preparation, process ownership and cancellation, snapshots, catalog/update validation and dependencies. No live Hub data was modified. Catalog membership and the public state schema remain unchanged.

## Fixed findings

| Priority | Finding | Result and evidence |
|---|---|---|
| P1 | Restoring an older snapshot left files created afterward, producing mixed save progress. | Restore stages the verified snapshot, removes only newer files in the relevant save subtree, and retains a complete pre-restore backup. RPCS3 installed game data stays untouched. Regression reproduced before the fix. |
| P1 | Snapshot contents could change after initial verification while the pre-restore backup ran. | Staged bytes must match the original snapshot manifest hash before commit. A deterministic locked-save/watcher test reproduces this race and proves current progress remains intact. |
| P1 | A crafted snapshot relative path could pass a textual RPCS3 save-prefix check and normalize into installed game data. | Reject dot segments before scope checks; reject duplicate canonical destinations. Regression verifies installed data is unchanged. |
| P2 | Valid JSON containing null library collections or nested controls passed deserialization and crashed downstream code. | StateStore validates required structures and calls ControlProfiles.ValidateStored; damaged state is kept and the previous saved state is recovered. Root-owned ControlAuditTests cover nested profile cases. |
| P2 | Reimporting an identical local patch overwrote its payload and reset user-selected sections. | Return a verified existing patch unchanged; a damaged payload is replaced atomically while preserving its valid metadata/options. A locked-payload test proves no unnecessary write occurs. |
| P2 | Configuration-module batches published entries before validating all imported modules, and null module fields caused unhelpful exceptions. | Validate a staged batch, publish only on success, and include the failing metadata path in HubException errors. Tests cover null hashes, editions, dependencies and values; catalog contents stay unchanged. |
| P2 | Imported configuration and local-patch metadata/payload reads were insufficiently bounded. | Read locked streams with explicit 512 KiB module, 128 KiB local descriptor and 16 MiB payload limits. Validate local paths, CRCs, hashes and section metadata before reading payloads. |
| P2 | A clean identical game copy could not repair an existing damaged external registration; touching unchanged media left stale launch timestamps. | Revalidate existing duplicates, relink damaged/moved copies and refresh their integrity metadata. Regression covers both damaged originals and unchanged touched images. |
| P2 | Empty integrity records could report successful verification even for absent media. | Verification rejects empty manifests and absent entry paths. |
| P2 | RPCS3 preparation treated one surviving firmware output as a complete installation. | Reuse the complete two-output firmware check and the edition-aware firmware chooser. A partial-install fixture fails before any emulator process starts. |
| P2 | Shipped mutable runtime files could share hardlinks with pristine runtime storage. | Copy mutable assets into private sessions; only executable/library/debug-symbol code assets may share links. Generated configuration uses cancellation-safe atomic replacement. The isolation test passed on this host before and after the change; the original hardlink risk is conditional on successful Windows hardlink creation, not falsely reported as reproduced here. |
| P3 | Reviewed PS1 MD5 algorithm names were compared case-sensitively. | Match documented algorithm spelling independently of capitalization; synthetic reviewed-reference test verifies the path. |
| P2 | Fresh Xenia storage had no local player profile, so native saves/achievements required an emulator setup dialog. | Source-verified native offline profile setup creates one complete identity only for fresh storage. Existing and ambiguous identities are preserved. See the separate profile-format report and native parser evidence. |

## Verification

- `CoreAuditTests`: 14/14 focused checks passed.
- `XeniaProfileTests`: five default lifecycle checks plus one optional real native parser check passed.
- Combined focused run: **20/20**. Root performs the consolidated application suite and final WPF checks; these counts do not claim exhaustive gameplay coverage.
- First six recovery cases failed against the pre-fix implementation. Additional malformed-module/path/media cases also failed before their guards were added.
- No new package, service, external executable dependency or gameplay launch was added by the review. The optional native check starts a copied official Canary executable with isolated workspace config/storage, no guest title, and closes that owned process.

## Dependency and scope notes

The sole Core package is SharpCompress 0.50.4. GitHub's primary advisory for [CVE-2026-44788 / GHSA-6c8g-7p36-r338](https://github.com/advisories/GHSA-6c8g-7p36-r338) identifies versions below 0.48.0 as affected and 0.48.0 as patched; the current package is outside that stated range. The Hub uses its own bounded path/link checks rather than the advisory's vulnerable directory-extraction API. The initial command sandbox could not reach NuGet; the root's subsequent permitted current advisory query succeeded and reported no vulnerable NuGet packages, including transitive dependencies. Evidence: `work/dependency-audit-0.3.9.json`. A temporary focused runner used command-only `NuGetAudit=false`; repository audit policy was not disabled and final builds retain it. This advisory result does not certify native Windows codecs, emulator code or the absence of all vulnerabilities.

This is targeted regression coverage and source review, not a penetration test or a claim that no defects can exist. Managed launch-time media checks intentionally use recorded size/time for latency; the explicit Verify operation hashes the recorded content. User-imported patches remain user-controlled game modifications. Native tracking, account UI and video rendering are reviewed separately by the agents owning those areas.
