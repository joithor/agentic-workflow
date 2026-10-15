namespace Aw.Context.Core.Review;

/// <summary>The draft's number and the commit it was built at.</summary>
/// <param name="Number">The version number.</param>
/// <param name="Commit">The commit (seven characters), or null.</param>
public sealed record ReviewVersion(int Number, string? Commit)
{
    /// <summary>No version known.</summary>
    public static ReviewVersion None { get; } = new(Number: 0, Commit: null);
}
