# Contributing

## Report an issue

Search the [issue tracker](https://github.com/peachyGT/armored-core-hub/issues), then use the [bug-report form](https://github.com/peachyGT/armored-core-hub/issues/new?template=bug.yml). For an idea, use the [feature-request form](https://github.com/peachyGT/armored-core-hub/issues/new?template=feature.yml).

## Build and test

Install the .NET 10 SDK and run `./scripts/build.ps1 -Test`. Add `-Publish` to create a self-contained Windows x64 app in `outputs/ArmoredCoreHub`; choose a fresh output directory for each publish. `-Offline` uses an existing restore cache.

Tests stay in the source repository so changes can be checked. They use synthetic fixtures and are excluded from user downloads. Native checks need a separate build: `./scripts/build.ps1 -Publish -Diagnostics -OutputDirectory outputs/ArmoredCoreHub-diagnostics`, then `./scripts/validate-portable.ps1 -PackageDirectory outputs/ArmoredCoreHub-diagnostics`. Never run diagnostic checks against a personal library.

`./scripts/package.ps1` builds, tests and audits a clean portable ZIP. To include the installer, add `-InstallerCompiler path/to/ISCC.exe` from [Inno Setup](https://jrsoftware.org/isinfo.php). The installer definition is `installer/ArmoredCoreHub.iss`. SHA-256 checksums accompany both downloads. The application downloads retain only runtime files, user documentation and required notices; planning material and private test records remain outside the repository.

Core services live in `src/Hub.Core`, WPF UI in `src/Hub.App`, and catalog/cover metadata in `manifests`. See [architecture](docs/architecture.md), [feature coverage](docs/feature-matrix.md) and [current compatibility limits](docs/compatibility.md).

Preserve console/region distinctions, existing working build selections, original media and saves. Recommendations need authoritative source URLs and exact edition/build context. Compilation or an upstream Playable rating is not local gameplay certification.

Use argument arrays, validate paths and archive entries, reject links, stage changes and retain rollback. Tests must not include commercial media, BIOS, saves, credentials or private paths. Patches need permission, source/payload hashes, revision gates and explicit opt-in for gameplay changes.
