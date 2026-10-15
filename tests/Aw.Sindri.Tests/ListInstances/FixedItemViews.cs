using Aw.Kernel;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>The 'items' view with fixed rows, for the list's title lookup; every other view is empty.</summary>
internal sealed class FixedItemViews(params ItemRow[] items) : IViewReader
{
    public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class =>
        ValueTask.FromResult<Result<TRow?>>((TRow?)null);

    public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
        where TRow : class
    {
        IReadOnlyList<TRow> rows = view == ViewNames.Items ? [.. items.OfType<TRow>()] : [];
        var found = Results.Success(rows);
        return ValueTask.FromResult(found);
    }

    public ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
        where TRow : class =>
        List<TRow>(string.Empty, cancellationToken);

    public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ProjectorState?>>((ProjectorState?)null);
}
