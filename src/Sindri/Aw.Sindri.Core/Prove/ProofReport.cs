namespace Aw.Sindri.Core.Prove;

/// <summary>What <see cref="ProofChecks"/> found.</summary>
/// <param name="Failures">The rules that did not hold.</param>
/// <param name="NotRun">The rules that could not run because a fact they read was not carried, each as 'RULE: why'; they are warnings.</param>
/// <param name="Unrunnable">The rules that must run for a proof to stand (PRV-ACC-001, PRV-SHA-001) and could not, each as 'RULE: why'; a proof with any is not a proof.</param>
public sealed record ProofReport(IReadOnlyList<ProofFinding> Failures, IReadOnlyList<string> NotRun, IReadOnlyList<string> Unrunnable)
{
    /// <summary>True when no rule failed (rules that did not run are warnings).</summary>
    public bool Holds => Failures.Count == 0;
}
