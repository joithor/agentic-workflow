namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>The answer of GET /sindri/launch/picks.</summary>
/// <param name="Picks">One row per run that has a 'model.picking' or a 'model.picked', oldest first.</param>
public sealed record LaunchPicks(IReadOnlyList<LaunchPickView> Picks);
