# Native finishing and icon provenance

Access date: 2026-10-09. Scope: the five final UI refinements requested after adopting A+C.

| Source | Conclusion | Confidence |
|---|---|---|
| [Microsoft DWM window attributes](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute) | Windows 11 build 22000+ supports per-window caption and caption-text colors. Keep native controls, snapping, resizing and accessibility; no global theme or registry change is needed. Caption colors are set-only attributes, so verification uses successful return codes and an owned-window capture, not unsupported readback. | High |
| [FromSoftware IP FAQ](https://www.fromsoftware.jp/ww/faq-other.html) | Personal fan creations are discussed subject to restrictions. This is not an explicit license to redistribute the supplied White Glint emblem as an application's icon. | High for what the FAQ states; no legal clearance inferred |
| [FromSoftware site policy](https://www.fromsoftware.jp/ww/policy_site.html) | Website graphics and marks are reserved to their owners; the policy does not establish an application-icon redistribution grant. Changing colors or removing the supplied emblem's background does not itself provide such a grant. | High for the published policy; scope is the site, not a definitive ruling on all fan works |

## Decisions

- The user's Joshua O'Brien/White Glint reference is suitable visually at larger sizes; fine curls lose detail at 16 pixels. Its exact scan/redraw author and reuse terms are not established. An ivory-on-charcoal adaptation is retained only in the user's separate local preview. No publisher affiliation is claimed.
- The redistributable uses an original three-part geometric wing/core mark drawn in `src/Hub.App/Assets/Hub.svg`, under the project's MIT license. This is a separate design, not a traced or simplified copy of the supplied emblem. `scripts/build-icon.ps1` creates nine ICO frames, 16–256 pixels. Both the executable resource and native window use it.
- `config/app-icon.ico` is an optional local window/taskbar override. An invalid file falls back to the bundled icon. It does not rewrite the executable's Explorer icon. A private build can use MSBuild's `ApplicationIcon` override; no private icon enters the standard package.
- Windows 11 captions use the app's `Ground` and `Ink` colors. High contrast restores the OS defaults. Older Windows keeps its native behavior with a best-effort dark-frame request; exact caption matching is a Windows 11 feature.
- Hover animates only a small item's border/background opacity for 120 ms. Completed clocks are removed. No layout, blur, image zoom, repeating animation or idle timer is added. Windows reduced motion, high contrast and WPF software rendering use an immediate highlight. This is inexpensive feedback, not a claim of zero rendering cost.
- The divider paints a one-DIP line, retains a seven-DIP full-height hit area, and adds a 15 × 46 DIP three-dot handle. Keyboard resizing, reset, collapse and saved widths remain intact.
- The Help disclosure uses a 44-DIP row, palette foreground, vector chevron and visible keyboard focus. Standard Expander automation still controls its state.

## Evidence

The native self-test verifies disclosure automation/readability, settled hover clocks and reduced-motion behavior, divider geometry, ICO frames and successful DWM caption requests. Captures cover 960-DIP and full-size windows, the Help disclosure in both states, and inherited 820/1280/1600-DIP artwork behavior. An owned-HWND `PrintWindow` capture confirms the native caption and private icon. These checks do not certify physical assistive devices or emulator gameplay.

The private raster was generated from the user-supplied emblem with recoloring onto a charcoal field; attempted transparent variants had edge artifacts and were not selected; its exact generation prompt is embedded in the private PNG source. It remains outside source control and the redistributable. Public reuse of that emblem remains unapproved; the original icon lets the app ship without depending on that question.
