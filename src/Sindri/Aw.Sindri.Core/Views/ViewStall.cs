namespace Aw.Sindri.Core.Views;

/// <summary>The event a projection failed on (SND-VIEW-001).</summary>
/// <param name="Sequence">Its ledger sequence.</param>
/// <param name="Type">Its event type.</param>
/// <param name="Code">The failure's code.</param>
/// <param name="Detail">The failure's message.</param>
public sealed record ViewStall(long Sequence, string Type, string Code, string Detail);
