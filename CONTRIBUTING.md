# Contributing

## Report an issue

Check the [issue tracker](https://github.com/peachyGT/armored-core-hub/issues), then use the [bug-report form](https://github.com/peachyGT/armored-core-hub/issues/new?template=bug.yml). It asks for your Hub version, steps and what happened. Game/setup details and screenshots or logs are optional.

For a new idea, use the [feature-request form](https://github.com/peachyGT/armored-core-hub/issues/new?template=feature.yml).

## Work on the Hub

Keep the product specific to Armored Core. Read PRODUCT.md, DESIGN.md and the ADRs.

Use .NET 10 and run scripts/build.ps1 -Test. Commercial media, BIOS, saves and private paths must never enter source fixtures, commits or packages. Tests generate synthetic ISO/SFO/XEX examples.

Every recommendation needs a source URL, access date, conclusion, confidence, edition and relevant build. Upstream Playable is not local certification. Do not mark a build tested without edition/hash/build/hardware evidence. Preserve alternate console editions and existing working selections.

The application reads manifests/catalog.json. Edit and validate it with the test suite. scripts/generate-catalog.py reproduces the initial snapshot when its named evidence files exist in work/research; it is not a build prerequisite.

Patch contributions need author, license/permission, exact source hashes, dependencies, incompatibilities, benefit, cost, risk and explicit opt-in for gameplay changes. Missing license text is not permission to redistribute.

Prefer explicit services and adapters. UI code must not branch on individual game IDs. Use argument arrays, not shell strings. Validate paths/archive entries, avoid reparse points, stage changes and retain rollback. Never overwrite external media or saves.

Run targeted checks, then the deterministic suite. Capture native UI at normal and compact sizes after layout changes. Web CSS detectors cannot certify WPF accessibility. Do not claim Narrator, DPI or gameplay validation from screenshots.

Commit source, manifests, docs, notices and tests. Exclude SDKs, runtimes, libraries, BIOS, saves, logs, generated output and credentials. Publishing requires separate authorization.
