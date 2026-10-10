namespace Aw.Packets.Core.Checks;

/// <summary>One check outcome. A failure names what it found and the fix; a pass or unchecked names the rule once.</summary>
/// <param name="Rule">The rule id, for example 'PKT-GRAPH-002'.</param>
/// <param name="State">Pass, fail or unchecked.</param>
/// <param name="Subject">What it is about: 'packet', 'task t1', 'question q2'.</param>
/// <param name="Message">What was found, or why it is unchecked.</param>
/// <param name="Fix">How to fix a failure; null otherwise.</param>
public sealed record CheckResult(string Rule, CheckState State, string Subject, string Message, string? Fix);
