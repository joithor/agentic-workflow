namespace Aw.BugFix.Core.Model;

/// <summary>Where one run writes: its log (or ui-evidence run directory) and its record, under the state's runs directory.</summary>
/// <param name="Output">The log file (test) or the ui-evidence run directory.</param>
/// <param name="Log">The log file of the process.</param>
/// <param name="Record">The run record to write.</param>
public sealed record RunSlot(string Output, string Log, string Record);
