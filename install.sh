#!/bin/bash
# Build, install, and launch RunnerDuck on macOS with the existing local settings.
set -euo pipefail

launch_app=true
case "${1:-}" in
    '') ;;
    --no-launch) launch_app=false ;;
    *) printf 'Usage: bash install.sh [--no-launch]\n' >&2; exit 2 ;;
esac
if [[ $# -gt 1 ]]; then printf 'Too many arguments.\n' >&2; exit 2; fi
if [[ "$(uname -s)" != Darwin ]]; then
    printf 'Use pwsh -File install.ps1 on Windows.\n' >&2
    exit 1
fi
if ! xcodebuild -version >/dev/null 2>&1; then
    printf 'Install and open Xcode, then select it with xcode-select before running this command.\n' >&2
    exit 1
fi

repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
build_dir="$repo_dir/artifacts/mac-build.noindex"
build_log="$repo_dir/artifacts/mac-build.log"
bundle_id=io.github.duks93.RunnerDuck
mkdir -p "$repo_dir/artifacts"
source_commit=""
if [[ "$(git -C "$repo_dir" rev-parse --show-toplevel 2>/dev/null)" == "$repo_dir" ]]; then
    source_commit="$(git -C "$repo_dir" rev-parse HEAD)"
elif [[ "${RUNNERDUCK_SOURCE_COMMIT:-}" =~ ^[0-9a-f]{40}$ ]]; then
    source_commit="$RUNNERDUCK_SOURCE_COMMIT"
fi
printf 'Building RunnerDuck (Release)...\n'
if ! xcodebuild build -project "$repo_dir/RunnerDuck.xcodeproj" -scheme RunnerDuck \
    -configuration Release -derivedDataPath "$build_dir" -skipPackagePluginValidation \
    CODE_SIGN_IDENTITY=- CODE_SIGN_STYLE=Manual DEVELOPMENT_TEAM= \
    RUNNERDUCK_COMMIT="$source_commit" >"$build_log" 2>&1; then
    tail -n 80 "$build_log" >&2
    printf 'Build failed. Full log: %s\n' "$build_log" >&2
    exit 1
fi
built_app="$build_dir/Build/Products/Release/RunnerDuck.app"
if [[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$built_app/Contents/Info.plist")" != "$bundle_id" ]]; then
    printf 'The build did not produce the expected RunnerDuck app.\n' >&2
    exit 1
fi
widget_plist="$built_app/Contents/PlugIns/RunnerDuckWidget.appex/Contents/Info.plist"
if [[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$widget_plist")" != "$bundle_id.Widget" ]]; then
    printf 'The built app is missing its RunnerDuck widget.\n' >&2
    exit 1
fi
codesign --verify --deep --strict "$built_app"

mkdir -p "$HOME/Applications"
install_dir="$(cd "$HOME/Applications" && pwd -P)"
installed_app="$install_dir/RunnerDuck.app"
if [[ -L "$installed_app" ]]; then
    printf 'The install destination must not be a symbolic link: %s\n' "$installed_app" >&2
    exit 1
fi
if [[ -e "$installed_app" ]]; then
    if [[ ! -d "$installed_app" ]] || [[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$installed_app/Contents/Info.plist")" != "$bundle_id" ]]; then
        printf 'The destination is not an existing RunnerDuck app: %s\n' "$installed_app" >&2
        exit 1
    fi
fi
stage_dir="$(mktemp -d "$install_dir/.RunnerDuck-install.XXXXXX")"
cleanup() {
    result=$?
    if [[ -d "$stage_dir/previous.app" && ! -e "$installed_app" ]]; then
        if ! mv "$stage_dir/previous.app" "$installed_app"; then
            printf 'Previous app preserved for recovery: %s/previous.app\n' "$stage_dir" >&2
            return 1
        fi
    fi
    case "$stage_dir" in
        "$install_dir"/.RunnerDuck-install.*) rm -rf "$stage_dir" ;;
    esac
    return "$result"
}
trap cleanup EXIT

ditto "$built_app" "$stage_dir/RunnerDuck.app"
codesign --verify --deep --strict "$stage_dir/RunnerDuck.app"
if pgrep -u "$(id -u)" -x RunnerDuck >/dev/null; then
    printf 'Closing RunnerDuck for the update...\n'
    pkill -u "$(id -u)" -x RunnerDuck || true
    for attempt in {1..20}; do
        if ! pgrep -u "$(id -u)" -x RunnerDuck >/dev/null; then break; fi
        sleep 0.2
    done
    if pgrep -u "$(id -u)" -x RunnerDuck >/dev/null; then
        printf 'Close RunnerDuck and run this command again.\n' >&2
        exit 1
    fi
fi
if [[ -e "$installed_app" ]]; then mv "$installed_app" "$stage_dir/previous.app"; fi
mv "$stage_dir/RunnerDuck.app" "$installed_app"
lsregister=/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister
if [[ -x "$lsregister" ]]; then
    "$lsregister" -u "$built_app"
    "$lsregister" -f "$installed_app"
fi
printf 'Installed RunnerDuck: %s\n' "$installed_app"
if [[ "$launch_app" == true ]]; then
    open "$installed_app"
    printf 'RunnerDuck launched.\n'
fi
