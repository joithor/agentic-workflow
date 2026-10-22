namespace Aw.Sindri.Core.Scoping;

/// <summary>How much of a tracker record to read (scoping spec §4: at most 50 child issues and the first 20 comments of each).</summary>
/// <param name="MaxChildren">The most child issues of a project.</param>
/// <param name="CommentsPerItem">The most comments of the record and of each child.</param>
public sealed record TrackerRead(int MaxChildren, int CommentsPerItem)
{
    /// <summary>The caps the scoping station reads with.</summary>
    public static TrackerRead Scoping { get; } = new(50, 20);
}
