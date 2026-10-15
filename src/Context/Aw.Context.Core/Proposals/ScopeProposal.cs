using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Proposals;

/// <summary>What a scoping run posts to 'POST map/proposals' (spec §8.7): its surfaces in one repo.</summary>
/// <param name="Run">The run's id; proposing the same surface twice from it is one row.</param>
/// <param name="Repo">The repo the surfaces were found in.</param>
/// <param name="Surfaces">The surfaces.</param>
public sealed record ScopeProposal(string Run, ContextRepoName Repo, IReadOnlyList<ProposedSurface> Surfaces);
