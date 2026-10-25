namespace Aw.Sindri.Features.RecordEval;

/// <summary>'recipe eval --record' (spec §6.3, §9.2): the live instance whose history becomes an eval case.</summary>
/// <param name="Address">The instance's address.</param>
public sealed record RecordEvalRequest(string Address);
