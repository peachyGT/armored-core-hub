# Archive extraction performance

Access date: 2026-10-11. Scope: local import containers, without changing original game archives.

The previous extractor already used a sequential reader for solid 7z archives, avoiding repeated decompression for each member. Its managed LZMA/LZMA2 decoder was the expensive stage. For Answer is an 8.85 GB LZMA2 archive with one solid block and roughly 14 GB of expanded data. Starting multiple extractions of that block would multiply the work and disk traffic; the Hub keeps one extraction at a time.

The Windows package now includes the official reduced **7zr.exe 26.04**, a 602,624-byte public-domain LZMA SDK tool. Its hash is verified before every launch. It handles 7z LZMA/LZMA2/copy archives with up to two native decoder threads. ZIP, RAR and less common 7z codecs supported by the managed reader retain SharpCompress. Missing native tooling also falls back to the managed reader. No system installation, service, shell extension or PATH lookup is involved.

## Measured comparison

One existing AC3 archive was unpacked by the old managed path and the new guarded native path into separate workspace folders on the same C drive, reading the same source on D. Expanded data was 1,857,880,530 bytes across two files.

| Extraction path | Elapsed time |
| --- | ---: |
| Previous managed decoder | 148.69 seconds |
| Native decoder with Hub preflight and live output checks | 33.56 seconds |

This sample was **4.43 times faster**. Both output files had identical SHA-256 digests. Timings exclude source hashing and later media registration. The second run could benefit from filesystem cache, and other compression methods, file counts, storage devices and background workloads will differ. This is one controlled extraction comparison, not a promised speedup for every archive.

## Safety and diagnostics

The managed metadata preflight still rejects traversal, invalid/reserved Windows names, duplicates, encryption, links, too many entries and excessive declared expanded sizes before native extraction. SevenZipEntry's LinkTarget is always null in the pinned SharpCompress source; the Hub additionally checks stored Windows reparse and Unix special-file attributes. An immutable read handle prevents replacing/changing the archive between that preflight and native extraction on Windows.

The native subprocess receives explicit argument tokens, closed stdin and a dedicated output path. It reads only the user-selected archive. The Hub checks output links, unexpected files, entry count and per-file declared size while extraction runs, then checks exact final sizes and completeness. Cancellation kills only the child started by that import and removes the unpublished staging folder. 7-Zip's own CRC checks must also succeed before publication. The existing expanded-data/free-space budgets, source size/time check, atomic publication and payload hashing remain in place.

The interface now reports separate source-hashing, extraction, output-check and game-identification phases. `logs/last-archive-import.json` records phase durations, extraction engine and errors for the most recent batch, making a slow decoder distinguishable from slow storage or metadata scanning. Up to 1,000 recent batch items are retained in that file.

Regression checks cover a real synthetic 7z, source-preserving cancellation, long staging paths, valid 7z headers carrying Unix symlink/device or Windows reparse attributes, progress phases, existing archive traversal/duplicates and expansion budgets. Fixtures contain no commercial game payload.

## Primary sources

- [7-Zip download page](https://www.7-zip.org/download.html) links the exact [26.04 official standalone release](https://github.com/ip7z/7zip/releases/download/26.04/7zr.exe). A fresh download matched SHA-256 `256feca8e274e5da655e2a284fabafd9f554365eb164862089dacd4e8276d282`.
- [LZMA SDK license and included tools](https://www.7-zip.org/sdk.html) explicitly includes 7zr.exe and grants public-domain use/distribution. The [version-specific source](https://github.com/ip7z/7zip/blob/26.04/C/7zVersion.h) identifies this reduced build as public domain; [build inputs](https://github.com/ip7z/7zip/blob/26.04/CPP/7zip/Bundles/Alone7z/makefile) show its LZMA/LZMA2 decoder and supported filter set.
- [SharpCompress 0.50.4 SevenZipEntry](https://github.com/adamhathcock/sharpcompress/blob/0.50.4/src/SharpCompress/Common/SevenZip/SevenZipEntry.cs) exposes declared sizes and attributes, while returning null for LinkTarget.
