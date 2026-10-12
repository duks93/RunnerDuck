# Windows installation details

Run `pwsh -File ./install.ps1` from either the repository or an extracted offline ZIP, then choose the components. If PowerShell 7 is not available, double-click `install.cmd`, which uses built-in Windows PowerShell.

## Automatic setup

- Source installs reuse .NET 10 SDK when available, or download it to `%LOCALAPPDATA%\RunnerDuck\dotnet` with Microsoft's [dotnet-install script](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script). Both .NET and Windows App SDK runtimes are included in the resulting app.
- Offline ZIPs use their bundled app without downloads or an SDK.
- Tray-only installs copy the app to `%LOCALAPPDATA%\RunnerDuck\TrayApp`, create a Start menu shortcut, and launch the EXE. They do not enable Developer Mode or register a Windows package.
- Widget modes register the development package. If necessary, the installer enables [Developer Mode](https://learn.microsoft.com/en-us/windows/apps/get-started/developer-mode-features-and-debugging) using a Windows administrator prompt. Only the setting change is elevated; app installation remains under the original user. Developer Mode remains enabled afterward. Organization policy or a declined prompt may prevent this step.
- Widgets require Windows 11 with an existing Windows Widgets board. Adding a widget to the board is a user action: press **Win+W** and select RunnerDuck / AI Usage.

## Optional arguments

| Argument | Meaning |
| --- | --- |
| `-InstallMode Both` / `Widget` / `Tray` | Select components without the terminal menu. |
| `-NoLaunch` | Install without starting the tray. |
| `-Platform x64` / `ARM64` | Override automatic architecture detection. |

## Create an offline ZIP (maintainers)

On a Windows build PC with .NET 10 SDK and internet access:

```powershell
pwsh -File ./Windows/New-OfflineBundle.ps1 -Platform x64
```

Use `-Platform ARM64` for an ARM64 target or `-OutputDirectory` to change the destination. The ZIP is written under `artifacts/offline`. Distribute the ZIP; recipients extract it and use the same `pwsh -File ./install.ps1` command or double-click `install.cmd`. There is no package restore on the target PC. Online AI services still need internet access.

## Updates and switching components

Rerun the same installer and select the desired components. Packaged installs retain their registered location. Do not delete that directory; older manual installations may use repository build output.

For an installed tray app, rebuild and try changes with `pwsh -File ./install.ps1 -InstallMode Tray`. This updates and starts `%LOCALAPPDATA%\RunnerDuck\TrayApp\RunnerDuck.exe`. Use the existing installation mode when testing widget modes. A build alone leaves the installed version used at login unchanged.

The installer records the installed EXE at `HKCU\Software\RunnerDuck\InstalledExecutable` before launching. Ordinary tray startup accepts that path or a process with package identity. Without a usable record, only the default `TrayApp\RunnerDuck.exe` location is accepted for compatibility with earlier installations. Other paths, including copied installation folders and `bin` output, show installation guidance and exit before taking the single-instance mutex, registering a tray icon, or changing startup settings. Copying `install-mode.txt` does not bypass this check. `--verify-animation` and widget COM activation retain their separate entry paths. No development bypass is provided; install the build to test the tray.

The runner remains the first Windows Forms notification icon, preserving its existing identity. Built-in metric icons reserve a fixed order and reuse the same icon objects when hidden and shown. Custom metric icons also reuse their objects during a process lifetime; changing the custom source set or arrival order across restarts can still affect their identities. The initial change to fixed metric ordering may change an existing metric's display preference. Older EXEs without the launch check can still create history when run elsewhere; updating does not delete arbitrary old app copies. Explorer restart and sign-out/sign-in behavior require separate verification.

Tray installation updates an existing RunnerDuck startup command to the installed path without recreating a removed entry or changing Windows' startup approval setting. After the app is copied successfully, it removes notification-area history only for replaced executable paths found in the running processes, build output, previous package, or existing startup command. The installed path's visibility preference, other apps, and RunnerDuck paths outside that set remain untouched. A backup of affected values is saved under `%LOCALAPPDATA%\RunnerDuck\TrayRegistrationBackups` before changes. If this repair fails, the installer warns and still starts the installed app; rerun installation to retry the repair. An already-open Windows taskbar settings page can retain an old list; navigate away and reopen it to refresh the view.

Switching from a widget mode to Tray copies settings, custom runners, and the AI usage snapshot to `%LOCALAPPDATA%\RunnerDuck` before removing the development package. Switching back copies those settings into the new package's LocalState. Previously pinned widgets may need to be added again. Tray-only launch-at-login uses the current user's Windows Run entry; packaged modes use the package startup task.
