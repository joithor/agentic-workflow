namespace Aw.Sindri.Core.Locks;

/// <summary>The result of asking for a heavy-job slot.</summary>
/// <param name="Lease">The held slot (dispose it to release), or null when every slot stayed busy.</param>
/// <param name="Busy">When <paramref name="Lease"/> is null: which slots were held and by whom, for the refusal's words.</param>
public sealed record HeavySlotTaken(IAsyncDisposable? Lease, string Busy);
