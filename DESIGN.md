---
name: Armored Core Hub
description: An organized native Windows collection beside a cinematic selected-game scene
colors:
  ground: "#151310"
  surface: "#24221E"
  raised: "#38362F"
  ink: "#F1EEE4"
  muted: "#C1BDB1"
  rule: "#49473F"
  accent: "#EBE5D3"
  good: "#B9CCA7"
typography:
  display:
    fontFamily: "Cinzel"
    fontSize: "38px"
    fontWeight: 400
    lineHeight: "45px"
  display-compact:
    fontFamily: "Cinzel"
    fontSize: "28px"
    fontWeight: 400
    lineHeight: "34px"
  subtitle:
    fontFamily: "Cinzel"
    fontSize: "29px"
    fontWeight: 400
    lineHeight: "35px"
  subtitle-compact:
    fontFamily: "Cinzel"
    fontSize: "21px"
    fontWeight: 400
    lineHeight: "27px"
  wordmark:
    fontFamily: "Cinzel"
    fontSize: "21px"
    fontWeight: 600
  title:
    fontFamily: "Segoe UI"
    fontSize: "22px"
    fontWeight: 600
  body:
    fontFamily: "Segoe UI"
    fontSize: "14px"
    fontWeight: 400
  supporting:
    fontFamily: "Segoe UI"
    fontSize: "14px"
    fontWeight: 400
    lineHeight: "21px"
  label:
    fontFamily: "Segoe UI"
    fontSize: "12px"
    fontWeight: 400
  metadata:
    fontFamily: "Segoe UI"
    fontSize: "11px"
    fontWeight: 400
  configuration:
    fontFamily: "Consolas"
    fontSize: "12px"
    fontWeight: 400
rounded:
  control: "0px"
spacing:
  small: "8px"
  medium: "16px"
  frame: "22px"
  large: "28px"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.ground}"
    rounded: "{rounded.control}"
    padding: "9px 16px"
  button-secondary:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
    padding: "9px 16px"
  button-hover:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.ink}"
  button-compact:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
    padding: "7px 9px"
  text-field:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    padding: "8px 10px"
  select-field:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    padding: "6px 30px 6px 10px"
  navigation:
    backgroundColor: "transparent"
    textColor: "{colors.muted}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "11px 0px"
  library-entry-selected:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.ink}"
    padding: "2px"
  cover-frame:
    backgroundColor: "{colors.surface}"
    width: "108px"
    height: "142.992px"
  cover-frame-compact:
    backgroundColor: "{colors.surface}"
    width: "96px"
    height: "127.104px"
  settings-tab:
    backgroundColor: "transparent"
    textColor: "{colors.muted}"
    typography: "{typography.body}"
    padding: "12px 10px"
  help-disclosure:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    padding: "10px 12px"
  divider-handle:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.control}"
    width: "13px"
    height: "28px"
---

# Design System: Armored Core Hub

## Overview

**Creative North Star: "Adopted cinematic library"**

An organized collection sits beside a cinematic selected-game scene. Warm charcoal, ivory controls, and Roman lettering give the artwork room without separating the player from the next useful action. The player controls how much room the collection receives.

This is the native Windows WPF system adopted after the user's A+C selection, refined for 0.3.0, and refined for 0.3.2 on 2026-10-09. The frontmatter records current native resources and repeated component values; its portable `px` notation represents WPF device-independent units, not physical display pixels. The prior Archive engineering preview and `design/review-v2` / `design/review-v3` browser comparisons are historical references. The approved refinement supersedes the inset feathered rectangle with a shared full-height backdrop, adds native muted video, and retains the side-by-side native layout. The finishing pass adds readable disclosure, finite hover feedback, a narrow visible divider, native caption colors, and an original application icon without changing the artwork composition or type hierarchy.

Ground truth is `src/Hub.App/App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.Library.cs`, `MainWindow.Presentation.cs`, `MainWindow.Chrome.cs`, and `LibraryAppearance.cs`. The current direction is in `.impeccable/surfaces/src-hub-app-mainwindow-xaml.md`; the earlier adoption contract records its history. Current captures are under `work/native-cover-captures`; the earlier `work/native-refined-confirmation` captures retain the unadded-cover evidence. Native caption verification uses successful DWM set results and an actual window capture, not unsupported color-attribute readback. Captures document visible states, not codec or gameplay verification. PRODUCT.md owns product and rights constraints; `docs/research/native-finishing.md` records chrome research and icon provenance.

**Key Characteristics:**
- An adjustable full-height collection beside the selected game.
- Cinzel identity and Segoe UI controls on warm charcoal and ivory.
- A shared cinematic backdrop beneath a translucent library, with a left-edge fade and cover-derived ambient color.
- Small covers for unadded games; split Cinzel titles or a usable local transparent logo.
- Written readiness and a contextual next action over a protected dark title area.
- Windows high contrast replaces brand brushes and suppresses image and tint layers.
- Native window controls, a distinct original icon, and brief feedback that settles at rest.

## Colors

The palette is warm, subdued, and tonally layered; the brightest neutral carries actions and focus.

### Primary
- **Ivory accent:** primary actions, selection outlines, and keyboard focus. Primary button text uses Ground; its hover switches to Raised with Ink text.

### Secondary
- **Readiness green:** Good accompanies the written “Ready to play” state; it never carries status alone.

### Neutral
- **Charcoal ground:** the application, scene floor, settings drawer, and Windows 11 native caption.
- **Warm surface:** buttons, inputs, cover fallbacks, and hover surfaces.
- **Raised charcoal:** selected library entries, view choices, and hover controls.
- **Warm ivory ink:** readable primary content, the wordmark, and Windows 11 caption text.
- **Muted stone:** metadata, secondary copy, group labels, and unselected tabs.
- **Stone rule:** dividers, borders, and scrollbar thumbs.

**The Foreground First Rule.** Sampled artwork color belongs behind content; semantic brushes and readable foreground contrast remain independent of the selected image.

**The System Contrast Rule.** Windows high contrast replaces the brand brushes with system window and text brushes and hides artwork, tint, and scene fade layers.

Ambient color is sampled from the available cover, not assigned per game as a fixed palette token. The shared radial brush uses alpha 57 of 255; a separate library glow uses 32 of 255. Settings → Appearance can disable both. The sampler ignores nearly white, deep black, transparent, and low-chroma pixels before choosing a hue-weighted color; missing usable color falls back to the neutral defined in `LibraryArtwork`.

## Typography

**Display Font:** Cinzel, loaded from the packaged font resource.
**Body Font:** Segoe UI for controls, task headings, and metadata.
**Configuration Font:** Consolas for editable advanced values only.

**Character:** Roman lettering provides the wordmark and selected-game identity. Plain Windows typography handles tasks and dense collection information. Cinzel and its SIL Open Font License ship in the application assets; the bundled historical Barlow font is not the current display voice.

### Hierarchy
- **Display / Subtitle:** explicit title metadata splits the main title from its subtitle into two Cinzel lines. Both use their compact variants below 650 DIPs of window height or when the scene is narrower than 480 DIPs. A game's validated wide transparent logo may replace both lines; absent or unsuitable logos retain text. The title is not inferred by splitting an arbitrary name.
- **Wordmark:** semibold Cinzel; the small “H U B” line is Cinzel at 10 DIPs beneath a thin rule.
- **Title:** the game-settings heading uses the title token. The library heading is 19 DIPs semibold; task subsection headings remain Segoe UI at 24 DIPs.
- **Body / Supporting:** standard control text and wrapped supporting explanations; only the supporting style sets an explicit line height.
- **Label / Metadata:** scene metadata and search labels use the label role; readiness and collection counts use metadata. Grid titles are 12 DIPs semibold, and list titles are 13 DIPs semibold.
- **Configuration:** compact monospace input in Advanced, not the normal play path.

**The Two Voices Rule.** Use Cinzel for identity and selected-game display; use Segoe UI for actions, task structure, and collection reading.

## Layout

The native window starts at 1320 × 880 DIPs and permits a minimum of 820 × 500. The header is 68 DIPs high and the footer 34. The collection and scene occupy the remaining height with a 7-DIP divider hit area and a 1-DIP visible line; the native layout stays side by side at compact sizes.

The normal library fraction defaults to 0.36, with the scene taking the remainder. Dragging the divider records the normal fraction; normalized preferences bound it to 0.22–0.68, and layout also enforces a 280-DIP library minimum and 320-DIP scene minimum. Collapse replaces the library with a 48-DIP restore rail. Expand requests 0.68 of the workspace subject to the scene minimum. Restoring normal mode uses the remembered fraction; double-clicking the divider or Reset library width restores 0.36. The divider is focusable and declares 16-DIP keyboard increments.

The library scrolls independently. Grid cover width is 96 DIPs below a 330-DIP library width and 108 DIPs otherwise; height is width × 1.324. List entries use a 34 × 42 DIP image area. Grouping is Console or Generation. Console, generation, added-only, and title-search filters intersect; Clear resets them together. Native group headings are text, not disclosure controls. The filter panel is initially collapsed and includes the on-demand search. Empty results show a recovery message and hide selected-game actions.

Added-game backgrounds share one full-height frame behind the library and scene. The frame begins at `max(0, library width × 0.28 − 55)` DIPs and reaches the workspace's right edge; image and video both use UniformToFill in that same frame. Its left-edge opacity mask falls beneath the library. There is no inset border, top mask, or right vignette. The library adds a translucent Ground wash above the backdrop.

Unadded games show only a small fitted cover near the scene's upper left, even when a custom background exists. Added games without a background or video use that same cover fallback. Its height is 26% of the workspace height, clamped to 80–174 DIPs; width is 75% of that height. The scene title and actions remain bottom aligned. Below 650 DIPs of window height, their bottom margin reduces from 36 to 20 DIPs. The title shade height follows the action block, with a minimum of 160 DIPs.

Game settings overlays the scene column and scrolls its tab content independently. Opening it disables the underlying scene actions and Artwork button, then focuses Close; closing restores focus to Game settings. The library remains visible. Appearance preferences live in portable `config/appearance.json`, separately from game and emulator configuration.

## Elevation & Depth

Depth comes from tonal surfaces and the shared photographic backdrop, with no ornamental container shadows. A full-workspace duplicate background is blurred at radius 48 with opacity 0.38. The main image/video frame fades only along its left edge: transparent at the edge, alpha 191 of 255 at 12%, and opaque from 28%. Above it, the library uses Ground at alpha 193 of 255. A full-workspace scene shade deepens toward the bottom; a separate lower title shade moves from transparent through alpha 201 of 255 at 45% to opaque Ground at its bottom.

**The Protected Title Rule.** Layer the lower scene fade and title shade beneath title and actions; artwork brightness must not determine action legibility.

Native background video is muted and loops in the same frame as still artwork. Pause/Resume background and the Appearance video preference give the player control. Playback pauses while the window is inactive or minimized, outside the collection, or while game settings is open. Disabling Windows animations suppresses the video layer entirely, revealing the available still artwork; changes to that preference apply live. High contrast also suppresses video. Windows codecs determine which local videos can play; failure hides the player, restores still artwork or the small cover, and writes an explanatory status. The implementation adds no image entrance animation. Native dialogs provide focus protection for file selection and import review.

Library hover lifts an item 3 DIPs over 140 ms and fades the item's Surface wash and Muted border opacity over 120 ms with WPF ExponentialEase, exponent 4, EaseOut. Completed clocks are removed; there is no repeating animation, idle timer, layout animation, new image blur, or zoom. Reduced motion, high contrast, and WPF software rendering use an immediate highlight without displacement.

**The Settled Feedback Rule.** Hover feedback ends at its resting state; use the immediate state when Windows motion, contrast, or rendering capabilities call for it.

## Shapes

Buttons use square corners from the control token, matching covers, collection entries, fields, tabs, and the settings drawer. Thin rules distinguish records and regions. Library selection uses an ivory outline; keyboard focus uses a brighter Ink outline. Icons are explicit WPF paths with accessible names, not font glyphs or decorative military emblems.

The bundled application mark is an original three-part geometric wing/core silhouette in Ivory accent with a Ground outline. `Assets/Hub.svg` is its vector source; the ICO contains 16, 20, 24, 32, 40, 48, 64, 128, and 256-pixel frames. It is separate from the supplied White Glint emblem, whose ivory-on-charcoal adaptation remains private and is not a distributed identity asset.

## Components

### Buttons

Quiet square surfaces make the next action easy to find. Standard buttons use a 1-DIP rule border and a minimum height of 38 DIPs; primary buttons use the accent fill and semibold type. Both switch to Raised and Ink on hover. Keyboard focus thickens the accent border to 2 DIPs; disabled buttons use opacity 0.42. Compact buttons use a 34-DIP minimum, while the scene's Play and Game settings actions use a 42-DIP minimum. The Artwork and video controls use an opaque Surface background over the scene so their labels stay readable.

### Inputs / Fields

Text input uses the Surface, Ink, Rule, and Accent caret brushes. Custom combo boxes use the same dark surface, a path chevron, and a 1-DIP accent focus border; highlighted and selected options use Raised. Dropdown scrolling is bounded to 320 DIPs. Native input behavior supplies editing and keyboard semantics. Status and errors are written in plain language.

### Navigation

Library, Emulators, and Help pair 16-DIP vector icons with text in the header. Resting text uses Muted; hover and the active page use Ink. A 1-DIP active underline begins beneath the label after a 24-DIP left inset; keyboard focus changes it to a 2-DIP Accent line. Add games and the labeled Settings icon sit at the right. About and licenses is reached from Settings. Game-settings tabs use a lower rule, thickening to 2 DIPs in Accent for the selected tab; keyboard focus adds Raised behind the header.

### Cards / Containers

Library entries keep cover, title, and written readiness together. Missing covers have a thin-rule frame and Cinzel title fallback. Available covers use Uniform, retaining their original square, PS2, PS3 or PSP proportions without cropping labels; empty image borders are suppressed. Hover fades in a Surface wash with a 1-DIP Muted border behind the item; selection stays Raised with an Accent outline and keyboard focus uses Ink. List rows preserve the same identity and state with smaller fitted artwork. These compact records do not imply a general card-based dashboard.

### Help disclosure

The Help Expander uses Ink text and a minimum 44-DIP header row. Thin Rule lines frame the header above and below. A 5 × 9 DIP vector chevron with a 1.5-DIP stroke occupies a 22-DIP leading column and rotates 90 degrees on expansion. Hover adds Surface; keyboard focus supplies a full 1-DIP Accent border. Expanded content begins 14 DIPs below the header. Standard Expander automation controls the expanded state.

### Library divider

A 1-DIP Rule line sits inside the retained full-height 7-DIP resize target. Its centered square handle uses the divider-handle token, a 1-DIP Rule border, and three 2-DIP Muted dots. Hover changes the handle to Raised with an Accent border; keyboard focus accents both border and line. Default dotted adorners are suppressed; mouse-down clears the keyboard-only highlight, and focus loss clears its state. Collapse hides the disabled handle. Dragging, 16-DIP keyboard increments, double-click reset, remembered width, and collapse/restore behavior remain intact.

### Native window chrome and icon

Windows 11 build 22000 and later receives Ground caption and Ink caption-text colors through per-window DWM requests. High contrast restores OS defaults. Earlier Windows retains native caption behavior with a best-effort dark-frame request. Native caption buttons, resizing, snapping, and window semantics remain in place; the app does not replace them with drawn controls or change a global theme.

The executable, native window and taskbar use the original bundled ICO, as requested for small-size legibility. The earlier private emblem override is no longer loaded; existing local files are left intact. There is no notification-area tray feature.

### Selected-game scene and artwork

The scene keeps the title/subtitle or accepted logo, edition, Play or required setup action, and Game settings together. Artwork offers local cover, background, transparent-logo, and video import, plus a reread of registered game artwork. The versioned cover manifest maps 100 editions to 83 source entries with 17 explicit fallbacks; automatic selected-cover downloads are hash-verified and cached, and can be disabled. Artwork exposes retry, source credit and fallback explanation. Console and PSP selections persist independently. Manual choices take priority over discovered assets. Discovery reads supported local PS3/PSP folder slots, PBP image slots, readable ISO9660 presentation slots, and per-file `.artwork` companions into a managed cache. It leaves source media unchanged and does not infer a logo from a cover or promise that a game contains one.

Local images and supported videos stay in the Hub's artwork storage, separately from emulator packaging. Videos use native Windows codecs; PAM/PMF playback is unavailable. Missing or failed artwork leaves usable text and the available still/cover fallback. Private user references belong in the separate local review copy; no fictional game assets or unlicensed cover-art set is bundled in the distributable.

The `.impeccable/design.json` snippets are self-contained browser translations for the design panel. They illustrate native values and states; they are not a web implementation, native runtime screenshots, or additional shipping components.

## Do's and Don'ts

### Do:
- **Do** preserve the player's chosen library width and restore the remembered normal split.
- **Do** keep written readiness and the next required action visible beside the selected game.
- **Do** share one background frame across library and scene, fade its left edge beneath the translucent library, and protect title/actions with the lower shades.
- **Do** keep unadded-game covers small and retain text when a transparent logo is unavailable.
- **Do** let Windows high contrast override brand colors and suppress decorative image layers.
- **Do** keep private reference imagery outside the public application package.
- **Do** retain native caption controls and let hover feedback settle without idle animation clocks.

### Don't:
- **Don't** restore the superseded Archive tokens or treat browser prototype values as native authority.
- **Don't** use cover-derived ambient color as a semantic status color or a foreground text color.
- **Don't** enlarge a small cover to crop-fill the cinematic scene.
- **Don't** introduce marketing heroes, arbitrary pill badges, unrelated military emblems, or fake telemetry into the play path.
- **Don't** restore the superseded inset feathered rectangle or add top/right vignettes to the shared backdrop.
- **Don't** promise a discovered logo, a Windows codec, PAM/PMF playback, a stacked layout, or collapsible group headings.
- **Don't** distribute the private emblem adaptation or treat a local window icon override as a change to the executable's Explorer icon.

0.3.3 import refinement (2026-10-10): recognized media shows game title, human-readable console/region and filename, followed by Add game. No serial jargon or full catalog dropdown appears for a detected copy. Unknown media chooses a game first, then its compatible editions. The compact modal adapts to its owner. Caption colors and icon share the native theme helper. ListBox owns its surface template so disabling the library cannot activate the Windows white background. The actual modal/disabled-owner regression and recovery complement the static native captures. See docs/research/import-and-code-audit.md.

0.3.4 input/import refinement: Controls leads with a compact backend-specific keyboard guide, followed by controller state and any relevant input-priority limitation. Archive review identifies the source archive, explains managed storage once, and hides the redundant Copy checkbox when every candidate is already unpacked. The adopted A+C presentation is unchanged; visible Portable titles are searchable.

0.3.5 compatibility repair preserves the A+C surface. Diagnostics opens at the top with Repair setup before the findings; errors and missing prerequisites precede routine status. Recommended-build text follows the selected edition. File checks report bounded progress without flooding the UI with internal filenames.

## Per-game settings revision — 0.3.6

Preserve the A+C library, palette, square controls, typography, motion and cinematic scene. The first tab is Settings, followed by Controls, Patches, Achievements, Save Data, Advanced and Details. Graphics separates preset, rendering scale, window size and FPS target; descriptions reflect the current selection. Controls separates keyboard and controller. Patch options and achievement records live under their own tabs. Compatibility evidence stays in Details. Native scrolling and wrapping keep every section reachable at 960×540; no new decorative cards or hero copy.


## Navigation and controls revision — 0.3.7

The 0.3.8 refinement keeps this visual world. Selected-game setup uses green vector checks and readable incomplete states; the next action stays directly beneath the game heading. The keyboard summary uses grouped Action / Game button / Key rows. Achievement artwork sits in compact dark tiles, grayscale while locked and original color while unlocked, with explicit status and focus/hover descriptions. The selected-game toolbar is one wrapping row so artwork has space at small sizes. Added games without cinematics can use a dim, softened high-quality cover backdrop; tiny menu icons and unadded games retain small covers.

The top menu adds a matching stroke trophy icon and Achievements. At narrow widths Emulators and Help retain their accessible icon buttons and tooltips; Library and Achievements keep labels. Collection progress lives in this page, with a per-game trophy shortcut beside Play. Tracking/account configuration remains in Game settings, without repeating the full record list. Grade color is accompanied by grade text; high contrast uses system colors.

Controls separates the game’s internal layout from the two physical input presets. Action-first rows show the game action, native console prompt and chosen key/controller input. Imported layouts have stable identities, independent selection, rename and JSON export. Community notes distinguish sourced priorities from inferred bindings. No selector silently changes an in-game save. Genuine transparent logo imports work whether media is added or not; PIC0 overlays are never assumed to be title logos.
