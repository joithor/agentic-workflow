namespace Aw.BugFix.Core.Model;

/// <summary>What the /rootCause investigation handoff says.</summary>
/// <param name="Status">The handoff's status line (diagnosed, unfixed, …).</param>
/// <param name="Hypotheses">The hypotheses table.</param>
/// <param name="RootCause">The Root Cause section's text; empty when the section is missing.</param>
public sealed record Handoff(string Status, IReadOnlyList<Hypothesis> Hypotheses, string RootCause);
