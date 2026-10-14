# Third-party notices

The Hub's original code is MIT-licensed. Other components keep their own licenses.

## Included in the portable application

- **Microsoft .NET 10 / WPF runtime:** MIT plus third-party component licenses. Full package license and notices are in third-party/dotnet-LICENSE.txt, third-party/dotnet-ThirdPartyNotices.txt and third-party/WPF-LICENSE.txt. [Upstream](https://github.com/dotnet/runtime).
- **SharpCompress 0.50.4**, Adam Hathcock: MIT. Full upstream license/component notices are in third-party/SharpCompress-LICENSE.txt. [Source at packaged commit](https://github.com/adamhathcock/sharpcompress/tree/c083c6efd843a844b0c8f7878787360e815be781).
- **7zr.exe 26.04**, Igor Pavlov: public domain, reduced standalone console tool from the [LZMA SDK](https://www.7-zip.org/sdk.html). Unmodified official release, exact hash and source/build links are in third-party/7zip/NOTICE.txt. Used only for local 7z decompression; ZIP/RAR and archive metadata validation continue to use SharpCompress.
- **Barlow Semi Condensed**, Jeremy Tribby and contributors: SIL Open Font License 1.1. Full text in third-party/Barlow-OFL.txt and the application assets directory. [Font source](https://github.com/google/fonts/tree/main/ofl/barlowsemicondensed).
- **Cinzel**, Natanael Gama: SIL Open Font License 1.1. Full text in third-party/Cinzel-OFL.txt and the application assets directory. [Font source](https://github.com/google/fonts/tree/main/ofl/cinzel).
- **Xenia profile format research:** the independently implemented offline Account/CON serializer follows the BSD-3-Clause Xenia Canary format declarations. Upstream notice is retained in third-party/Xenia-LICENSE.txt; exact sources and native acceptance evidence are in [the profile report](docs/research/xenia-local-profile.md). No emulator binary is bundled.

## Downloaded only when selected; not bundled

- DuckStation: CC-BY-NC-ND 4.0. Unmodified upstream archives; local post-install configuration. No derivative distribution.
- PCSX2: GPL-3.0. Original upstream license preserved.
- PPSSPP: GPL-2.0-or-later. Original upstream license preserved.
- RPCS3: GPL-2.0. Original upstream license preserved.
- Xenia Canary: BSD-3-Clause. Original upstream license preserved.
- PlayStation 3 system software: Sony proprietary software distributed through Sony's official support infrastructure, subject to its terms. Not relicensed or bundled by the Hub.

Source/release/license links and exact SHA-256 digests are in the manifest and docs/research/runtimes-and-licenses.md.

## Metadata and development tools

Product titles/serials and compatibility conclusions cite DuckStation, PCSX2, PPSSPP, RPCS3, Xenia and preservation metadata in the catalog/research ledger. This is attribution for factual research, not a claim that upstream projects endorse this product.

Impeccable was used during development from [pbakaus/impeccable](https://github.com/pbakaus/impeccable) under Apache-2.0. The full license is in third-party/Impeccable-LICENSE.txt. Its local tool checkout is separate from the published application source; the Hub's MIT license does not replace its license.

No commercial media, BIOS dump, texture pack, community binary patch or copyrighted cover artwork is included in the standard ZIP. The Hub downloads selected covers locally using the sources, hashes and attributions in `manifests/covers.json`. Artwork remains the property of its original owners and is not covered by the Hub's MIT license. Sources: xlenore PSX/PS2 covers, Libretro thumbnails, GameTDB, aldostools Resources, Xenia Manager x360db and LaunchBox Games Database. See [cover source and use notes](docs/research/cover-art.md), including the credit link to [GameTDB](https://www.gametdb.com). Armored Core and associated game names remain their owners' trademarks.

The bundled geometric wing/core application icon (`Assets/Hub.svg` and generated `Hub.ico`) is original project artwork under the project's MIT license. It is used consistently in 0.3.2, including the local preview. The supplied Joshua O'Brien/White Glint emblem was evaluated in the older private 0.3.1 preview; it is not part of this version. See [icon provenance research](docs/research/native-finishing.md).

## Optional preview decoder and format research (0.3.6)

FFmpeg is downloaded only when a supported local game movie needs conversion; it is not in the portable ZIP. The pinned BtbN LGPL shared build retains its archive notices. [Release](https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-10-11-13-04), [build scripts and corresponding source inputs](https://github.com/BtbN/FFmpeg-Builds), [FFmpeg source](https://git.ffmpeg.org/ffmpeg.git), [license](https://ffmpeg.org/legal.html). The manifest records its exact release asset and hash. No generated game preview is redistributed.

The Hub's original bounded achievement/patch readers were informed by Xenia's BSD-licensed XDBF declarations, RPCS3's GPL trophy declarations, the original ApplyPPF file-format implementation, and RetroAchievements API documentation. Their implementations were not copied into the Hub. Links and format decisions are in docs/research/settings-storage-achievements.md. Local user-supplied patches retain their authors' rights; VanLaser payloads are not bundled.
