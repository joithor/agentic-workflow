namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/decisions?pending=&amp;item=: the decisions in the API's order.</summary>
/// <param name="Decisions">The decisions.</param>
/// <param name="Empty">The served sentence when there are none.</param>
/// <param name="AsOf">How current the views are.</param>
internal sealed record DecisionList(IReadOnlyList<DecisionView> Decisions, string? Empty, AsOfView AsOf);
