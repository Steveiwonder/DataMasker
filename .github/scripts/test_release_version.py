"""Exercise release retry behavior without contacting GitHub or changing tags."""
import contextlib
import io
import json
import os
from pathlib import Path
import runpy
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

SCRIPT = Path(__file__).with_name("reserve-release.py")


class ReleaseVersionTests(unittest.TestCase):
    def execute(self, tags, revisions=None, release=None, error="gh: Not Found (HTTP 404)"):
        revisions = revisions or {}
        calls = []

        def output(args, **kwargs):
            if args == ["git", "tag", "--list"]:
                return "\n".join(tags)
            return revisions.get(args[-1], "other-commit")

        def run(args, **kwargs):
            calls.append(args)
            if args[0] == "gh":
                return subprocess.CompletedProcess(args, 0 if release else 1,
                                                   json.dumps(release), error)
            return subprocess.CompletedProcess(args, 0)

        with tempfile.TemporaryDirectory() as folder:
            result = Path(folder) / "output"
            tree = ET.ElementTree(ET.fromstring("<Project><VersionPrefix>3.0.0</VersionPrefix></Project>"))
            with patch.dict(os.environ, {"GITHUB_SHA": "current-commit", "GITHUB_REPOSITORY": "example/repo", "GITHUB_OUTPUT": str(result)}), \
                    patch("subprocess.check_output", side_effect=output), \
                    patch("subprocess.run", side_effect=run), \
                    patch("xml.etree.ElementTree.parse", return_value=tree), \
                    contextlib.redirect_stdout(io.StringIO()):
                runpy.run_path(str(SCRIPT), run_name="__main__")
            return result.read_text(), calls

    def test_stable_tags_only_determine_next_version(self):
        result, calls = self.execute(["3.0.2", "3.0.20-rc.1", "4.0.99"])
        self.assertIn("version=3.0.3", result)
        self.assertIn(["git", "push", "origin", "refs/tags/3.0.3"], calls)

    def test_first_release_starts_at_zero(self):
        result, _ = self.execute([])
        self.assertIn("version=3.0.0", result)

    def test_reserved_tag_retry_does_not_increment(self):
        result, calls = self.execute(["3.0.2", "3.0.3"], {"3.0.2": "current-commit"})
        self.assertIn("version=3.0.2", result)
        self.assertFalse(any(args[:2] == ["git", "push"] for args in calls))

    def test_existing_complete_release_reuses_bytes(self):
        names = ["DataMasker.3.0.2.nupkg", "DataMasker.Runner.3.0.2.net10.0.win-x64.zip",
                 "DataMasker.Runner.3.0.2.net11.0.win-x64.zip", "SHA256SUMS"]
        result, _ = self.execute(["3.0.2"], {"3.0.2": "current-commit"},
                                 {"assets": [{"name": name} for name in names]})
        self.assertIn("existing=true", result)

    def test_incomplete_release_fails_without_replacing_assets(self):
        with self.assertRaisesRegex(SystemExit, "incomplete assets"):
            self.execute(["3.0.2"], {"3.0.2": "current-commit"}, {"assets": []})

    def test_api_failure_is_not_treated_as_missing_release(self):
        with self.assertRaisesRegex(SystemExit, "HTTP 403"):
            self.execute(["3.0.2"], {"3.0.2": "current-commit"}, error="Forbidden (HTTP 403)")

    def test_ambiguous_existing_tags_fail(self):
        with self.assertRaisesRegex(SystemExit, "Multiple releases"):
            self.execute(["3.0.2", "3.0.3"], {"3.0.2": "current-commit", "3.0.3": "current-commit"})


if __name__ == "__main__":
    unittest.main()
