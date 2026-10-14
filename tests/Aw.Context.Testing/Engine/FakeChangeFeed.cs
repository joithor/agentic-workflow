using System.Globalization;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Engine;

/// <summary>The in-memory <see cref="IChangeFeed"/>: a fixed list of changes paged by position (the cursor is the next index). It passes <see cref="ChangeFeedContract{TFeed}"/>.</summary>
/// <param name="changes">Every change, oldest first.</param>
public sealed class FakeChangeFeed(IReadOnlyList<SourceRef> changes) : IChangeFeed
{
    /// <inheritdoc />
    public ValueTask<Result<ChangePage>> Changes(SourceCursor? after, int limit, CancellationToken cancellationToken)
    {
        var start = after is { } cursor ? int.Parse(cursor.Value, CultureInfo.InvariantCulture) : 0;
        var page = changes.Skip(start).Take(limit).ToList();
        var next = SourceCursor.From((start + page.Count).ToString(CultureInfo.InvariantCulture));
        return ValueTask.FromResult<Result<ChangePage>>(new ChangePage(page, [], next));
    }
}
