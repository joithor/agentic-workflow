using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Coordination;

/// <summary>The team config and the pull requests a train takes, in train order.</summary>
/// <param name="Team">The team config.</param>
/// <param name="Prs">The pull request numbers.</param>
public sealed record TrainCandidates(TeamConfig Team, IReadOnlyList<int> Prs);
