namespace Aw.Meta.Features.GateRun;

/// <summary>The GateRun request: run the merge gate for one pull request.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="Scopes">The scopes to run; empty to plan them from the changed paths and the team config.</param>
public sealed record GateRunRequest(int Pr, IReadOnlyList<string> Scopes);
