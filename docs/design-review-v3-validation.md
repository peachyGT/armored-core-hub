# Design review 3 verification

2026-10-09. This round refines the user-selected C identity and compares two working browser layouts. The WPF app and gameplay services are unchanged. Synthetic setup/Play actions remain explicitly labeled.

## User evidence and changes

The supplied screenshot disproved the previous review's list-layout confidence: wrapped horizontal lists overlapped, and the selected-game area consumed most of the window. Version 3 replaces that layout with vertically scrolling groups and a bounded scene. The hybrid dedicates a separate column to the selected game above 850px window width.

Both variants group by console, generation, or neither. Console and generation filters are independent; Added narrows the result further. Group buttons collapse sections. Filters and selection preserve useful library positions. Generation membership follows the current catalog; PSP adaptations remain distinct entries within their game's generation.

## Bounded review

A fresh independent Impeccable reviewer received the user's contrary screenshot, the previously favored C screenshot, source, the direction contract, and new captures. It identified three material fixes: poor cover density, lost focus after Finish setup, and the unnamed compact Add button. One correction batch packed groups into two columns when the library is at least 540px wide, repaired focus after making the background inert, and added an explicit accessible name. The final verdict scored all three resolved (ship at that fix-list scope).

The detector ran once. Its command output exceeded the tool response limit; visible findings concerned deliberate differences from the old native DESIGN.md palette and type ramp. This is not a complete automated accessibility certification. No second detector was run.

## Browser checks

| Window | Complete cover entries: C | Complete cover entries: hybrid |
| --- | ---: | ---: |
| 1713×1148 | 11 | 19 |
| 1280×720 | 8 | 9 |
| 960×540 | 8 | 9 |
| 768×540 | 7 | 7 |

Counts describe the initial console-grouped viewport, with full title/status within the scrolling library area. Document width matched viewport width for all eight measurements. Lists use a single vertical track; compact rows measured 42px content height. These cover counts are evidence for this fixture, not a product performance promise.

- Generation 4 returned AC4 and For Answer.
- PSP + Generation 4 returned an explicit empty state; reset recovered the collection.
- PSP + Generation 3 returned four Portable entries; Added reduced this to AC3 Portable in the sample state.
- At 768×540, Finish setup focused the visible BIOS Add button. The compact header retained the Add games accessible name.
- Eleven final captures cover both layouts at four sizes plus compact list, filters, and BIOS setup. Captures were opened and checked; stale-paint captures were discarded and recaptured after confirming the DOM state.
- JavaScript syntax and Git whitespace checks passed. No backend tests were rerun because backend code was unchanged.
- Offline-bundle smoke check found both direction controls, 19 entries, a loaded 710px For Answer image through a local Blob URL, loaded Cinzel, and no external application stylesheets. Image origin metadata scan: three rasters, zero missing. PNG pixels were not changed.

## Artifacts and limitations

Maintained source: design/review-v3. Offline review: outputs/Design-review-v3.html. Split assets and screenshots: outputs/Design-lab-v3. Package with `node scripts/package-design-review.cjs v3` using the private reference assets from the prior output folder.

Existing local-video handling is retained; this round did not repeat the file-picker playback test from review 2. No trailers, games, or BIOS files were downloaded. Games without dedicated background art show a normal-size cover and quiet background rather than an enlarged thumbnail. Reference art remains private review material, excluded from Git and the native package.

The user-facing loopback preview serves only outputs/Design-lab-v3 on 127.0.0.1:8767 (owned exec session 26792). The offline HTML does not require that server. A native implementation and final layout choice remain pending.
