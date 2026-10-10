# Aw.Kernel.Data

SQLite access for every C# package: hand-written ADO.NET over `Microsoft.Data.Sqlite` (decision R8,
`docs/readiness/aot-spike.md`), the unit of work, read sessions and the migrations runner (spec §4b.2). Tier 0 in
`deps.yaml`: `Aw.Kernel` plus one external package.

**Who owns what.** Kernel.Data provides connections, sessions, binders, readers and the runner. Each package's
`Infrastructure` project owns its row mappers (`IRowMapper<T>`), its SQL and its migrations. Strongly typed ids follow
`docs/csharp/typed-ids.md` (PR #100); bind them with `BindId(name, id, accessor)` for now. Once #100 lands, an
`ITypedId`-constrained `BindId`/`GetId` can replace the accessor.

## What's in it

| Type | Job |
|---|---|
| `SqliteConnectionFactory` / `SqliteOptions` | `Open`: read-write, creates the file, sets `busy_timeout`, `journal_mode=WAL` (checked: a connection that can't get WAL is refused), `foreign_keys=ON`, `synchronous=NORMAL`. `OpenReadOnly`: `query_only`, never creates the file, doesn't touch the journal mode. Pooling is off |
| `SqliteBind` | Typed parameters: text, integer (`int`, `long`), real, boolean (0/1), blob, `DateTimeOffset` (UTC round-trip text, `2026-10-10T07:05:07.0420000Z`, the same text AwJson writes), `Guid` (lowercase hyphenated text), and `BindId(name, id, accessor)` for typed ids stored as text, integer or Guid. Cast a bare `null` (`(string?)null`) |
| `SqliteRead` | `GetGuid`: reads lowercase hyphenated text strictly; a BLOB, upper case or braces throw `InvalidDataException`. `GetTimestamp`: reads only the UTC round-trip text `SqliteBind` writes |
| `IRowMapper<T>` | Maps one row, columns by ordinal |
| `SqlStatement`, `SqliteQueries` | `One`, `List`, `Exists`, `Execute`, each returning `Result<T>` |
| `SqliteUnitOfWork` / `SqliteUnitOfWorkFactory.BeginSession` | One `BEGIN IMMEDIATE` per use case; disposing without `Commit` rolls back. Implements `Aw.Kernel.Persistence.IUnitOfWork` |
| `SqliteReadSession` / `BeginRead` | A deferred transaction on a read-only connection: one snapshot, no write lock, reads beside a writer, no file created. For status, doctor and report commands |
| `SqliteReadRepository<TEntity,TId>` | Implements `IReadRepository` from a repository's two SQL constants and its id binder |
| `SqliteOutbox` / `SqliteOutboxSchema` | Implements `Aw.Kernel.Outbox.IOutbox` on the session it's given (normally the use case's `SqliteUnitOfWork`, so a command commits or rolls back with the state change). A package adds `SqliteOutboxSchema.CreateMigration(<version>)` to its migration set to create `outbox_commands`. Time from the injected `TimeProvider`, retries from `OutboxRetryPolicy` |
| `Migration`, `EmbeddedMigrations`, `MigrationRunner` | See below |

Every path that fails closes the connection it opened, cancellation included; disposing a session closes the
connection even if the rollback throws.

## Contracts

`SqliteReadRepository`, `SqliteUnitOfWorkFactory` and `SqliteOutbox` pass the shared port contracts in `Aw.Kernel.Testing`
(`ReadRepositoryContract`, `UnitOfWorkContract`, `OutboxContract`), the same suites the in-memory fakes pass
(`tests/Aw.Kernel.Data.Tests/Sqlite*ContractTests.cs`, integration lane). A new adapter for either port (another engine)
binds the same base.

## Errors

Expected store failures are `Result` errors; everything else (broken SQL, a mapper bug, `SQLITE_LOCKED`) throws.

| Code | Kind | When |
|---|---|---|
| AW-DATA-001 | notFound | `One` found no row |
| AW-DATA-002 | unavailable | The file can't be opened, read or written (missing, read-only, full, permission), or WAL isn't available there |
| AW-DATA-003 | timeout | Another connection held the lock past the busy timeout |
| AW-DATA-004 | conflict | A constraint failed, or a migration left foreign-key violations |
| AW-DATA-005 | invalid | The migration set is empty, has a gap, a duplicate, the wrong order, or a file without a version; or no embedded resources matched the prefix |
| AW-DATA-006 | conflict | The applied history doesn't match the build: an applied migration's text or name changed, or `schema_version` isn't exactly 1..n |
| AW-DATA-007 | conflict | The database has a migration this build doesn't know |
| AW-DATA-008 | invalid | The file is damaged or isn't a SQLite database. Not retryable: restore from a backup |
| AW-DATA-009 | conflict | The database has tables but no `schema_version`, or a `schema_version` whose columns aren't exactly `version, name, checksum, applied_at`: this runner didn't create it (usually the wrong path). Nothing is written |

## Migrations

- One file per change, `NNNN_name.sql`, embedded as a resource. Versions run 1, 2, 3 with no gaps. An applied
  migration never changes: add a new one.
- A package that uses the outbox adds the one migration Kernel.Data provides in code: load its own set with
  `EmbeddedMigrations`, insert `SqliteOutboxSchema.CreateMigration(<n>)` at the version it picks, and pass the list to
  `MigrationRunner.Migrate`. The SQL belongs to Kernel.Data but is recorded in the package's `schema_version`, so
  Kernel.Data never changes it in place: a later change to `outbox_commands` ships as a new Kernel.Data migration that
  each package adds as its own next version.
- The checksum is SHA-256 of the UTF-8 text without a leading BOM and with CRLF or lone CR turned into LF, so line
  endings or a different loader never look like an edit.
- Before anything is written, the runner checks the set, then checks the file on a read-only connection: who owns it
  (AW-DATA-009) and its applied history (AW-DATA-006, AW-DATA-007). A refused file is left byte-for-byte unchanged; only
  after these checks does the read-write open switch it to WAL.
- Before upgrading a database that already has migrations, it writes a consistent copy with `VACUUM INTO` to
  `<file>.bak-v<N>`. The first backup of a version is kept; the copy is moved into place atomically.
- Each migration runs in its own transaction with its `schema_version` row. `foreign_keys` is switched off before
  `BEGIN` (inside a transaction the pragma does nothing), so the SQLite table-rebuild recipe works without cascading
  deletes; `PRAGMA foreign_key_check` runs before commit, and violations roll back as AW-DATA-004. It is switched back on
  afterwards.
- Two runners at once apply each migration once; both report the final version.
- `Migrate(set, ct, targetVersion: n)` stops at `n`, so a package's migration test can apply each step to a copy of
  the previous schema.
- The C# build starts from fresh state: no database the TS build wrote is read or migrated (owner decision,
  2026-10-10), so the runner keeps its version only in `schema_version`, never in `PRAGMA user_version`.

## Durability: synchronous=NORMAL

In WAL mode, `synchronous=NORMAL` can't corrupt the database, and survives an application crash. On power loss or an
OS crash it can lose the last few committed transactions. That's the trade for a local toolkit; a package that needs
every commit on disk sets `synchronous=FULL` on its own connections. It only holds while the file really is in WAL mode,
which is why `Open` refuses a connection that didn't get WAL.

## Busy timeout

`busy_timeout` is set in milliseconds, but `Microsoft.Data.Sqlite` also retries `SQLITE_BUSY` itself until the
command timeout, which it counts in whole seconds. The factory sets that to the busy timeout rounded up to a second,
so a 300 ms busy timeout waits about 1.3 s before AW-DATA-003.

## Native SQLite: how it ships

**The bundled `e_sqlite3`, as a side-by-side file** (`libe_sqlite3.dylib` / `.so`, 1.7 MB on osx-arm64) next to each
AOT tool, listed in `binaries.lock` (spec §4c B6; the decision is recorded by the spec PR, S1).

- `Microsoft.Data.Sqlite` 10.0.12 pulls `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12, which ships a static archive only for
  browser-wasm; macOS and Linux get the shared library only, so a static link would mean building SQLite ourselves.
- The bundled build gives every host the same SQLite version and features (JSON, WAL), so behaviour doesn't depend on
  the OS's SQLite.
- `setup.sh` installs and checks both files; a hook or provider installer must never copy the binary alone.
- `src/Aw.Kernel.Data.AotSmoke/run.sh` publishes the canary with NativeAOT, fails on any IL2xxx/IL3xxx warning and
  checks its output, so the side-by-side layout and the embedded migrations are exercised on every run of the AOT job.

## AOT tools set InvariantGlobalization

The canary (and the A5 spike numbers) use `InvariantGlobalization=true`. Every AOT entry point sets it: the toolkit
formats with `CultureInfo.InvariantCulture`, and a Linux AOT binary would otherwise need `libicu` at run time.
