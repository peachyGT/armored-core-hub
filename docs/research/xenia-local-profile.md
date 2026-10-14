# Xenia local player profile — 2026-10-11

The Hub can prepare a native offline player for fresh Xenia save storage without an emulator profile dialog. This is a local identity, not an Xbox Live account. No network account, password, achievement unlock or game progress is fabricated.

## Primary format sources

- [ProfileManager](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/profile_manager.cc): account loading, native HMAC/RC4 serialization, profile paths, creation and login. The constructor loads existing profiles; it does not synthesize a default player.
- [ProfileManager declarations](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/profile_manager.h): offline XUID generation range.
- [Native account structure](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/xam.h): packed account size and big-endian UTF-16 gamertag.
- [Public native crypto format constants](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/util/crypto_utils.cc).
- [Content directory reader/writer](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/xcontent/xcontent_package_directory.cc), [content structures](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/xcontent/xcontent.h), and [package initialization](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/xcontent/xcontent_package.cc): CON metadata, header placement and padded header length.
- [Native creation UI](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/ui/create_profile_ui.cc): first-account auto-login behavior.

The implementation is independent C# serialization of this native format, with the project license included at `third-party/Xenia-LICENSE.txt`. It writes a 0x194-byte Account and a 0xA000-byte native directory header with offline profile/dashboard metadata. These two files are staged together under a new XUID directory and published atomically. Creation never writes outside managed save storage.

## Identity and recovery policy

`ExistingLocal` reads bounded accounts, checks their native integrity and supplies gamertags plus an explicit validity flag. A damaged account stays present and cannot be selected as ready. `ChooseLocal` saves the selected existing XUID in `hub-selected-profile.json` inside that game's save family. The marker survives runtime build changes and Hub relocation.

Selection priority is the saved Hub choice, a valid native config choice, then the sole existing account. Several accounts without a choice require selection in the Hub. Missing selected accounts and orphaned/older save data fail with a preservation/migration message; they do not cause silent replacement identities. Shared all-zero DLC storage does not count as a player. Scanning is bounded to 128 identity directories and 1024 directories overall.

Xenia restore points include the complete profile packages and selection marker. Restoring deliberately rewinds the identity/save/achievement state together. Keeping a newer marker while deleting newer profile packages would leave the choice pointing to an absent player. A pre-restore backup retains the current complete state.

## Native acceptance

The optional `HUB_TEST_XENIA_PROFILES` test copied the already-downloaded official **canary_experimental@828014b90, built Oct 8 2026** into isolated workspace storage. It started no guest title. The native log reports Account loading, **Hub signed into slot 0**, and no decryption failure; the process closed normally with exit code 0.

Evidence: `work/xenia-profile-parser-d9532efe/runtime/parser/native-profile-parser.log`. The source/parser implementation was also checked against the corresponding pinned profile manager. The normal command sandbox stalled before window/kernel initialization; the successful probe required normal desktop process permissions. That earlier startup limitation is not falsely counted as a format rejection.

Five default tests cover fresh stable creation, byte-preserving selection among multiple players, damaged/legacy data preservation, cancellation and managed-root bounds, and actual launch preparation/TOML integration. The sixth optional test covers the real native decrypt/mount/login path. These checks establish native profile setup, not gameplay trophy unlock coverage.
