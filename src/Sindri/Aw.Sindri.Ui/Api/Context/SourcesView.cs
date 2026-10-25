namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/sources (no golden yet).</summary>
/// <param name="Sources">Sources, as served.</param>
/// <param name="SessionsEnabled">SessionsEnabled, as served.</param>
/// <param name="AsOf">AsOf, as served.</param>
internal sealed record SourcesView(
    IReadOnlyList<SourceRow> Sources,
    bool SessionsEnabled,
    DateTimeOffset AsOf);
