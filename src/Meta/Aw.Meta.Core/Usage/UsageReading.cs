namespace Aw.Meta.Core.Usage;

/// <summary>One lane's (or the meta's) standing at one look.</summary>
/// <param name="Subject">The lane name, or "meta".</param>
/// <param name="State">Whether it can work.</param>
/// <param name="Stop">The stop it is at; set when limited.</param>
/// <param name="Idle">Whether its pane shows that stop as the last output at an empty, idle prompt (the only time the watch types).</param>
/// <param name="Reason">Why the state is unknown.</param>
/// <param name="Source">Where the stop was seen: pane, transcript or both.</param>
public sealed record UsageReading(string Subject, UsageState State, UsageStop? Stop, bool Idle, string? Reason = null, string? Source = null);
