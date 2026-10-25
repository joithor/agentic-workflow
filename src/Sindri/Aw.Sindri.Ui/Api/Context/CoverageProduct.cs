namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Product coverage.</summary>
/// <param name="Confirmed">Confirmed, as served.</param>
/// <param name="Of">Of, as served.</param>
internal sealed record CoverageProduct(
    int Confirmed,
    int Of);
