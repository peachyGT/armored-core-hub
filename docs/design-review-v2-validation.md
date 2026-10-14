# Design review 2: verification

Date: 2026-10-09. Scope is three private browser interaction prototypes, not a native application rebuild or gameplay certification.

## Method

Two independent Impeccable assessments: design_review_a (design/interaction), design_review_b (detector/browser evidence). A finished before B's findings were read by the parent. Each used a separate browser tab. A's original five issues and B's focus issue were fixed in one batch with a bounded follow-up correction. The final reviewer marked all six listed issues resolved. Original scores describe the pre-fix state and were not relabeled as a post-fix audit.

The detector ran once: 83 findings, all intentional differences from the incumbent Archive design system (46 color, 6 font, 24 size, 7 radius). These are not 83 usability defects. The existing DESIGN.md and sidecar describe the native engineering preview; new candidate tokens remain scoped until a direction is selected. No mutable detector overlay was injected because the browser API is read-only. No overlay is claimed.

## Browser evidence

- Libraries A/B/C, grid/list, console filtering, imported-only filtering, empty search and recovery exercised.
- Synthetic PS2 BIOS flow: Add → Find existing BIOS → sample found → Use this BIOS → Play. Shared readiness updates across PS2 titles.
- Edition switching changes backend requirements; sample registration is now keyed to edition so PS3 media does not imply Xbox media.
- Cinematic backdrop changes between AC4 and For Answer. Selected cover is revealed and strip offset survives settings changes.
- At 1280×720, C workspace width is 1280; opened settings pane spans x900–1280. At 960×540, workspace is 960; stage y106–312, cover strip y312–540; selected cover x441–519.
- At 1280×720, A's missing BIOS Add control spans y483.89–519.89, visible without scrolling. Completed setup steps are collapsed and redundant art hidden.
- List rows measured 38px, with 22×29px thumbnails.
- Keyboard Enter into B's game focuses the game H2. Return focuses game-answer and preserves scrollTop 664. Edition, tab buttons and dialog steps retain useful focus; BIOS completion focuses Play.
- Representative normal text/action color pairs measured 5.65:1 through 12.29:1. This is not a blanket accessibility certification; artwork compositing and native assistive technology remain separate work.

## Video and offline package

Downloaded MDN's CC0 flower video as a test fixture into work/design-v2 only. Source: https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4. Documentation: https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/video. No commercial trailer was downloaded.

Browser file-picker selection was exercised. Video reached readyState 4, videoWidth 960, currentTime 2.75, paused=false, muted=true, and a local blob URL. Pause set paused=true; Still image removed the video and restored artwork. No upload or network video playback is implemented. Reduced-motion behavior is implemented but was not separately emulated in this pass.

The offline HTML embeds its images, font, CSS and JS. Images use one Blob URL per asset to avoid repeating large base64 attributes. Packaged smoke check: For Answer image naturalWidth 710, Cinzel loaded, 19 covers, no external application stylesheets. A browser extension generated its own error on an earlier packaging variant; no extension configuration was changed. The final variant avoids the repeated large DOM attributes that triggered that observation.

## Delivery and limits

- outputs/Design-review-v2.html — self-contained review, opens in a browser without the dev server.
- outputs/Design-lab/ — separate files, source assets and four screenshot captures.
- outputs/Design-review-notes.md — user-facing comparison and test summary.

The loopback server on port 8766 was created to present the review to the user, not solely for critique; it remains available during manual review. Its serving root is outputs/Design-lab. The owned exec session is 20659; stopping that session stops this server. Temporary browser viewport overrides were reset.

No native application services, commercial media, firmware or user saves were changed. Gameplay, settings, save and install operations in these prototypes are explicitly synthetic. The baseline Windows executable remains the previous build. Adoption of a final visual direction is pending the user's review.
