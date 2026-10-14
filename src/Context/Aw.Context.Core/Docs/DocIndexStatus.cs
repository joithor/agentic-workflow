using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Docs;

/// <summary>One repo's doc index state.</summary>
/// <param name="Head">The commit the index reflects, or null before the first refresh.</param>
/// <param name="LastSync">When it was last refreshed, or null.</param>
/// <param name="Code">The last refresh's error code, or null.</param>
/// <param name="Documents">Documents indexed.</param>
/// <param name="Chunks">Chunks indexed.</param>
public sealed record DocIndexStatus(SourceVersion? Head, DateTimeOffset? LastSync, string? Code, int Documents, int Chunks);
