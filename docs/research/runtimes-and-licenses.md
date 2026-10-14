# Runtime, firmware and license evidence

Accessed 2026-10-09. Confidence high for upstream release/license metadata; none of these pins is a gameplay certification.

| Backend | Captured release | License / authoritative source |
|---|---|---|
| DuckStation | Rolling archive, executable 0.1-12074-g697599c47 | [CC-BY-NC-ND 4.0](https://github.com/stenzek/duckstation/blob/master/LICENSE), [release](https://github.com/stenzek/duckstation/releases/tag/latest) |
| PCSX2 | v2.8.2 | [GPLv3](https://github.com/PCSX2/pcsx2/blob/master/COPYING.GPLv3), [releases](https://github.com/PCSX2/pcsx2/releases) |
| PPSSPP | v1.20.4 | [GPLv2-or-later](https://github.com/hrydgard/ppsspp/blob/master/LICENSE.TXT), [release](https://github.com/hrydgard/ppsspp/releases/tag/v1.20.4) |
| RPCS3 | 0.0.43-20250-304d544b | [GPLv2](https://github.com/RPCS3/rpcs3/blob/master/LICENSE), [binary releases](https://github.com/RPCS3/rpcs3-binaries-win/releases) |
| Xenia Canary | 828014b | [BSD 3-Clause](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/LICENSE), [releases](https://github.com/xenia-canary/xenia-canary/releases) |

Exact upstream URLs, asset names and GitHub-published SHA-256 digests are in catalog backend records. Every archive was independently hashed before integration testing. Original archives retain license files. Nothing here implies third-party trademarks belong to the Hub.

DuckStation's [README distribution guidance](https://github.com/stenzek/duckstation) treats preconfigured packages differently from unmodified binaries. The Hub does not bundle it, alter its executable, or distribute generated emulator configuration as part of an emulator package. The user downloads upstream and configuration is generated locally after installation.

## Firmware

[Sony's PS3 support page](https://www.playstation.com/en-us/support/hardware/ps3/system-software/) is the authority for the official PUP. The captured 4.93 update has SHA-256 158471fd834f8ea8036136b6aab43cd86c7ba73d79ca30e0af3c0fe0001cf365 and MD5 a2b60b6ac1d2e49e230144345616927c. The MD5 agrees with the official URL's identifier. No firmware bytes are shipped in the Hub.

[DuckStation BIOS source](https://github.com/stenzek/duckstation/blob/master/src/core/bios.cpp) supplies the small reference BIOS hash set. Only recognized owned dumps are imported automatically. [PCSX2 setup documentation](https://pcsx2.net/docs/usage/setup/) requires a legitimately dumped PS2 BIOS. PS2 ROMDIR/ROMVER recognition establishes structure/region/version, not an exact known dump; UI says unverified.

No community mirror for proprietary BIOS is configured. Asset providers cannot request commercial game media. Future permitted mirrors require explicit provenance and integrity.

## Bundled application dependencies

WPF/.NET 10: [Microsoft .NET license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT), with runtime third-party notices retained.

SharpCompress 0.50.4: [MIT and component notices](https://github.com/adamhathcock/sharpcompress/blob/master/LICENSE.txt). Sequential extraction follows [the archive API guidance](https://github.com/adamhathcock/sharpcompress/blob/master/src/SharpCompress/Archives/IArchive.cs).

Barlow Semi Condensed: [SIL Open Font License](https://github.com/google/fonts/blob/main/ofl/barlowsemicondensed/OFL.txt). The original font and full OFL text are included. No cover-art library or texture pack was redistributed.
