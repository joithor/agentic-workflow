namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The needs-you list as the app gets it: the rows in the API's order, the served empty sentence, whether the views are
/// stalled or rebuilding, and Home's other totals for the summary line. The contract unit maps the served list onto it.
/// </summary>
/// <param name="Rows">The rows, in the API's order.</param>
/// <param name="Sentence">The served sentence ("Nothing needs you. N running, M queued." when empty; the stalled or rebuilding sentence when those hold).</param>
internal sealed record NeedsYouFeed(IReadOnlyList<NeedsYouEntry> Rows, string Sentence)
{
    /// <summary>The views are stalled: rows still show, marked stale, under the banner.</summary>
    internal bool Stalled { get; init; }

    /// <summary>The views are rebuilding: actions that read them are disabled until it ends.</summary>
    internal bool Rebuilding { get; init; }

    /// <summary>Home's other totals.</summary>
    internal HomeCounts Counts { get; init; } = new(Running: 0, WithYou: 0, Queued: 0);
}
