namespace Aw.BugFix.Core.Model;

/// <summary>Where one run writes: its log (or ui-evidence run directory) and its record, under the state's runs directory.</summary>
/// <param name="Output">The log file (test) or the ui-evidence run directory.</param>
/// <param name="Log">The log file of the process.</param>
/// <param name="Record">The run record to write.</param>
/// <param name="DigestKey">The bug fix's secret key (hex) for digests of the run's environment: kept beside the state, never in it, so a digest in state.json or a run record can't be checked against guesses.</param>
public sealed record RunSlot(string Output, string Log, string Record, string DigestKey);
