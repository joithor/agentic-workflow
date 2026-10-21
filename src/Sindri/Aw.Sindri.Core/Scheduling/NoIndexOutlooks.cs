using Aw.Kernel;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>The outlooks until the code index is composed: every repo is absent, so the gate is off and a map publishes at once.</summary>
public sealed class NoIndexOutlooks : IIndexOutlooks
{
    private static readonly IndexOutlookDetail _absent = new(IndexOutlook.Absent, RepoWait.None, RetryAt: null);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyDictionary<string, IndexOutlookDetail>>> Read(MapHoldState hold, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hold);
        IReadOnlyDictionary<string, IndexOutlookDetail> all = hold.Heads.Keys.ToDictionary(static r => r, static _ => _absent, StringComparer.Ordinal);
        return ValueTask.FromResult(Results.Success(all));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyDictionary<string, string>>> IndexCommits(IReadOnlyList<string> repos, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> none = new Dictionary<string, string>(StringComparer.Ordinal);
        return ValueTask.FromResult(Results.Success(none));
    }
}
