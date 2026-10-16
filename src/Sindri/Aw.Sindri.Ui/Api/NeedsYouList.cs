namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/needs-you: the rows in the API's order (the app never sorts), the served empty sentence and the views' state.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Empty">The served sentence when there are none; null otherwise.</param>
/// <param name="AsOf">How current the views are (stalled, rebuilding).</param>
internal sealed record NeedsYouList(IReadOnlyList<NeedsYouRowView> Rows, string? Empty, AsOfView AsOf);
