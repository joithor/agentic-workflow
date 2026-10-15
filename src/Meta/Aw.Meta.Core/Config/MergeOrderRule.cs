namespace Aw.Meta.Core.Config;

/// <summary>Pull request <see cref="After"/> may not merge while pull request <see cref="Before"/> is open.</summary>
/// <param name="Before">The pull request that merges first.</param>
/// <param name="After">The pull request that waits.</param>
public sealed record MergeOrderRule(int Before, int After);
