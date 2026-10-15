namespace Aw.Meta.Core.Gate;

/// <summary>One step of a gate run.</summary>
/// <param name="Name">The step name.</param>
/// <param name="Verdict">Pass or fail.</param>
/// <param name="Log">The log file path.</param>
public sealed record GateStep(string Name, GateVerdict Verdict, string Log)
{
    /// <summary>The flake classification of a failed step, once rerun.</summary>
    public FlakeVerdict? Flake { get; init; }
}
