namespace Aw.Sindri.Core.EngineReads;

/// <summary>What an instance waits on, as the adapter read it (the read side of <see cref="EngineWaitingOn"/>).</summary>
/// <param name="Kind">'activity', 'signal', 'update', 'child' or 'timer'.</param>
/// <param name="Name">The activity or timer name, when it has one.</param>
/// <param name="Since">When the wait began.</param>
public sealed record EngineWaiting(string Kind, string? Name, DateTimeOffset Since);
