namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/requests?state=&amp;item=: the requests in the API's order.</summary>
/// <param name="Requests">The requests.</param>
/// <param name="Empty">The served sentence when there are none.</param>
/// <param name="AsOf">How current the views are.</param>
internal sealed record RequestList(IReadOnlyList<RequestView> Requests, string? Empty, AsOfView AsOf);
