namespace Aw.Meta.Features.CheckMerge;

/// <summary>The CheckMerge request: are the merge preconditions met for a pull request.</summary>
/// <param name="Pr">The pull request number.</param>
public sealed record CheckMergeRequest(int Pr);
