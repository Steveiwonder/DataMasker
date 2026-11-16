# CI and release procedure

PR validation covers Windows/Linux and .NET 10/11, installed NuGet consumers,
required SQL Server integration jobs and extracted Windows runner executables.
The Windows legacy consumer executes the .NET Standard asset under .NET Framework
4.7.2. Package restores use committed lock files and SDK selection honors
`global.json`. The consumer has its own NuGet configuration so local package
and public feed paths work consistently on Windows and Linux.

For local SQL integration set `DATAMASKER_TEST_SQL_CONNECTION` to a disposable
SQL Server database connection; tests create/drop uniquely named tables.
`DATAMASKER_REQUIRE_SQL_TESTS=true` makes unavailable SQL infrastructure a failure.
Without these settings, Windows uses LocalDB and unavailable LocalDB is skipped.
CI provisions SQL Server explicitly and requires these tests to execute.

## Publishing

Master pushes validate once through the reusable workflow. Unless the commit
message contains `[skip release]`, publication reserves one stable version per
commit. It builds self-contained Windows x64 ZIPs for .NET 10 and .NET 11 and
one NuGet package, records SHA256 checksums, creates a draft GitHub release,
publishes through NuGet OIDC, then publishes the GitHub release.

Runner files include version, framework and architecture in their names:
`DataMasker.Runner.<version>.net10.0.win-x64.zip` and
`DataMasker.Runner.<version>.net11.0.win-x64.zip`. Both extracted executable
variants run an in-memory masking fixture before publication.

Retries reuse reserved tags and existing complete release assets, including
the exact NuGet package bytes. Rerun failed jobs after correcting publishing
configuration; rerunning the full workflow for the same commit also reuses the
version. Incomplete release assets fail explicitly and require inspection rather
than silently replacing a package. Release concurrency serializes publishing;
GitHub can replace older pending runs when additional pushes arrive.
A self-contained runtime receives security updates only when republished.

## Permissions and maintenance

Workflow token permissions are explicitly minimal. Production publication only
runs on master. Actions are pinned to reviewed commit IDs; Dependabot checks
Actions and NuGet dependencies weekly. Test reports are retained for 14 days.

Repository-wide branch protection and default token/PR-approval settings are
administrative settings outside these workflows. Configure required validation
and reviews, and disable unnecessary Actions PR approvals in repository settings.
NuGet's existing trusted-publishing policy must continue to reference
`.github/workflows/release.yml`.

Release-version behavior is covered by isolated Python tests:

```sh
python3 -m unittest discover -s .github/scripts -p 'test_*.py'
```
