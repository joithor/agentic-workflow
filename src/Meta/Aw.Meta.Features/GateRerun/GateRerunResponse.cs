using Aw.Meta.Core.Gate;

namespace Aw.Meta.Features.GateRerun;

/// <summary>The GateRerun response.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="Run">The gate run whose step was classified.</param>
/// <param name="Step">The step.</param>
/// <param name="Test">The test.</param>
/// <param name="Verdict">Flake or real, as stored on the step.</param>
/// <param name="Reason">Why, in one sentence.</param>
public sealed record GateRerunResponse(int Pr, int Run, string Step, string Test, FlakeVerdict Verdict, string Reason)
{
    /// <summary>The log of the run alone on the gate tree: its full output.</summary>
    public string? AloneLog { get; init; }

    /// <summary>The log of the run on the plain-main tree; null when that was not needed.</summary>
    public string? MainLog { get; init; }
}
