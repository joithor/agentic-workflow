namespace Aw.Context.Infrastructure.Linear;

/// <summary>A connection's paging.</summary>
/// <param name="HasNextPage">The HasNextPage Linear serves.</param>
/// <param name="EndCursor">The EndCursor Linear serves.</param>
internal sealed record LinearPageInfo(bool HasNextPage, string? EndCursor = null);
