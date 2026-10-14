# Design feedback and source assessment

Access date: 2026-10-09.

## User evidence

Playnite screenshot establishes cover density and console grouping. Annotations reject tall records, large search, filler prose, exposed serials, compatibility internals, and repeated license material. Prototypes use Emulators, tuck licenses into Settings → About, explain only missing requirements, and keep guides in Help.

AC4 / For Answer references establish pale skies, dark mechanical silhouettes, warm desaturated colors, photographic impact, classical serif lettering. The later Ace Combat 5 logo specifies Roman capitals and a thin horizontal division. User favors this over Electrosphere and requests changing backdrops with optional video.

## Primary design references

- https://github.com/LykosAI/StabilityMatrix — user-named familiarity reference. Evaluate a Windows-style shell alongside expressive alternatives, without changing application frameworks. Confidence: high for preference.
- https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography — Segoe UI Variable for Windows controls/body, hierarchy through weight/size. Confidence: high.
- https://github.com/google/fonts/tree/main/ofl/cinzel — openly licensed Roman display typeface; font and OFL downloaded from the official repository. Confidence: high for license/provenance; suitability awaits human review.

## Local BIOS import

Sources inspected:

- https://pcsx2.net/docs/setup/bios/
- https://github.com/PCSX2/pcsx2-net-www/blob/main/docs/setup/bios.md

Third-party BIOS download sources were not implemented. The Hub imports user-supplied console dumps and preserves their originals.

PCSX2's authoritative guide states BIOS is required and no open-source substitute exists. It recommends dumping a console and optionally checking preservation-database hashes.

Decision in this design round: retain one-time Find existing BIOS / Add BIOS file; make requirements console-specific; demonstrate official-source PS3 setup. No third-party BIOS download is offered.

Confidence: high that PS2 requires BIOS and these inspected pages lack authorization evidence. Downloaded files were not security-audited.

| Console | Player-facing requirement |
| --- | --- |
| PlayStation | BIOS, added once |
| PlayStation 2 | BIOS, added once |
| PlayStation 3 | System software from Sony |
| PSP | No BIOS needed |
| Xbox 360 | No BIOS needed |

Show the distinction when needed; avoid a universal, vague System files prerequisite.
