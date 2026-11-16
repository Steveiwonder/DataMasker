"""Reserve a stable tag once; retries of the same commit reuse the same version."""
import json
import os
import re
import subprocess
import xml.etree.ElementTree as ET


def git(*args):
    return subprocess.check_output(["git", *args], text=True).strip()


prefix = ET.parse("Directory.Build.props").findtext(".//VersionPrefix")
if not prefix or not re.fullmatch(r"\d+\.\d+\.\d+", prefix):
    raise SystemExit("VersionPrefix must be a stable major.minor.patch version")
major_minor = prefix.rsplit(".", 1)[0]
pattern = re.compile(re.escape(major_minor) + r"\.\d+")
sha = os.environ["GITHUB_SHA"]
tags = [tag for tag in git("tag", "--list").splitlines() if pattern.fullmatch(tag)]
existing = [tag for tag in tags if git("rev-list", "-n", "1", tag) == sha]
if len(existing) > 1:
    raise SystemExit("Multiple releases already point to this commit; resolve explicitly")
if existing:
    version = existing[0]
else:
    patch = max([int(tag.rsplit(".", 1)[1]) for tag in tags], default=-1) + 1
    version = f"{major_minor}.{patch}"
    subprocess.run(["git", "tag", version, sha], check=True)
    subprocess.run(["git", "push", "origin", f"refs/tags/{version}"], check=True)

# A complete draft already contains the exact package for this version. Never
# replace it with a rebuild on a retry (SDK patches can change build bytes).
release = subprocess.run(
    ["gh", "api", f"repos/{os.environ['GITHUB_REPOSITORY']}/releases/tags/{version}"],
    text=True, capture_output=True,
)
if release.returncode and "HTTP 404" not in release.stderr:
    raise SystemExit(release.stderr)
ready = False
if release.returncode == 0:
    data = json.loads(release.stdout)
    assets = {asset["name"] for asset in data["assets"]}
    expected = {
        f"DataMasker.{version}.nupkg",
        f"DataMasker.Runner.{version}.net10.0.win-x64.zip",
        f"DataMasker.Runner.{version}.net11.0.win-x64.zip",
        "SHA256SUMS",
    }
    ready = expected.issubset(assets)
    if not ready:
        raise SystemExit("Release exists with incomplete assets; inspect it before retrying")

with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
    output.write(f"version={version}\nexisting={'true' if ready else 'false'}\n")
print(f"Release {version}: {'reuse immutable assets' if ready else 'build assets'}")
