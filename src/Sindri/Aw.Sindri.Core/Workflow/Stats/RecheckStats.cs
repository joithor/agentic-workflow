namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>Proof rechecks and their verdicts.</summary>
/// <param name="Runs">Entries into the recheck state.</param>
/// <param name="Resolved">Rechecks that went back to the snapshot.</param>
/// <param name="Unresolved">Rechecks that stopped at the owner's desk.</param>
public sealed record RecheckStats(int Runs, int Resolved, int Unresolved);
