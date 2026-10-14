# Game action labels and control profiles

Research date: 2026-10-11. The Hub distinguishes the game's own control scheme from the emulator's physical keyboard/controller mapping. Its binding editor presents **game action — native console input — physical input**. Choosing a scheme in the Hub changes the labels and mapping template; it does not alter the game's saved KEY ASSIGN configuration.

All 19 catalog titles/platform variants have an action table. The tables emphasize battle controls, with important contextual actions such as Scan Mode, weapon purging, reload holds and AI camera controls. They are descriptions of the declared built-in scheme, not an automatic reading of a user's customized save.

## Primary manual evidence

The [FromSoftware manual index](https://www.fromsoftware.jp/jp/manual.html) supplies the Japanese manuals below. Relevant pages were rendered locally with Poppler and visually read; this matters because several are image-only PDFs, and the web reader refuses files larger than 10 MB. Public US manual scans were checked where early Japanese Start/Select behavior differs. No game or firmware files were obtained during this research.

| Catalog title/platform | Manual and inspected printed pages | Mapping distinctions |
|---|---|---|
| Armored Core, PS1 | [Japanese manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/004_ARMORED_CORE.pdf), 4–5; [US manual](https://www.videogamemanual.com/ps1/Armored%20Core%20%28USA%29.pdf), 3–5 | Digital movement and shoulder camera; JP Select pauses/Start opens map; US Start pauses/Select opens map. |
| Project Phantasma, PS1 | [Japanese manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/005_ARMORED_CORE_PROJECT_PHANTASMA.pdf), controls spread; [US manual](https://www.videogamemanual.com/ps1/Armored%20Core%20-%20Project%20Phantasma%20%28USA%29.pdf), 3 | Original digital scheme, same verified regional pause/map swap. |
| Master of Arena, PS1 | [Japanese manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/008_ARMORED_CORE_MASTER_OF_ARENA.pdf), controls spread; [US manual](https://www.videogamemanual.com/ps1/Armored%20Core%20-%20Master%20of%20Arena%20%28USA%29.pdf), 4–6 | Original digital scheme; original game does not use L3/R3. Same verified regional pause/map swap. |
| Armored Core 2, PS2 | [Japanese manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/015_ARMORED_CORE_2.pdf), 2–3; [US manual scan](https://www.scribd.com/document/649095851/Armored-Core-2-USA), control diagram | Digital movement/camera, L3 extension, R3 Overboost. JP Start=map/Select=pause; US Start=pause/Select=map. |
| Another Age, PS2 | [Japanese manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/017_ARMORED_CORE_%202_ANOTHER_AGE.pdf), 2–3 | Same digital controls and JP pause/map behavior. Western pause/map follows the AC2 layout; a separate Western AA manual still warrants a follow-up check. |
| Armored Core 3, PS2 | [Manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/020_ARMORED_CORE_3.pdf), 2–3 | Original digital controls; Start pauses/Select opens map. Inside parts use a chord, not a separate default single-button action. |
| Silent Line, PS2 | [Manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/024_ARMORED_CORE_3_SILENT_LINE.pdf), 2–3 | Original digital controls, extension and Overboost/Exceed Orbit depend on equipped parts. |
| Nexus, PS2 | [Manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/031_ARMORED_CORE_NEXUS.pdf), 2–3 | Dual-stick scheme: L1 left weapon, R1 selected weapon, L2 boost, R2 weapon cycle, Cross inside, Square reset camera, Triangle purge modifier. Classic Type B is exposed separately. |
| Nine Breaker, PS2 | [Manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/035_ARMORED_CORE_NINE_BREAKER.pdf), 2–3 | Same late PS2 dual-stick table, with classic scheme available separately. |
| Last Raven, PS2 | [Manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/043_ARMORED_CORE_LAST_RAVEN.pdf), 2–3 | Same late PS2 dual-stick table; selecting classic controls must match the game's selected scheme. |
| Formula Front, PS2 | [Manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/040_ARMORED_CORE_FORMULA_FRONT.pdf), 2–3 | Ordinary battles are AI controlled: camera views, zoom, display and battle speed. Manual controls are only used in AC Test → Action. |
| Armored Core 4, PS3/Xbox 360 | [PS3 manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/054_ARMORED_CORE_4_PS4.pdf), [Xbox manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/054_ARMORED_CORE_4_Xbox360.pdf), 4–5 | Type A uses face buttons for arm weapons; Type B uses L1/R1 or LB/RB. Both retain boost, quick boost and separate shoulder weapon actions. The official PS3 file name misleadingly says PS4. |
| For Answer, PS3/Xbox 360 | [PS3 manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/064_ARMORED_CORE_for_Answer_PS3.pdf), [Xbox manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/064_ARMORED_CORE_for_Answer_Xbox360.pdf), 4–5 | Regular A/B plus Simple. Simple turns with the left stick and boosts at full forward/backward deflection; it keeps Cross/A right arm and Square/X left arm. See the PS3 Simple discrepancy below. |
| Armored Core V, PS3/Xbox 360 | [PS3 manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/087_ARMORED_CORE_V_PS3.pdf), [Xbox manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/087_ARMORED_CORE_V_Xbox360.pdf), battle operation sections | Independent arm triggers; high boost/held Boost Charge, recon, booster toggle, Glide Boost, bay modifier, Combat/Scan mode contextual trigger actions. |
| Verdict Day, PS3/Xbox 360 | [PS3 manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/091_ARMORED_CORE_VERDICT_DAY_PS3.pdf), [Xbox manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/091_ARMORED_CORE_VERDICT_DAY_Xbox360.pdf), 6–7 | Base battle controls retained; operator controls are a separate context. Y/Triangle is explicitly a bay modifier in the published default battle table. |
| Armored Core 3 Portable, PSP | [Manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/074_ARMORED_CORE_3_Portable.pdf), 4–5 | Analog forward/back/turn; L/R strafe; Triangle/Circle vertical aim; D-pad selects weapon, uses left arm, toggles extension/OB. |
| Silent Line Portable, PSP | [Manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/077_ARMORED_CORE_SILENT_LINE_Portable.pdf), 4–5 | Verified separately; retains the Portable digital vertical camera controls. |
| Last Raven Portable, PSP | [Manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/081_ARMORED_CORE_LAST_RAVEN_Portable.pdf), 4–5 | Verified separately; differs materially from PS2 Last Raven's dual-stick layout. |
| Formula Front International/Extreme Battle, PSP | [Manual](https://media.fromsoftware.jp/fromsoftware/jp/static/pdf/045_ARMORED_CORE_FORMULA_FRONT_INTERNATIONAL.pdf), 2–5 | Manual and AI camera schemes separated. Manual Circle is left weapon, Triangle cycles weapon, D-pad Down uses inside, Up modifies purge; vertical aim is automatic. These must not inherit AC3 Portable's Circle/Triangle camera labels. |

### Source discrepancies and limits

The official **PS3 For Answer Simple** table calls R1 the right weapon, while the corresponding Xbox table calls RB the shoulder weapon. The PS3 caption is marked “manual; verify in game” and the scheme notes expose the discrepancy. Resolve it against an actual PS3 game's KEY ASSIGN screen before treating it as confirmed gameplay behavior. Regular A/B are unambiguous and unaffected.

Japanese manuals are primary evidence for their editions. US scans independently establish the early PS1 and AC2 pause/map differences. Non-Japanese Another Age and EU variations use the corresponding Western baseline; those remaining region-specific details should be checked in-game. Late PS2 classic Type B follows the original digital action table; the inspected manual diagrams directly document the dual-stick default, rather than every possible customized KEY ASSIGN record.

Neither selecting a Hub control scheme nor applying an emulator mapping can turn an arbitrary in-game custom layout into a built-in one. Users must select a matching game layout or use the native editor when their save has a different scheme. Complex chords remain game-level combinations; the editor does not pretend they are independently bindable single actions.

## True Analogs

[VanLaser's patch project](https://github.com/VanLaser/analog-controls-for-armored-core-ps2) is the primary patch-author source. The analog option changes stick movement/camera labels while retaining original weapon buttons. Automatic launch selection snapshots the **enabled** matching patch state, including selected PNACH sections, without rewriting the user's saved “Game default” choice. Controller mappings remain directional emulator inputs; actual analog processing requires the patch itself.

## Community templates and confirmation

There is no substantiated universal community layout across every game/platform. The shipped Community entries are explicitly named **inferred**. They adapt source-supported preferences to the declared game scheme; keyboard permutations and uncovered titles are a Hub inference. The user confirmed this baseline on 2026-10-11: left stick/WASD movement, right stick/IJKL camera, LT/RT arm weapons, LB boost/jump, and RB quick boost or weapon cycle.

Evidence for those priorities includes the [True Analogs author](https://github.com/VanLaser/analog-controls-for-armored-core-ps2), a [For Answer player's shoulder-based layout](https://gamefaqs.gamespot.com/boards/944263-armored-core-for-answer/46957723), and a [Verdict Day guide author's control recommendations](https://gamefaqs.gamespot.com/ps3/704812-armored-core-verdict-day/faqs/69136). The latter two are first-person reports, not a vote establishing consensus. Formula Front AI uses a camera template instead of a combat template.

The emulator maps original console commands, so remapping combat can also move menu confirm/cancel commands. The UI explains this. Keyboard and controller presets remain independent, and Community never silently changes an in-game save.

## Imported profile storage

Each edition stores up to 64 named imported profiles with stable IDs. Keyboard and controller can choose different imports independently. Import, rename, removal, export and JSON round-trip preserve profile identities and copy dictionaries/edition arrays so editing an export cannot alter stored settings. A profile's edition and declared game scheme are checked again during resolution, so changing the game scheme after selection cannot silently launch a mismatched profile. Blank source URLs are valid for DIY exports; nonblank sources must be HTTPS. Existing single-slot imported Community settings migrate once into the Imported list, retaining their exact bindings.

Xenia canonical bindings are emitted through SDL's documented reserved XInput mapping, with adapter changes scoped to the child emulator environment. They do not depend on guessing the user's controller GUID. Raw imported SDL mappings remain available for advanced devices.

## Research trail and validation

Knowledge-base search `um kb search "Armored Core"` returned no matching field notes. Queries included `Armored Core manual controls`, `Armored Core Project Phantasma manual pdf controls start select`, `Armored Core 2 USA manual SELECT`, and the official manual index. Additional platform manuals were reached through the index rather than guessed as evidence. No login-gated source was needed.

Ignored research material is under `work/controls-research/`: public manual PDFs and rendered controller spreads. The AC1/Phantasma/MoA US control pages prove the pause/map swap; PSP Formula Front pages prove its distinct manual layout; AC4/For Answer spreads prove A/B/Simple differences; V/VD sections prove contextual battle commands. This material is not included in the portable build.

Automated regression coverage checks every catalog scheme's complete labels/source URLs, Sony/Xbox input naming, Fourth Generation and PSP scheme distinctions, regional pause/map labels, enabled analog launch snapshots, named import lifecycle and serialization, legacy migration, scheme/edition mismatch rejection, and actual adapter launch configurations. These checks validate mapping construction and persistence; they do not substitute for physical testing of every mission, menu and optional in-game custom layout.
