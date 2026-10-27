namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/launch/picks (model-pick spec §6.1): the model picks of an item's or an instance's runs, oldest first.</summary>
/// <param name="Picks">One row per run that has a pick.</param>
internal sealed record LaunchPicksView(IReadOnlyList<LaunchPickRow> Picks);
