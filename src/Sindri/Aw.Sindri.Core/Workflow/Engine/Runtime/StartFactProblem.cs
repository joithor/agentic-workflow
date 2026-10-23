namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a start-time fact check found wrong with one seeded input fact.</summary>
/// <param name="errors">Each problem: where in the fact, and what.</param>
/// <param name="fix">How to write the fact so it passes.</param>
public sealed class StartFactProblem(IReadOnlyList<string> errors, string fix)
{
    /// <summary>Each problem: where in the fact, and what.</summary>
    public IReadOnlyList<string> Errors { get; } = errors;

    /// <summary>How to write the fact so it passes.</summary>
    public string Fix { get; } = fix;
}
