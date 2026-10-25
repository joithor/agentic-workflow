using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>
/// A read-only view of one published index file (spec §4.1): opened <c lang="text">mode=ro</c> with <c lang="text">query_only</c>, never creating
/// a file; every query is parameterized and nothing a caller passes becomes SQL or a path.
/// </summary>
internal sealed partial class SqliteIndexReader(SqliteConnection connection, IndexHeader header) : IIndexReader
{
    // The columns of one SymbolHit, read by ReadHit at an offset; the first is the symbol's row id.
    private const string HitColumns = "s.id, s.ref, s.kind, s.name, s.file, s.start_line, s.end_line, s.signature, s.exported";

    public IndexHeader Header { get; } = header;

    /// <summary>Opens a published file and reads its header; a file of another schema or with an unreadable header throws <see cref="InvalidDataException"/>.</summary>
    internal static async Task<Result<SqliteIndexReader>> Open(string path, CancellationToken cancellationToken)
    {
        var opened = await new SqliteConnectionFactory(new SqliteOptions(path)).OpenReadOnly(cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return SqliteFailure.Translate(opened.Error, "Opening the index file");
        }

        var connection = opened.Value;
        var handedOver = false;
        try
        {
            var header = await HeaderReader.Read(connection, cancellationToken).ConfigureAwait(false);
            var reader = new SqliteIndexReader(connection, header);
            handedOver = true;
            return reader;
        }
        finally
        {
            if (!handedOver)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Runs <c lang="sql">PRAGMA quick_check(1)</c>.</summary>
    /// <returns>True when SQLite finds no problem.</returns>
    internal async Task<bool> QuickCheck(CancellationToken cancellationToken)
        => string.Equals(await Sql.Scalar(connection, "PRAGMA quick_check(1)", cancellationToken).ConfigureAwait(false) as string, "ok", StringComparison.Ordinal);

    public ValueTask DisposeAsync() => connection.DisposeAsync();

    private static SymbolHit ReadHit(SqliteDataReader reader, int offset)
        => new(
            SymbolRef.TryFrom(reader.GetString(offset + 1), out var symbol) ? symbol : throw new InvalidDataException("a stored symbol ref is invalid"),
            StoredNames.SymbolKindOf(reader.GetString(offset + 2)),
            reader.GetString(offset + 3),
            reader.GetString(offset + 4),
            reader.GetInt32(offset + 5),
            reader.GetInt32(offset + 6),
            reader.GetString(offset + 7),
            reader.GetInt32(offset + 8) != 0);

    private async Task<long?> IdOf(SymbolRef symbol, CancellationToken cancellationToken)
    {
        var command = Sql.Command(connection, "SELECT id FROM symbols WHERE ref = $ref").With("$ref", symbol.Value);
        await using (command.ConfigureAwait(false))
        {
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is long id ? id : null;
        }
    }
}
