using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The checks' result: the outcome and the failing findings (the fact:findings body), the warnings beside them, and every rule's status.</summary>
/// <param name="Stage">The stage that ran.</param>
/// <param name="Outcome">The outcome.</param>
/// <param name="Findings">The failing findings only: warnings never reach fact:findings.</param>
/// <param name="Warnings">The warnings, for the stop and the CLI.</param>
/// <param name="Rules">Each rule of the stage with its live status.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScopingCheckResponse(
    ScopeStage Stage,
    ScopeOutcome Outcome,
    IReadOnlyList<ScopeFinding> Findings,
    IReadOnlyList<ScopeFinding> Warnings,
    IReadOnlyList<ScopeRuleResult> Rules)
{
    /// <summary>Whether the run is clean: exit 0.</summary>
    [JsonIgnore]
    public bool Passed => Outcome is ScopeOutcome.Pass or ScopeOutcome.Complete;
}
