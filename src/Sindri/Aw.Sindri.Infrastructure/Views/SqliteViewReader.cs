using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>The queries' read side of views.db.</summary>
internal sealed class SqliteViewReader : IViewReader
{
    public SqliteViewReader(ViewsDatabase database, JsonSerializerOptions options)
    {
        _ = database;
        _ = options;
    }

    public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class => throw new NotSupportedException("red: unit B");

    public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
        where TRow : class => throw new NotSupportedException("red: unit B");

    public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) => throw new NotSupportedException("red: unit B");
}
