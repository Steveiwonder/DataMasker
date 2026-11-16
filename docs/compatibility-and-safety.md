# Compatibility, safety and release changes

## Supported targets

The NuGet package contains .NET Standard 2.0 and .NET 10 assets. The runner
ships separately for .NET 10 and .NET 11 as self-contained Windows x64 ZIPs.
CI tests installed NuGet consumers on .NET Framework 4.7.2 (Windows), .NET 10
and .NET 11. SQL client platform support still applies; Standard compatibility
is not a promise of browser, mobile or Unity database support.

## Configuration changes

- Primary-key columns must be omitted from masking or marked `ignore: true`.
  Mutating the key used for paging and updates is not supported.
- `unique: true` on Computed or Sql columns is rejected before execution.
  These providers cannot reliably generate a new candidate on collision.
- Global mappings are shared between ordinary and unique columns. Reusing a
  mapping reserves its replacement in each destination unique column.
  A mapping conflicting with existing data fails explicitly; it is never
  silently changed after another row has used it.
- Local mappings and uniqueness sets include schema, table and column identity.
- `useValue: ""` is an intentional empty replacement.
- Ambiguous SQL parameter names (such as `First Name` and `First-Name`) fail
  validation. Existing underscore parameter spellings remain supported.
- All table configurations are validated before the runner starts writing.
  Database/runtime errors can still occur after earlier batches have committed;
  a run is not one atomic transaction.

SQL Server uniqueness seeding uses column collation metadata for case, accent,
width, kana, binary and trailing-space comparisons. SQL Server unique indexes
remain the final authority, including collation-version-specific behavior and
composite/filtered indexes which a per-column setting does not model. Custom
sources can implement `IUniqueValueComparerSource`.

## Dry-run and queries

Both single-row and batch update APIs honor `DryRun`. Scalar SQL providers
receive the configured timeout and execute inside a rolled-back transaction
when the runner is in dry-run mode. Custom library callers can use
`new SqlDataProvider(connection, commandTimeout, dryRun: true)`.

Configured scalar queries should be read-only. Transaction rollback cannot
undo external effects, explicit transaction control, sequences or other
nontransactional operations. Use appropriately restricted database credentials;
SQL configuration is trusted executable input. Post-processing statements are
skipped during dry-run.

## Performance

SQL Server staging tables and their schema are reused across batches within a
table operation. Provider resolution is cached. Existing keyset pagination and
bulk updates remain in place. Mapping synchronization protects the central
provider generation path; a masker/configuration is intended for one masking
run, with configuration unchanged during that run.

Unique sets and global/local mappings retain distinct values for the run;
batching does not bound their total memory. Monitor memory on large datasets.
Moving those mappings to disk is a separate architecture change. Likewise,
SQL scalar queries still execute per row: automatic caching would change
nondeterministic or row-dependent query semantics. Benchmark representative
workloads before adding explicit batched-query/cache modes or parallelism.
No numerical speed-up claim is made without a controlled benchmark.

## CI and release procedure

PR validation covers Windows/Linux and both modern runtimes, installed package
consumers, required SQL Server integration jobs and extracted Windows runners.
For local SQL integration set `DATAMASKER_TEST_SQL_CONNECTION` to a disposable
SQL Server database connection; tests create/drop uniquely named tables.
`DATAMASKER_REQUIRE_SQL_TESTS=true` makes unavailable SQL infrastructure a failure.
Without these settings, Windows uses LocalDB and unavailable LocalDB is skipped.

Master publication reserves one stable version per commit, builds both runner
ZIPs and the NuGet package, records SHA256 checksums, creates a draft GitHub
release, publishes through NuGet OIDC, then publishes the GitHub release.
Retries reuse reserved tags and existing complete release assets. Incomplete
release assets require inspection rather than silently replacing a package.
A self-contained runtime receives security updates only when republished.

Workflow token permissions are explicitly minimal. Repository-wide branch
protection and the default token/PR-approval settings are administrative settings
outside the PR; configure required validation/reviews and disable unnecessary
Actions PR approvals in repository settings. The workflow restricts production
publication to master. NuGet's existing trusted-publishing policy must continue
to reference `.github/workflows/release.yml`.

Global mapping identity continues to use exact original values; it does not
infer relational joins or normalize originals using one arbitrarily chosen
table's collation. Related columns with differently cased originals need an
explicit normalization/mapping strategy. Global mappings are keyed by column
name, as before; differently named foreign-key columns are not automatically
associated.
