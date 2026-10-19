using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Usage;

/// <summary>One watch poll's usage work: look at the lanes and the meta, then apply the rules.</summary>
/// <param name="reader">Looks at the lanes and the meta.</param>
/// <param name="watcher">Prints, remembers, logs and resumes.</param>
public sealed class UsageMonitor(UsageReader reader, UsageWatcher watcher)
{
    /// <summary>Takes one look and applies the rules.</summary>
    /// <param name="team">The team.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lines to print; none when the look could not be taken (advisory: the poll goes on without usage).</returns>
    public async ValueTask<IReadOnlyList<string>> Check(TeamConfig team, CancellationToken cancellationToken) =>
        await reader.ReadOrNull(team, cancellationToken).ConfigureAwait(false) is { } readings
            ? await watcher.Tick(team, readings, cancellationToken).ConfigureAwait(false)
            : [];
}
