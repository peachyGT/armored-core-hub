# Native collection review and resolution
Date: 2026-10-09. Target: src/Hub.App/MainWindow.xaml and related native application code.

## Provenance and scoring
Two explicitly authorized sub-agents independently reviewed the earlier native captures and source. Assessment A did not receive Assessment B's findings; B ran the detector once without reading A. Usability was 25/40; native audit was 12/20. These are historical pre-fix review scores, not fresh scores for the current source fingerprint. No post-fix rescoring is claimed.

| Heuristic | Initial score / 4 |
|---|---:|
| Status visibility | 2 |
| Familiar language and concepts | 3 |
| User control | 2 |
| Consistency | 3 |
| Error prevention | 2 |
| Recognition over recall | 3 |
| Efficiency | 2 |
| Visual restraint | 3 |
| Error recovery | 3 |
| Help | 2 |

## Product specificity and strengths
The generation list, adjacent edition choice, explicit imported state and typographic catalog marks are grounded in this franchise. Restrained industrial colors and native controls are coherent. Honest empty states, retained runtime versions and save protection are strengths. Setup and runtime utilities initially looked more generic than the collection.

## Priority findings and resolution
- A1 / P1: The large header hid readiness and tabs. Fixed with a compact header and independently scrolling tab content; confirmed in native captures. Smaller work areas hide decorative title material and retain the task viewport. Impeccable adapt/layout informed the fix.
- A2 / P1: Ten equally weighted setup actions obscured the three-step path. Fixed with actual sections, selected-console guidance and a disclosure for guides and maintenance. Confirmed in the bounded review. Impeccable onboard/distill informed the fix.
- A3 / P1: Profile changes and refreshes could discard advanced edits. Per-profile choices now persist, same-context refreshes preserve drafts, and navigation/filter/close use Save/Discard/Cancel. The follow-up caught the zero-result filter bypass; it is now guarded before changing selection. Impeccable harden informed the fix.
- A4 / P2: Search lacked a visible label and left stale actions after zero results. Added labeling, filtered/imported counts, empty-detail behavior and deferred selected-row scrolling.
- B1 / P2: Status updates had no explicit UI Automation event. Added debounced LiveRegionChanged events.
- B2 / P2: Static theme colors ignored system contrast. Added dynamic semantic resources and paired system window/text colors, including input controls. Actual Windows contrast-theme and screen-reader behavior remains untested.
- B3 / P2: Search boundary was weak. Added a visible label and stronger semantic border.
- B4 / P2: Stale search detail duplicated A4. Fixed as above.
- B5 / P2: Import review silently truncated more than 500 candidates. Now stops with an actionable narrower-folder message; no silent omissions.

## Persona findings
Jordan, a first-time owner, needed the selected console's next setup action rather than a generic toolbox; contextual setup addresses this. Alex, an experienced collector, needed drafts and profile settings preserved; this is now implemented. Sam, a keyboard or assistive-technology user, still needs hands-on focus, Narrator and enlarged-text validation. Source changes alone do not establish accessibility conformance.

## Minor observations and remaining limits
Empty restore controls are disabled. Selected-tab styling no longer changes content text color. Runtime library density, top-navigation current-state semantics and manual controller overrides remain opportunities. No screen-reader, physical controller, actual OS DPI-change or gameplay certification is claimed.

## Detector and confirmation
The web-oriented detector returned exit 0 and an empty JSON array: zero reported primary/advisory findings. WPF source coverage is not established by that result. No browser overlay or live server was used for the native application. Native RenderTargetBitmap captures and source review supplied the relevant evidence. A bounded follow-up confirmed the major layout/setup corrections; later targeted tests cover search recovery and draft preservation. Screenshots do not prove keyboard or contrast-theme behavior.

Questions skipped: the user requested autonomous unattended work and reversible fixes. This overrides the critique's normal question gate. The three working direction prototypes remain available for later human selection. Archive is a reversible implementation default, not a recorded human design approval.
