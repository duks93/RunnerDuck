"""Publish only commit identifiers from a fully fetched, CI-validated checkout."""
import argparse
import json
from pathlib import Path
import re
import subprocess


def create_manifest(repository: Path, commit: str) -> dict:
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("A full lowercase commit SHA is required.")
    shallow = subprocess.check_output(
        ["git", "rev-parse", "--is-shallow-repository"], cwd=repository, text=True
    ).strip()
    if shallow != "false":
        raise ValueError("Fetch full history before generating an update manifest.")
    commits = subprocess.check_output(
        ["git", "rev-list", commit], cwd=repository, text=True
    ).splitlines()
    if not commits or commits[0] != commit or any(
        not re.fullmatch(r"[0-9a-f]{40}", value) for value in commits
    ):
        raise ValueError("Git returned invalid commit history.")
    return {"schemaVersion": 1, "commit": commit, "ancestors": commits[1:]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", type=Path, default=Path.cwd())
    parser.add_argument("--commit", required=True)
    parser.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args()
    manifest = create_manifest(arguments.repository, arguments.commit)
    arguments.output.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
