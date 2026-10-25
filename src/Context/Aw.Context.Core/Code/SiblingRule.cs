namespace Aw.Context.Core.Code;

/// <summary>The sibling rule (S-CL): a test file whose stem, with test markers removed, equals an implementing file's stem names it.</summary>
public static class SiblingRule
{
    private static readonly string[] _generic = ["index", "main", "mod", "__init__", "utils", "util", "helpers", "types", "common", "constants"];
    private static readonly string[] _dottedSuffixes = [".test", ".spec", "_test", "_tests", "_spec"];
    private static readonly string[] _pascalSuffixes = ["Tests", "Test", "Spec"];

    /// <summary>Whether a test file ties an implementing file by name.</summary>
    /// <param name="testPath">The test file's path.</param>
    /// <param name="implementingPath">The implementing file's path.</param>
    /// <returns>True when the stems are equal without regard to case and are not generic ('index', 'main', 'utils', ...).</returns>
    public static bool Ties(string testPath, string implementingPath)
    {
        ArgumentNullException.ThrowIfNull(testPath);
        ArgumentNullException.ThrowIfNull(implementingPath);
        var tested = WithoutMarkers(PathStem.Of(LastSegment(testPath)));
        var implementing = PathStem.Of(LastSegment(implementingPath));
        return tested.Length > 0
            && string.Equals(tested, implementing, StringComparison.OrdinalIgnoreCase)
            && !_generic.Contains(implementing, StringComparer.OrdinalIgnoreCase);
    }

    private static string LastSegment(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static string WithoutMarkers(string stem)
    {
        if (stem.StartsWith("test_", StringComparison.OrdinalIgnoreCase))
        {
            return stem["test_".Length..];
        }

        var dotted = _dottedSuffixes.FirstOrDefault(s => stem.EndsWith(s, StringComparison.OrdinalIgnoreCase));
        if (dotted is not null)
        {
            return stem[..^dotted.Length];
        }

        var pascal = _pascalSuffixes.FirstOrDefault(s => stem.EndsWith(s, StringComparison.Ordinal));
        return pascal is null ? stem : stem[..^pascal.Length];
    }
}
