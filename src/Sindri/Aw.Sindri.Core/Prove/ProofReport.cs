namespace Aw.Sindri.Core.Prove;

/// <summary>What <see cref="ProofChecks"/> found.</summary>
/// <param name="Failures">The rules that did not hold.</param>
/// <param name="NotRun">The rules that could not run because a fact they read was not carried, each as 'RULE: why'; they are warnings.</param>
/// <param name="Unrunnable">The rules of <see cref="ProofChecks.Required"/> that could not run, each as 'RULE: why'; a proof with any is not a proof, and nothing is frozen.</param>
public sealed record ProofReport(IReadOnlyList<ProofFinding> Failures, IReadOnlyList<string> NotRun, IReadOnlyList<string> Unrunnable)
{
    /// <summary>True when no rule failed (rules that did not run are warnings).</summary>
    public bool Holds => Failures.Count == 0;
}
