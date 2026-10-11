"""Exercise feed generation with real temporary Git histories, without publishing."""
from pathlib import Path
import subprocess
import tempfile
import unittest

from create_update_manifest import create_manifest


class UpdateManifestTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.repository = Path(self.directory.name) / "source"
        self.repository.mkdir()
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Feed Test")
        self.git("config", "user.email", "test@example.invalid")
        self.git("-c", "commit.gpgsign=false", "commit", "--allow-empty", "-m", "Initial test commit")
        self.initial = self.git("rev-parse", "HEAD")

    def git(self, *arguments):
        return subprocess.check_output(
            ["git", *arguments], cwd=self.repository, text=True, stderr=subprocess.DEVNULL
        ).strip()

    def test_feed_contains_only_identifiers_for_requested_revision(self):
        self.git("-c", "commit.gpgsign=false", "commit", "--allow-empty", "-m", "Private message must not be published")
        latest = self.git("rev-parse", "HEAD")
        self.assertEqual(create_manifest(self.repository, latest), {
            "schemaVersion": 1, "commit": latest, "ancestors": [self.initial],
        })
        self.assertEqual(create_manifest(self.repository, self.initial), {
            "schemaVersion": 1, "commit": self.initial, "ancestors": [],
        })

    def test_missing_ancestry_is_rejected(self):
        shallow = Path(self.directory.name) / "shallow"
        subprocess.run(
            ["git", "clone", "--depth=1", self.repository.as_uri(), str(shallow)],
            check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        )
        with self.assertRaisesRegex(ValueError, "full history"):
            create_manifest(shallow, self.initial)

    def test_ref_names_and_command_options_are_rejected(self):
        for value in ("main", "HEAD", "--all", "../main", "z" * 40):
            with self.subTest(value=value), self.assertRaises(ValueError):
                create_manifest(self.repository, value)


if __name__ == "__main__":
    unittest.main()
