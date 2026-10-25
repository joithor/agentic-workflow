namespace Aw.Context.Core.Code;

/// <summary>The files a search found (S-CL): paths only, never a line or a snippet.</summary>
/// <param name="Paths">The repo-relative paths, in ordinal order, at most the cap asked for.</param>
/// <param name="Truncated">True when more files matched than were returned.</param>
public sealed record CodeSearchHits(IReadOnlyList<string> Paths, bool Truncated);
