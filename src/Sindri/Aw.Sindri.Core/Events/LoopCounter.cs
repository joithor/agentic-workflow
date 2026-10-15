namespace Aw.Sindri.Core.Events;

/// <summary>A loop's counters.</summary>
/// <param name="Round">The round it is in.</param>
/// <param name="Allowed">The rounds allowed.</param>
public sealed record LoopCounter(int Round, int Allowed);
