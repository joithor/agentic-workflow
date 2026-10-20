namespace Aw.Sindri.Core.Scoping;

/// <summary>How much of a change to read (scoping spec §4: the diff is at most 2 MiB).</summary>
/// <param name="MaxDiffBytes">The most diff text, in UTF-8 bytes.</param>
public sealed record ChangeRead(int MaxDiffBytes)
{
    /// <summary>The cap the scoping station reads with.</summary>
    public static ChangeRead Scoping { get; } = new(2 * 1024 * 1024);
}
