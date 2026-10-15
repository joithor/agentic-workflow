namespace Aw.Meta.Features.GateRun;

/// <summary>What a gate run will do: the pull request, its run number, the scopes and the prepared tree.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="Run">The run number the logs and the record get.</param>
/// <param name="Scopes">The scopes to run, in order.</param>
/// <param name="Prepared">The gate tree.</param>
internal sealed record GatePlan(int Pr, int Run, IReadOnlyList<string> Scopes, PreparedGate Prepared);
