# Native refinement review — 0.3.0

Date: 2026-10-09. Target: `src/Hub.App/MainWindow.xaml` and native presentation helpers.

## User authority

The seven supplied comparison screenshots supersede the prior 0.2.0 acceptance of a fitted artwork rectangle. The selected A+C structure remains; this pass changes native parity, artwork scale/composition, ambient strength and presentation assets. The browser comparison is a visual reference, not a new web-wrapper architecture.

## Verification scope

WPF rendered captures cover 1600×900, 1280×720, 960×540 and 820×500 window settings, plus unadded games and emulator navigation. Exported content sizes exclude Windows non-client borders. Tests also exercise split resize/collapse/restore, grid/list/filter/settings, transparent-logo geometry and unsafe paths. The 820-width cover grid was corrected to retain two columns in a narrow library.

The added-scene captures register a temporary synthetic folder containing the user's supplied background. They show setup still required; no commercial executable or gameplay is represented. Other captures show the normal unadded state. A locally generated RGB AVI validates the actual Windows video pipeline; a corrupt MP4 validates the failure event and still fallback.

Core fixtures validate standard image extraction, PBP bounds, ISO9660 lookup, duplicate assets across registrations, isolated companions, missing/corrupt caches and cancellation. The full core suite has 60 tests. The native workflow suite has 17 grouped assertions.

## Independent finish review

A fresh reviewer read the user's web and rejected-native screenshots before the builder's direction contract, then inspected all six requested current captures and relevant source. WPF is outside the Impeccable web detector's coverage; this was an explicit native visual/code review.

The reviewer accepted the icon/underline/square-control treatment, Cinzel title hierarchy, shared full-size backdrop, stronger subdued ambient color, small unadded covers and compact layouts. The only material finding was outdated PRODUCT/DESIGN documentation.

An independent documenter reconciled PRODUCT.md, DESIGN.md, the sidecar and native surface brief with the implementation. The reviewer then scored that finding **resolved**, with no documentation regressions and disposition **ship** for the scored fix. The first visual assessment and subsequent documentation verdict are distinct; this is not gameplay, accessibility-device or universal-codec certification.

## Portable video regression

The initial isolated package test found a Windows media-reader failure (`C00D1331`) on a 280-character extracted asset path, although .NET could read the file. The asset store was flattened to shared full-hash objects directly under `artwork/`, retaining separate per-media indexes. This also avoids duplicate menu assets across registrations. No user files or OS settings were changed.

Final package results are recorded in STATUS.md. Private reference artwork and test captures remain outside the redistributable ZIP.
