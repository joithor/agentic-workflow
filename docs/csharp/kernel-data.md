# Aw.Kernel.Data

SQLite access for every C# package: hand-written ADO.NET over `Microsoft.Data.Sqlite` with an `IRowMapper<T>` per row
type (decision R8, `docs/readiness/aot-spike.md`), the unit of work, and the migrations runner (spec §4b.2). Tier 0
in `deps.yaml`: `Aw.Kernel` plus one external package.

## What's in it

| Type | Job |
|---|---|
| `SqliteConnectionFactory` / `SqliteOptions` | Opens a connection with `journal_mode=WAL`, `busy_timeout`, `foreign_keys=ON`, `synchronous=NORMAL`. Pooling is off |
| `SqliteBind` | Typed parameters (`Bind` for text, integer, real, boolean, blob; null is SQL NULL) and `BindId(name, id, accessor)` for strongly typed ids |
| `IRowMapper<T>` | Maps one row, columns by ordinal |
| `SqlStatement`, `SqliteQueries` | `One`, `List`, `Exists`, `Execute`, each returning `Result<T>` |
| `ISqliteSession`, `SqliteUnitOfWork`, `SqliteUnitOfWorkFactory` | One `BEGIN IMMEDIATE` transaction per use case; disposing without `Commit` rolls back |
| `SqliteReadRepository<TEntity,TId>` | Implements `Aw.Kernel.Persistence.IReadRepository` from a repository's two SQL constants and its id binder |
| `Migration`, `EmbeddedMigrations`, `MigrationRunner` | Numbered `NNNN_name.sql` resources, `schema_version` with SHA-256 checksums, one transaction per migration |

The ports a `Core` project declares against (`IReadRepository<TEntity,TId>`, `IUnitOfWork`, `IUnitOfWorkFactory`) live
in `Aw.Kernel.Persistence`, because `Core` references `Aw.Kernel` only (spec §4b.1).

## Errors

Expected store failures are `Result` errors; everything else (broken SQL, a mapper bug) throws.

| Code | Kind | When |
|---|---|---|
| AW-DATA-001 | notFound | `One` found no row |
| AW-DATA-002 | unavailable | The file can't be opened, read or written (missing directory, read-only, full, not a database) |
| AW-DATA-003 | timeout | Another connection held the lock past the busy timeout |
| AW-DATA-004 | conflict | A constraint failed (unique, foreign key, not null, check) |
| AW-DATA-005 | invalid | The migration set has a gap, a duplicate, the wrong order, or a file without a version |
| AW-DATA-006 | conflict | An applied migration's text changed |
| AW-DATA-007 | conflict | The database has a migration this build doesn't know |

## Migrations

- One file per change, `NNNN_name.sql`, embedded as a resource. Versions run 1, 2, 3 with no gaps.
- The checksum is SHA-256 of the UTF-8 text with CRLF turned into LF, so a checkout's line endings never look like an
  edit. An applied migration never changes: add a new one.
- `Migrate(set, ct, targetVersion: n)` stops at `n`, so a package's migration test can apply each step to a copy of the
  previous schema (spec §4b.2).
- A migration whose SQL fails is a bug: its transaction rolls back (DDL included) and the exception propagates.

## Busy timeout

`busy_timeout` is set in milliseconds, but `Microsoft.Data.Sqlite` also retries `SQLITE_BUSY` itself until the
command timeout, which it counts in whole seconds. The factory sets that to the busy timeout rounded up to a second,
so a 300 ms busy timeout waits about 1.3 s before AW-DATA-003 (measured in the C1 smoke run).

## Native SQLite: how it ships

**Decision: the side-by-side `libe_sqlite3` file**, one per AOT tool and platform, listed in `binaries.lock` next to
the tool (spec §4c B6).

- `Microsoft.Data.Sqlite` 10.0.12 pulls `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12. Its `lib.e_sqlite3` package ships a
  static archive (`e_sqlite3.a`) only for `browser-wasm`. For `osx-arm64`, `osx-x64` and `linux-x64` it ships
  `libe_sqlite3.dylib` / `libe_sqlite3.so` only, so a NativeAOT static link (one file per tool) would mean building
  SQLite ourselves. That isn't worth it for R1.
- The OS SQLite (`SQLitePCLRaw.bundle_sqlite3`) would also give one file, but the SQLite version would then differ
  between machines (macOS ships its own; Linux distributions vary). Byte-compatible goldens (spec §4c) want the same
  SQLite everywhere, so the bundled build wins.
- A NativeAOT publish puts `libe_sqlite3.dylib` (1.7 MB on osx-arm64) next to the binary. `setup.sh` and
  `binaries.lock` must install and check both files.

## AOT numbers assume InvariantGlobalization

The C1 smoke (and the A5 spike numbers) were published with `InvariantGlobalization=true`. Every AOT entry point
should set it: the toolkit formats with `CultureInfo.InvariantCulture` anyway, and it keeps ICU out of the startup
path. The macOS binary still links `libicucore`, but doesn't load culture data at run time.
