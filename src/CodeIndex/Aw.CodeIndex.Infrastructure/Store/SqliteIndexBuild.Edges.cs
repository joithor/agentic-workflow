using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

internal sealed partial class SqliteIndexBuild
{
    private const string ReadSymbolRows = "SELECT ref, file, name, qualified, kind, arity, start_line, end_line FROM symbols ORDER BY id";

    private const string ReadSiteRows = "SELECT s.ref, c.kind, c.name, c.arity, c.receiver, c.line "
        + "FROM call_sites c JOIN symbols s ON s.id = c.src ORDER BY c.rowid";

    // A source that is not in the index adds nothing; a target that is not in it is stored as unresolved.
    private const string InsertEdge = "INSERT OR IGNORE INTO edges(src, dst, dst_name, kind, basis, confidence, line, candidates) "
        + "SELECT s.id, (SELECT id FROM symbols WHERE ref = $dst), $name, $kind, $basis, $confidence, $line, $candidates "
        + "FROM symbols s WHERE s.ref = $src";

    public ValueTask<Result<IReadOnlyList<ResolutionSymbol>>> ReadSymbols(CancellationToken cancellationToken)
        => SqliteFailure.Guard("Reading the symbols", async () => await ReadAllSymbols(cancellationToken).ConfigureAwait(false));

    public async IAsyncEnumerable<CallSiteRow> ReadCallSites([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var command = Sql.Command(connection, ReadSiteRows);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    yield return new CallSiteRow(
                        SymbolRef.TryFrom(reader.GetString(0), out var caller) ? caller : throw new InvalidDataException("a stored symbol ref is invalid"),
                        new CallSite(StoredNames.KindOf(reader.GetString(1)), reader.GetString(2), reader.GetInt32(3), reader.GetString(4), reader.GetInt32(5)));
                }
            }
        }
    }

    public ValueTask<Result<Unit>> ClearEdges(EdgeBasis basis, CancellationToken cancellationToken)
        => SqliteFailure.Guard("Clearing the edges", async () =>
        {
            var command = Sql.Command(connection, "DELETE FROM edges WHERE basis = $basis").With("$basis", StoredNames.Text(basis));
            await using (command.ConfigureAwait(false))
            {
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            return (Result<Unit>)Unit.Value;
        });

    public ValueTask<Result<Unit>> AddEdges(IReadOnlyList<EdgeRecord> edges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edges);
        return SqliteFailure.Guard("Adding edges", async () => await InsertEdges(edges, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Result<IReadOnlyList<ResolutionSymbol>>> ReadAllSymbols(CancellationToken cancellationToken)
    {
        var symbols = new List<ResolutionSymbol>();
        var command = Sql.Command(connection, ReadSymbolRows);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    symbols.Add(new ResolutionSymbol(
                        SymbolRef.TryFrom(reader.GetString(0), out var symbol) ? symbol : throw new InvalidDataException("a stored symbol ref is invalid"),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        StoredNames.SymbolKindOf(reader.GetString(4)),
                        reader.GetInt32(5),
                        reader.GetInt32(6),
                        reader.GetInt32(7)));
                }
            }
        }

        return symbols;
    }

    private async Task<Result<Unit>> InsertEdges(IReadOnlyList<EdgeRecord> edges, CancellationToken cancellationToken)
    {
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var insert = new Stmt(connection, transaction, InsertEdge, "$src", "$dst", "$name", "$kind", "$basis", "$confidence", "$line", "$candidates");
            await using (insert.ConfigureAwait(false))
            {
                foreach (var edge in edges)
                {
                    _ = await insert
                        .Set("$src", edge.Source.Value)
                        .Set("$dst", edge.Target?.Value)
                        .Set("$name", edge.TargetName)
                        .Set("$kind", StoredNames.Text(edge.Kind))
                        .Set("$basis", StoredNames.Text(edge.Basis))
                        .Set("$confidence", StoredNames.Text(edge.Confidence))
                        .Set("$line", edge.Line)
                        .Set("$candidates", edge.Candidates)
                        .Run(cancellationToken).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }
}
