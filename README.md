# Roblox Multi Desk

A compact Windows launcher for multiple Roblox game windows.

## Start playing

1. Close existing Roblox game windows and open **Roblox Multi Desk.exe**.
2. Click **Enable multi-instance**.
3. In your browser, sign in to Roblox and press the game's **Play** button.
4. After that game loads, use a different Roblox account in your browser and press Play again.
5. Keep Multi Desk open while playing.

Use your own browser profiles or separate browsers to keep accounts signed in independently. Multi Desk now focuses on the launcher only; the account-label manager and game-link entry have been removed.

**Choose Roblox player** lets you select the installed RobloxPlayerBeta.exe if automatic detection fails. **Focus latest game** brings the most recently launched running game to the front. The status area shows the number of game processes launched by this app.

## Download or build

Extract the app ZIP into a writable folder, then run **Roblox Multi Desk.exe**. To build from this source repository, run **build.ps1** in PowerShell; it produces that executable without downloading packages.

Requires 64-bit Windows 10/11, the .NET Framework desktop runtime, the desktop Roblox Player from roblox.com, and a local drive supporting directory junctions (normally NTFS).

## Updating from the previous version

Disable multi-instance and close the old app before opening this version. Keep the old app and its Data folder together until it has restored Roblox's normal Play links. Existing account browser folders are not deleted or migrated by this update. This version no longer creates browser profiles.

## Stop and recover

Click **Disable multi-instance**, or close the app, to restore the previous Roblox Play-link settings. Running games are left open. Close them before enabling again.

After an app crash or Windows restart, reopen the same copy of Multi Desk. It attempts recovery using Data/protocol-backup.xml before you enable it again. Keep the executable and Data folder together until recovery completes.

If Roblox or another app changed the Play-link handler meanwhile, Multi Desk preserves the newer handler and retains its backup. If recovery continues to fail, close Multi Desk and repair Roblox using its official installer.

If you manually chose the player and Roblox later updates, select the current RobloxPlayerBeta.exe again. Let Roblox update normally before enabling Multi Desk.

## Privacy

Settings and recovery files remain in the local Data folder. The launcher does not ask for passwords, read browser cookies, or store launch links in logs. Data is excluded from source control. Legacy Data/Browsers folders may still contain signed-in sessions, so never share the Data folder.

There is no telemetry, bundled Roblox client, downloaded helper, or automatic updater. The executable is locally compiled and unsigned.

## Compatibility and verification

The user reported that the original multi-instance implementation works on their computer. This version keeps that implementation and simplifies the interface. That report is not a guarantee of compatibility with future Roblox versions or other computers. See [TESTING.md](TESTING.md) for the checks and their limits.

## Implementation

While enabled, Multi Desk temporarily handles roblox-player: links for the current Windows user. It forwards each link through a user-restricted named pipe, launches the installed player through a unique directory junction, and clears only the shared singleton mutex/event handles in clients it launched. It leaves path-specific mutexes and unrelated handles alone.

The app does not modify Roblox executable files or inject code. The mechanism follows [Fleet's technical notes](https://github.com/Toluwer/Fleet/blob/main/docs/TECHNICAL.md). Multi-instance compatibility limitations are described by [MultipleRobloxInstances](https://github.com/Avaluate/MultipleRobloxInstances).
