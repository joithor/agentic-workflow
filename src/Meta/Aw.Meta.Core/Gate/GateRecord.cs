namespace Aw.Meta.Core.Gate;

/// <summary>A recorded gate run.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="Head">The pull request head the gate ran.</param>
/// <param name="Main">The main commit it was merged onto.</param>
/// <param name="Scopes">The scopes that ran.</param>
/// <param name="Steps">Every step.</param>
/// <param name="Verdict">The overall verdict.</param>
/// <param name="At">When it finished.</param>
public sealed record GateRecord(int Pr, string Head, string Main, IReadOnlyList<string> Scopes, IReadOnlyList<GateStep> Steps, GateVerdict Verdict, DateTimeOffset At)
{
    /// <summary>The run number within the pull request, assigned when stored.</summary>
    public int Run { get; init; }
}
