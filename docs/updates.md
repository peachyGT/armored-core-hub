# Updates and rollback

Hub binaries, catalogs/profiles, emulator builds, patches, artwork/metadata and extensions are independent update domains. Catalog revisions must preserve referenced working profile identities or require an explicit migration.

Each backend records project, license, channel, endpoint, archive digest, pinned recommendation and policy. Installed builds record archive/executable hashes, time and source. Checking upstream requires its SHA-256 release digest. Installation and per-edition selection are explicit. Older builds stay selectable.

Rolling Git hashes are identities, not comparable semantic versions. BuildVersion returns unknown for incomparable strings.

Downloads verify cache first, then primary/retries, permitted fallbacks, cache recheck and actionable failure. Partials are unique and removed on error/cancellation. Corruption never becomes an installed build.

Sony's official support page currently links an HTTP PS3 update endpoint whose HTTPS certificate does not cover that host. Only that exact official host/path is exempted from HTTPS, with a pinned SHA-256. General insecure transport and disabled certificate validation are not implemented.

## Signed catalogs

ManifestUpdates verifies RSA-PSS/SHA-256 over decoded UTF-8 payload using a trusted public key/key ID, checks a newer version and schema, then atomically installs.

~~~json
{"payloadBase64":"...","signatureBase64":"...","keyId":"maintainer-key-id"}
~~~

An independently verified public key belongs in config/catalog-trust.pem; its ID belongs in config/catalog-trust-id.txt. Never bundle a private key. No release authority exists yet, so no key/feed is invented. Setup imports a signed envelope or rolls back the previous catalog; restart loads it.

The future remote feed can use Metadata providers. Trust and rollback are already independent of UI.

## Hub binaries

Close the Hub and extract a new signed release beside the old one, preserving data directories. A future automatic updater should stage a verified package and swap program files only after shutdown, retaining rollback. That automatic binary updater is an architectural seam, not a hosted service.

Patch/extension install is separate from enable/select. Artwork requires redistribution permission. Game media is not an asset-provider kind.
