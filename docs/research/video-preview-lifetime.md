# Native video preview lifetime — 2026-10-11

## Reason for the replacement

Native WPF checks found that eager `MediaElement` construction retained about 300 Windows handles per window. Lazy creation removed the unopened-window cost, and pausing/hiding/direct source changes reduced it, but repeated actual MP4 changes still retained approximately seven handles per source. Supported WPF public lifetime methods did not produce a plateau. The separate managed-window retention issue was already fixed by materializing the deferred UI option lists.

The replacement uses a WPF Image/WriteableBitmap sink backed by the Windows Media Foundation Source Reader. It adds no package, bundled codec, external player or subprocess. It does not reflect into WPF internals or close Windows handles it does not own.

## Reader ownership and boundaries

`MediaFoundationVideoReader` owns one read-only file lease, MF byte stream, explicitly created source resolver/media source, source reader and temporary COM references. Successful and failed construction release all temporary references before shutting down MF/COM. Disposal flushes the reader, shuts down the owned source, releases them, closes/releases the byte stream and file lease, then balances MFStartup/MFShutdown and CoInitializeEx/CoUninitialize. Windows DLL imports resolve from System32 only.

Input must be an existing absolute file path; URLs and UNC paths are rejected. Sources are bounded to 2 GiB. Native dimensions are limited to 8192 on either axis and 16 million pixels. RGB32 conversion/downscaling uses Windows advanced video processing, normally capped at 960 pixels wide. Native buffer start, length, signed row pitch and every copied row are checked; the returned data is tightly packed, top-down BGRX for WPF `PixelFormats.Bgr32`.

The reader belongs to one physical worker thread. Reading, seeking and disposal cannot hop between async continuations or occur concurrently. Cancellation is checked before and after a synchronous native sample read; an in-flight read completes before teardown. The UI adapter owns the worker, pacing and cancellation. It bounds pending UI work and copies frame pixels before allowing the worker to reuse their array. Pause/inactivity stops frame production. End-of-stream can seek back to zero for looping. No native object relies on an RCW finalizer.

Opaque `MFCreateSourceReaderFromURL` and `MFCreateSourceReaderFromByteStream` factory failure paths showed handle growth when given unsupported input on this PC. Replacing those paths with explicit byte-stream/source-resolver/media-source ownership eliminated the reproduced growth; this is an observed result, not a claim about every possible Windows codec implementation.

## Evidence

The read-only cached clip was the user's 20-second MP4. No game launched and no source media was modified.

- Decoded frame at **7.000 seconds**, **960×540**, stride **3840**; duration **20.00 seconds**. `work/mf-decoder-proof/first-frame.png` was visually checked for orientation and content.
- Initial successful-source proof: four batches of twelve open/seek/read/dispose cycles gave **451, 451, 451, 452 handles**, with **12 threads** throughout. Source SHA-256 remained unchanged.
- Reusable-buffer streaming: **240 frames / 9.96 media seconds decoded in 742 ms** on this PC, allocating **11,520 managed bytes** for frame records rather than a fresh image array per frame. This measures decoding/copying, not the complete WPF rendering pipeline or a hardware-independent performance guarantee.
- Final explicit-ownership failure test: **295 → 296 → 298 handles** across warm-up and two batches of twelve malformed opens, without forced garbage collection. Each failure also released its exclusive source-file lease.
- `MediaFoundationVideoTests`: **4/4 focused checks passed**, covering local/empty input rejection, repeated malformed teardown, native scaling/buffer size/time/seek/cancellation/end/loop/owner-thread guard, and repeated valid disposal/file release. Two native cases are enabled by `HUB_TEST_MEDIAFOUNDATION_VIDEO`; root performs consolidated app and WPF switching checks separately.

Focused native tests require normal desktop process permissions. Tests remain bounded, use the already cached video and write only workspace proof artifacts.

## Primary API references

- [Source Reader processing](https://learn.microsoft.com/en-us/windows/win32/medfound/processing-media-data-with-the-source-reader).
- [Advanced video processing](https://learn.microsoft.com/en-us/windows/win32/medfound/mf-source-reader-enable-advanced-video-processing).
- [MFStartup and matching shutdown requirements](https://learn.microsoft.com/en-us/windows/win32/api/mfapi/nf-mfapi-mfstartup).
- [MFCreateFile](https://learn.microsoft.com/en-us/windows/win32/api/mfapi/nf-mfapi-mfcreatefile), [source resolution](https://learn.microsoft.com/en-us/windows/win32/api/mfidl/nf-mfidl-imfsourceresolver-createobjectfromurl), and [reader creation from an owned media source](https://learn.microsoft.com/en-us/windows/win32/api/mfreadwrite/nf-mfreadwrite-mfcreatesourcereaderfrommediasource).
- [Image stride](https://learn.microsoft.com/en-us/windows/win32/medfound/image-stride), including bottom-up RGB images.
- Microsoft [Windows API declarations](https://github.com/microsoft/win32metadata/tree/main/generation/WinSDK/RecompiledIdlHeaders/um) for GUIDs, native COM interface order and lock/buffer boundaries.
