namespace Aw.Sindri.Ui.Api;

/// <summary>
/// How fresh a view is (API spec §2.2 asOf, UI spec U10): the projector's cursor, the ledger head, when the cursor last
/// moved, and whether the views are rebuilding or stalled.
/// </summary>
/// <param name="Cursor">The last ledger sequence the views include.</param>
/// <param name="LedgerHead">The ledger's head as the projector last saw it.</param>
/// <param name="ProjectedAt">When the cursor last moved: the clock behind "updated Ns ago"; null before it ever moved.</param>
/// <param name="Rebuilding">True while the views are replayed from the ledger.</param>
/// <param name="Stalled">True when a projection threw and the projector stopped.</param>
/// <param name="StalledAt">The ledger sequence the projection threw at (the status serves it; the banner's sentence comes from the services view); null when not stalled.</param>
/// <param name="StalledType">The event type it threw on; null when not stalled.</param>
internal sealed record AsOfView(long Cursor, long LedgerHead, DateTimeOffset? ProjectedAt, bool Rebuilding, bool Stalled, long? StalledAt = null, string? StalledType = null);
