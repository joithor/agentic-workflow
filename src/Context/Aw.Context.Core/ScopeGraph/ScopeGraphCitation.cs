namespace Aw.Context.Core.ScopeGraph;

/// <summary>Where a scoping graph node is in the record store: the same shape as the concept read's citations.</summary>
/// <param name="Key">A record key, such as <c>repo:&lt;repo&gt;/&lt;path&gt;</c>.</param>
/// <param name="Span">A line range or a hash, or null for the whole record.</param>
public sealed record ScopeGraphCitation(string Key, string? Span = null);
