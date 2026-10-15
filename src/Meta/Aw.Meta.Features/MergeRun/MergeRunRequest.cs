namespace Aw.Meta.Features.MergeRun;

/// <summary>The MergeRun request: check, merge and announce a pull request.</summary>
/// <param name="Pr">The pull request number.</param>
public sealed record MergeRunRequest(int Pr);
