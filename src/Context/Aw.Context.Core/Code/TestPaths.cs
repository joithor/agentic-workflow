namespace Aw.Context.Core.Code;

/// <summary>Whether a path is a test file, by the profile's 'context.code.testGlobs' (S-CL).</summary>
public static class TestPaths
{
    /// <summary>Whether a path matches any test glob, without regard to case.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="globs">The test globs.</param>
    /// <returns>True for a test file.</returns>
    public static bool IsTest(string path, IReadOnlyList<string> globs) => throw new NotSupportedException();
}
