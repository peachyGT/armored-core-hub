# Blocked actions

| Action | Exact dependency | Current handling |
|---|---|---|
| Certify complete AC4 / For Answer playthroughs | Sustained progression beyond the sampled older-build workaround | Official RPCS3 20152 passed both sampled first missions after 20250 stalled. Hub 0.3.5 provides an explicit per-edition repair, including bounded ISO/Joliet preparation for AC4; original media and saves remain intact. No whole-game certification; see docs/testing/2026-10-10/RESULTS.md |
| Verify exact retail dumps against reference hashes | Published, provenance-tracked hashes matching the exact container/disc-track representation | Serial recognition remains unverified; no invented hashes or ISO/CHD comparisons |
| Distribute community binary patch bytes | Identifiable author, license or redistribution grant, exact target revisions | Metadata/research only until each asset passes this gate |
| Validate native game save-transfer semantics, title updates, multiplayer and rendering | Exact owned editions, updates and suitable test hardware/controllers | Shared families/backups and explicit caveats; no unsupported claims |
| Certify RetroAchievements sign-in and real unlocks | User-controlled service account and supported exact game copy | In-Hub native sign-in/default setup delivered; mocked POST/authentication, token formats/cleanup and cache isolation tested. All catalog title/platform support is explicit. No credentials requested or fabricated; actual service unlocks remain unverified |
| Certify one community consensus / all layouts in real gameplay | Broader community evidence and per-game physical checks | Sourced modern templates with inferred details are delivered in 0.3.7; the user confirmed the baseline. Exact action labels come from per-game manuals; no universal consensus or exhaustive gameplay check is claimed |
| Accept production remote manifest/Hub updates | Maintainer-controlled trust key, signed packages and release endpoint | Signature verification/import/rollback implemented; no private key or invented service |
| Certify assistive technology and remaining physical controller behavior | Hands-on Narrator/contrast-theme/text-scaling and remaining backend/controller exercise | AC2 original/patched dual sticks, AC4 movement/camera/pause/reconnect, and ACV movement/camera/pause passed with the user's 8BitDo. ACV reconnect and other certification remain open |

The native application, upstream runtime installation, firmware workflow, synthetic tests and packaging do not depend on these blocks. Local PPF/PNACH application and supported console-movie previews are delivered in 0.3.6. Arbitrary conversion and PSP executable-patch preparation remain feature gaps, not permission denials.

No system-wide access or external publishing is needed. Network/package actions require command-scoped sandbox escalation; successful access is recorded in docs/capabilities.md. No blanket permission mechanism is available. No approval remains pending for the delivered baseline.
