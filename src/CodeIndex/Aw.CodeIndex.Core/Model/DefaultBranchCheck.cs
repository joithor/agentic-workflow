namespace Aw.CodeIndex.Core.Model;

/// <summary>Whether a commit is on the default branch (re-checked by a build before it reads anything).</summary>
/// <param name="DefaultBranch">The default branch: the target of origin/HEAD, else main, else master, as a local ref.</param>
/// <param name="Contains">True when the commit is the branch's tip or one of its ancestors.</param>
public sealed record DefaultBranchCheck(string DefaultBranch, bool Contains);
