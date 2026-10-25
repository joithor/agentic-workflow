using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Context.Core.History;

/// <summary>What a commit subject says about its pull request and branch. Only the subject is ever read, never a body.</summary>
/// <param name="Pr">The pull request number, or null.</param>
/// <param name="Branch">The merged branch (owner segment stripped), or null.</param>
public sealed partial record MergeSubject(int? Pr, string? Branch)
{
    /// <summary>Reads the built-in forms: GitHub merge, squash suffix '(#N)', Bitbucket '(pull request #N)' and 'Merge branch'.</summary>
    /// <param name="subject">A one-line commit subject.</param>
    /// <returns>What it names; both null when it names neither.</returns>
    public static MergeSubject Parse(string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (GitHubMerge.Match(subject) is { Success: true } github)
        {
            var branch = github.Groups["branch"].Value;
            return new MergeSubject(Number(github), branch[(branch.IndexOf('/', StringComparison.Ordinal) + 1)..]);
        }

        if (BitbucketMerge.Match(subject) is { Success: true } merged)
        {
            return new MergeSubject(Number(merged), merged.Groups["branch"].Value);
        }

        if (Suffix.Match(subject) is { Success: true } suffix)
        {
            return new MergeSubject(Number(suffix), Branch: null);
        }

        return MergeBranch.Match(subject) is { Success: true } local ? new MergeSubject(Pr: null, local.Groups["branch"].Value) : new MergeSubject(Pr: null, Branch: null);
    }

    private static int Number(Match match) => int.Parse(match.Groups["pr"].Value, NumberStyles.None, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^Merge pull request #(?<pr>\d{1,9}) from (?<branch>\S+)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GitHubMerge { get; }

    [GeneratedRegex(@"^Merged in (?<branch>\S+) \(pull request #(?<pr>\d{1,9})\)\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BitbucketMerge { get; }

    [GeneratedRegex(@"(?:^|\s)\((?:pull request )?#(?<pr>\d{1,9})\)\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Suffix { get; }

    [GeneratedRegex(@"^Merge branch '(?<branch>[^']+)'", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MergeBranch { get; }
}
