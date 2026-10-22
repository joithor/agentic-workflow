using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>The index file's schema, version 1 (spec §4.2). A different version is rebuilt from scratch, never migrated.</summary>
internal static class IndexSchema
{
    // Keep equal to CodeIndexRules.SchemaVersion; a store test pins it.
    private const string Version = "PRAGMA user_version = 1";

    private const string Tables = "CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL); "
        + "CREATE TABLE layers (layer TEXT PRIMARY KEY, stamp TEXT NOT NULL, status TEXT NOT NULL, "
        + "code TEXT NULL, detail TEXT NOT NULL, built_at TEXT NOT NULL); "
        + "CREATE TABLE files (path TEXT PRIMARY KEY, blob TEXT NOT NULL, size INTEGER NOT NULL, "
        + "lang TEXT NOT NULL, parsed INTEGER NOT NULL); "
        + "CREATE TABLE symbols ( "
        + "id INTEGER PRIMARY KEY, file TEXT NOT NULL REFERENCES files(path) ON DELETE CASCADE, "
        + "ref TEXT NOT NULL UNIQUE, name TEXT NOT NULL, qualified TEXT NOT NULL, kind TEXT NOT NULL, arity INTEGER NOT NULL, "
        + "start_line INTEGER NOT NULL, end_line INTEGER NOT NULL, exported INTEGER NOT NULL, utility INTEGER NOT NULL, "
        + "signature TEXT NOT NULL, shape_hash TEXT NOT NULL, token_count INTEGER NOT NULL, complexity INTEGER NOT NULL, "
        + "minhash BLOB NULL); "
        + "CREATE INDEX symbols_name ON symbols(name); "
        + "CREATE INDEX symbols_file ON symbols(file); "
        + "CREATE INDEX symbols_shape ON symbols(shape_hash); "
        + "CREATE TABLE edges ( "
        + "src INTEGER NOT NULL REFERENCES symbols(id) ON DELETE CASCADE, "
        + "dst INTEGER NULL REFERENCES symbols(id) ON DELETE SET NULL, "
        + "dst_name TEXT NOT NULL, "
        + "kind TEXT NOT NULL, basis TEXT NOT NULL, confidence TEXT NOT NULL, line INTEGER NOT NULL, "
        + "candidates INTEGER NOT NULL); "
        + "CREATE UNIQUE INDEX edges_uq ON edges(src, dst_name, kind, basis, line, IFNULL(dst, 0)); "
        + "CREATE TABLE call_sites ( "
        + "src INTEGER NOT NULL REFERENCES symbols(id) ON DELETE CASCADE, kind TEXT NOT NULL, "
        + "name TEXT NOT NULL, arity INTEGER NOT NULL, receiver TEXT NOT NULL, line INTEGER NOT NULL); "
        + "CREATE INDEX call_sites_name ON call_sites(name); "
        + "CREATE INDEX edges_src ON edges(src); "
        + "CREATE INDEX edges_dst ON edges(dst); "
        + "CREATE INDEX edges_name ON edges(dst_name); "
        + "CREATE TABLE bands (key TEXT NOT NULL, symbol_id INTEGER NOT NULL REFERENCES symbols(id) ON DELETE CASCADE, "
        + "PRIMARY KEY (key, symbol_id)); "
        + "CREATE INDEX bands_key ON bands(key); "
        + "CREATE TABLE deps (manifest TEXT NOT NULL, name TEXT NOT NULL, version TEXT NOT NULL, kind TEXT NOT NULL, tags TEXT NOT NULL, "
        + "PRIMARY KEY (manifest, name)); "
        + "CREATE TABLE bodies (symbol_id INTEGER PRIMARY KEY REFERENCES symbols(id) ON DELETE CASCADE, text TEXT NOT NULL); "
        + "CREATE TABLE embeddings (symbol_id INTEGER PRIMARY KEY REFERENCES symbols(id) ON DELETE CASCADE, model TEXT NOT NULL, vector BLOB NOT NULL); "
        + "CREATE TABLE graph_nodes (id TEXT PRIMARY KEY, file TEXT NULL, name TEXT NULL, line INTEGER NULL, community INTEGER NULL); "
        + "CREATE TABLE graph_edges (src TEXT NOT NULL, dst TEXT NOT NULL, relation TEXT NOT NULL, confidence TEXT NOT NULL);";

    internal static async Task Create(SqliteConnection connection, CancellationToken cancellationToken)
    {
        _ = await Sql.Run(connection, Tables, cancellationToken).ConfigureAwait(false);
        _ = await Sql.Run(connection, Version, cancellationToken).ConfigureAwait(false);
    }
}
