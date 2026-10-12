namespace Aw.Sindri.Core.Views;

/// <summary>The projector's status: GET /status 'views', the doctor line and the health (API spec §2.2, §2.3).</summary>
/// <param name="Cursor">The last ledger sequence the views include.</param>
/// <param name="LedgerHead">The ledger's head as the projector last saw it.</param>
/// <param name="ProjectedAt">When the cursor last moved, or null before the first event.</param>
/// <param name="Rebuilding">True while the views are rebuilding.</param>
/// <param name="Stalled">True when the projector stopped on an event.</param>
/// <param name="StalledAt">The sequence it stopped at, when stalled.</param>
/// <param name="StalledType">That event's type, when stalled.</param>
public sealed record ViewsStatus(long Cursor, long LedgerHead, DateTimeOffset? ProjectedAt, bool Rebuilding, bool Stalled,
    long? StalledAt, string? StalledType)
{
    /// <summary>The freshness a view answer carries.</summary>
    public AsOf AsOf => new(Cursor, LedgerHead, ProjectedAt, Rebuilding, Stalled);
}
