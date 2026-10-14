# Capability inventory

Current publication: [GitHub repository](https://github.com/peachyGT/armored-core-hub), a Windows preview release and Windows build/test CI are live. User library files, BIOS, credentials, saves and local tools remain outside the uploaded source. The entries below record what was available at each earlier inspection date.
Inspected 2026-10-09. User authorized the engineering/build/test task and sensible local Git checkpoints.

| Capability | Scope / evidence |
|---|---|
| Read/write repository | This project directory; no source pre-existed |
| Create/delete artifacts | work/ for tools, fixtures, downloads; outputs/ for deliverables; delete only validated descendants |
| Git | Local init/checkpoints authorized. No remote configured; no push/publish |
| Package manager | npm cache in work/npm-cache; NuGet cache in work/nuget; command-scoped network escalation |
| Outbound network / GitHub | Browser research available; direct GitHub API and NuGet read access succeeded with escalation |
| Emulator / metadata / patch downloads | Authoritative upstream endpoints, license recorded; no commercial games or proprietary BIOS mirrors |
| Compilers | Local .NET SDK 10.0.401 inspected; self-contained publish may require official runtime packages |
| Processes | Hub, tests, emulator smoke tests and required helpers, scoped to workspace; no system service |
| Browser | Optional local design-review page; no remote publication |
| External library filesystem | Not currently selected. App uses user-selected folders; agent uses fixtures only |
| Native UI verification | App-owned render/export and self-test hooks; native computer-control API unavailable |
| Signing / publishing | No certificate or release destination; not requested, no external release |

Default sandbox DNS failed for api.github.com. Escalated public setup succeeded. Impeccable v4.5.1 / engine 0.1.12 installed using upstream `npx impeccable install --providers=codex --scope=project`. Initial installer rooted at ancestor without a Git root; subsequent installation is in this project. Do not delete ancestor files without checking provenance/authorization.
Final capability check: public Xenia download through the actual Hub pipeline and self-contained publishing succeeded. No approval is pending. No user game-library folder, private upload, external release, or system configuration was used.

2026-10-10 QA expansion: the user authorized read-only testing of supplied media and BIOS in Downloads, native keyboard/gamepad exercise, Universal Modder installation, and unattended operation. Extracted media, disposable saves, emulator builds and captured game-window/process-audio evidence live in `D:\ArmoredCoreHub-QA`, with approved command-scoped access. Original files and existing player saves are not modified. Native Computer Use initialization failed (missing kernel assets), so the inspected Universal Modder owned-window tools are used; foreground checks prevent input into unrelated applications. Physical controller results are recorded only when the user reports them. No external upload, account login, system driver or global security change was performed.

The user explicitly approved Windows UAC for a bounded standalone PresentMon capture. The official executable's SHA-256 was checked before elevation; only the owned Xenia PID was included in the CSV, with input tracking disabled. The first 30-second capture completed and the helper exited. No driver, service or group-membership change was made.

2026-10-11 storage/settings work: turn-scoped permissions granted network access and writes to the existing QA lab, the user's existing Hub folder in Downloads, and D:\ArmoredCoreHub-Storage. Creating the new storage root required a narrowly scoped escalation, which succeeded. The original archives remain read only. The three failed imports are retried into that dedicated root; no files elsewhere are relocated or deleted. Optional FFmpeg came from its pinned public upstream release. No credentials, game files, previews or saves were uploaded.
