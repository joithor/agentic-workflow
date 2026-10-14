namespace Aw.Context.Core.Sources;

/// <summary>One page of a source's change feed.</summary>
/// <param name="Changed">Records changed since the cursor.</param>
/// <param name="Removed">Records removed since the cursor.</param>
/// <param name="Next">The cursor after this page; it is persisted only after the page's links commit.</param>
public sealed record ChangePage(IReadOnlyList<SourceRef> Changed, IReadOnlyList<ExternalId> Removed, SourceCursor Next);
