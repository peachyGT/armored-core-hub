# Native UI resource and lifecycle review — 2026-10-11

Scope: the Hub's WPF UI, artwork/preview selection, settings drafts, callbacks and closing. No emulators or games were launched. Native fixtures use offline state in `work/ui-resource-native`; source game media and the live Hub configuration were unchanged. Root performs the consolidated visual review separately.

## Concrete fixes

- Materialized combo-box item queries. A private `dotnet-dump gcroot` capture proved a closed window was held by `DataBindEngine → ViewManager → ViewTable → EnumerableCollectionView → ProfileBox LINQ iterator → predicate → MainWindow`. Creating arrays preserves choices/order and removes the captured window. Twelve subsequently shown/minimized/closed fixture windows collected.
- Removed the status dependency-property descriptor callback, announcement timer and publisher handlers on `Closed`. Static preference handlers detach; posted callbacks check shutdown/closed state. The shared HTTP client now disposes once on `Closed`.
- Artwork reads cancel when selection changes or the window closes. UI waits detach on cancellation, stale completions cannot apply to another selection, and completed read tasks leave the in-flight map. Extracted-asset results are bounded.
- Artwork lookup entries use weak references; the decoded-image LRU owns at most 48 entries and 64 MiB. Both decode dimensions are bounded to 1800. Stream-based `OnLoad` decoding avoids the global URI cache and releases file handles. This limits **cache ownership**, excluding images held by active rows/scenes and native/GPU allocations.
- Immutable palette brushes and icons are frozen. This avoids unnecessary inheritance contexts; the measured closed-window retaining path was the enumerable view, not these brushes.
- Advanced graphics drafts survive ordinary detail refreshes and participate in save/discard on navigation/closing. Edited fields validate before saving; failed disk writes restore prior in-memory values. Graphics/raw advanced saves preserve independent edits and synchronize their baselines. Local patch toggles, analog metadata and PNACH sections refresh action labels/control summaries without discarding unrelated graphics drafts.

## Cinematic playback

An unused WPF `MediaElement` added roughly 300 native handles per instance on this PC. Public `Close`, `Source=null`, forced open/close, garbage collection and UI pumping did not free that growth. Lazy construction and direct source changes helped, but the real MP4 still added approximately seven handles per changed URI after warm-up: three batches of eight switches grew `3245 → 3333 → 3390 → 3447`; after ten seconds idle, `3399` remained. Native sampling isolated retained Key/Mutant/Section/Event/WaitCompletionPacket handles. The managed heap contained only one `MediaPlayerState`.

The production scene now uses Windows Media Foundation decoding and a WPF `Image`, bypassing the WMP playback graph. There are no new packages or shipped binaries. One synchronous MTA worker owns/releases the reader, reuses its pixel buffer only after the UI copy, and blocks on an event while paused. It queues at most one frame; source **and seek** revisions are checked inside dispatcher publication. Closing cancels without joining the worker on the UI thread. Read failure preserves still artwork; the same failed URI is not retried on every refresh.

Motion and video preferences gate construction. Switching to a still pauses/hides the prepared video; allowed previews reuse the display. The adapter preserves position/seek, loop, play/pause and failure events for the existing scene checks. Output is muted by design because the decoder has no audio graph.

Microsoft source references: [AVElementHelper](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/AVElementHelper.cs), [MediaPlayerState](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Media/MediaPlayerState.cs), and [dotnet-dump guidance](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump).

## Reproduction and measured evidence

Build using the workspace SDK and restored packages:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD 'work/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD 'work/nuget'
$env:DOTNET_ROOT = Join-Path $PWD 'work/sdk'
& work/sdk/dotnet.exe build src/Hub.App -c Release --no-restore --nologo
$env:HUB_TEST_PREVIEW = '<absolute path to a local, cached MP4>'
& src/Hub.App/bin/Release/net10.0-windows/ArmoredCoreHub.exe `
  --root '<absolute path to an offline fixture containing manifests>' --resource-self-test
```

This writes `logs/resource-self-test.json`, `logs/ui-resource-check.json` and `logs/ui-video-reuse-check.json` under the chosen fixture root. `--resource-dump` adds a diagnostic pause after completion; only the private fixture process was captured. Diagnostic tool `dotnet-dump` 9.0.661903 was installed locally from official NuGet, publisher Microsoft, repository `dotnet/diagnostics`, with no shipped dependency.

The first successful MF-adapter run recorded:

| Check | Result |
| --- | --- |
| 80 small and 8 large artwork replacements | Cache within 48 entries/64 MiB; evicted image collected; source file reopened exclusively |
| 100×4000 image | Decoded dimensions bounded to 1800 |
| 12 closed Hub windows | 0 retained; handles `2525 → 2525 → 2525` across two batches |
| Managed heap across those windows | 10.8 → 11.9 MB approximately |
| Private bytes across those windows | 167.9 → 206.3 MB; WPF/native pooling is not a total-memory ceiling |
| Real MP4, 24 different-URI switches | Handles `2771 → 2776 → 2775 → 2775`, then `2769` after idle |
| Graphics drafts | Refresh preservation, invalid-value atomic rejection, save/discard and independent raw-editor synchronization passed |

Private evidence is under `work/ui-resource-native/logs/` and `work/ui-resource-other-gcroot.txt`. Native OS handle snapshots are `work/ui-handle-*.json`; they enumerate only the owned fixture PID.

### Final payload and failure-path validation

The `20:56:44` published 0.3.9 payload (App DLL SHA-256 `e8c39725f2cd9c622f8e46d86773b883247433b6a254c1f6d52de136e5b328c1`) passed the same resource suite using a new offline root, `work/ui-resource-published`. Its 12 windows collected and held `2524 → 2524 → 2524` handles; 24 MP4 source switches held `2786 → 2790 → 2791 → 2791`, settling to `2785`. The run used the exact cached MP4 shown above, via `HUB_TEST_PREVIEW`, and did not run screenshots or gameplay.

The subsequent source build added a focused 12-malformed-preview adapter fixture and passed it: handles `2786 → 2664 → 2664 → 2664`, clean decoder/worker closure, visible small-cover fallback and no automatic same-URI retry. Evidence: `work/ui-resource-native/logs/ui-video-failure-check.json`. Core's opaque URL factory was replaced by explicitly owned byte stream/source resolver/media source objects, so native cleanup also covers creation failures. Core's four reader checks passed, including 24 malformed opens (`295 → 296 → 298` handles without forced GC) and seek/end/reuse/cancellation/thread ownership. A 240-frame reusable-buffer proof decoded in 742 ms with 11,520 managed bytes allocated.

Root republished at `21:01` with the additional malformed fixture and a separate compact achievement-account layout adjustment; those changes do not alter decoder behavior. The final payload's fresh/relocated full native confirmation is tracked by the root validation report rather than repeating this resource suite solely for that layout adjustment.

## Limits

These are bounded behavioral/resource fixtures, not a certification that all WPF, GPU or codec resources never leak. Active artwork references and OS/native decoder pools can exceed the cache's own 64 MiB. Media Foundation needs the Windows media components/codecs available on the host; unsupported or malformed previews fall back to stills. No rendering-tier-zero pixel/performance certification was performed; hover animations already respect hardware tier and Windows motion preferences.
