using Aw.Meta.Core.Watch;

namespace Aw.Meta.Core.Github;

/// <summary>A pull request with its head branch, as `gh pr list --json number,state,headRefName,headRefOid` reports it.</summary>
/// <param name="Number">The number.</param>
/// <param name="State">Open, closed or merged.</param>
/// <param name="HeadRef">The head branch.</param>
/// <param name="Head">The head commit.</param>
public sealed record PrBranch(int Number, PrState State, string HeadRef, string Head);
