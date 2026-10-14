# ADR-002: Immutable upstream builds, local configuration

Date: 2026-10-09. Accepted.

Download official archives on demand and verify SHA-256. Retain version directories and cached objects. Resolve builds per edition. Generate configuration separately, only after installation. Hardlink assets when possible and unlink configuration destinations before rewriting.

DuckStation currently uses CC-BY-NC-ND and warns about preconfigured redistribution. The user explicitly requested unmodified installation followed by local configuration. No emulator binaries or preconfigured DuckStation distribution enter the package. Source availability is not described as permissive open-source licensing.

Tradeoff: session homes consume more disk space when hardlinks are unavailable. Safety and rollback outrank aggressive deduplication.
