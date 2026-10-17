using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>
/// A map version a caller names ('mapVersion=' / '--map-version', scoping spec C10): N, vN or vN@commit, the label an answer prints.
/// </summary>
/// <param name="Number">The version number.</param>
/// <param name="Commit">The commit part, or null when none was given.</param>
public readonly record struct MapVersionRef(int Number, string? Commit)
{
    /// <summary>Parses what a caller sent.</summary>
    /// <param name="text">'12', 'v12' or 'v12@a41b07d'.</param>
    /// <returns>The reference, or SND-CONTEXT-006 naming the value.</returns>
    public static Result<MapVersionRef> Parse(string? text) => throw new NotSupportedException();

    /// <summary>Whether <paramref name="version"/> is the one named: the same number and, when a commit part was given, a commit that starts with it.</summary>
    /// <param name="version">A stored version.</param>
    /// <returns>True when it is the one named.</returns>
    public bool Names(MapVersion version) => throw new NotSupportedException();
}
