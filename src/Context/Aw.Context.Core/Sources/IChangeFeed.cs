using Aw.Kernel;

namespace Aw.Context.Core.Sources;

/// <summary>A source's change feed, which drives scheduled syncs (spec §5.1). Replaying a page is idempotent.</summary>
public interface IChangeFeed
{
    /// <summary>The records changed or removed after <paramref name="after"/>.</summary>
    /// <param name="after">The persisted cursor, or null from the start.</param>
    /// <param name="limit">At most this many changes.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One page and the cursor after it.</returns>
    public ValueTask<Result<ChangePage>> Changes(SourceCursor? after, int limit, CancellationToken cancellationToken);
}
