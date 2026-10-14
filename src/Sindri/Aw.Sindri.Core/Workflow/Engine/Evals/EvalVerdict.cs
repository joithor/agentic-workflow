namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>One case's result (spec §9.2): passed, or the first line that diverged and how.</summary>
/// <param name="Passed">Whether every line reached its expected state.</param>
/// <param name="Line">The first line (1-based, in the file) that diverged; null when it passed.</param>
/// <param name="Detail">What diverged ('expected checks, got parked (no-progress)'); null when it passed.</param>
public sealed record EvalVerdict(bool Passed, int? Line, string? Detail);
