namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>An eval file as <see cref="EvalCaseReader"/> read it: the case, or the line that isn't in the §9.2 format.</summary>
/// <param name="Case">The case; null when a line is malformed.</param>
/// <param name="Invalid">The malformed line, as a failed verdict; null when the case read.</param>
public sealed record EvalReading(EvalCase? Case, EvalVerdict? Invalid);
