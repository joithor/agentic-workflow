namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>One eval case file, 'evals/&lt;recipe id&gt;/&lt;case&gt;.jsonl' (spec §9.2): its start, then its trigger lines (line 2 onwards).</summary>
/// <param name="Name">The case's name: its file name without '.jsonl' ('happy').</param>
/// <param name="Start">Line 1.</param>
/// <param name="Steps">Lines 2 onwards, in order.</param>
public sealed record EvalCase(string Name, EvalStartLine Start, IReadOnlyList<EvalStepLine> Steps);
