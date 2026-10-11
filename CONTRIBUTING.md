# Contributing to RunnerDuck

Keep Windows and macOS behavior aligned. Explain the observable problem and include validation appropriate to the change. Use one issue or pull request per coherent change.

Build and test commands are in [README.md](README.md). New animation behavior belongs in the pure state machines with boundary and transition tests on both platforms. Runtime code must use the shared atlas and manifest; keep generation caches and credentials outside Git.

Preserve source copyright headers and the full bundled legal notices when modifying reused code. Run `python scripts/verify_repository.py` before submitting a change. Asset updates also require `python scripts/verify_assets.py` with Pillow installed.

Custom runner contributions should include their own license and a small preview. The existing custom runner JSON format remains compatible. See [CustomRunners.md](docs/CustomRunners.md).
