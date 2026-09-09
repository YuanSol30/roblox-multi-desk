# Roblox Multi Desk — experimental Windows app

Run **Roblox Multi Desk.exe**. No installer is needed. Keep the app in an extracted, writable folder; do not run it inside the ZIP.

## Play with different accounts

1. Close any existing Roblox game windows.
2. Open Multi Desk and click **Enable multi-instance**.
3. Type an account label, such as Main, and click **Add account label**. Repeat for your other accounts. Labels are for your reference; they do not create Roblox accounts.
4. Select a label. Optionally paste a Roblox game link or place ID, then click **Open selected account**.
5. Sign in on Roblox's website with the account for that label. Check the displayed username and click the game's **Play** button. Accept Edge's request to open the external application if shown.
6. Wait for the game to load. Select another label in Multi Desk, open it, sign in to a different account, and press Play again.
7. Keep Multi Desk open while playing. **Focus latest game** brings the newest launched game to the front.

The account comes from the browser in which you press Play. Selecting a label alone does not change a running game's account. Each label opens a separate persistent Edge profile, so its Roblox login is independent of your normal browser and the other labels.

## Requirements and limits

- Windows 10 or 11, 64-bit, Microsoft Edge, and the desktop Roblox Player from roblox.com.
- A writable folder on a local drive that supports directory junctions, normally NTFS.
- The Microsoft .NET Framework desktop runtime; compilation and component tests used the runtime already installed on this computer.
- Experimental, unofficial multi-instance behavior. The app was built and component-tested, but simultaneous play with two real Roblox accounts has **not** been verified. Roblox client updates, permissions, or platform restrictions can prevent it from working. There is no guarantee of uninterrupted play or future compatibility.
- This version opens each account manually. It does not automatically log in, create accounts, or force accounts into the same server. Use Roblox's own join or private-server options when needed.

## Turning it off and recovery

Click **Disable multi-instance**, or close Multi Desk, to restore the previous Roblox Play-link settings. Running games and browser windows are left open. Close existing games before enabling again.

If Multi Desk crashes or Windows restarts while it is enabled, open the same copy of the app again. It reads `Data/protocol-backup.xml` and attempts to restore the original settings before you enable it again. Keep the executable and its Data folder together until recovery is complete. Do not delete the app while it is enabled.

If another program or Roblox's installer changes the link handler in the meantime, Multi Desk preserves that newer handler and retains its backup. If a recovery message persists, close Multi Desk and repair/reinstall Roblox using Roblox's official installer. Do not manually import the XML file into Windows Registry Editor.

If Roblox is not found, use **Choose Roblox player** to select `RobloxPlayerBeta.exe` in the installed Roblox version folder. If you selected a file manually and Roblox later updates, select the current player again. Let Roblox update normally before starting multi-instance mode.

If the second game replaces the first, or either game closes, simultaneous play is not working with that client/setup. Disable Multi Desk and use the normal Roblox launcher. If the app reports access denied, the launch mechanism is unavailable in that environment; it does not request administrator privileges or alter security settings.

## Local data

Multi Desk stores settings and browser profiles in the `Data` folder beside its executable. It does not ask for passwords or read/export Roblox cookies. Edge stores your signed-in sessions in its browser profiles, so **do not share the Data folder**. Removing an account label leaves its browser data intact.

There is no telemetry, downloaded helper, automatic updater, or bundled Roblox client. The executable is locally compiled and unsigned.

## How it works

While enabled, the app temporarily changes the current Windows user's `roblox-player:` link handler. Browser Play links are forwarded over a pipe restricted to that Windows user; launch links are held in memory and never written to logs.

Each game is launched through a fresh directory junction pointing at the installed player. A background task inspects only game processes launched by this app and closes handles whose names exactly match Roblox's shared singleton mutex/event. It leaves path-specific mutexes and unrelated handles alone. It does not modify Roblox executable files or inject code. Existing Roblox sessions must be closed before enabling.

The design follows the mechanism described in [Fleet's technical notes](https://github.com/Toluwer/Fleet/blob/main/docs/TECHNICAL.md). The instability of multi-instance play is also described by [MultipleRobloxInstances](https://github.com/Avaluate/MultipleRobloxInstances). Native operations use Windows handle duplication and directory reparse points; see [Microsoft's DuplicateHandle documentation](https://learn.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-duplicatehandle) and [FSCTL_SET_REPARSE_POINT](https://learn.microsoft.com/en-us/windows-hardware/drivers/ifs/fsctl-set-reparse-point).

## Included files

- `Roblox Multi Desk.exe` — runnable Windows app.
- `Program.cs` — complete source code.
- `build.ps1` — rebuild with the Windows .NET Framework C# compiler.
- `TESTING.md` — what was and was not verified.

To rebuild, run `build.ps1` from PowerShell. It writes the executable into this folder and requires no package downloads.
