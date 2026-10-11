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

Switching from a widget mode to Tray copies settings, custom runners, and the AI usage snapshot to `%LOCALAPPDATA%\RunnerDuck` before removing the development package. Switching back copies those settings into the new package's LocalState. Previously pinned widgets may need to be added again. Tray-only launch-at-login uses the current user's Windows Run entry; packaged modes use the package startup task.
