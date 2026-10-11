#!/usr/bin/env bash
set -euo pipefail

: "${VALIDATED_SHA:?Set the full CI-validated source commit}"
: "${GH_REPO:?Set the GitHub repository}"
git fetch origin main
if [[ "$(git rev-parse origin/main)" != "$VALIDATED_SHA" ]]; then
    printf 'Skipping superseded source revision.\n'
    exit 0
fi

publication_dir="$(mktemp -d)"
trap 'rm -rf "$publication_dir"' EXIT
python3 scripts/create_update_manifest.py --commit "$VALIDATED_SHA" --output "$publication_dir/latest.json"
tag="build-$VALIDATED_SHA"
cat > "$publication_dir/notes.md" <<EOF
Version: ${VALIDATED_SHA:0:12}

This release does not include prebuilt installers. To update, get the latest source and run the installer again.

See the [installation and update instructions](https://github.com/duks93/RunnerDuck#update-or-reinstall).
EOF

if ! gh release view "$tag" > /dev/null 2>&1; then
    gh release create "$tag" --target "$VALIDATED_SHA" --draft \
        --title "RunnerDuck build ${VALIDATED_SHA:0:12}" --notes-file "$publication_dir/notes.md"
fi
if [[ "$(gh release view "$tag" --json isDraft --jq .isDraft)" == true ]]; then
    gh release upload "$tag" "$publication_dir/latest.json" --clobber
    gh release edit "$tag" --draft=false --latest
else
    mkdir "$publication_dir/published"
    gh release download "$tag" --pattern latest.json --dir "$publication_dir/published"
    cmp "$publication_dir/latest.json" "$publication_dir/published/latest.json"
    gh release edit "$tag" --latest
fi
