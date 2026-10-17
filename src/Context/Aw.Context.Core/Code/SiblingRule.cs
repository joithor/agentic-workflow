namespace Aw.Context.Core.Code;

/// <summary>The sibling rule (S-CL): a test file whose stem, with test markers removed, equals an implementing file's stem names it.</summary>
public static class SiblingRule
{
    /// <summary>Whether a test file ties an implementing file by name.</summary>
    /// <param name="testPath">The test file's path.</param>
    /// <param name="implementingPath">The implementing file's path.</param>
    /// <returns>True when the stems are equal without regard to case and are not generic ('index', 'main', 'utils', ...).</returns>
    public static bool Ties(string testPath, string implementingPath) => throw new NotSupportedException();
}
