namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The three bands.</summary>
/// <param name="Intent">Intent, as served.</param>
/// <param name="Product">Product, as served.</param>
/// <param name="Code">Code, as served.</param>
internal sealed record LensBands(
    IntentBand Intent,
    ProductBand Product,
    CodeBand Code);
