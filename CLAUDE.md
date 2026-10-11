# RunnerDuck development

Windows (.NET 10 / Windows Forms / Windows App SDK) and macOS (Swift 6.2 / SwiftUI / AppKit) share one duck atlas and manifest. Follow [ARCHITECTURE.md](ARCHITECTURE.md) and [CODING_STYLE.md](CODING_STYLE.md).

macOS layers are DataSource → Model → UserInterface; only UserInterface loads image/string resources. Windows separates Core, AIUsage, and App. Keep system effects at dependency boundaries and animation state machines independent of UI clocks.

The macOS main app is not sandboxed because enabled AI usage sources read local CLI state. Its widget uses a narrow read-only home-relative entitlement for the RunnerDuck snapshot. Local builds use ad-hoc signing; choose a real signing team only for distribution.

CI runs on main pushes, pull requests, and manual dispatch. Do not commit build outputs, cached generations, tokens, or local settings. Asset reproduction and tool versions live under artwork/duck. Preserve required legal attribution; product UI and identifiers use RunnerDuck.
