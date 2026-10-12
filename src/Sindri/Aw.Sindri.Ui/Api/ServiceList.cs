namespace Aw.Sindri.Ui.Api;

/// <summary>
/// GET /sindri/services (API spec §3.1, P2), as far as U1 reads it: the view's EmptyText sentence for its state and its
/// asOf, which drive the stalled and rebuilding banners (UI spec §4.1). The rows are the Services page's (U4).
/// </summary>
/// <param name="Empty">The EmptyText sentence for the view's state (empty, rebuilding or stalled), or null when it has rows and is fresh.</param>
/// <param name="AsOf">How fresh the view is.</param>
internal sealed record ServiceList(string? Empty, AsOfView AsOf);
