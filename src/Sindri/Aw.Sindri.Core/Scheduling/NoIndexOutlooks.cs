using Aw.Kernel;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>The outlooks until the code index is composed: every repo is absent, so the gate is off and a map publishes at once.</summary>
public sealed class NoIndexOutlooks : IIndexOutlooks
{
    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyDictionary<string, IndexOutlookDetail>>> Read(MapHoldState hold, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyDictionary<string, string>>> IndexCommits(IReadOnlyList<string> repos, CancellationToken cancellationToken) => throw new NotSupportedException();
}
