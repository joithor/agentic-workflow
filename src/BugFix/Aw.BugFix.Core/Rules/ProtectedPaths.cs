using System.Text.RegularExpressions;

namespace Aw.BugFix.Core.Rules;

/// <summary>
/// Files a fix must not touch without the user's approval: the check and its directory (unless that is the repository
/// root), tests, fixtures, mocks, snapshots, runner config and package manifests. Weakening what the check exercises is
/// as bad as editing the check.
/// </summary>
public static partial class ProtectedPaths
{
    /// <summary>The changed files a fix may not touch without approval.</summary>
    /// <param name="changedFiles">Repository-relative paths with '/'.</param>
    /// <param name="checkPath">The frozen check's repository-relative path.</param>
    /// <returns>The protected ones, in input order.</returns>
    public static IReadOnlyList<string> Touched(IEnumerable<string> changedFiles, string checkPath)
    {
        var slash = checkPath.LastIndexOf('/');
        var checkDirectory = slash < 0 ? null : checkPath[..(slash + 1)];
        return [.. changedFiles.Where(f =>
            f == checkPath
            || (checkDirectory is not null && f.StartsWith(checkDirectory, StringComparison.Ordinal))
            || Matches(f))];
    }

    // A pattern that times out counts as protected: a timeout never lets a change through.
    private static bool Matches(string file)
    {
        try
        {
            return TestDirectoryOrName.IsMatch(file) || RunnerConfig.IsMatch(file) || JvmStyleTest.IsMatch(file);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    [GeneratedRegex(
        "(^|/)(tests?|__tests__|specs?|fixtures?|__fixtures__|mocks?|__mocks__|__snapshots__|e2e|cypress|playwright|test-?utils|testing|testdata|\\.ui-evidence)(/|$)"
        + "|\\.(test|spec)\\.[^/]+$|(^|/)test_[^/]+\\.py$|_test\\.(go|py|rb)$|\\.snap$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex TestDirectoryOrName { get; }

    [GeneratedRegex(
        "(^|/)(vitest|vite|jest|playwright|karma|cypress|babel)\\.(config|setup|workspace)\\.[^/]+$|(^|/)(jest|vitest)[.-]setup[^/]*$"
        + "|(^|/)setupTests\\.[^/]+$|(^|/)conftest\\.py$|(^|/)\\.mocharc[^/]*$|(^|/)(pytest\\.ini|tox\\.ini|pyproject\\.toml|setup\\.cfg)$"
        + "|(^|/)\\.babelrc[^/]*$|(^|/)\\.env\\.test[^/]*$|(^|/)tsconfig[^/]*\\.json$|(^|/)package\\.json$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex RunnerConfig { get; }

    // FooTest.swift, AppUITests.swift, ApiTest.java: case-sensitive, so Latest.kt or Contest.cs don't match.
    [GeneratedRegex("(^|/)[^/]*Tests?\\.(swift|kt|java|cs)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JvmStyleTest { get; }
}
