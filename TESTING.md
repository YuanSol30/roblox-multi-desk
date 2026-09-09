# Verification

Built on September 9, 2026, using the installed 64-bit .NET Framework compiler.

Passed:

- C# compilation.
- Roblox game-ID and URL validation, including rejection of deceptive domains, non-HTTPS links, alternate ports, and argument injection.
- Settings serialization and atomic file replacement.
- Directory-junction creation, access through the junction, and removal without deleting the original target files.
- Inspection of a synthetic child process: exactly two designated mutex/event handles removed, an unrelated event retained, and a repeated scan making no further changes. No live Roblox process was touched.
- Windows Forms construction and image rendering; the interface was visually inspected.

Unavailable or unverified:

- Registry integration tests attempted a randomly named test key, separate from Roblox. The environment denied write access, so protocol registration, restoration, and crash recovery have not been verified against the Windows registry.
- The installed Roblox files were inaccessible from the agent environment. No authenticated Roblox launch or simultaneous two-account game session was tested.
- Edge's external-protocol prompt, Roblox updates and teleports, sustained play, resource consumption, and display scaling on other systems need real-world testing.

The included `--self-test <folder>` mode tests URL handling, storage, junctions, synthetic process handles, and form rendering. It launches only a temporary dummy process. Use a new, empty test folder. `--protocol-test <folder>` additionally exercises a dedicated temporary Windows registry key; it does not use Roblox's real protocol key.
