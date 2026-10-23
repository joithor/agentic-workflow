namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>CI fixes: entries into the fix state, and whether the next snapshot found the checks green.</summary>
/// <param name="Attempts">Entries into the CI-fix state.</param>
/// <param name="Succeeded">Attempts whose next snapshot did not send the instance back to CI-fix.</param>
/// <param name="StillRed">Attempts whose next snapshot sent it back to CI-fix.</param>
/// <param name="Rate">Succeeded over succeeded plus still red; null with no finished attempt.</param>
public sealed record CiFixStats(int Attempts, int Succeeded, int StillRed, double? Rate);
