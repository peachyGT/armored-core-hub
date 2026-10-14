# Edition cover artwork

Accessed 2026-10-09. Cover manifest: `manifests/covers.json`, schema 1, version 1.0.0.

The Hub fetches cover images into its local, hash-verified cache. The release ZIP contains the source manifest, not these artwork bytes. Cover art remains copyright of its original owners; public availability is not a blanket redistribution license. Source links are available from Artwork and About. No commercial game data is downloaded by this feature.

## Sources and permitted use

| Source | Conclusion | Confidence |
|---|---|---|
| [xlenore PSX covers](https://github.com/xlenore/psx-covers) and [PS2 covers](https://github.com/xlenore/ps2-covers) | Readmes explicitly supply serial-based cover-download URLs for DuckStation and PCSX2. Use unchanged scans for local library identification, with attribution. No blanket relicensing claim. | High for intended downloader use and serial mapping; individual scans still require visual review. |
| [Libretro PSP thumbnails](https://github.com/libretro-thumbnails/Sony_-_PlayStation_Portable) and [thumbnail documentation](https://github.com/libretro-thumbnails/libretro-thumbnails) | Platform-specific box scans intended for frontend display. Full 512-pixel-wide scans retain PSP branding, artwork and rating marks. | High for platform identity; title filenames and front covers checked together. |
| [GameTDB FAQ](https://www.gametdb.com/Main/FAQ) | Explicitly permits database/artwork use in software and requests a credit link. Its public artwork service supplies region-specific PS3 fronts. GameTDB also requests project contact; no message has been sent without user authorization. | High. |
| [aldostools Resources](https://github.com/aldostools/Resources) | Cover resources provided for webMAN/IRISMAN. Used for the Japanese AC4 PS3 scan missing from GameTDB. PSP 192-pixel padded icons were examined and rejected as the default high-quality covers. | High for identity; AC4's 260×300 scan is the smallest accepted source. |
| [Xenia Manager x360db](https://github.com/xenia-manager/x360db) | Metadata/artwork archive explicitly intended for identification and frontend consumption. Maintains original-owner attribution. Xbox Marketplace fronts remain separate from PS3 scans. | High for the inspected fronts. |
| [LaunchBox AC4 Xbox 360 images](https://gamesdb.launchbox-app.com/games/images/6844-armored-core-4) | Publicly categorized Japanese retail front, 500×713. Used for the distinct Japanese AC4 Xbox cover. No claim that the scan is independently licensed for redistribution. | High for title/platform/region identity. |

GitHub URLs are pinned to the inspected commit. Every asset includes SHA-256, original dimensions and byte count. Source files are unaltered; provenance lives in the manifest instead of embedded metadata that would change their verified hashes. Most fronts are 350–512 pixels wide. The 83 source entries total about 10.1 MiB before content deduplication; the initial library downloads only its 19 selected covers. No AI upscaling, screenshot crops, recomposed covers or full-size poster downloads are used.

## Edition handling

All 100 currently cataloged editions resolve to a same-game, same-platform cover. There are 83 directly mapped source entries and 17 explicit fallbacks. A fallback's reason is retained in the manifest and shown in Artwork. It never affects game identification, region selection or emulator settings.

- US/EU/JP scans and available reissue covers are mapped by serial, not by fuzzy title matching.
- PS2 and PSP art never substitutes across platforms. PSP choices persist independently from the original console edition.
- AC3, Silent Line and Last Raven's US digital PSP entries, plus Silent Line EU, use the detailed Japanese PSP retail scan. Suitable physical-release scans for these digital editions were not found; small store icons were poorer display assets. These are explicitly marked fallbacks, not claimed exact regional packaging.
- Japanese Xbox For Answer currently uses the US Xbox cover, explicitly noted. Its distinct Japanese scan remains a dataset gap.
- Several Asian releases, trials and PSone/PS2 reissues use the available original release on the same platform. The 17 exceptions are enumerated by `fallbackEditionId` in the manifest.
- The unadded-game scene retains a small cover; a cover is never promoted into a stretched cinematic background.
- Manual covers, backgrounds, logos and videos remain separate. Only the old locally seeded `collection-reference.png` crops are ignored in favor of the new covers; those files and custom backgrounds are left intact.

The PSP regional availability is corroborated by [FromSoftware's product listing](https://www.fromsoftware.jp/ww/detail.html?csm=077) and the [LaunchBox AC3 Portable](https://gamesdb.launchbox-app.com/games/images/73336-armored-core-3-portable), [Silent Line Portable](https://gamesdb.launchbox-app.com/games/images/65752-armored-core-silent-line-portable) and [Last Raven Portable](https://gamesdb.launchbox-app.com/games/images/28069-armored-core-last-raven-portable) image inventories. Fan-made English boxes were not misrepresented as retail covers.

## Research uncertainty carried forward

x360db uses `465307DF` for Japanese AC V artwork and `465307E3` for Japanese Verdict Day artwork. Its E3 title text misleadingly says V, while the media record and inspected front say Verdict Day. These source paths are artwork provenance, not confirmation of this Hub's older shared-region title-ID assumptions. Correcting media recognition requires separate disc/executable evidence; no title-ID detection logic changed in this visual pass.

## Verification

Downloaded fronts were decoded and inspected in contact sheets. Tests cover all edition mappings, cross-platform rejection, regional fallback explanations, unapproved hosts, cyclic mappings, deduplication, corruption rejection and relocated offline caches. Native fixture checks exercise edition changes in both panes, manual-cover priority and independent PSP selection. Native image rendering preserves aspect ratios and never crops away rating or platform labels.

For updates, edit this artwork manifest separately from compatibility profiles and retain prior cache objects. A changed remote file fails its pinned hash; the Hub keeps a quiet fallback and offers Download covers under Artwork. Offline mode cancels outstanding cover work and uses already cached assets. Automatic fetching can be disabled in Settings.
