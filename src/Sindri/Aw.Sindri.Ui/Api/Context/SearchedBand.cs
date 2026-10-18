namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Whether a band came back empty.</summary>
/// <param name="Empty">Empty, as served.</param>
internal sealed record SearchedBand(
    bool Empty);
