# Native cover and interaction refinement

2026-10-09, version 0.3.2. Impeccable finishing review of the user-approved A+C direction.

Requested changes: restore small-size icon clarity, shorten the resize tab and remove dotted/sticky pointer focus, add cheap hover lift, and replace screenshot cover crops with proper regional/platform covers.

Implemented: original nine-frame Hub icon throughout; 13×28-DIP handle inside the existing seven-DIP divider hit area; explicit keyboard-only focus; three-DIP/140-ms finite lift, with reduced-motion/high-contrast/software fallback; and versioned cover sources with hash validation, offline cache and manual override priority. Covers retain their complete original proportions. No notification-area tray was added.

An independent Impeccable finish reviewer returned **disposition: ship**, with no material fixes. It reviewed native captures and code against the adopted contract. TYPE, MATERIAL, GROUND, splitter, edition artwork and compact layout matched; the original icon and finite lift were accepted adaptations explicitly within the user's request. No separate quality-bar card or WPF detector was available; these limits were disclosed.

Evidence: `work/native-cover-captures` holds 1320/960 layouts, hover, Help, shared-scene tests and an actual HWND capture of a fresh online launch. Core tests: 64/64. Native grouped checks: 24/24. Fresh app cover download: 19/19 verified images in 4.7 seconds on this connection. Speed is an observation, not a product promise. Core tests use synthetic bytes; the native edition-selection tests use generated red/blue images. No commercial game data is used for tests.

The first animation test caught WPF freezing a template transform; the implementation now gives a hovered item its own transform. A fixed transparent pointer target surrounds the moving content so its lower edge remains hittable during the lift. A repeat-test fixture was isolated per run so a previous custom-art test could not contaminate later checks. The confirmed animation ends without a retained animation clock. These fixes do not add idle rendering work or layout animation.

The self-contained 0.3.2 package passes 24 native checks in a clean data root and after relocation, running the packaged .NET runtime. Configuration, appearance and a synthetic save marker survive the move. This is an isolated local test, not a fresh Windows virtual machine or hands-on assistive-device certification.

Outstanding dataset limitations are the 17 explicitly recorded cover fallbacks, not undisclosed substitutions. See [cover provenance](cover-art.md). Gameplay, physical controller and assistive-technology certification remain outside this visual review.
