# Verification

## Launcher-only revision — September 10, 2026

- Compiled successfully with the installed 64-bit .NET Framework C# compiler.
- Compared with the original GitHub source: protocol backup/recovery, native singleton-handle and junction operations, client launch, monitoring, and relay code are unchanged.
- Removed the account management and game-link controls and their handlers. Legacy profile settings remain readable to preserve existing local data.
- Runtime self-tests and UI rendering were attempted, but Windows Application Control blocked the new executable before it started. This revision's interface has therefore not been verified at runtime in the agent environment.
- The user reported successful multi-instance play with the original version. That is a user report, not an independent test of this revised build.

## Earlier checks — original version

Compilation, launch-link validation, settings serialization and replacement, directory junction creation/removal, synthetic child-process handle inspection, and Windows Forms construction/rendering passed. The synthetic test removed exactly two designated handles while preserving an unrelated event.

Registry integration tests were blocked by access permissions in the agent environment. Authenticated Roblox gameplay was not independently tested by the agent.

## Available tests

Use a new, empty folder with `--self-test <folder>` to exercise launch-link validation, settings storage, directory junctions, a synthetic child process, and form rendering.

`--protocol-test <folder>` exercises a dedicated temporary registry key, separate from Roblox's actual protocol key.

Compatibility with future Roblox updates, teleports, sustained play, and other display scaling settings still requires real-world testing.
