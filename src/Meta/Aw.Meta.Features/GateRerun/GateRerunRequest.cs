namespace Aw.Meta.Features.GateRerun;

/// <summary>The GateRerun request: rerun one test of a red gate step alone and on plain main.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="Step">The gate step the test belongs to (as named in the gate).</param>
/// <param name="Test">The test name or filter.</param>
public sealed record GateRerunRequest(int Pr, string Step, string Test);
