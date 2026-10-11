# Update publication

RunnerDuck is a public repository. Clients fetch `https://github.com/duks93/RunnerDuck/releases/latest/download/latest.json` on demand, with a 15-second timeout and without credentials. GitHub redirects the request to its release asset host. No separate repository, deploy key, or personal token is required.

`schemaVersion: 1` defines `commit` (the full lowercase SHA of the latest validated revision) and `ancestors` (all reachable ancestor SHAs). A matching commit is current; membership in `ancestors` means an update is available. An unrecognized commit is an unpublished build, not an assumed update or downgrade. Missing build metadata, invalid feeds, network errors, and HTTP errors are shown explicitly. Revision comparison happens locally; the installed revision is not sent in the request.

The `publish-update` job in `.github/workflows/test.yml` runs only after the Windows and macOS jobs succeed for `main` pushes or manual runs on `main`. Its short-lived `GITHUB_TOKEN` has `contents: write` only in the publishing job. It skips superseded commits and generates the manifest from complete Git history. `scripts/publish_update.sh` creates a draft release tagged `build-<full-commit-sha>`, uploads `latest.json`, then publishes and marks it latest. Each tag identifies its validated main commit, so GitHub's source archives correspond to the published source revision. No source branch commits or pushes are made by publication.

Retries resume a draft release. If the release is already published, the script verifies its existing manifest matches before marking it latest; it does not silently replace a published manifest. Failed CI never publishes an update. Until successful publication, clients see the previous validated release. Network or publication failures are visible in Actions. A stable release created manually must also contain a valid `latest.json`, otherwise update checks correctly report a failure.

These releases provide source archives and update metadata, not prebuilt installers. See the [README update instructions](../README.md#update-or-reinstall). Windows offline ZIP users need a new ZIP from the app owner. Installations made before this feature need one manual update before the app can check updates. The check runs only when the user clicks in Settings.

Local uncommitted changes are not represented by a Git commit identifier. Builds without Git metadata report an unknown build. macOS installers embed HEAD (direct Xcode builds can pass `RUNNERDUCK_COMMIT`); Windows uses `AssemblyInformationalVersion`. The Windows CI offline-bundle check verifies the packaged DLL's informational version against the source commit.

The JSON feed contains only version identifiers. Normal source commit messages and author information remain visible in the public source repository's Git history.
