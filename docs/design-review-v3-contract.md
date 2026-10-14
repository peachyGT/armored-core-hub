# Design review 3

Scope: working desktop browser prototypes, Operate mode. User explicitly selected C's visual identity and requested two choices: improved C and an A/C hybrid. No new identity roll or approval gate is needed before building these requested comparisons.

## Direction contract

THESIS: retain cinematic game selection while making the library usable in short windows. Replace the broken horizontal list with bounded, vertically scrolling groups.

OWN-WORLD: C's dark brown charcoal, ivory actions, Cinzel Roman capitals for identity/game titles, Segoe UI controls, supplied AC4/For Answer photographs. No additional decorative material.

STORY: scan by console or game generation; filter either independently; select a game; play or finish setup. Selection changes the backdrop without losing the library position.

FIRST VIEWPORT: C has a bounded panoramic game area above a grouped grid/list that receives the remaining height. The hybrid places the grouped collection at left and selected-game art/actions at right, stacking only below a narrow-window breakpoint. Play remains visible. Test 1713×1148 (user screenshot), 1280×720, 960×540 and 768×540, including list, filters and settings.

FORM: two precisely requested refinements in the established C world, code-led. Seed: not applicable; user-pinned candidates supersede exploration. Signature interaction: selected game changes the scene while controls and browsing position remain stable. Existing local video support retained.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

Unresolved: human preference between the two structures; native implementation follows that choice. Existing low-resolution cover screenshots are private review assets; they must not be enlarged as cinematic backgrounds for games without dedicated art. Use a quiet fallback instead.
