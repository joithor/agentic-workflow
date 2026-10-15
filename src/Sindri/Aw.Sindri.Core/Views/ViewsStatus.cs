using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Views;

/// <summary>The projector's status: GET /status 'views', the doctor line and the health (API spec §2.2, §2.3).</summary>
/// <param name="Cursor">The last ledger sequence the views include.</param>
/// <param name="LedgerHead">The ledger's head as the projector last saw it.</param>
/// <param name="ProjectedAt">When the cursor last moved, or null before the first event.</param>
/// <param name="Rebuilding">True while the views are rebuilding.</param>
/// <param name="Stalled">True when the projector stopped on an event, or (with a null <paramref name="StalledAt"/>) couldn't run at all.</param>
/// <param name="StalledAt">The sequence it stopped at, when stalled on an event.</param>
/// <param name="StalledType">That event's type, when stalled on an event; else why it can't run, as 'SND-VIEW-001: detail'.</param>
public sealed record ViewsStatus(long Cursor, long LedgerHead, DateTimeOffset? ProjectedAt, bool Rebuilding, bool Stalled,
    long? StalledAt, string? StalledType)
{
    /// <summary>The freshness a view answer carries.</summary>
    [JsonIgnore]
    public AsOf AsOf => new(Cursor, LedgerHead, ProjectedAt, Rebuilding, Stalled);

    /// <summary>The sentence for the stalled or rebuilding views (API spec §3.6), or null when they are fresh.</summary>
    public string? Text => Stalled ? (StalledAt is null ? EmptyText.Failing(StalledType) : EmptyText.Stalled(StalledAt.Value)) : (Rebuilding ? EmptyText.Rebuilding(Cursor, LedgerHead) : null);
}
