# ADR 005: Achievement setup inside the Hub

Accepted 2026-10-11 for 0.3.9. Supersedes ADR 004's emulator-only sign-in and separate web API refresh decisions.

Use each emulator's existing achievement implementation to detect unlocks. The Hub configures supported integration on by default, with standard tracking rather than Hardcore as the default. An explicit support table records verified provider game IDs, checked dates, absent sets and exact-hash limitations. No universal achievement engine or invented achievements are added.

RetroAchievements sign-in uses the official native POST API from an in-Hub form. Store only the native login token, encrypted with Windows account protection, outside launch plans, diagnostics and save snapshots. Web API keys are distinct and unnecessary. Apply the backend's actual token format and secret location when preparing the session, and clear session secrets on completion/failure and local sign-out. Another Windows account requires sign-in again.

Progress refresh is explicit or follows an owned session. There is no background polling service. Cached records are bounded and keyed by edition, provider, account and set. Preserve already recorded unlocks only within that same identity when saves are restored; keep Hardcore status and supplied definitions separate from inferred gameplay compatibility. Hidden descriptions/art remain hidden until unlocked.

Native console data stays authoritative. RPCS3 games without a trophy set are identified as such. Xenia uses a local player identity rather than Xbox Live credentials. Existing identities and saves must be preserved, and ambiguous identities require a Hub selection.

The feature adds no database, web browser runtime, injected input service or new shipped dependency. Mocked authentication, native configuration fixtures and parser checks establish app behavior; they do not certify a real user's service login or a game's unlock triggers.
