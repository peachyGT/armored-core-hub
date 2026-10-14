# Synthetic archives

`synthetic-media.7z` contains a single `synthetic.chd` text file: "Synthetic test payload. Not a valid disc and contains no game data." It tests transport extraction only, not CHD decoding. Created locally with 7-Zip 26.04 (`7zr a synthetic-media.7z synthetic.chd -mx=5 -ms=on`). The payload is original project test data, covered by this repository's license.

`password-media.7z` contains the same test text with encrypted headers (`-pfixture-only -mhe=on`). The public fixture password is deliberately trivial and protects no private data; the test verifies that import skips password-protected archives.
