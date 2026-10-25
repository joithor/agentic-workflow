namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The product band's counts.</summary>
/// <param name="Own">Own, as served.</param>
/// <param name="Affects">Affects, as served.</param>
internal sealed record ProductCounts(
    int Own,
    int Affects);
