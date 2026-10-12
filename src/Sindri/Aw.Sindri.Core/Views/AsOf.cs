namespace Aw.Sindri.Core.Views;

/// <summary>How fresh a view answer is (API spec §2.2): every view response carries it.</summary>
/// <param name="Cursor">The last ledger sequence the views include.</param>
/// <param name="LedgerHead">The ledger's head as the projector last saw it.</param>
/// <param name="ProjectedAt">When the cursor last moved, or null before the first event.</param>
/// <param name="Rebuilding">True while the views are rebuilding.</param>
/// <param name="Stalled">True when the projector stopped on an event (SND-VIEW-001).</param>
public sealed record AsOf(long Cursor, long LedgerHead, DateTimeOffset? ProjectedAt, bool Rebuilding, bool Stalled);
