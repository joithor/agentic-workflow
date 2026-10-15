using Aw.Meta.Core.Config;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Merge;

namespace Aw.Meta.Core.Coordination;

/// <summary>The merge preconditions for one pull request, checked.</summary>
/// <param name="Team">The team config.</param>
/// <param name="Checks">Each check with its detail.</param>
/// <param name="Pr">The pull request as GitHub reports it; null when it was never gated (nothing was asked of GitHub).</param>
public sealed record MergeAssessment(TeamConfig Team, IReadOnlyList<MergeCheck> Checks, PrDetails? Pr)
{
    /// <summary>Whether every check holds.</summary>
    public bool Ready => Checks.All(check => check.Ok);
}
