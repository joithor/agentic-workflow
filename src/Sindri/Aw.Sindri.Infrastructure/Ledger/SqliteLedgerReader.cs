using Aw.Kernel;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>The ledger's read-only side in SQLite (API spec C9): read sessions only; it never migrates or creates the file.</summary>
internal sealed class SqliteLedgerReader : ILedgerReader
{
    public SqliteLedgerReader(LedgerDatabase database)
    {
        _ = database;
    }

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: unit A");

    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: unit A");

    public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => throw new NotSupportedException("red: unit A");
}
