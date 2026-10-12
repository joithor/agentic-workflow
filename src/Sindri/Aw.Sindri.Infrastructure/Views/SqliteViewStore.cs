using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>The projector's writer over views.db.</summary>
internal sealed class SqliteViewStore : IViewStore
{
    public SqliteViewStore(ViewsDatabase database, JsonSerializerOptions options)
    {
        _ = database;
        _ = options;
    }

    public ValueTask<Result<ProjectorState>> Open(int schemaVersion, CancellationToken cancellationToken) => throw new NotSupportedException("red: unit B");

    public ValueTask<Result<PageApplied>> Apply(IReadOnlyList<LedgerEvent> page, IReadOnlyList<IProjection> projections, DateTimeOffset at,
        CancellationToken cancellationToken) => throw new NotSupportedException("red: unit B");

    public ValueTask<Result<ProjectorState>> Reset(IReadOnlyList<string> views, int schemaVersion, DateTimeOffset at, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: unit B");

    public ValueTask<Result<Unit>> FinishRebuild(CancellationToken cancellationToken) => throw new NotSupportedException("red: unit B");
}
