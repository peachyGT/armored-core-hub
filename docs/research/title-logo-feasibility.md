# Title-logo feasibility

Access date: 2026-10-11. This is a bounded audit of official web assets, current presentation code and standard local media slots.

The [official Armored Core lineup](https://www.armoredcore.net/lineup.html) contains the 19 console/Portable entries represented by the Hub, alongside mobile releases. Its per-game images use JPG/WebP artwork, rather than transparent title logos. The [official AC4 product page](https://www.fromsoftware.jp/jp/detail.html?csm=054) loads product/package screenshots and text titles through its [public detail script](https://www.fromsoftware.jp/jp/static/js/products/detail.js). Those inspected references also point to opaque package, screenshot and promotional JPGs. The old official For Answer site route was unavailable during this audit.

No practical official source for a complete, consistent set of transparent title logos was identified. This is not a claim that no individual logo exists elsewhere. Importing an arbitrary web image or cropping a cover would provide uneven quality and unreliable edition matching.

PS3/PSP PIC0 is a menu overlay slot; it may contain marketing text rather than the game's title. The Hub therefore no longer treats PIC0 as a title logo automatically. A quick local search of extracted media did not reveal independently named title/logo files usable for this purpose; decoding title-screen textures inside game-specific containers is a separate project.

The retained path is practical and lightweight: an explicitly supplied transparent `logo.png` or Artwork → Logo image replaces the two-line text title. The title remains accessible through the game labels and returns when no valid logo exists. The corrected display path also works for games not yet added. No website artwork or extracted commercial texture is bundled with the Hub.
