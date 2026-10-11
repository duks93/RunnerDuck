# RunnerDuck privacy

RunnerDuck reads system metrics locally. Settings, custom runner files, cached AI usage, and logs stay in the app's data directory. No analytics or advertising SDK is included.

AI usage sources are optional. When enabled, their integrations may read locally installed CLI configuration, session records, or credentials and contact the corresponding provider's usage endpoint. Credentials are not sent to the RunnerDuck repository. The widget reads the app's local usage snapshot.

Custom metrics read user-selected JSON files. Example scripts may contact external services; their source and setup instructions describe that behavior. Opening a documentation or issue link uses your browser.

Duck images are generated during development and bundled with the app. The animation never sends CPU readings or images to a generation service at runtime.

Checking for updates runs only when you click it in Settings. It downloads public version information from RunnerDuck's GitHub Releases and compares it on your device. Your installed version and usage data are not sent.
