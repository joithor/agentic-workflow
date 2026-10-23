namespace Aw.Sindri.Core.EngineReads;

/// <summary>What an instance waits on (API spec §3.8, E-30).</summary>
/// <param name="Kind">'activity', 'signal', 'update', 'child' or 'timer'.</param>
/// <param name="Name">The activity or timer name, when it has one.</param>
/// <param name="AgeSeconds">How long it has waited, in seconds.</param>
public sealed record EngineWaitingOn(string Kind, string? Name, long AgeSeconds);
