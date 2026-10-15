namespace Aw.Meta.Core.Gate;

/// <summary>Matches repository-relative paths against globs: "*" and "?" within a segment, "**" across segments.</summary>
public static class GlobMatcher
{
    /// <summary>Whether <paramref name="path"/> matches <paramref name="pattern"/>.</summary>
    /// <param name="pattern">The glob.</param>
    /// <param name="path">A path with "/" separators.</param>
    /// <returns>True on a match.</returns>
    public static bool IsMatch(string pattern, string path) => throw new NotSupportedException("GlobMatcher: make GlobMatcherTests pass");
}
