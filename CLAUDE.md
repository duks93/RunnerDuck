# RunnerDuck development

Windows (.NET 10 / Windows Forms / Windows App SDK) and macOS (Swift 6.2 / SwiftUI / AppKit) share one duck atlas and manifest. Follow [ARCHITECTURE.md](ARCHITECTURE.md) and [CODING_STYLE.md](CODING_STYLE.md).

macOS layers are DataSource → Model → UserInterface; only UserInterface loads image/string resources. Windows separates Core, AIUsage, and App. Keep system effects at dependency boundaries and animation state machines independent of UI clocks.

The macOS main app is not sandboxed because enabled AI usage sources read local CLI state. Its widget uses a narrow read-only home-relative entitlement for the RunnerDuck snapshot. Local builds use ad-hoc signing; choose a real signing team only for distribution.

CI runs on main pushes, pull requests, and manual dispatch. Do not commit build outputs, cached generations, tokens, or local settings. Asset reproduction and tool versions live under artwork/duck. Preserve required legal attribution; product UI and identifiers use RunnerDuck.

When rebuilding an installed Windows app for the user to try, run `install.ps1` with the existing install mode so the installed executable, startup command, and tray identity stay at the same path. For Tray mode use `pwsh -File ./install.ps1 -InstallMode Tray`. Build-only output does not update the installed app. Ordinary tray startup now rejects non-installed paths before taking the mutex or writing startup settings; use the installer to test tray changes, never launch `bin` output directly. Animation verification and packaged widget COM activation retain their separate entry paths. The Tray installer backs up and removes entries for replaced executable paths while preserving the installed path's display preferences and startup opt-out.
