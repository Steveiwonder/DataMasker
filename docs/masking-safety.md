# Masking correctness and SQL safety

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
- Computed columns cannot reference active SQL columns, themselves, or later computed columns; those references would copy original values before masking. Put computed dependencies first.
- Ordinary columns retain explicit constant/list and null/blank retention precedence over value mappings. Different explicit overrides do not promise shared global replacements.
- Ambiguous SQL parameter names (such as `First Name` and `First-Name`) fail
  validation. Existing underscore parameter spellings remain supported.
- All table configurations are validated before the runner starts writing.
  Database/runtime errors can still occur after earlier batches have committed;
  a run is not one atomic transaction.

SQL Server uniqueness seeding uses column collation metadata for case, accent,
width, kana, binary and trailing-space comparisons. SQL Server unique indexes
remain the final authority, including collation-version-specific behavior and
composite/filtered indexes which a per-column setting does not model. Custom
sources can implement `IUniqueValueComparerSource`. Seed destination values before
masking a table. Source equality rules replace default rules for supplemental
seeds before masking starts; once reservations exist, their comparison rules
remain stable for the rest of the run.

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

## SQL integration tests

Set `DATAMASKER_TEST_SQL_CONNECTION` to a disposable SQL Server database connection; tests create and drop uniquely named tables. Set `DATAMASKER_REQUIRE_SQL_TESTS=true` to fail when SQL infrastructure is unavailable. Otherwise Windows uses LocalDB and skips unavailable SQL tests.

Global mapping identity continues to use exact original values; it does not
infer relational joins or normalize originals using one arbitrarily chosen
table's collation. Related columns with differently cased originals need an
explicit normalization/mapping strategy. Global mappings are keyed by column
name, as before; differently named foreign-key columns are not automatically
associated.
